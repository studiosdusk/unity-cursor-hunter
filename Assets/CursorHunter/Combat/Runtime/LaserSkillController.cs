using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>Owns the laser cast clock and its pooled vertical beams.</summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class LaserSkillController : MonoBehaviour
    {
        public const string SkillId = "skill.laser";

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
        private bool _visualsPaused;
        private bool _poolWarningLogged;

        public int ActiveEffectCount => _active.Count;
        public int PooledEffectCount => _active.Count + _free.Count;
        internal bool IsAcquiredForRun(bool savedAcquired) =>
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
            _runtime = new LaserRuntimeStat(baseStat, skill,
                combat.CombatSnapshot.AttackPower);
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

            int needed = Mathf.Clamp(Mathf.CeilToInt(
                _runtime.EffectDurationSeconds / _runtime.CooldownSeconds) + 1,
                1, Mathf.Max(1, maximumPooledEffects));
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

            float now = _combat.ElapsedSeconds;
            Physics2D.SyncTransforms();
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                LaserEffect effect = _active[i];
                if (effect != null && effect.Tick(now, _enemyFilter)) continue;
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
                _runtime.EffectDurationSeconds, _runtime.SizeMultiplier);
            _active.Add(next);
            _nextCastAt = now + _runtime.CooldownSeconds;
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
