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
        public SkillCombatSnapshot(string skillId, bool unlocked)
            : this(skillId, unlocked, null)
        {
        }

        public SkillCombatSnapshot(string skillId, bool unlocked,
            SkillStatUpgrade[] upgrades)
        {
            SkillId = skillId ?? string.Empty;
            Unlocked = unlocked;
            _upgrades = upgrades == null
                ? Array.Empty<SkillStatUpgrade>()
                : (SkillStatUpgrade[])upgrades.Clone();
            _upgradeView = Array.AsReadOnly(_upgrades);
        }

        private readonly SkillStatUpgrade[] _upgrades;
        private readonly IReadOnlyList<SkillStatUpgrade> _upgradeView;
        public string SkillId { get; }
        public bool Unlocked { get; }
        public IReadOnlyList<SkillStatUpgrade> Upgrades =>
            _upgradeView ?? Array.Empty<SkillStatUpgrade>();
        public bool IsValid
        {
            get
            {
                if (!SkillStatUpgrade.IsKnownSkill(SkillId)) return false;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < Upgrades.Count; i++)
                    if (!Upgrades[i].IsValidFor(SkillId) ||
                        !seen.Add(Upgrades[i].StatId)) return false;
                return true;
            }
        }

        public float GetUpgrade(string statId)
        {
            for (int i = 0; i < Upgrades.Count; i++)
                if (Upgrades[i].StatId == statId) return Upgrades[i].Delta;
            return 0f;
        }
    }

    /// <summary>One already-aggregated additive upgrade for a skill stat.</summary>
    public readonly struct SkillStatUpgrade
    {
        public SkillStatUpgrade(string statId, float delta)
        {
            StatId = statId ?? string.Empty;
            Delta = delta;
        }

        public string StatId { get; }
        public float Delta { get; }

        public bool IsValidFor(string skillId)
        {
            return !float.IsNaN(Delta) && !float.IsInfinity(Delta) &&
                (StatId != "count" || Delta == (float)Math.Floor(Delta)) &&
                IsAllowed(skillId, StatId);
        }

        public static bool IsKnownSkill(string skillId)
        {
            return skillId == "skill.tornado" || skillId == "skill.headshot" ||
                skillId == "skill.thunder" || skillId == "skill.flame_exhaust" ||
                skillId == "skill.magic_ball" || skillId == "skill.laser" ||
                skillId == "skill.pet_baby_dragon";
        }

        public static bool IsAllowed(string skillId, string statId)
        {
            if (statId == "cooldownSeconds")
                return IsKnownSkill(skillId) && skillId != "skill.magic_ball" &&
                    skillId != "skill.pet_baby_dragon";
            switch (skillId)
            {
                case "skill.tornado":
                    return statId == "count" || statId == "durationSeconds" ||
                        statId == "damageMultiplier" || statId == "firstCastDelaySeconds" ||
                        statId == "hitIntervalSeconds" || statId == "moveSpeedWorldUnitsPerSecond" ||
                        statId == "directionChangeSeconds" || statId == "stopChanceOnDirectionChange" ||
                        statId == "stopDurationSeconds" || statId == "shrinkOutSeconds";
                case "skill.headshot":
                    return statId == "firstCastDelaySeconds" ||
                        statId == "explosionDelaySeconds" || statId == "effectDurationSeconds" ||
                        statId == "splashDamageMultiplier" || statId == "bossDamageMultiplier";
                case "skill.thunder":
                    return statId == "firstCastDelaySeconds" ||
                        statId == "impactDelaySeconds" || statId == "effectDurationSeconds" ||
                        statId == "damageMultiplier";
                case "skill.flame_exhaust":
                    // The established upgrade direction is cooldown only.
                    return false;
                case "skill.magic_ball":
                    return statId == "moveSpeedWorldUnitsPerSecond" ||
                        statId == "sizeMultiplier" || statId == "damageMultiplier";
                case "skill.laser":
                    return statId == "sizeMultiplier" || statId == "damageMultiplier";
                case "skill.pet_baby_dragon":
                    return statId == "damageMultiplier" ||
                        statId == "attackIntervalSeconds" ||
                        statId == "moveSpeedWorldUnitsPerSecond" ||
                        statId == "detectionRadiusWorldUnits" ||
                        statId == "attackRangeWorldUnits";
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// One monster's immutable progression handoff. ProductionCount is the
    /// resolved count per spawn opportunity; base combat stats and species
    /// traits are authored in MonsterDefinition and copied by App at run start.
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
        /// <summary>
        /// Increase above the game's initial one-monster output. The App adds
        /// this value to the species BaseStats.PackSize when composing a run.
        /// </summary>
        public int ProductionBonusCount => Math.Max(0, ProductionCount - 1);
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
    /// Immutable relic definition and discovered state captured at run start.
    /// DropChancePercent is the fixed per-relic probability for SourceMonsterId;
    /// acquiring another relic never changes it.
    /// </summary>
    public readonly struct RelicCombatSnapshot
    {
        public RelicCombatSnapshot(
            string relicId,
            bool owned,
            string sourceMonsterId,
            float dropChancePercent,
            string effectId,
            float effectValue,
            float effectValue2)
        {
            RelicId = relicId ?? string.Empty;
            Owned = owned;
            SourceMonsterId = sourceMonsterId ?? string.Empty;
            DropChancePercent = dropChancePercent;
            EffectId = effectId ?? string.Empty;
            EffectValue = effectValue;
            EffectValue2 = effectValue2;
        }

        public string RelicId { get; }
        public bool Owned { get; }
        public string SourceMonsterId { get; }
        public float DropChancePercent { get; }
        public string EffectId { get; }
        public float EffectValue { get; }
        public float EffectValue2 { get; }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(RelicId) &&
            !string.IsNullOrWhiteSpace(SourceMonsterId) &&
            !string.IsNullOrWhiteSpace(EffectId) &&
            DropChancePercent > 0f && DropChancePercent <= 100f &&
            !float.IsNaN(DropChancePercent) && !float.IsInfinity(DropChancePercent) &&
            EffectValue >= 0f && EffectValue <= 1000f &&
            !float.IsNaN(EffectValue) && !float.IsInfinity(EffectValue) &&
            EffectValue2 >= 0f && EffectValue2 <= 100f &&
            !float.IsNaN(EffectValue2) && !float.IsInfinity(EffectValue2);
    }

    /// <summary>Authored relic award policy captured with the run.</summary>
    public readonly struct RelicDropRulesCombatSnapshot
    {
        public RelicDropRulesCombatSnapshot(
            int maxPerKill,
            string candidatePolicy,
            string resolution,
            bool preserveBaseChanceAfterAcquisition)
        {
            MaxPerKill = maxPerKill;
            CandidatePolicy = candidatePolicy ?? string.Empty;
            Resolution = resolution ?? string.Empty;
            PreserveBaseChanceAfterAcquisition = preserveBaseChanceAfterAcquisition;
        }

        public int MaxPerKill { get; }
        public string CandidatePolicy { get; }
        public string Resolution { get; }
        public bool PreserveBaseChanceAfterAcquisition { get; }

        public bool IsValid =>
            (MaxPerKill == 0 && string.IsNullOrEmpty(CandidatePolicy) &&
             string.IsNullOrEmpty(Resolution) && !PreserveBaseChanceAfterAcquisition) ||
            (MaxPerKill == 1 && CandidatePolicy == "unownedOnly" &&
             Resolution == "sumFixedChanceThenWeightedPick" &&
             PreserveBaseChanceAfterAcquisition);
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
        private readonly RelicCombatSnapshot[] _relics;
        private readonly IReadOnlyList<SkillCombatSnapshot> _skillView;
        private readonly IReadOnlyList<MonsterCombatSnapshot> _monsterView;
        private readonly IReadOnlyList<RelicCombatSnapshot> _relicView;

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
            float bossFieldDurationSeconds = 30f,
            CursorCombatStatBonusesSnapshot cursorStatBonuses = default,
            RelicCombatSnapshot[] relics = null,
            RelicDropRulesCombatSnapshot relicDropRules = default)
        {
            GlobalAliveLimit = Math.Max(1, Math.Min(80, globalAliveLimit));
            PerMonsterAliveLimit = Math.Max(1, Math.Min(GlobalAliveLimit, perMonsterAliveLimit));
            BossFieldDurationSeconds = bossFieldDurationSeconds;
            SourceJson = sourceJson ?? string.Empty;
            Combat = combat;
            CursorStatBonuses = cursorStatBonuses;
            RelicDropRules = relicDropRules;
            _skills = skills == null
                ? Array.Empty<SkillCombatSnapshot>()
                : (SkillCombatSnapshot[])skills.Clone();
            _monsters = monsters == null
                ? Array.Empty<MonsterCombatSnapshot>()
                : (MonsterCombatSnapshot[])monsters.Clone();
            _relics = relics == null
                ? Array.Empty<RelicCombatSnapshot>()
                : (RelicCombatSnapshot[])relics.Clone();
            _skillView = Array.AsReadOnly(_skills);
            _monsterView = Array.AsReadOnly(_monsters);
            _relicView = Array.AsReadOnly(_relics);
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
        public CursorCombatStatBonusesSnapshot CursorStatBonuses { get; }
        public RelicDropRulesCombatSnapshot RelicDropRules { get; }
        public IReadOnlyList<SkillCombatSnapshot> Skills => _skillView;
        public IReadOnlyList<MonsterCombatSnapshot> Monsters => _monsterView;
        public IReadOnlyList<RelicCombatSnapshot> Relics => _relicView;
        public float NormalFieldDurationSeconds { get; }

        public bool IsValid =>
            Combat.IsValid &&
            CursorStatBonuses.IsValid &&
            RelicDropRules.IsValid &&
            GlobalAliveLimit >= 1 && GlobalAliveLimit <= 80 &&
            PerMonsterAliveLimit >= 1 && PerMonsterAliveLimit <= GlobalAliveLimit &&
            BossFieldDurationSeconds > 0f && !float.IsNaN(BossFieldDurationSeconds) && !float.IsInfinity(BossFieldDurationSeconds) &&
            AreValid(_skills) &&
            AreValid(_monsters) &&
            AreValid(_relics);

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

        private static bool AreValid(RelicCombatSnapshot[] values)
        {
            for (int i = 0; i < values.Length; i++)
                if (!values[i].IsValid) return false;
            return true;
        }
    }
}
