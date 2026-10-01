using System;
using System.Collections.Generic;

namespace CursorHunter.Contracts
{
    /// <summary>
    /// The immutable skill values captured at the beginning of a run. Combat
    /// consumes this data but never reaches into Progression or a save file.
    /// </summary>
    public readonly struct SkillCombatSnapshot
    {
        public SkillCombatSnapshot(
            string skillId,
            bool unlocked,
            float damageMultiplier,
            float radiusMultiplier,
            float cooldownMultiplier,
            long damage = 1L,
            float radiusWorldUnits = 1f,
            float cooldownSeconds = 1f)
        {
            Damage = damage;
            RadiusWorldUnits = radiusWorldUnits;
            CooldownSeconds = cooldownSeconds;
            SkillId = skillId ?? string.Empty;
            Unlocked = unlocked;
            DamageMultiplier = SanitizeMultiplier(damageMultiplier);
            RadiusMultiplier = SanitizeMultiplier(radiusMultiplier);
            CooldownMultiplier = cooldownMultiplier > 0f &&
                                 !float.IsNaN(cooldownMultiplier) &&
                                 !float.IsInfinity(cooldownMultiplier)
                ? cooldownMultiplier
                : 1f;
        }

        public long Damage { get; }
        public float RadiusWorldUnits { get; }
        public float CooldownSeconds { get; }
        public string SkillId { get; }
        public bool Unlocked { get; }
        public float DamageMultiplier { get; }
        public float RadiusMultiplier { get; }
        public float CooldownMultiplier { get; }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(SkillId) &&
            Damage > 0L && RadiusWorldUnits > 0f && CooldownSeconds > 0f &&
            !float.IsNaN(RadiusWorldUnits) && !float.IsInfinity(RadiusWorldUnits) &&
            !float.IsNaN(CooldownSeconds) && !float.IsInfinity(CooldownSeconds) &&
            DamageMultiplier > 0f &&
            RadiusMultiplier > 0f &&
            CooldownMultiplier > 0f;

        private static float SanitizeMultiplier(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value)
                ? value
                : 1f;
        }
    }

    /// <summary>
    /// One monster's immutable progression values. Spawn behavior flags remain
    /// a combat concern; this structure carries only the values needed by the
    /// first integration seam.
    /// </summary>
    public readonly struct MonsterCombatSnapshot
    {
        public MonsterCombatSnapshot(
            string monsterId,
            bool unlocked,
            long hitPoints,
            float spawnIntervalSeconds,
            float productionMultiplier)
            : this(
                monsterId,
                unlocked,
                hitPoints,
                spawnIntervalSeconds,
                productionMultiplier,
                3L,
                string.Empty,
                0L,
                0f)
        {
        }

        public MonsterCombatSnapshot(
            string monsterId,
            bool unlocked,
            long hitPoints,
            float spawnIntervalSeconds,
            float productionMultiplier,
            long garnetReward,
            string bonusDropCurrencyId,
            long bonusDropAmount,
            float bonusDropChancePercent,
            int productionCount = 1,
            MonsterBehaviorType behaviorType = MonsterBehaviorType.None)
        {
            ProductionCount = Math.Max(1, productionCount);
            MonsterId = monsterId ?? string.Empty;
            Unlocked = unlocked;
            HitPoints = hitPoints > 0L ? hitPoints : 1L;
            SpawnIntervalSeconds = spawnIntervalSeconds > 0f &&
                                   !float.IsNaN(spawnIntervalSeconds) &&
                                   !float.IsInfinity(spawnIntervalSeconds)
                ? spawnIntervalSeconds
                : 1f;
            ProductionMultiplier = productionMultiplier > 0f &&
                                   !float.IsNaN(productionMultiplier) &&
                                   !float.IsInfinity(productionMultiplier)
                ? productionMultiplier
                : 1f;
            GarnetReward = garnetReward >= 0L ? garnetReward : 0L;
            BonusDropCurrencyId = bonusDropCurrencyId ?? string.Empty;
            BonusDropAmount = bonusDropAmount > 0L ? bonusDropAmount : 0L;
            BonusDropChancePercent = SanitizePercent(bonusDropChancePercent);
            BehaviorType = behaviorType;
        }

        public int ProductionCount { get; }
        public string MonsterId { get; }
        public bool Unlocked { get; }
        public long HitPoints { get; }
        public float SpawnIntervalSeconds { get; }
        public float ProductionMultiplier { get; }
        public long GarnetReward { get; }
        public string BonusDropCurrencyId { get; }
        public long BonusDropAmount { get; }
        public float BonusDropChancePercent { get; }
        public MonsterBehaviorType BehaviorType { get; }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(MonsterId) &&
            HitPoints > 0L &&
            SpawnIntervalSeconds > 0f &&
            ProductionMultiplier > 0f &&
            GarnetReward >= 0L &&
            BonusDropAmount >= 0L &&
            (BonusDropAmount == 0L || !string.IsNullOrWhiteSpace(BonusDropCurrencyId)) &&
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

    /// <summary>
    /// A copied, read-only handoff from Progression to App/Combat. Arrays are
    /// copied at construction so a later UI purchase cannot mutate an active
    /// run's values.
    /// </summary>
    public sealed class ProgressionCombatSnapshot
    {
        private readonly SkillCombatSnapshot[] _skills;
        private readonly MonsterCombatSnapshot[] _monsters;
        private readonly IReadOnlyList<SkillCombatSnapshot> _skillView;
        private readonly IReadOnlyList<MonsterCombatSnapshot> _monsterView;

        public ProgressionCombatSnapshot(
            CombatSnapshot combat,
            SkillCombatSnapshot[] skills,
            MonsterCombatSnapshot[] monsters)
            : this(combat, skills, monsters, 15f)
        {
        }

        public ProgressionCombatSnapshot(
            CombatSnapshot combat,
            SkillCombatSnapshot[] skills,
            MonsterCombatSnapshot[] monsters,
            float normalFieldDurationSeconds,
            string sourceJson = null,
            int globalAliveLimit = 80,
            int perMonsterAliveLimit = 80,
            float bossFieldDurationSeconds = 60f)
        {
            GlobalAliveLimit = Math.Max(1, Math.Min(80, globalAliveLimit));
            PerMonsterAliveLimit = Math.Max(1, Math.Min(GlobalAliveLimit, perMonsterAliveLimit));
            BossFieldDurationSeconds = bossFieldDurationSeconds;
            SourceJson = sourceJson ?? string.Empty;
            Combat = combat;
            _skills = skills == null
                ? Array.Empty<SkillCombatSnapshot>()
                : (SkillCombatSnapshot[])skills.Clone();
            _monsters = monsters == null
                ? Array.Empty<MonsterCombatSnapshot>()
                : (MonsterCombatSnapshot[])monsters.Clone();
            _skillView = Array.AsReadOnly(_skills);
            _monsterView = Array.AsReadOnly(_monsters);
            NormalFieldDurationSeconds = normalFieldDurationSeconds >= 15f &&
                                         !float.IsNaN(normalFieldDurationSeconds) &&
                                         !float.IsInfinity(normalFieldDurationSeconds)
                ? Math.Min(30f, normalFieldDurationSeconds)
                : 15f;
        }

        public int GlobalAliveLimit { get; }
        public int PerMonsterAliveLimit { get; }
        public float BossFieldDurationSeconds { get; }
        public string SourceJson { get; }
        public CombatSnapshot Combat { get; }
        public IReadOnlyList<SkillCombatSnapshot> Skills => _skillView;
        public IReadOnlyList<MonsterCombatSnapshot> Monsters => _monsterView;
        public float NormalFieldDurationSeconds { get; }

        public bool IsValid =>
            Combat.IsValid &&
            GlobalAliveLimit >= 1 && GlobalAliveLimit <= 80 &&
            PerMonsterAliveLimit >= 1 && PerMonsterAliveLimit <= GlobalAliveLimit &&
            BossFieldDurationSeconds > 0f && !float.IsNaN(BossFieldDurationSeconds) && !float.IsInfinity(BossFieldDurationSeconds) &&
            AreValid(_skills) &&
            AreValid(_monsters);

        private static bool AreValid(SkillCombatSnapshot[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!values[i].IsValid)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreValid(MonsterCombatSnapshot[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!values[i].IsValid)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
