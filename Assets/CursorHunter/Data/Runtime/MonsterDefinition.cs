using System;
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
        [SerializeField] private GameObject prefab;

        [Header("Combat")]
        [SerializeField, Min(1)] private long maxHealth = 20;
        [SerializeField, Min(0.05f)] private float spawnIntervalSeconds = 1.5f;
        [SerializeField, Min(1)] private int packSize = 1;
        [SerializeField, Min(0)] private long garnetReward = 3;

        [Header("Bonus drop")]
        [SerializeField] private string bonusDropCurrencyId = string.Empty;
        [SerializeField, Min(0)] private long bonusDropAmount;
        [SerializeField, Range(0f, 100f)] private float bonusDropChancePercent;
        [SerializeField] private MonsterBehaviorType behaviorType;

        public string MonsterId => monsterId;
        public string DisplayName => displayName;
        public string PrefabKey => prefabKey;
        public GameObject Prefab => prefab;
        public long MaxHealth => maxHealth;
        public float SpawnIntervalSeconds => spawnIntervalSeconds;
        public int PackSize => packSize;
        public long GarnetReward => garnetReward;
        public string BonusDropCurrencyId => bonusDropCurrencyId ?? string.Empty;
        public long BonusDropAmount => Math.Max(0L, bonusDropAmount);
        public float BonusDropChancePercent => Mathf.Clamp(bonusDropChancePercent, 0f, 100f);
        public MonsterBehaviorType BehaviorType => behaviorType;

        public SpawnSnapshot CreateSnapshot(int aliveLimit)
        {
            return new SpawnSnapshot(
                monsterId,
                prefabKey,
                maxHealth,
                spawnIntervalSeconds,
                packSize,
                aliveLimit,
                garnetReward,
                BonusDropCurrencyId,
                BonusDropAmount,
                BonusDropChancePercent,
                BehaviorType);
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

            bonusDropCurrencyId = bonusDropCurrencyId == null
                ? string.Empty
                : bonusDropCurrencyId.Trim();
            if (bonusDropAmount < 0L)
            {
                bonusDropAmount = 0L;
            }

            bonusDropChancePercent = Mathf.Clamp(bonusDropChancePercent, 0f, 100f);
            if (bonusDropAmount == 0L || string.IsNullOrEmpty(bonusDropCurrencyId))
            {
                bonusDropAmount = 0L;
                bonusDropChancePercent = 0f;
            }

            behaviorType = MonsterBehaviorType.None;

        }
    }
}
