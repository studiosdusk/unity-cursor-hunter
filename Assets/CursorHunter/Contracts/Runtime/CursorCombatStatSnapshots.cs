namespace CursorHunter.Contracts
{
    public static class CursorCombatStatLimits
    {
        public const float MinimumAttackCooldownSeconds = 0.05f;
        public const float MaximumCriticalChancePercent = 100f;
    }

    /// <summary>
    /// Authored cursor-combat defaults shared by every new run. This is a
    /// value copy so Progression and App do not depend on the Unity component.
    /// </summary>
    public readonly struct CursorCombatStatDefaultsSnapshot
    {
        public CursorCombatStatDefaultsSnapshot(
            long attackPower,
            float attackRadiusWorldUnits,
            float attackCooldownSeconds,
            float criticalChancePercent,
            float criticalDamageMultiplier,
            float bossDamageMultiplier,
            bool autoAttackEnabled)
        {
            AttackPower = attackPower;
            AttackRadiusWorldUnits = attackRadiusWorldUnits;
            AttackCooldownSeconds = attackCooldownSeconds;
            CriticalChancePercent = criticalChancePercent;
            CriticalDamageMultiplier = criticalDamageMultiplier;
            BossDamageMultiplier = bossDamageMultiplier;
            AutoAttackEnabled = autoAttackEnabled;
        }

        public long AttackPower { get; }
        public float AttackRadiusWorldUnits { get; }
        public float AttackCooldownSeconds { get; }
        public float CriticalChancePercent { get; }
        public float CriticalDamageMultiplier { get; }
        public float BossDamageMultiplier { get; }
        public bool AutoAttackEnabled { get; }

        public bool IsValid =>
            AttackPower > 0L &&
            IsFinitePositive(AttackRadiusWorldUnits) &&
            IsFinite(AttackCooldownSeconds) &&
            AttackCooldownSeconds >= CursorCombatStatLimits.MinimumAttackCooldownSeconds &&
            IsFinite(CriticalChancePercent) &&
            CriticalChancePercent >= 0f &&
            CriticalChancePercent <= CursorCombatStatLimits.MaximumCriticalChancePercent &&
            IsFinitePositive(CriticalDamageMultiplier) &&
            CriticalDamageMultiplier >= 1f &&
            IsFinitePositive(BossDamageMultiplier) &&
            (!AutoAttackEnabled || AttackCooldownSeconds > 0f);

        public CombatSnapshot ToCombatSnapshot()
        {
            return new CombatSnapshot(
                AttackPower,
                AttackRadiusWorldUnits / GameInformation.BaseAttackRadiusWorldUnits,
                AttackCooldownSeconds,
                1,
                CriticalChancePercent,
                BossDamageMultiplier,
                AutoAttackEnabled,
                AttackCooldownSeconds,
                CriticalDamageMultiplier);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinitePositive(float value) =>
            value > 0f && IsFinite(value);
    }

    /// <summary>
    /// Additive trait bonuses for cursor combat. Cooldown uses a signed delta,
    /// so cooldown-reduction traits carry negative values.
    /// </summary>
    public readonly struct CursorCombatStatBonusesSnapshot
    {
        public CursorCombatStatBonusesSnapshot(
            long attackPowerDelta,
            float attackRadiusWorldUnitsDelta,
            float attackCooldownSecondsDelta,
            float criticalChancePercentDelta,
            float criticalDamageMultiplierDelta,
            float bossDamageMultiplierDelta)
        {
            AttackPowerDelta = attackPowerDelta;
            AttackRadiusWorldUnitsDelta = attackRadiusWorldUnitsDelta;
            AttackCooldownSecondsDelta = attackCooldownSecondsDelta;
            CriticalChancePercentDelta = criticalChancePercentDelta;
            CriticalDamageMultiplierDelta = criticalDamageMultiplierDelta;
            BossDamageMultiplierDelta = bossDamageMultiplierDelta;
        }

        public long AttackPowerDelta { get; }
        public float AttackRadiusWorldUnitsDelta { get; }
        public float AttackCooldownSecondsDelta { get; }
        public float CriticalChancePercentDelta { get; }
        public float CriticalDamageMultiplierDelta { get; }
        public float BossDamageMultiplierDelta { get; }

        public bool IsValid =>
            AttackPowerDelta >= 0L &&
            IsFiniteNonNegative(AttackRadiusWorldUnitsDelta) &&
            IsFinite(AttackCooldownSecondsDelta) &&
            IsFiniteNonNegative(CriticalChancePercentDelta) &&
            IsFiniteNonNegative(CriticalDamageMultiplierDelta) &&
            IsFiniteNonNegative(BossDamageMultiplierDelta);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFiniteNonNegative(float value) =>
            value >= 0f && IsFinite(value);
    }
}
