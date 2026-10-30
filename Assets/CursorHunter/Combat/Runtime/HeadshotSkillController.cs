using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class HeadshotSkillController : MonoBehaviour, IRunSkillController, ISkillBalanceController
    {
        public const string SkillId = "skill.headshot";

        string IRunSkillController.Id => SkillId;
        bool IRunSkillController.IsAcquiredForRun(bool savedAcquired) =>
            IsAcquiredForRun(savedAcquired);
        bool IRunSkillController.ConfigureForRun(CombatRunController combat,
            RunId runId, ulong seed, SkillCombatSnapshot skill) =>
            ConfigureForRun(combat, runId, seed, skill);
        void IRunSkillController.StopSkill() => StopSkill();

        [Header("Editor Play Mode test")]
        [SerializeField, InspectorName("Skill Acquired"),
         Tooltip("Treat Headshot as acquired in Editor Play Mode without changing saved progress.")]
        private bool editorSkillAcquired;

        [Header("Effect")]
        [SerializeField] private HeadshotEffect effectPrefab;

        [Header("Base balance")]
        [SerializeField] private HeadshotBaseStat baseStat = new HeadshotBaseStat
        {
            CooldownSeconds = 5f,
            FirstCastDelaySeconds = 1f,
            ExplosionDelaySeconds = 1.15f,
            EffectDurationSeconds = 2.5f,
            SplashDamageMultiplier = 2f,
            BossDamageMultiplier = 10f
        };

        [Header("Pool")]
        [SerializeField, Min(1)] private int maximumPooledEffects = 16;

        private readonly List<MonsterCombatTarget> _eligibleTargets =
            new List<MonsterCombatTarget>(80);
        private readonly List<HeadshotEffect> _active = new List<HeadshotEffect>(4);
        private readonly Stack<HeadshotEffect> _free = new Stack<HeadshotEffect>(4);
        private readonly List<Collider2D> _overlaps = new List<Collider2D>(128);
        private readonly HashSet<MonsterCombatTarget> _uniqueTargets =
            new HashSet<MonsterCombatTarget>();

        private CombatRunController _combat;
        private SeededRandom _random;
        private RunId _runId;
        private float _nextCastAt;
        private HeadshotRuntimeStat _runtime;
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

        internal bool ConfigureForRun(CombatRunController combat, RunId runId,
            ulong seed, SkillCombatSnapshot skill = default)
        {
            StopSkill();
            if (combat == null || !runId.IsValid || effectPrefab == null ||
                !effectPrefab.HasHitbox)
                return false;
            if (!string.IsNullOrEmpty(skill.SkillId) &&
                (skill.SkillId != SkillId || !skill.IsValid)) return false;

            _combat = combat;
            _runId = runId;
            _random = new SeededRandom(seed ^ 0x4845414453484F54UL);
            if (_balance != null) skill = default;
            _runtime = new HeadshotRuntimeStat(BalanceBasis, skill,
                combat.CombatSnapshot.AttackPower);
            _hasCast = false;
            _nextCastAt = combat.ElapsedSeconds + _runtime.FirstCastDelaySeconds;
            _configured = true;
            _visualsPaused = false;
            _poolWarningLogged = false;

            int needed = SkillBalanceMath.PoolNeed(_runtime.EffectDurationSeconds,
                _runtime.CooldownSeconds, maximumPooledEffects);
            while (PooledEffectCount < needed)
            {
                HeadshotEffect instance = Instantiate(effectPrefab, transform);
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
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                HeadshotEffect effect = _active[i];
                bool alive = effect != null && effect.Tick(now);
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

            if (now < _nextCastAt) return;
            MonsterCombatTarget.CollectActiveForRun(_runId, _eligibleTargets);
            if (_eligibleTargets.Count == 0) return;
            if (_free.Count == 0)
            {
                if (!_poolWarningLogged)
                {
                    _poolWarningLogged = true;
                    Debug.LogWarning("Headshot effect pool exhausted; waiting for an effect to return.", this);
                }
                return;
            }

            int index = (int)(_random.NextUInt64() % (ulong)_eligibleTargets.Count);
            MonsterCombatTarget target = _eligibleTargets[index];
            HeadshotEffect next = _free.Pop();
            next.Activate(this, _runId, target, now,
                _runtime.ExplosionDelaySeconds,
                _runtime.EffectDurationSeconds,
                _combat.AreaSkillRadiusMultiplier);
            _active.Add(next);
            _nextCastAt = now + _combat.ResolveAutoSkillCooldown(
                _runtime.CooldownSeconds);
            _hasCast = true;
        }

        internal bool IsCurrentRun(RunId runId) =>
            _configured && _combat != null && _combat.IsRunning &&
            _combat.CurrentRunId == runId && _runId == runId;

        internal void Explode(RunId runId, MonsterCombatTarget primary,
            BoxCollider2D hitbox)
        {
            if (!IsCurrentRun(runId) || hitbox == null || !hitbox.enabled) return;

            if (primary != null && primary.gameObject.activeInHierarchy &&
                primary.RunId == runId &&
                primary.IsActive && primary.IsRegistered)
            {
                long damage = _combat.IsBossRun
                    ? _runtime.BossDamage : primary.CurrentHealth;
                _combat.TryApplySkillDamage(runId, primary, damage);
            }

            if (!IsCurrentRun(runId)) return;
            Physics2D.SyncTransforms();
            ContactFilter2D filter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true
            };
            filter.SetLayerMask(_combat.EnemyLayers);
            _overlaps.Clear();
            hitbox.Overlap(filter, _overlaps);
            _uniqueTargets.Clear();
            for (int i = 0; i < _overlaps.Count; i++)
            {
                Collider2D collider = _overlaps[i];
                MonsterCombatTarget target = collider == null
                    ? null : collider.GetComponentInParent<MonsterCombatTarget>();
                if (target != null && target != primary && target.RunId == runId &&
                    target.IsActive && target.IsRegistered)
                    _uniqueTargets.Add(target);
            }
            foreach (MonsterCombatTarget target in _uniqueTargets)
            {
                if (!IsCurrentRun(runId)) break;
                _combat.TryApplySkillDamage(runId, target,
                    _runtime.SplashDamage);
                if (!IsCurrentRun(runId)) return;
            }
        }

        private HeadshotBaseStat BalanceBasis => _balance == null ? baseStat :
            SkillBalanceMath.Read<HeadshotBaseStat>(_balance);

        public SkillBalanceSettings CaptureBalanceSettings() =>
            SkillBalanceMath.Capture(SkillId, _balance != null ? _balance.Enabled :
                _configured || Application.isEditor && editorSkillAcquired,
                BalanceBasis, _runtime, _configured);

        public bool ValidateBalanceSettings(SkillBalanceSettings settings, out string error)
        {
            if (!SkillBalanceMath.Validate(settings, CaptureBalanceSettings(), out error))
                return false;
            HeadshotBaseStat value = SkillBalanceMath.Read<HeadshotBaseStat>(settings);
            if (value.EffectDurationSeconds < value.ExplosionDelaySeconds + 0.1f)
            {
                error = "효과 지속 시간은 폭발 대기 시간보다 0.1초 이상 길어야 합니다.";
                return false;
            }
            if (settings.Enabled && (effectPrefab == null || !effectPrefab.HasHitbox))
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

            HeadshotRuntimeStat previous = _runtime;
            _balance = next;
            _runtime = new HeadshotRuntimeStat(BalanceBasis, default,
                combat.CombatSnapshot.AttackPower);
            float now = combat.ElapsedSeconds;
            _nextCastAt = SkillBalanceMath.Retime(_nextCastAt, now,
                _hasCast ? previous.CooldownSeconds : previous.FirstCastDelaySeconds,
                _hasCast ? _runtime.CooldownSeconds : _runtime.FirstCastDelaySeconds);
            for (int i = 0; i < _active.Count; i++)
                if (_active[i] != null) _active[i].RefreshBalance(now, previous, _runtime);
            int needed = SkillBalanceMath.PoolNeed(_runtime.EffectDurationSeconds,
                _runtime.CooldownSeconds, maximumPooledEffects);
            while (PooledEffectCount < needed)
            {
                HeadshotEffect instance = Instantiate(effectPrefab, transform);
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
                HeadshotEffect effect = _active[i];
                if (effect == null) continue;
                effect.ReturnToPool();
                _free.Push(effect);
            }
            _active.Clear();
            _eligibleTargets.Clear();
            _overlaps.Clear();
            _uniqueTargets.Clear();
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
