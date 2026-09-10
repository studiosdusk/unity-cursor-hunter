using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Data
{
    /// <summary>
    /// Static definition for one normal-monster species.
    /// Runtime HP and run progress are never stored in this asset.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MonsterDefinition",
        menuName = "Cursor Hunter/Data/Monster Definition")]
    public sealed class MonsterDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string monsterId = "monster.slime";
        [SerializeField] private string displayName = "Slime";
        [SerializeField] private string prefabKey = "walker_stump";

        [Header("Combat")]
        [SerializeField, Min(1)] private long maxHealth = 20;
        [SerializeField, Min(0.05f)] private float spawnIntervalSeconds = 1.5f;
        [SerializeField, Min(1)] private int packSize = 1;
        [SerializeField, Min(0)] private long garnetReward = 3;

        public string MonsterId => monsterId;
        public string DisplayName => displayName;
        public string PrefabKey => prefabKey;
        public long MaxHealth => maxHealth;
        public float SpawnIntervalSeconds => spawnIntervalSeconds;
        public int PackSize => packSize;
        public long GarnetReward => garnetReward;

        public SpawnSnapshot CreateSnapshot(int aliveLimit)
        {
            return new SpawnSnapshot(
                monsterId,
                prefabKey,
                maxHealth,
                spawnIntervalSeconds,
                packSize,
                aliveLimit,
                garnetReward);
        }

        private void OnValidate()
        {
            if (maxHealth < 1L)
            {
                maxHealth = 1L;
            }

            spawnIntervalSeconds = Mathf.Max(0.05f, spawnIntervalSeconds);
            packSize = Mathf.Max(1, packSize);

            if (garnetReward < 0L)
            {
                garnetReward = 0L;
            }
        }
    }
}
