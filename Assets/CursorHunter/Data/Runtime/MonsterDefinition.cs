using System;
using CursorHunter.Contracts;
using UnityEngine;
using UnityEngine.Serialization;

namespace CursorHunter.Data
{
    /// <summary>
    /// The authored, species-specific baseline. Runtime combat state and
    /// progression purchases are copied into a SpawnSnapshot at run start.
    /// </summary>
    [Serializable]
    public sealed class MonsterBaseStats
    {
        [SerializeField, Min(1)] private long maxHealth = 20;
        [SerializeField, Min(0.05f)] private float spawnIntervalSeconds = 1.5f;
        [SerializeField, Min(1)] private int packSize = 1;
        [SerializeField, Min(0.01f)] private float moveSpeed = 0.45f;
        [SerializeField, Min(0.01f)] private float visualScale = 1f;
        [SerializeField, Min(0.01f)] private float hitAreaWidth = 1f;
        [SerializeField, Min(0.01f)] private float hitAreaHeight = 1.2f;
        [SerializeField, Min(0)] private long garnetReward = 3;
        [SerializeField] private string bonusDropCurrencyId = string.Empty;
        [SerializeField, Min(0)] private long bonusDropAmount;
        [SerializeField, Range(0f, 100f)] private float bonusDropChancePercent;

        public long MaxHealth => Math.Max(1L, maxHealth);
        public float SpawnIntervalSeconds => IsFinitePositive(spawnIntervalSeconds)
            ? spawnIntervalSeconds
            : 1.5f;
        public int PackSize => Math.Max(1, packSize);
        public float MoveSpeed => IsFinitePositive(moveSpeed) ? moveSpeed : 0.45f;
        public float VisualScale => IsFinitePositive(visualScale) ? visualScale : 1f;
        public float HitAreaWidth => IsFinitePositive(hitAreaWidth) ? hitAreaWidth : 1f;
        public float HitAreaHeight => IsFinitePositive(hitAreaHeight) ? hitAreaHeight : 1.2f;
        public long GarnetReward => Math.Max(0L, garnetReward);
        public string BonusDropCurrencyId => bonusDropCurrencyId ?? string.Empty;
        public long BonusDropAmount => Math.Max(0L, bonusDropAmount);
        public float BonusDropChancePercent => SanitizePercent(bonusDropChancePercent);

        internal void Normalize()
        {
            maxHealth = Math.Max(1L, maxHealth);
            spawnIntervalSeconds = IsFinitePositive(spawnIntervalSeconds)
                ? spawnIntervalSeconds
                : 1.5f;
            packSize = Math.Max(1, packSize);
            moveSpeed = IsFinitePositive(moveSpeed) ? moveSpeed : 0.45f;
            visualScale = IsFinitePositive(visualScale) ? visualScale : 1f;
            hitAreaWidth = IsFinitePositive(hitAreaWidth) ? hitAreaWidth : 1f;
            hitAreaHeight = IsFinitePositive(hitAreaHeight) ? hitAreaHeight : 1.2f;
            garnetReward = Math.Max(0L, garnetReward);
            bonusDropCurrencyId = string.IsNullOrWhiteSpace(bonusDropCurrencyId)
                ? string.Empty
                : bonusDropCurrencyId.Trim();
            bonusDropAmount = Math.Max(0L, bonusDropAmount);
            bonusDropChancePercent = SanitizePercent(bonusDropChancePercent);

            if (bonusDropAmount == 0L || string.IsNullOrEmpty(bonusDropCurrencyId))
            {
                bonusDropAmount = 0L;
                bonusDropChancePercent = 0f;
            }
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float SanitizePercent(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Mathf.Clamp(value, 0f, 100f);
        }
    }

    /// <summary>
    /// Static identity and baseline data for one monster species. Its behavior
    /// profiles are independent contributions composed over BaseStats.
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
        [FormerlySerializedAs("prefab")]
        [SerializeField] private GameObject visualPrefab;

        [Header("Base stats")]
        [SerializeField] private MonsterBaseStats baseStats = new MonsterBaseStats();

        [Header("Composable behavior profiles")]
        [SerializeField] private MonsterBehaviorProfile[] behaviorProfiles =
            Array.Empty<MonsterBehaviorProfile>();

        public string MonsterId => monsterId ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public string PrefabKey => prefabKey ?? string.Empty;
        public GameObject VisualPrefab => visualPrefab;
        public GameObject Prefab => VisualPrefab;
        public MonsterBaseStats BaseStats => baseStats;
        public MonsterBehaviorProfile[] BehaviorProfiles => behaviorProfiles == null
            ? Array.Empty<MonsterBehaviorProfile>()
            : (MonsterBehaviorProfile[])behaviorProfiles.Clone();

        // Compatibility accessors for existing data consumers. These expose
        // the base values; profile-adjusted values live in SpawnSnapshot.
        public long MaxHealth => BaseStats.MaxHealth;
        public float SpawnIntervalSeconds => BaseStats.SpawnIntervalSeconds;
        public int PackSize => BaseStats.PackSize;
        public long GarnetReward => BaseStats.GarnetReward;
        public string BonusDropCurrencyId => BaseStats.BonusDropCurrencyId;
        public long BonusDropAmount => BaseStats.BonusDropAmount;
        public float BonusDropChancePercent => BaseStats.BonusDropChancePercent;
        public MonsterBehaviorType BehaviorType => MonsterBehaviorType.None;

        public SpawnSnapshot CreateSnapshot(int aliveLimit)
        {
            return CreateSnapshot(aliveLimit, BaseStats.PackSize);
        }

        public SpawnSnapshot CreateSnapshotWithProductionBonus(
            int aliveLimit,
            int productionBonusCount)
        {
            long finalPackSize = (long)BaseStats.PackSize +
                                 Math.Max(0, productionBonusCount);
            return CreateSnapshot(
                aliveLimit,
                finalPackSize >= int.MaxValue
                    ? int.MaxValue
                    : (int)finalPackSize);
        }

        /// <summary>
        /// Builds final run stats from authored base values and trait modules.
        /// The final pack size is supplied after App adds Progression's count
        /// increase to this species' BaseStats.PackSize.
        /// </summary>
        public SpawnSnapshot CreateSnapshot(int aliveLimit, int finalPackSize)
        {
            MonsterBaseStats stats = BaseStats;
            float healthMultiplier = 1f;
            float moveSpeedMultiplier = 1f;
            float visualScaleMultiplier = 1f;
            float hitAreaScaleMultiplier = 1f;
            MonsterMovementMode movementMode = MonsterMovementMode.BoundedWander;
            float orbitRadius = 0.65f;
            float orbitAngularSpeedDegrees = 90f;

            if (behaviorProfiles != null)
            {
                for (int index = 0; index < behaviorProfiles.Length; index++)
                {
                    MonsterBehaviorProfile profile = behaviorProfiles[index];
                    if (profile == null)
                    {
                        continue;
                    }

                    healthMultiplier = MultiplyFinite(
                        healthMultiplier,
                        profile.HealthMultiplier);
                    moveSpeedMultiplier = MultiplyFinite(
                        moveSpeedMultiplier,
                        profile.MoveSpeedMultiplier);
                    visualScaleMultiplier = MultiplyFinite(
                        visualScaleMultiplier,
                        profile.VisualScaleMultiplier);
                    hitAreaScaleMultiplier = MultiplyFinite(
                        hitAreaScaleMultiplier,
                        profile.HitAreaScaleMultiplier);

                    // List order is intentional: if multiple profiles select
                    // a movement strategy, the later profile takes precedence.
                    if (profile.OverridesMovementMode)
                    {
                        movementMode = profile.MovementMode;
                        orbitRadius = profile.OrbitRadius;
                        orbitAngularSpeedDegrees = profile.OrbitAngularSpeedDegrees;
                    }
                }
            }

            return new SpawnSnapshot(
                MonsterId,
                PrefabKey,
                ScaleHealth(stats.MaxHealth, healthMultiplier),
                stats.SpawnIntervalSeconds,
                Math.Max(1, finalPackSize),
                aliveLimit,
                stats.GarnetReward,
                stats.BonusDropCurrencyId,
                stats.BonusDropAmount,
                stats.BonusDropChancePercent,
                MonsterBehaviorType.None,
                movementMode,
                stats.MoveSpeed * moveSpeedMultiplier,
                stats.VisualScale * visualScaleMultiplier,
                stats.HitAreaWidth * hitAreaScaleMultiplier,
                stats.HitAreaHeight * hitAreaScaleMultiplier,
                orbitRadius,
                orbitAngularSpeedDegrees);
        }

        private void OnValidate()
        {
            monsterId = monsterId == null ? string.Empty : monsterId.Trim();
            displayName = displayName == null ? string.Empty : displayName.Trim();
            prefabKey = prefabKey == null ? string.Empty : prefabKey.Trim();
            if (baseStats == null)
            {
                baseStats = new MonsterBaseStats();
            }
            baseStats.Normalize();
            if (behaviorProfiles == null)
            {
                behaviorProfiles = Array.Empty<MonsterBehaviorProfile>();
            }
        }

        private static float MultiplyFinite(float accumulated, float contribution)
        {
            double result = (double)accumulated * contribution;
            if (double.IsNaN(result) || double.IsInfinity(result) || result <= 0d)
            {
                return accumulated;
            }

            return result >= float.MaxValue ? float.MaxValue : (float)result;
        }

        private static long ScaleHealth(long value, float multiplier)
        {
            double result = Math.Round(value * (double)multiplier);
            if (double.IsNaN(result) || result < 1d)
            {
                return 1L;
            }

            return result >= long.MaxValue ? long.MaxValue : (long)result;
        }
    }
}
