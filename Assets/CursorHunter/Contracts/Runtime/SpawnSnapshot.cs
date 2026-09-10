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
        {
            MonsterId = monsterId ?? string.Empty;
            PrefabKey = prefabKey ?? string.Empty;
            MaxHealth = maxHealth > 0 ? maxHealth : 1;
            SpawnIntervalSeconds = spawnIntervalSeconds > 0f ? spawnIntervalSeconds : 1f;
            PackSize = packSize > 0 ? packSize : 1;
            AliveLimit = aliveLimit > 0 ? aliveLimit : 1;
            GarnetReward = garnetReward >= 0 ? garnetReward : 0;
        }

        public string MonsterId { get; }
        public string PrefabKey { get; }
        public long MaxHealth { get; }
        public float SpawnIntervalSeconds { get; }
        public int PackSize { get; }
        public int AliveLimit { get; }
        public long GarnetReward { get; }
    }
}
