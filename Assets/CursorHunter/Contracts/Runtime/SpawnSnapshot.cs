namespace CursorHunter.Contracts
{
    /// <summary>
    /// Read-only spawn values copied from a monster definition at run start.
    /// </summary>
    public readonly struct SpawnSnapshot
    {
        public SpawnSnapshot(
            string monsterId,
            string prefabKey,
            long maxHealth,
            float spawnIntervalSeconds,
            int packSize,
            int aliveLimit,
            long garnetReward)
            : this(
                monsterId,
                prefabKey,
                maxHealth,
                spawnIntervalSeconds,
                packSize,
                aliveLimit,
                garnetReward,
                string.Empty,
                0L,
                0f)
        {
        }

        public SpawnSnapshot(
            string monsterId,
            string prefabKey,
            long maxHealth,
            float spawnIntervalSeconds,
            int packSize,
            int aliveLimit,
            long garnetReward,
            string bonusDropCurrencyId,
            long bonusDropAmount,
            float bonusDropChancePercent,
            MonsterBehaviorType behaviorType = MonsterBehaviorType.None)
        {
            MonsterId = monsterId ?? string.Empty;
            PrefabKey = prefabKey ?? string.Empty;
            MaxHealth = maxHealth > 0 ? maxHealth : 1;
            SpawnIntervalSeconds = spawnIntervalSeconds > 0f ? spawnIntervalSeconds : 1f;
            PackSize = packSize > 0 ? packSize : 1;
            AliveLimit = aliveLimit > 0 ? aliveLimit : 1;
            GarnetReward = garnetReward >= 0 ? garnetReward : 0;
            BonusDropCurrencyId = bonusDropCurrencyId ?? string.Empty;
            BonusDropAmount = bonusDropAmount > 0L ? bonusDropAmount : 0L;
            BonusDropChancePercent = SanitizePercent(bonusDropChancePercent);
            BehaviorType = behaviorType;
        }

        public string MonsterId { get; }
        public string PrefabKey { get; }
        public long MaxHealth { get; }
        public float SpawnIntervalSeconds { get; }
        public int PackSize { get; }
        public int AliveLimit { get; }
        public long GarnetReward { get; }
        public string BonusDropCurrencyId { get; }
        public long BonusDropAmount { get; }
        public float BonusDropChancePercent { get; }
        public MonsterBehaviorType BehaviorType { get; }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(MonsterId) &&
            !string.IsNullOrWhiteSpace(PrefabKey) &&
            MaxHealth > 0L &&
            SpawnIntervalSeconds > 0f &&
            !float.IsNaN(SpawnIntervalSeconds) &&
            !float.IsInfinity(SpawnIntervalSeconds) &&
            PackSize > 0 &&
            AliveLimit > 0 &&
            GarnetReward >= 0L &&
            BonusDropAmount >= 0L &&
            (BonusDropAmount == 0L ||
             (!string.IsNullOrWhiteSpace(BonusDropCurrencyId) &&
              BonusDropChancePercent > 0f)) &&
            BehaviorType == MonsterBehaviorType.None;

        private static float SanitizePercent(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return value < 0f ? 0f : value > 100f ? 100f : value;
        }
    }
}
