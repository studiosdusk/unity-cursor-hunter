using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class FlameExhaustSkillController : MonoBehaviour, IRunSkillController, ISkillBalanceController
    {
        public const string SkillId = "skill.flame_exhaust";

        string IRunSkillController.Id => SkillId;
        bool IRunSkillController.IsAcquiredForRun(bool savedAcquired) =>
            IsAcquiredForRun(savedAcquired);
        bool IRunSkillController.ConfigureForRun(CombatRunController combat,
            RunId runId, ulong seed, SkillCombatSnapshot skill) =>
            ConfigureForRun(combat, runId, seed, skill);
        void IRunSkillController.StopSkill() => StopSkill();

        [Header("Editor Play Mode test")]
        [SerializeField, InspectorName("Skill Acquired"),
         Tooltip("Treat Flame Exhaust as acquired in Editor Play Mode without changing saved progress.")]
        private bool editorSkillAcquired;

        [Header("Cursor and prefabs")]
        [SerializeField] private CircleCollider2D sourceCursorHitArea;
        [SerializeField] private SpriteRenderer sourceCursorVisual;
        [SerializeField] private FlameExhaustProjectile projectilePrefab;
        [SerializeField] private FlameExhaustHitEffect hitEffectPrefab;

        [Header("Base balance")]
        [SerializeField] private FlameExhaustBaseStat baseStat =
            new FlameExhaustBaseStat
            {
                CooldownSeconds = 2f,
                FirstCastDelaySeconds = 0f,
                ProjectileDurationSeconds = 1f,
                SpeedWorldUnitsPerSecond = 8f,
                CursorSizeRatio = 0.45f,
                HitRadiusMultiplier = 1.2f,
                DamageMultiplier = 1.5f,
                HitEffectDurationSeconds = 0.8f
            };

        [Header("Pools")]
        [SerializeField, Min(1)] private int maximumPooledProjectiles = 8;
        [SerializeField, Min(1)] private int maximumPooledHitEffects = 80;
        [SerializeField, Min(1)] private int initialPooledHitEffects = 16;
        [SerializeField, Min(1)] private int hitEffectGrowthPerFrame = 2;

        private readonly List<FlameExhaustProjectile> _activeProjectiles =
            new List<FlameExhaustProjectile>(8);
        private readonly Stack<FlameExhaustProjectile> _freeProjectiles =
            new Stack<FlameExhaustProjectile>(8);
        private readonly List<FlameExhaustHitEffect> _activeHitEffects =
            new List<FlameExhaustHitEffect>(80);
        private readonly Stack<FlameExhaustHitEffect> _freeHitEffects =
            new Stack<FlameExhaustHitEffect>(80);

        private CombatRunController _combat;
        private SeededRandom _random;
        private RunId _runId;
        private ContactFilter2D _enemyFilter;
        private float _nextCastAt;
        private FlameExhaustRuntimeStat _runtime;
        private bool _configured;
        private SkillBalanceSettings _balance;
        private SeededRandom _balanceRandom;
        private RunId _balanceRandomRun;
        private bool _hasCast;
        private bool _visualsPaused;
        private bool _poolWarningLogged;
        private bool _hitEffectPoolWarningLogged;
        private bool _hitEffectPoolStarved;
        private bool _refreshProjectileGeometry;

        public int ActiveProjectileCount => _activeProjectiles.Count;
        public int ActiveHitEffectCount => _activeHitEffects.Count;
        public int PooledHitEffectCount =>
            _activeHitEffects.Count + _freeHitEffects.Count;

        internal bool IsAcquiredForRun(bool savedAcquired) =>
            _balance != null ? _balance.Enabled :
            savedAcquired || Application.isEditor && editorSkillAcquired;

        internal bool ConfigureForRun(CombatRunController combat, RunId runId,
            ulong seed, SkillCombatSnapshot skill = default)
        {
            StopSkill();
            if (combat == null || !runId.IsValid || projectilePrefab == null ||
                hitEffectPrefab == null || !projectilePrefab.HasVisualAndHitArea ||
                sourceCursorHitArea == null || sourceCursorVisual == null)
                return false;
            if (!string.IsNullOrEmpty(skill.SkillId) &&
                (skill.SkillId != SkillId || !skill.IsValid)) return false;

            _combat = combat;
            _runId = runId;
            _random = new SeededRandom(seed ^ 0x464C414D45584UL);
            if (_balance != null) skill = default;
            _runtime = new FlameExhaustRuntimeStat(BalanceBasis, skill);
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
            _hitEffectPoolWarningLogged = false;
            _hitEffectPoolStarved = false;

            int needed = SkillBalanceMath.PoolNeed(_runtime.ProjectileDurationSeconds,
                _runtime.CooldownSeconds, maximumPooledProjectiles);
            while (_activeProjectiles.Count + _freeProjectiles.Count < needed)
            {
                FlameExhaustProjectile instance = Instantiate(projectilePrefab, transform);
                instance.ReturnToPool();
                _freeProjectiles.Push(instance);
            }
            WarmHitEffects(Mathf.Min(Mathf.Max(1, maximumPooledHitEffects),
                Mathf.Max(1, initialPooledHitEffects)));
            return true;
        }

        private void WarmHitEffects(int targetCount)
        {
            while (PooledHitEffectCount < targetCount)
            {
                FlameExhaustHitEffect instance = Instantiate(hitEffectPrefab, transform);
                instance.ReturnToPool();
                _freeHitEffects.Push(instance);
            }
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

            if (_refreshProjectileGeometry)
            {
                // App synchronizes the cursor collider earlier in LateUpdate.
                // A simultaneous cursor-radius edit must also resize existing copies.
                _refreshProjectileGeometry = false;
                Physics2D.SyncTransforms();
                for (int i = 0; i < _activeProjectiles.Count; i++)
                    _activeProjectiles[i].RefreshGeometry(sourceCursorHitArea,
                        sourceCursorVisual, _runtime.CursorSizeRatio,
                        _runtime.HitRadiusMultiplier,
                        _combat.AreaSkillRadiusMultiplier);
            }

            if (_combat.IsPaused)
            {
                if (!_visualsPaused)
                {
                    _visualsPaused = true;
                    for (int i = 0; i < _activeHitEffects.Count; i++)
                        _activeHitEffects[i].SetVisualPaused(true);
                }
                return;
            }
            if (!_combat.IsRunning) return;

            if (_visualsPaused)
            {
                _visualsPaused = false;
                for (int i = 0; i < _activeHitEffects.Count; i++)
                    _activeHitEffects[i].SetVisualPaused(false);
            }

            float now = _combat.ElapsedSeconds;
            for (int i = _activeHitEffects.Count - 1; i >= 0; i--)
            {
                FlameExhaustHitEffect effect = _activeHitEffects[i];
                if (effect.Tick(now)) continue;
                _activeHitEffects.RemoveAt(i);
                effect.ReturnToPool();
                _freeHitEffects.Push(effect);
            }
            if (_hitEffectPoolStarved)
            {
                // Spread new instances across frames, outside the hit callback.
                _hitEffectPoolStarved = false;
                WarmHitEffects(Mathf.Min(Mathf.Max(1, maximumPooledHitEffects),
                    PooledHitEffectCount + Mathf.Max(1, hitEffectGrowthPerFrame)));
            }

            bool physicsSynced = _activeProjectiles.Count > 0;
            if (physicsSynced)
                Physics2D.SyncTransforms();
            for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
            {
                FlameExhaustProjectile projectile = _activeProjectiles[i];
                if (projectile.Tick(now, _enemyFilter)) continue;
                _activeProjectiles.RemoveAt(i);
                projectile.ReturnToPool();
                _freeProjectiles.Push(projectile);
            }

            if (now < _nextCastAt || sourceCursorVisual.sprite == null ||
                !sourceCursorHitArea.gameObject.activeInHierarchy ||
                !sourceCursorHitArea.enabled ||
                !sourceCursorVisual.gameObject.activeInHierarchy)
                return;
            if (_freeProjectiles.Count == 0)
            {
                if (!_poolWarningLogged)
                {
                    _poolWarningLogged = true;
                    Debug.LogWarning("Flame Exhaust projectile pool exhausted; waiting for a free projectile.", this);
                }
                return;
            }

            float angle = _random.NextFloat(0f, Mathf.PI * 2f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (!physicsSynced)
                Physics2D.SyncTransforms();
            FlameExhaustProjectile next = _freeProjectiles.Pop();
            next.Activate(this, _runId, sourceCursorHitArea,
                sourceCursorVisual, direction, now,
                _runtime.ProjectileDurationSeconds,
                _runtime.SpeedWorldUnitsPerSecond,
                _runtime.CursorSizeRatio, _runtime.HitRadiusMultiplier,
                _combat.AreaSkillRadiusMultiplier);
            _activeProjectiles.Add(next);
            next.Tick(now, _enemyFilter);
            _nextCastAt = now + _combat.ResolveAutoSkillCooldown(
                _runtime.CooldownSeconds);
            _hasCast = true;
            CombatAudioFeedback.PlaySkill(SkillId);
        }

        internal bool IsCurrentRun(RunId runId) =>
            _configured && _combat != null && _combat.IsRunning &&
            _combat.CurrentRunId == runId && _runId == runId;

        internal bool Hit(RunId runId, MonsterCombatTarget target)
        {
            if (!IsCurrentRun(runId) || target == null ||
                !target.gameObject.activeInHierarchy || target.RunId != runId ||
                !target.IsActive || !target.IsRegistered)
                return false;

            Vector3 position = target.TryGetHitEffectBounds(out Bounds bounds)
                ? bounds.center : target.transform.position;
            float targetSize = Mathf.Max(bounds.size.x, bounds.size.y);
            if (!_combat.TryApplySkillDamage(runId, target,
                    _runtime.Damage)) return false;

            if (_freeHitEffects.Count > 0)
            {
                FlameExhaustHitEffect effect = _freeHitEffects.Pop();
                effect.Activate(position, _random.NextFloat(0f, 360f),
                    _combat.ElapsedSeconds,
                    _runtime.HitEffectDurationSeconds,
                    targetSize > 0f ? targetSize : 1f);
                _activeHitEffects.Add(effect);
            }
            else
            {
                _hitEffectPoolStarved = true;
                if (!_hitEffectPoolWarningLogged)
                {
                    _hitEffectPoolWarningLogged = true;
                    Debug.LogWarning("Flame Exhaust hit VFX pool exhausted; damage was still applied.", this);
                }
            }
            return true;
        }

        private FlameExhaustBaseStat BalanceBasis => _balance == null ? baseStat :
            SkillBalanceMath.Read<FlameExhaustBaseStat>(_balance);

        public SkillBalanceSettings CaptureBalanceSettings() =>
            SkillBalanceMath.Capture(SkillId, _balance != null ? _balance.Enabled :
                _configured || Application.isEditor && editorSkillAcquired,
                BalanceBasis, _runtime, _configured);

        public bool ValidateBalanceSettings(SkillBalanceSettings settings, out string error)
        {
            if (!SkillBalanceMath.Validate(settings, CaptureBalanceSettings(), out error))
                return false;
            if (settings.Enabled && (projectilePrefab == null || hitEffectPrefab == null || !projectilePrefab.HasVisualAndHitArea || sourceCursorHitArea == null || sourceCursorVisual == null))
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

            FlameExhaustRuntimeStat previous = _runtime;
            _balance = next;
            _runtime = new FlameExhaustRuntimeStat(BalanceBasis, default);
            float now = combat.ElapsedSeconds;
            _nextCastAt = SkillBalanceMath.Retime(_nextCastAt, now,
                _hasCast ? previous.CooldownSeconds : previous.FirstCastDelaySeconds,
                _hasCast ? _runtime.CooldownSeconds : _runtime.FirstCastDelaySeconds);
            for (int i = 0; i < _activeProjectiles.Count; i++)
                _activeProjectiles[i].RefreshBalance(now, previous, _runtime,
                    sourceCursorHitArea, sourceCursorVisual);
            _refreshProjectileGeometry = true;
            for (int i = 0; i < _activeHitEffects.Count; i++)
                _activeHitEffects[i].RefreshBalance(now,
                    previous.HitEffectDurationSeconds, _runtime.HitEffectDurationSeconds);
            int needed = SkillBalanceMath.PoolNeed(_runtime.ProjectileDurationSeconds,
                _runtime.CooldownSeconds, maximumPooledProjectiles);
            while (_activeProjectiles.Count + _freeProjectiles.Count < needed)
            {
                FlameExhaustProjectile instance = Instantiate(projectilePrefab, transform);
                instance.ReturnToPool();
                _freeProjectiles.Push(instance);
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
            for (int i = 0; i < _activeProjectiles.Count; i++)
            {
                _activeProjectiles[i].ReturnToPool();
                _freeProjectiles.Push(_activeProjectiles[i]);
            }
            _activeProjectiles.Clear();
            for (int i = 0; i < _activeHitEffects.Count; i++)
            {
                _activeHitEffects[i].ReturnToPool();
                _freeHitEffects.Push(_activeHitEffects[i]);
            }
            _activeHitEffects.Clear();
            _combat = null;
            _random = null;
            _runId = default;
            _runtime = default;
            _configured = false;
            _visualsPaused = false;
            _hitEffectPoolStarved = false;
            _refreshProjectileGeometry = false;
        }

        private void OnDisable() => StopSkill();

    }
}
