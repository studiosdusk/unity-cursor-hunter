using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Tornado's authored balance, independent clock, field movement and pool.
    /// Future skills do not inherit its geometry or balance fields.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class TornadoSkillController : MonoBehaviour, IRunSkillController, ISkillBalanceController
    {
        public const string SkillId = "skill.tornado";

        string IRunSkillController.Id => SkillId;
        bool IRunSkillController.IsAcquiredForRun(bool savedAcquired) =>
            IsAcquiredForRun(savedAcquired);
        bool IRunSkillController.ConfigureForRun(CombatRunController combat,
            RunId runId, ulong seed, SkillCombatSnapshot skill) =>
            ConfigureForRun(combat, runId, seed, skill);
        void IRunSkillController.StopSkill() => StopSkill();

        [Header("Editor Play Mode test")]
        [SerializeField, InspectorName("Skill Acquired"), Tooltip("In Editor Play Mode, treat Tornado as acquired even if it has not been purchased. Saved acquisitions still work when this is off.")]
        private bool editorSkillAcquired;

        [Header("Prefabs and field")]
        [SerializeField] private TornadoObject tornadoObjectPrefab;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float spawnPlaneZ;
        [SerializeField, Range(0f, 0.4f)] private float horizontalViewportPadding = 0.02f;
        [SerializeField, Range(0f, 0.4f)] private float bottomViewportPadding = 0.09f;
        [SerializeField, Range(0f, 0.4f)] private float topViewportPadding = 0.15f;

        [Header("Base balance")]
        [SerializeField] private TornadoBaseStat baseStat = new TornadoBaseStat
        {
            Count = 1,
            DurationSeconds = 5f,
            DamageMultiplier = 2f,
            CooldownSeconds = 8f,
            FirstCastDelaySeconds = 1f,
            HitIntervalSeconds = 0.5f,
            MoveSpeedWorldUnitsPerSecond = 1f,
            DirectionChangeSeconds = 3f,
            StopChanceOnDirectionChange = 0.25f,
            StopDurationSeconds = 0.75f,
            ShrinkOutSeconds = 1f
        };
        [SerializeField, Min(0f)] private float fieldEdgePaddingWorldUnits = 1.5f;

        [Header("Pool")]
        [SerializeField, Range(1, 64)] private int maximumPooledObjects = 16;

        private readonly List<TornadoObject> _active = new List<TornadoObject>(16);
        private readonly Stack<TornadoObject> _free = new Stack<TornadoObject>(16);
        private readonly HashSet<MonsterCombatTarget> _uniqueTargets =
            new HashSet<MonsterCombatTarget>(128);
        private Collider2D[] _overlapBuffer;
        private CombatRunController _combat;
        private SeededRandom _random;
        private RunId _runId;
        private float _nextCastAt;
        private TornadoRuntimeStat _runtime;
        private int _syncedFrame = -1;
        private bool _configured;
        private SkillBalanceSettings _balance;
        private SeededRandom _balanceRandom;
        private RunId _balanceRandomRun;
        private bool _hasCast;
        private bool _visualsPaused;
        private bool _overflowLogged;
        private bool _poolExhaustionLogged;

        public int ActiveObjectCount => _active.Count;
        public int PooledObjectCount => _active.Count + _free.Count;
        internal float MoveSpeedWorldUnitsPerSecond =>
            _runtime.MoveSpeedWorldUnitsPerSecond;
        internal bool IsAcquiredForRun(bool savedAcquired) =>
            _balance != null ? _balance.Enabled :
            savedAcquired || Application.isEditor && editorSkillAcquired;

        private void Awake()
        {
            _overlapBuffer = new Collider2D[512];
            if (worldCamera == null) worldCamera = Camera.main;
        }

        internal bool ConfigureForRun(
            CombatRunController combat, RunId runId, ulong seed,
            SkillCombatSnapshot skill = default)
        {
            StopSkill();
            if (combat == null || !runId.IsValid || tornadoObjectPrefab == null)
                return false;
            if (!string.IsNullOrEmpty(skill.SkillId) &&
                (skill.SkillId != SkillId || !skill.IsValid)) return false;
            if (!tornadoObjectPrefab.HasHitbox)
            {
                Debug.LogError("TornadoObject needs an enabled trigger BoxCollider2D hit area.",
                    tornadoObjectPrefab);
                return false;
            }

            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null)
            {
                Debug.LogWarning("Tornado needs a world camera.", this);
                return false;
            }

            _combat = combat;
            _runId = runId;
            _random = new SeededRandom(seed ^ 0x70524E41444FUL);
            if (_balance != null) skill = default;
            _runtime = new TornadoRuntimeStat(BalanceBasis, skill,
                combat.CombatSnapshot.AttackPower, maximumPooledObjects);
            if (_runtime.Count < SkillStatMath.AddCount(BalanceBasis.Count,
                    skill, "count"))
                Debug.LogWarning("Tornado count exceeds the pool limit; increase Maximum Pooled Objects.", this);
            _hasCast = false;
            _nextCastAt = combat.ElapsedSeconds + _runtime.FirstCastDelaySeconds;
            _configured = true;
            _visualsPaused = false;
            _overflowLogged = false;
            _poolExhaustionLogged = false;
            _syncedFrame = -1;

            int needed = SkillBalanceMath.PoolNeed(_runtime.DurationSeconds,
                _runtime.CooldownSeconds, maximumPooledObjects, _runtime.Count);
            while (PooledObjectCount < needed)
            {
                TornadoObject instance = Instantiate(tornadoObjectPrefab, transform);
                instance.ReturnToPool();
                _free.Push(instance);
            }
            return true;
        }

        private void LateUpdate()
        {
            if (!_configured) return;
            if (_combat == null || !_combat.IsRunActive ||
                _combat.CurrentRunId != _runId)
            {
                StopSkill();
                return;
            }

            if (_combat.IsPaused)
            {
                if (!_visualsPaused)
                {
                    _visualsPaused = true;
                    for (int i = 0; i < _active.Count; i++)
                        _active[i].SetVisualPaused(true);
                }
                return;
            }

            if (!_combat.IsRunning) return;
            if (_visualsPaused)
            {
                _visualsPaused = false;
                for (int i = 0; i < _active.Count; i++)
                    _active[i].SetVisualPaused(false);
            }

            float now = _combat.ElapsedSeconds;
            if (!TryGetFieldBounds(out Rect bounds)) return;
            if (now >= _nextCastAt)
            {
                Cast(now, bounds);
                _nextCastAt = now + _combat.ResolveAutoSkillCooldown(
                    _runtime.CooldownSeconds);
                _hasCast = true;
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                TornadoObject instance = _active[i];
                if (instance != null && instance.Tick(now, bounds)) continue;
                _active.RemoveAt(i);
                if (instance != null)
                {
                    instance.ReturnToPool();
                    _free.Push(instance);
                }
            }
            for (int i = 0; i < _active.Count; i++)
                _active[i].TryHit(now);
        }

        private void Cast(float now, Rect bounds)
        {
            _syncedFrame = -1;
            if (_free.Count < _runtime.Count && !_poolExhaustionLogged)
            {
                _poolExhaustionLogged = true;
                Debug.LogWarning("Tornado pool exhausted; some objects in this cast will be skipped.", this);
            }
            for (int i = 0; i < _runtime.Count && _free.Count > 0; i++)
            {
                TornadoObject instance = _free.Pop();
                Vector2 position = new Vector2(
                    _random.NextFloat(bounds.xMin, bounds.xMax),
                    _random.NextFloat(bounds.yMin, bounds.yMax));
                instance.Activate(this, _runId, position, now,
                    _runtime.DurationSeconds, _runtime.DirectionChangeSeconds,
                    _runtime.HitIntervalSeconds,
                    _runtime.StopChanceOnDirectionChange,
                    _runtime.StopDurationSeconds,
                    _runtime.ShrinkOutSeconds, _runtime.Damage,
                    _combat.AreaSkillRadiusMultiplier);
                _active.Add(instance);
            }
        }

        private bool TryGetFieldBounds(out Rect bounds)
        {
            bounds = default;
            if (worldCamera == null) return false;
            float distance = Mathf.Abs(spawnPlaneZ - worldCamera.transform.position.z);
            Vector3 bottomLeft = worldCamera.ViewportToWorldPoint(
                new Vector3(horizontalViewportPadding, bottomViewportPadding, distance));
            Vector3 topRight = worldCamera.ViewportToWorldPoint(
                new Vector3(1f - horizontalViewportPadding,
                    1f - topViewportPadding, distance));
            float minX = Mathf.Min(bottomLeft.x, topRight.x) + fieldEdgePaddingWorldUnits;
            float maxX = Mathf.Max(bottomLeft.x, topRight.x) - fieldEdgePaddingWorldUnits;
            float minY = Mathf.Min(bottomLeft.y, topRight.y) + fieldEdgePaddingWorldUnits;
            float maxY = Mathf.Max(bottomLeft.y, topRight.y) - fieldEdgePaddingWorldUnits;
            if (maxX <= minX || maxY <= minY) return false;
            bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        internal bool IsCurrentRun(RunId runId) =>
            _configured && _combat != null && _combat.IsRunning &&
            runId == _runId && _combat.CurrentRunId == runId;

        internal Vector2 RandomDirection()
        {
            float angle = _random.NextFloat(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        internal bool RollStop(float chance) => _random.NextFloat(0f, 1f) < chance;

        internal void HitAt(RunId runId, BoxCollider2D hitbox, long damage)
        {
            if (!IsCurrentRun(runId) || hitbox == null || !hitbox.enabled) return;
            if (_syncedFrame != Time.frameCount)
            {
                Physics2D.SyncTransforms();
                _syncedFrame = Time.frameCount;
            }

            ContactFilter2D filter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true
            };
            filter.SetLayerMask(_combat.EnemyLayers);
            int count = hitbox.Overlap(filter, _overlapBuffer);
            if (count == _overlapBuffer.Length && !_overflowLogged)
            {
                _overflowLogged = true;
                Debug.LogWarning("Tornado target buffer filled; increase its capacity.", this);
            }
            _uniqueTargets.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = _overlapBuffer[i];
                MonsterCombatTarget target = collider == null
                    ? null : collider.GetComponentInParent<MonsterCombatTarget>();
                if (target != null && target.IsActive && target.IsRegistered &&
                    target.RunId == runId)
                    _uniqueTargets.Add(target);
            }
            foreach (MonsterCombatTarget target in _uniqueTargets)
            {
                if (!IsCurrentRun(runId)) break;
                _combat.TryApplySkillDamage(runId, target, damage);
            }
        }

        private TornadoBaseStat BalanceBasis => _balance == null ? baseStat :
            SkillBalanceMath.Read<TornadoBaseStat>(_balance);

        public SkillBalanceSettings CaptureBalanceSettings() =>
            SkillBalanceMath.Capture(SkillId, _balance != null ? _balance.Enabled :
                _configured || Application.isEditor && editorSkillAcquired,
                BalanceBasis, _runtime, _configured, maximumPooledObjects);

        public bool ValidateBalanceSettings(SkillBalanceSettings settings, out string error)
        {
            if (!SkillBalanceMath.Validate(settings, CaptureBalanceSettings(), out error))
                return false;
            if (settings.Enabled && (tornadoObjectPrefab == null || !tornadoObjectPrefab.HasHitbox || (worldCamera == null && Camera.main == null)))
            {
                error = "스킬 프리팹·카메라·전투 참조를 확인하세요.";
                return false;
            }
            return true;
        }

        public bool ApplyBalanceSettings(CombatRunController combat, RunId runId,
            ulong seed, SkillBalanceSettings settings)
        {
            if (combat == null || !runId.IsValid ||
                !ValidateBalanceSettings(settings, out _)) return false;
            SkillBalanceSettings next = settings.Clone();
            if (!next.Enabled)
            {
                if (_configured && _runId == runId)
                {
                    _balanceRandom = _random;
                    _balanceRandomRun = runId;
                }
                StopSkill();
                _balance = next;
                return true;
            }
            if (!_configured || _combat != combat || _runId != runId)
            {
                SkillBalanceSettings previousSettings = _balance;
                _balance = next;
                if (ConfigureForRun(combat, runId, seed))
                {
                    if (_balanceRandomRun == runId && _balanceRandom != null)
                        _random = _balanceRandom;
                    return true;
                }
                _balance = previousSettings;
                return false;
            }

            TornadoRuntimeStat previous = _runtime;
            _balance = next;
            _runtime = new TornadoRuntimeStat(BalanceBasis, default,
                combat.CombatSnapshot.AttackPower, maximumPooledObjects);
            float now = combat.ElapsedSeconds;
            _nextCastAt = SkillBalanceMath.Retime(_nextCastAt, now,
                _hasCast ? previous.CooldownSeconds : previous.FirstCastDelaySeconds,
                _hasCast ? _runtime.CooldownSeconds : _runtime.FirstCastDelaySeconds);
            for (int i = 0; i < _active.Count; i++)
                if (_active[i] != null) _active[i].RefreshBalance(now, previous, _runtime);
            int needed = SkillBalanceMath.PoolNeed(_runtime.DurationSeconds,
                _runtime.CooldownSeconds, maximumPooledObjects, _runtime.Count);
            while (PooledObjectCount < needed)
            {
                TornadoObject instance = Instantiate(tornadoObjectPrefab, transform);
                instance.ReturnToPool();
                _free.Push(instance);
            }
            return true;
        }

        public void ClearBalanceSettings()
        {
            _balance = null;
            _balanceRandom = null;
            _balanceRandomRun = default;
        }

        internal void StopSkill()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                TornadoObject instance = _active[i];
                if (instance == null) continue;
                instance.ReturnToPool();
                _free.Push(instance);
            }
            _active.Clear();
            _configured = false;
            _combat = null;
            _runId = default;
            _random = null;
            _runtime = default;
            _visualsPaused = false;
        }

        private void OnDisable() => StopSkill();
    }
}
