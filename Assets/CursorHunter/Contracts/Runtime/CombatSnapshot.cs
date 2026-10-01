namespace CursorHunter.Contracts
{
    /// <summary>
    /// Immutable combat values captured for one run.
    /// Unity-facing systems must not mutate this snapshot while a run is active.
    /// </summary>
    public readonly struct CombatSnapshot
    {
        public CombatSnapshot(
            long attackPower,
            float rangeMultiplier,
            float attackCooldownSeconds,
            int hitsPerBundle)
            : this(
                attackPower,
                rangeMultiplier,
                attackCooldownSeconds,
                hitsPerBundle,
                0f,
                1f,
                false,
                0f)
        {
        }

        /// <summary>
        /// Full progression handoff. The four-argument constructor above is
        /// retained for existing tests and combat callers.
        /// </summary>
        public CombatSnapshot(
            long attackPower,
            float rangeMultiplier,
            float attackCooldownSeconds,
            int hitsPerBundle,
            float criticalChancePercent,
            float bossDamageMultiplier,
            bool autoAttackEnabled,
            float autoAttackIntervalSeconds,
            float criticalDamageMultiplier = 2f)
        {
            CriticalDamageMultiplier = SanitizeMultiplier(criticalDamageMultiplier);
            AttackPower = attackPower > 0 ? attackPower : 1;
            RangeMultiplier = rangeMultiplier > 0f ? rangeMultiplier : 1f;
            AttackCooldownSeconds = attackCooldownSeconds >= 0f ? attackCooldownSeconds : 0f;
            // Kept as a compatibility property for old callers; multi-hit no longer exists.
            HitsPerBundle = 1;
            CriticalChancePercent = SanitizePercent(criticalChancePercent);
            BossDamageMultiplier = SanitizeMultiplier(bossDamageMultiplier);
            AutoAttackEnabled = autoAttackEnabled;
            AutoAttackIntervalSeconds = autoAttackIntervalSeconds > 0f &&
                                         !float.IsNaN(autoAttackIntervalSeconds) &&
                                         !float.IsInfinity(autoAttackIntervalSeconds)
                ? autoAttackIntervalSeconds
                : 0f;
        }

        public float CriticalDamageMultiplier { get; }
        public long AttackPower { get; }
        public float RangeMultiplier { get; }
        public float AttackCooldownSeconds { get; }
        public int HitsPerBundle { get; }
        public float CriticalChancePercent { get; }
        public float BossDamageMultiplier { get; }
        public bool AutoAttackEnabled { get; }
        public float AutoAttackIntervalSeconds { get; }

        public bool IsValid =>
            AttackPower > 0L &&
            RangeMultiplier > 0f &&
            !float.IsNaN(RangeMultiplier) &&
            !float.IsInfinity(RangeMultiplier) &&
            AttackCooldownSeconds >= 0f &&
            !float.IsNaN(AttackCooldownSeconds) &&
            !float.IsInfinity(AttackCooldownSeconds) &&
            HitsPerBundle > 0 &&
            CriticalChancePercent >= 0f &&
            CriticalChancePercent <= 100f &&
            BossDamageMultiplier > 0f &&
            !float.IsNaN(BossDamageMultiplier) &&
            !float.IsInfinity(BossDamageMultiplier) &&
            (!AutoAttackEnabled || AutoAttackIntervalSeconds > 0f);

        private static float SanitizePercent(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : UnityClamp(value, 0f, 100f);
        }

        private static float SanitizeMultiplier(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value)
                ? value
                : 1f;
        }

        private static float UnityClamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
