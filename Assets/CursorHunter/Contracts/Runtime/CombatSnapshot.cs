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
        {
            AttackPower = attackPower > 0 ? attackPower : 1;
            RangeMultiplier = rangeMultiplier > 0f ? rangeMultiplier : 1f;
            AttackCooldownSeconds = attackCooldownSeconds >= 0f ? attackCooldownSeconds : 0f;
            HitsPerBundle = hitsPerBundle > 0 ? hitsPerBundle : 1;
        }

        public long AttackPower { get; }
        public float RangeMultiplier { get; }
        public float AttackCooldownSeconds { get; }
        public int HitsPerBundle { get; }
    }
}
