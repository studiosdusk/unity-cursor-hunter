using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class ThunderSkillController : MonoBehaviour
    {
        public const string SkillId = "skill.thunder";

        [Header("Editor Play Mode test")]
        [SerializeField, InspectorName("Skill Acquired"),
         Tooltip("Treat Thunder as acquired in Editor Play Mode without changing saved progress.")]
        private bool editorSkillAcquired;

        [Header("Effect")]
        [SerializeField] private ThunderEffect effectPrefab;

        [Header("Base balance")]
        [SerializeField] private ThunderBaseStat baseStat = new ThunderBaseStat
        {
            CooldownSeconds = 2f,
            FirstCastDelaySeconds = 0f,
            ImpactDelaySeconds = 0.1f,
            EffectDurationSeconds = 0.5f,
            DamageMultiplier = 4f
        };

        [Header("Pool")]
        [SerializeField, Min(1)] private int maximumPooledEffects = 8;

        private readonly List<MonsterCombatTarget> _eligibleTargets =
            new List<MonsterCombatTarget>(80);
        private readonly List<ThunderEffect> _active = new List<ThunderEffect>(2);
        private readonly Stack<ThunderEffect> _free = new Stack<ThunderEffect>(2);

        private CombatRunController _combat;
        private SeededRandom _random;
        private RunId _runId;
        private float _nextCastAt;
        private ThunderRuntimeStat _runtime;
        private bool _configured;
        private bool _visualsPaused;
        private bool _poolWarningLogged;

        public int ActiveEffectCount => _active.Count;
        public int PooledEffectCount => _active.Count + _free.Count;

        internal bool IsAcquiredForRun(bool savedAcquired) =>
            savedAcquired || Application.isEditor && editorSkillAcquired;

        internal bool ConfigureForRun(CombatRunController combat, RunId runId,
            ulong seed, SkillCombatSnapshot skill = default)
        {
            StopSkill();
            if (combat == null || !runId.IsValid || effectPrefab == null)
                return false;
            if (!string.IsNullOrEmpty(skill.SkillId) &&
                (skill.SkillId != SkillId || !skill.IsValid)) return false;

            _combat = combat;
            _runId = runId;
            _random = new SeededRandom(seed ^ 0x5448554E444552UL);
            _runtime = new ThunderRuntimeStat(baseStat, skill,
                combat.CombatSnapshot.AttackPower);
            _nextCastAt = combat.ElapsedSeconds + _runtime.FirstCastDelaySeconds;
            _configured = true;
            _visualsPaused = false;
            _poolWarningLogged = false;

            int needed = Mathf.Clamp(Mathf.CeilToInt(
                _runtime.EffectDurationSeconds / _runtime.CooldownSeconds) + 1,
                1, Mathf.Max(1, maximumPooledEffects));
            while (PooledEffectCount < needed)
            {
                ThunderEffect instance = Instantiate(effectPrefab, transform);
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
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ThunderEffect effect = _active[i];
                if (effect != null && effect.Tick(now)) continue;
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
                    Debug.LogWarning("Thunder effect pool exhausted; waiting for an effect to return.", this);
                }
                return;
            }

            int index = (int)(_random.NextUInt64() % (ulong)_eligibleTargets.Count);
            MonsterCombatTarget target = _eligibleTargets[index];
            ThunderEffect next = _free.Pop();
            next.Activate(this, _runId, target, now,
                _runtime.ImpactDelaySeconds,
                _runtime.EffectDurationSeconds);
            _active.Add(next);
            _nextCastAt = now + _runtime.CooldownSeconds;
        }

        internal bool IsCurrentRun(RunId runId) =>
            _configured && _combat != null && _combat.IsRunning &&
            _combat.CurrentRunId == runId && _runId == runId;

        internal void Strike(RunId runId, MonsterCombatTarget target)
        {
            if (!IsCurrentRun(runId) || target == null ||
                !target.gameObject.activeInHierarchy || target.RunId != runId ||
                !target.IsActive || !target.IsRegistered)
                return;

            _combat.TryApplySkillDamage(runId, target, _runtime.Damage);
        }

        internal void StopSkill()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                ThunderEffect effect = _active[i];
                if (effect == null) continue;
                effect.ReturnToPool();
                _free.Push(effect);
            }
            _active.Clear();
            _eligibleTargets.Clear();
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
