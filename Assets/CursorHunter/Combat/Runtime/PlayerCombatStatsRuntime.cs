using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Captures combat values for the current run and exposes the live cursor
    /// attack values so they can be tuned from the Inspector during Play Mode.
    /// Skill snapshots remain a separate extension point on CombatRunController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCombatStatsRuntime : MonoBehaviour
    {
        private PlayerCombatStatsDefaults _defaults;

        [Header("Run")]
        [Tooltip("True after a valid run snapshot has been copied here.")]
        [SerializeField] private bool hasRunSnapshot;
        [Tooltip("True only while CombatRunController has committed the run.")]
        [SerializeField] private bool runActive;

        [Header("Cursor Attack")]
        [Tooltip("Cursor attack damage. Changes apply to subsequent hits immediately.")]
        [SerializeField, Min(1)] private long attackPower = 1L;
        [Tooltip("Cursor attack radius in world units. Changes resize the cursor attack area during a run.")]
        [SerializeField, Min(GameInformation.BaseAttackRadiusWorldUnits * 0.01f)] private float attackRadiusWorldUnits =
            GameInformation.BaseAttackRadiusWorldUnits;
        [Tooltip("Time between cursor auto-attacks. Changes affect an attack already waiting on cooldown.")]
        [SerializeField, Min(0f)] private float attackCooldownSeconds = 0.8f;
        [SerializeField] private bool autoAttackEnabled = true;
        [SerializeField, Range(0f, 100f)] private float criticalChancePercent;
        [SerializeField, Min(1f)] private float criticalDamageMultiplier = 2f;
        [SerializeField, Min(0.01f)] private float bossDamageMultiplier = 1f;

        public bool HasRunSnapshot => hasRunSnapshot;
        public bool IsRunActive => runActive;
        public long AttackPower => attackPower;
        public float AttackRadiusWorldUnits => attackRadiusWorldUnits;
        public float AttackRangeMultiplier =>
            attackRadiusWorldUnits / GameInformation.BaseAttackRadiusWorldUnits;
        public float AttackCooldownSeconds => attackCooldownSeconds;
        public bool AutoAttackEnabled => autoAttackEnabled;
        public float AutoAttackIntervalSeconds => attackCooldownSeconds;
        public float CriticalChancePercent => criticalChancePercent;
        public float CriticalDamageMultiplier => criticalDamageMultiplier;
        public float BossDamageMultiplier => bossDamageMultiplier;
        public CursorCombatStatDefaultsSnapshot Defaults => ResolveDefaults().Snapshot;
        public CombatSnapshot DefaultsCombatSnapshot => Defaults.ToCombatSnapshot();
        public CombatSnapshot Snapshot => hasRunSnapshot
            ? new CombatSnapshot(
                attackPower,
                AttackRangeMultiplier,
                attackCooldownSeconds,
                1,
                criticalChancePercent,
                bossDamageMultiplier,
                autoAttackEnabled,
                attackCooldownSeconds,
                criticalDamageMultiplier)
            : default;

        private void Awake()
        {
            CopyDefaults(ResolveDefaults().Snapshot);
        }

        internal bool TryCaptureSnapshot(CombatSnapshot snapshot)
        {
            if (!snapshot.IsValid)
            {
                return false;
            }

            hasRunSnapshot = true;
            attackPower = snapshot.AttackPower;
            attackRadiusWorldUnits =
                GameInformation.BaseAttackRadiusWorldUnits * snapshot.RangeMultiplier;
            attackCooldownSeconds = snapshot.AutoAttackIntervalSeconds > 0f
                ? snapshot.AutoAttackIntervalSeconds
                : snapshot.AttackCooldownSeconds;

            // A zero auto interval is how legacy four-argument CombatSnapshot
            // callers indicate that no separate auto-attack policy was supplied.
            autoAttackEnabled = snapshot.AutoAttackEnabled ||
                                snapshot.AutoAttackIntervalSeconds <= 0f;

            criticalChancePercent = snapshot.CriticalChancePercent;
            criticalDamageMultiplier = snapshot.CriticalDamageMultiplier;
            bossDamageMultiplier = snapshot.BossDamageMultiplier;
            return true;
        }

        internal bool TryCaptureSnapshot(CursorCombatStatBonusesSnapshot bonuses)
        {
            CursorCombatStatDefaultsSnapshot defaultsSnapshot = ResolveDefaults().Snapshot;
            if (!defaultsSnapshot.IsValid || !bonuses.IsValid)
            {
                return false;
            }

            long resolvedAttackPower = AddNonNegative(
                defaultsSnapshot.AttackPower,
                bonuses.AttackPowerDelta);
            float resolvedRadius = defaultsSnapshot.AttackRadiusWorldUnits +
                                   bonuses.AttackRadiusWorldUnitsDelta;
            float resolvedCooldown = Mathf.Max(
                CursorCombatStatLimits.MinimumAttackCooldownSeconds,
                defaultsSnapshot.AttackCooldownSeconds +
                bonuses.AttackCooldownSecondsDelta);
            float resolvedCriticalChance = Mathf.Clamp(
                defaultsSnapshot.CriticalChancePercent +
                bonuses.CriticalChancePercentDelta,
                0f,
                CursorCombatStatLimits.MaximumCriticalChancePercent);
            float resolvedCriticalDamage = defaultsSnapshot.CriticalDamageMultiplier +
                                           bonuses.CriticalDamageMultiplierDelta;
            float resolvedBossDamage = defaultsSnapshot.BossDamageMultiplier +
                                       bonuses.BossDamageMultiplierDelta;

            if (resolvedAttackPower <= 0L || !IsFinitePositive(resolvedRadius) ||
                !IsFinitePositive(resolvedCooldown) ||
                !IsFinite(resolvedCriticalChance) ||
                !IsFinite(resolvedCriticalDamage) || resolvedCriticalDamage < 1f ||
                !IsFinitePositive(resolvedBossDamage))
            {
                return false;
            }

            CombatSnapshot resolvedSnapshot = new CombatSnapshot(
                resolvedAttackPower,
                resolvedRadius / GameInformation.BaseAttackRadiusWorldUnits,
                resolvedCooldown,
                1,
                resolvedCriticalChance,
                resolvedBossDamage,
                defaultsSnapshot.AutoAttackEnabled,
                resolvedCooldown,
                resolvedCriticalDamage);
            if (!resolvedSnapshot.IsValid)
            {
                return false;
            }

            hasRunSnapshot = true;
            attackPower = resolvedAttackPower;
            attackRadiusWorldUnits = resolvedRadius;
            attackCooldownSeconds = resolvedCooldown;
            autoAttackEnabled = defaultsSnapshot.AutoAttackEnabled;
            criticalChancePercent = resolvedCriticalChance;
            criticalDamageMultiplier = resolvedCriticalDamage;
            bossDamageMultiplier = resolvedBossDamage;
            return true;
        }

        internal void SetRunActive(bool active)
        {
            runActive = hasRunSnapshot && active;
        }

        private PlayerCombatStatsDefaults ResolveDefaults()
        {
            if (_defaults == null)
            {
                _defaults = GetComponent<PlayerCombatStatsDefaults>();
            }

            if (_defaults == null)
            {
                _defaults = gameObject.AddComponent<PlayerCombatStatsDefaults>();
            }

            return _defaults;
        }

        private void CopyDefaults(CursorCombatStatDefaultsSnapshot defaults)
        {
            attackPower = defaults.AttackPower;
            attackRadiusWorldUnits = defaults.AttackRadiusWorldUnits;
            attackCooldownSeconds = defaults.AttackCooldownSeconds;
            autoAttackEnabled = defaults.AutoAttackEnabled;
            criticalChancePercent = defaults.CriticalChancePercent;
            criticalDamageMultiplier = defaults.CriticalDamageMultiplier;
            bossDamageMultiplier = defaults.BossDamageMultiplier;
        }

        private void CopySnapshot(CombatSnapshot snapshot)
        {
            attackPower = snapshot.AttackPower;
            attackRadiusWorldUnits =
                GameInformation.BaseAttackRadiusWorldUnits * snapshot.RangeMultiplier;
            attackCooldownSeconds = snapshot.AutoAttackIntervalSeconds > 0f
                ? snapshot.AutoAttackIntervalSeconds
                : snapshot.AttackCooldownSeconds;
            autoAttackEnabled = snapshot.AutoAttackEnabled ||
                                snapshot.AutoAttackIntervalSeconds <= 0f;
            criticalChancePercent = snapshot.CriticalChancePercent;
            criticalDamageMultiplier = snapshot.CriticalDamageMultiplier;
            bossDamageMultiplier = snapshot.BossDamageMultiplier;
        }

        private static long AddNonNegative(long value, long delta)
        {
            return delta > long.MaxValue - value ? long.MaxValue : value + delta;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinitePositive(float value) =>
            value > 0f && IsFinite(value);
    }
}
