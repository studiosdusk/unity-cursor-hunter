using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class HeadshotSkillController : MonoBehaviour
    {
        public const string SkillId = "skill.headshot";

        [Header("Editor Play Mode test")]
        [SerializeField, InspectorName("Skill Acquired"),
         Tooltip("Treat Headshot as acquired in Editor Play Mode without changing saved progress.")]
        private bool editorSkillAcquired;

        [Header("Effect")]
        [SerializeField] private HeadshotEffect effectPrefab;

        [Header("Base balance")]
        [SerializeField, Min(0.1f)] private float baseCooldownSeconds = 5f;
        [SerializeField, Min(0f)] private float firstCastDelaySeconds = 1f;
        [SerializeField, Min(0f)] private float explosionDelaySeconds = 1.15f;
        [SerializeField, Min(0.1f)] private float effectDurationSeconds = 2.5f;
        [SerializeField, Min(0.01f)] private float splashDamageMultiplier = 2f;
        [SerializeField, Min(0.01f)] private float bossDamageMultiplier = 10f;

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
        private long _splashDamage;
        private long _bossDamage;
        private bool _configured;
        private bool _visualsPaused;
        private bool _poolWarningLogged;

        public int ActiveEffectCount => _active.Count;
        public int PooledEffectCount => _active.Count + _free.Count;

        internal bool IsAcquiredForRun(bool savedAcquired) =>
            savedAcquired || Application.isEditor && editorSkillAcquired;

        internal bool ConfigureForRun(CombatRunController combat, RunId runId, ulong seed)
        {
            StopSkill();
            if (combat == null || !runId.IsValid || effectPrefab == null ||
                !effectPrefab.HasHitbox)
                return false;

            _combat = combat;
            _runId = runId;
            _random = new SeededRandom(seed ^ 0x4845414453484F54UL);
            _nextCastAt = combat.ElapsedSeconds + Mathf.Max(0f, firstCastDelaySeconds);
            _splashDamage = ScaleDamage(combat.CombatSnapshot.AttackPower,
                splashDamageMultiplier);
            _bossDamage = ScaleDamage(combat.CombatSnapshot.AttackPower,
                bossDamageMultiplier);
            _configured = true;
            _visualsPaused = false;
            _poolWarningLogged = false;

            float cooldown = Mathf.Max(0.1f, baseCooldownSeconds);
            float duration = Mathf.Max(0.1f, effectDurationSeconds);
            int needed = Mathf.Clamp(Mathf.CeilToInt(duration / cooldown) + 1,
                1, Mathf.Max(1, maximumPooledEffects));
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

            float now = _combat.ElapsedSeconds;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                HeadshotEffect effect = _active[i];
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
                    Debug.LogWarning("Headshot effect pool exhausted; waiting for an effect to return.", this);
                }
                return;
            }

            int index = (int)(_random.NextUInt64() % (ulong)_eligibleTargets.Count);
            MonsterCombatTarget target = _eligibleTargets[index];
            HeadshotEffect next = _free.Pop();
            next.Activate(this, _runId, target, now,
                Mathf.Max(0f, explosionDelaySeconds),
                Mathf.Max(effectDurationSeconds, explosionDelaySeconds + 0.1f));
            _active.Add(next);
            _nextCastAt = now + Mathf.Max(0.1f, baseCooldownSeconds);
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
                long damage = _combat.IsBossRun ? _bossDamage : primary.CurrentHealth;
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
                _combat.TryApplySkillDamage(runId, target, _splashDamage);
            }
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
            _configured = false;
            _visualsPaused = false;
        }

        private void OnDisable() => StopSkill();

        private static long ScaleDamage(long attackPower, float multiplier)
        {
            double value = attackPower * (double)Mathf.Max(0.01f, multiplier);
            return value >= long.MaxValue
                ? long.MaxValue
                : Math.Max(1L, (long)Math.Round(value, MidpointRounding.AwayFromZero));
        }
    }
}
