using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Data
{
    [CreateAssetMenu(fileName = "Boss01", menuName = "Cursor Hunter/Data/Boss Definition")]
    public sealed class BossDefinition : ScriptableObject
    {
        public const int StageCount = 20;
        public const float DurationSeconds = 30f;

        [SerializeField, Range(1, StageCount)] private int stage = 1;
        [SerializeField] private string bossId = "boss.01";
        [SerializeField] private string displayName = "보스";
        [SerializeField] private LocalizedText[] localizedNames = Array.Empty<LocalizedText>();
        [SerializeField] private GameObject visualPrefab;
        [Header("Common boss stats")]
        [SerializeField, Min(1)] private long maxHealth = 750;
        [SerializeField, Min(0.01f)] private float moveSpeed = 0.6f;
        [SerializeField, Min(0.01f)] private float visualScale = 2f;
        [SerializeField] private Vector2 hitArea = new Vector2(1.8f, 2.1f);
        [Header("Behavior")]
        [SerializeField] private MonsterMovementMode movementMode;
        [SerializeField] private MonsterSpecialAbility specialAbility;
        [SerializeField, Min(0.1f)] private float abilityInterval = 4f;
        [SerializeField, Min(0f)] private float abilityDuration = 1f;
        [SerializeField, Range(0f, 1f)] private float abilityStrength = 0.5f;
        [Serializable]
        public struct GemstoneReward
        {
            public string currencyId;
            [Min(0)] public long amount;
        }

        [Header("Clear rewards (all listed gemstones are guaranteed)")]
        [SerializeField] private GemstoneReward[] clearRewards =
        {
            new GemstoneReward { currencyId = "gem.garnet", amount = 1000 },
            new GemstoneReward { currencyId = "gem.topaz", amount = 350 },
            new GemstoneReward { currencyId = "gem.amethyst", amount = 150 }
        };

        public int Stage => stage;
        public string BossId => bossId;
        public string DefaultDisplayName => displayName;
        public string DisplayName => GetDisplayName(LocalizationCatalog.Language);
        public string GetDisplayName(string language) => LocalizedText.Find(localizedNames, language) ?? displayName;
        public GameObject VisualPrefab => visualPrefab;
        public bool IsValid => stage >= 1 && stage <= StageCount &&
            !string.IsNullOrWhiteSpace(bossId) && visualPrefab != null && maxHealth > 0 &&
            HasValidRewards();

        public SpawnSnapshot CreateSnapshot()
        {
            // The immutable run request owns boss rewards, independently of normal monster drops.
            return new SpawnSnapshot(bossId, bossId, maxHealth, DurationSeconds + 1f,
                1, 1, 0, string.Empty, 0, 0, movementMode: movementMode,
                moveSpeed: moveSpeed, visualScale: visualScale,
                hitAreaWidth: hitArea.x, hitAreaHeight: hitArea.y,
                specialAbility: specialAbility, specialAbilityIntervalSeconds: abilityInterval,
                specialAbilityDurationSeconds: abilityDuration, specialAbilityStrength: abilityStrength);
        }

        public ResourceRewardSnapshot[] CreateClearRewards()
        {
            if (!HasValidRewards()) throw new InvalidOperationException($"Invalid boss rewards: {bossId}");
            var result = new List<ResourceRewardSnapshot>(clearRewards.Length);
            foreach (GemstoneReward reward in clearRewards)
                if (reward.amount > 0) result.Add(new ResourceRewardSnapshot(reward.currencyId, reward.amount));
            return result.ToArray();
        }

        private bool HasValidRewards()
        {
            if (clearRewards == null || clearRewards.Length == 0) return false;
            bool any = false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (GemstoneReward reward in clearRewards)
            {
                if (!IsGemstone(reward.currencyId) || reward.amount < 0 || !ids.Add(reward.currencyId)) return false;
                any |= reward.amount > 0;
            }
            return any;
        }

        private static bool IsGemstone(string id) => id == "gem.garnet" || id == "gem.topaz" ||
            id == "gem.amethyst" || id == "gem.sapphire" || id == "gem.diamond" || id == "gem.dragon";
    }
}
