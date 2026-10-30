using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>Owns the laser cast clock and its pooled vertical beams.</summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class LaserSkillController : MonoBehaviour, IRunSkillController, ISkillBalanceController
    {
        public const string SkillId = "skill.laser";

        string IRunSkillController.Id => SkillId;
        bool IRunSkillController.IsAcquiredForRun(bool savedAcquired) =>
            IsAcquiredForRun(savedAcquired);
        bool IRunSkillController.ConfigureForRun(CombatRunController combat,
            RunId runId, ulong seed, SkillCombatSnapshot skill) =>
            ConfigureForRun(combat, runId, seed, skill);
        void IRunSkillController.StopSkill() => StopSkill();

        [Header("Editor Play Mode test")]
        [SerializeField, InspectorName("Skill Acquired"),
         Tooltip("Treat Laser as acquired in Editor Play Mode without changing saved progress.")]
        private bool editorSkillAcquired;

        [Header("Effect and field")]
        [SerializeField] private LaserEffect effectPrefab;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float spawnPlaneZ;
        [SerializeField, Range(0f, 0.4f)] private float horizontalViewportPadding = 0.02f;
        [SerializeField, Range(0f, 0.4f)] private float bottomViewportPadding;
        [SerializeField, Range(0f, 0.4f)] private float topViewportPadding;
        [SerializeField, Min(0f)] private float fieldEdgePaddingWorldUnits;

        [Header("Base balance")]
        [SerializeField] private LaserBaseStat baseStat = new LaserBaseStat
        {
            CooldownSeconds = 5f,
            FirstCastDelaySeconds = 0f,
            EffectDurationSeconds = 1f,
            SizeMultiplier = 1f,
            DamageMultiplier = 2f
        };

        [Header("Pool")]
        [SerializeField, Min(1)] private int maximumPooledEffects = 8;

        private readonly List<LaserEffect> _active = new List<LaserEffect>(2);
        private readonly Stack<LaserEffect> _free = new Stack<LaserEffect>(2);
        private CombatRunController _combat;
        private SeededRandom _random;
        private RunId _runId;
        private LaserRuntimeStat _runtime;
        private ContactFilter2D _enemyFilter;
        private float _nextCastAt;
        private bool _configured;
        private SkillBalanceSettings _balance;
        private SeededRandom _balanceRandom;
        private RunId _balanceRandomRun;
        private bool _hasCast;
        private bool _visualsPaused;
        private bool _poolWarningLogged;

        public int ActiveEffectCount => _active.Count;
        public int PooledEffectCount => _active.Count + _free.Count;
        internal bool IsAcquiredForRun(bool savedAcquired) =>
            _balance != null ? _balance.Enabled :
            savedAcquired || Application.isEditor && editorSkillAcquired;

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
        }

        internal bool ConfigureForRun(CombatRunController combat, RunId runId,
            ulong seed, SkillCombatSnapshot skill = default)
        {
            StopSkill();
            if (combat == null || !runId.IsValid || effectPrefab == null ||
                !effectPrefab.HasHitbox) return false;
            if (!string.IsNullOrEmpty(skill.SkillId) &&
                (skill.SkillId != SkillId || !skill.IsValid)) return false;
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null) return false;

            _combat = combat;
            _runId = runId;
            _random = new SeededRandom(seed ^ 0x4C41534552UL);
            if (_balance != null) skill = default;
            _runtime = new LaserRuntimeStat(BalanceBasis, skill,
                combat.CombatSnapshot.AttackPower);
            _hasCast = false;
            _nextCastAt = combat.ElapsedSeconds + _runtime.FirstCastDelaySeconds;
            _enemyFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true
            };
            _enemyFilter.SetLayerMask(combat.EnemyLayers);
            _configured = true;
            _visualsPaused = false;
            _poolWarningLogged = false;

            int needed = SkillBalanceMath.PoolNeed(_runtime.EffectDurationSeconds,
                _runtime.CooldownSeconds, maximumPooledEffects);
            while (PooledEffectCount < needed)
            {
                LaserEffect instance = Instantiate(effectPrefab, transform);
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

            RunId runId = _runId;
            float now = _combat.ElapsedSeconds;
            if (_active.Count > 0)
                Physics2D.SyncTransforms();
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                LaserEffect effect = _active[i];
                bool alive = effect != null && effect.Tick(now, _enemyFilter);
                // A hit can synchronously finish the run and clear this list.
                if (!IsCurrentRun(runId)) return;
                if (alive) continue;
                _active.RemoveAt(i);
                if (effect != null)
                {
                    effect.ReturnToPool();
                    _free.Push(effect);
                }
            }
            if (!_combat.IsRunning || now < _nextCastAt ||
                !TryGetFieldBounds(out Rect fieldBounds)) return;
            if (_free.Count == 0)
            {
                if (!_poolWarningLogged)
                {
                    _poolWarningLogged = true;
                    Debug.LogWarning("Laser effect pool exhausted; waiting for an effect to return.", this);
                }
                return;
            }

            LaserEffect next = _free.Pop();
            next.Activate(this, _runId, fieldBounds,
                _random.NextFloat(0f, 1f), spawnPlaneZ, now,
                _runtime.EffectDurationSeconds, _runtime.SizeMultiplier *
                    _combat.AreaSkillRadiusMultiplier);
            _active.Add(next);
            _nextCastAt = now + _combat.ResolveAutoSkillCooldown(
                _runtime.CooldownSeconds);
            _hasCast = true;
            Physics2D.SyncTransforms();
            if (!next.Tick(now, _enemyFilter) && _active.Remove(next))
            {
                next.ReturnToPool();
                _free.Push(next);
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
            _combat.CurrentRunId == runId && _runId == runId;

        internal bool Hit(RunId runId, MonsterCombatTarget target)
        {
            return IsCurrentRun(runId) && target != null &&
                target.gameObject.activeInHierarchy && target.IsActive &&
                target.IsRegistered && target.RunId == runId &&
                _combat.TryApplySkillDamage(runId, target, _runtime.Damage);
        }

        private LaserBaseStat BalanceBasis => _balance == null ? baseStat :
            SkillBalanceMath.Read<LaserBaseStat>(_balance);

        public SkillBalanceSettings CaptureBalanceSettings() =>
            SkillBalanceMath.Capture(SkillId, _balance != null ? _balance.Enabled :
                _configured || Application.isEditor && editorSkillAcquired,
                BalanceBasis, _runtime, _configured);

        public bool ValidateBalanceSettings(SkillBalanceSettings settings, out string error)
        {
            if (!SkillBalanceMath.Validate(settings, CaptureBalanceSettings(), out error))
                return false;
            if (settings.Enabled && (effectPrefab == null || !effectPrefab.HasHitbox || (worldCamera == null && Camera.main == null)))
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

            LaserRuntimeStat previous = _runtime;
            _balance = next;
            _runtime = new LaserRuntimeStat(BalanceBasis, default,
                combat.CombatSnapshot.AttackPower);
            float now = combat.ElapsedSeconds;
            _nextCastAt = SkillBalanceMath.Retime(_nextCastAt, now,
                _hasCast ? previous.CooldownSeconds : previous.FirstCastDelaySeconds,
                _hasCast ? _runtime.CooldownSeconds : _runtime.FirstCastDelaySeconds);
            if (TryGetFieldBounds(out Rect bounds))
                for (int i = 0; i < _active.Count; i++)
                    if (_active[i] != null)
                        _active[i].RefreshBalance(now, previous, _runtime, bounds);
            int needed = SkillBalanceMath.PoolNeed(_runtime.EffectDurationSeconds,
                _runtime.CooldownSeconds, maximumPooledEffects);
            while (PooledEffectCount < needed)
            {
                LaserEffect instance = Instantiate(effectPrefab, transform);
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
                LaserEffect effect = _active[i];
                if (effect == null) continue;
                effect.ReturnToPool();
                _free.Push(effect);
            }
            _active.Clear();
            _combat = null;
            _random = null;
            _runId = default;
            _runtime = default;
            _configured = false;
            _visualsPaused = false;
        }

        private void OnDisable() => StopSkill();
    }
}
