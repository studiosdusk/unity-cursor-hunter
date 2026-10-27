using System;

namespace CursorHunter.Contracts
{
    // JSON DTOs use English keys and explicit units. DTOs are mutable only while
    // assembling/loading; combat receives copied immutable snapshots.
    [Serializable]
    public sealed class GameInformation
    {
        public int schemaVersion;
        public int balanceVersion;
        public StatInformation stats;
        public CombatRulesInformation rules = new CombatRulesInformation();
        public GemstoneInformation[] gemstones;
        public MonsterInformation[] monsters;
        public SkillInformation[] skills;
        public RelicInformation[] relics;
        public RelicDropRulesInformation relicDropRules;

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (schemaVersion != 4 || balanceVersion < 1 || stats == null ||
                gemstones == null || monsters == null || skills == null || relics == null ||
                relicDropRules == null)
                return Fail("Missing sections or unsupported schemaVersion.", out error);
            if (stats.attackPower < 1 || !Positive(stats.attackRadiusWorldUnits) ||
                !Positive(stats.attackCooldownSeconds) || stats.attackCooldownSeconds < 0.05f ||
                !Percent(stats.criticalChancePercent) || !Positive(stats.bossDamageMultiplier) ||
                !Positive(stats.normalFieldDurationSeconds) ||
                stats.normalFieldDurationSeconds < 15f || stats.normalFieldDurationSeconds > 30f)
                return Fail("Invalid stats or field duration (15..30 seconds).", out error);
            if (rules == null || !Positive(rules.criticalDamageMultiplier) || rules.criticalDamageMultiplier < 1f ||
                !Positive(rules.bossFieldDurationSeconds) || rules.globalAliveLimit < 1 || rules.globalAliveLimit > 80 ||
                rules.perMonsterAliveLimit < 1 || rules.perMonsterAliveLimit > rules.globalAliveLimit)
                return Fail("Invalid combat rules (maximum 80 alive targets).", out error);
            if (relicDropRules.maxPerKill != 1 ||
                relicDropRules.candidatePolicy != "unownedOnly" ||
                relicDropRules.resolution != "sumFixedChanceThenWeightedPick" ||
                !relicDropRules.preserveBaseChanceAfterAcquisition)
                return Fail("Invalid relic drop rules.", out error);
            if (gemstones.Length != 6 || monsters.Length != 10 || relics.Length != 20 ||
                (skills.Length != 1 && skills.Length != 4 && skills.Length != 5 &&
                 skills.Length != 6 && skills.Length != 7))
                return Fail("Expected 6 gemstones, 10 monsters, 20 relics and 1, 4, 5, 6 or 7 skills.", out error);
            string[] gemIds = { "gem.garnet", "gem.topaz", "gem.amethyst", "gem.sapphire", "gem.diamond", "gem.dragon" };
            for (int i = 0; i < gemstones.Length; i++)
            {
                var gem = gemstones[i];
                if (gem == null || gem.id != gemIds[i] || gem.amount < 0)
                    return Fail("Invalid gemstone ID/order/amount.", out error);
            }
            for (int i = 0; i < monsters.Length; i++)
            {
                var monster = monsters[i];
                if (monster == null || monster.id != "monster." + (i + 1).ToString("00") ||
                    monster.hitPoints < 1 || monster.productionCount < 1 || monster.productionCount > 16 ||
                    !Positive(monster.spawnIntervalSeconds) || monster.spawnIntervalSeconds < 0.1f ||
                    monster.garnetReward < 0 || monster.gemstoneAmount < 0 ||
                    !Percent(monster.gemstoneChancePercent) ||
                    Array.IndexOf(gemIds, monster.gemstoneId) < 0 ||
                    monster.behaviorType != MonsterBehaviorType.None)
                    return Fail("Invalid monster data.", out error);
            }
            string[] skillIds = { "skill.tornado", "skill.headshot",
                "skill.thunder", "skill.flame_exhaust", "skill.magic_ball",
                "skill.laser", "skill.pet_baby_dragon" };
            for (int i = 0; i < skills.Length; i++)
            {
                SkillInformation skill = skills[i];
                if (skill == null || skill.id != skillIds[i])
                    return Fail("Invalid skill ID/order.", out error);
                if (skill.upgrades == null) continue;
                var seen = new System.Collections.Generic.HashSet<string>(
                    StringComparer.Ordinal);
                for (int j = 0; j < skill.upgrades.Length; j++)
                {
                    SkillStatUpgradeInformation upgrade = skill.upgrades[j];
                    if (upgrade == null || !seen.Add(upgrade.statId) ||
                        !new SkillStatUpgrade(upgrade.statId,
                            upgrade.delta).IsValidFor(skill.id))
                        return Fail("Invalid skill upgrade.", out error);
                }
            }
            string[] relicIds = {
                "relic.red_potion", "relic.blue_potion", "relic.monster_meat",
                "relic.thief_shoes", "relic.archers_bow", "relic.warrior_armor",
                "relic.wizards_hat", "relic.valor_ring", "relic.lucky_dice",
                "relic.kraken_eye", "relic.four_leaf_clover", "relic.ancient_book",
                "relic.adventurers_bag", "relic.compass", "relic.megaphone",
                "relic.telescope", "relic.stone", "relic.rainbow_feather",
                "relic.scales_of_judgment", "relic.treasure_chest"
            };
            string[] relicEffectIds = {
                "cursorAttackDamagePercent", "autoSkillDamagePercent", "petDamagePercent",
                "petMoveSpeedPercent", "petAttackIntervalReductionPercent",
                "normalFieldDurationBonusSeconds", "areaSkillRadiusPercent",
                "criticalDamageMultiplierBonus", "petDoubleDamageChancePercent",
                "lowHealthExecuteChancePercent", "bonusGemDropChancePercent",
                "petAttackRadiusPercent", "garnetKillRewardPercent",
                "petDetectionRadiusPercent", "monsterSpawnIntervalReductionPercent",
                "cursorAttackRadiusPercent", "fullHealthFirstHitDamagePercent",
                "autoSkillCooldownReductionPercent", "bossDamagePercent",
                "monsterKillRewardPercent"
            };
            var relicDropTotals = new float[monsters.Length];
            for (int i = 0; i < relics.Length; i++)
            {
                RelicInformation relic = relics[i];
                int sourceIndex = i / 2;
                if (relic == null || relic.id != relicIds[i] ||
                    relic.sourceMonsterId != "monster." + (sourceIndex + 1).ToString("00") ||
                    relic.effectId != relicEffectIds[i] ||
                    !Percent(relic.dropChancePercent) || relic.dropChancePercent <= 0f ||
                    !Finite(relic.effectValue, 0f, 1000f) ||
                    !Finite(relic.effectValue2, 0f, 100f))
                    return Fail("Invalid relic ID, source, drop chance or effect data.", out error);
                relicDropTotals[sourceIndex] += relic.dropChancePercent;
                if (i == 9 && (relic.effectValue2 <= 0f || relic.effectValue2 > 100f))
                    return Fail("Kraken Eye needs a valid low-health threshold.", out error);
                if (i != 9 && relic.effectValue2 != 0f)
                    return Fail("Only Kraken Eye uses a secondary effect value.", out error);
            }
            for (int i = 0; i < relicDropTotals.Length; i++)
                if (relicDropTotals[i] > 100f)
                    return Fail("A monster's individual relic drop chances cannot exceed 100%.", out error);
            return true;
        }

        public ProgressionCombatSnapshot ToCombatSnapshot(string sourceJson)
        {
            return ToCombatSnapshot(
                sourceJson,
                default(CursorCombatStatBonusesSnapshot));
        }

        public ProgressionCombatSnapshot ToCombatSnapshot(
            string sourceJson,
            CursorCombatStatBonusesSnapshot cursorStatBonuses)
        {
            if (!TryValidate(out string error))
                throw new ArgumentException(error);
            var skillSnapshots = new SkillCombatSnapshot[skills.Length];
            for (int i = 0; i < skills.Length; i++)
            {
                var value = skills[i];
                SkillStatUpgrade[] upgrades = value.upgrades == null
                    ? Array.Empty<SkillStatUpgrade>()
                    : new SkillStatUpgrade[value.upgrades.Length];
                for (int j = 0; j < upgrades.Length; j++)
                    upgrades[j] = new SkillStatUpgrade(
                        value.upgrades[j].statId, value.upgrades[j].delta);
                skillSnapshots[i] = new SkillCombatSnapshot(value.id,
                    value.enabled, upgrades);
            }
            var monsterSnapshots = new MonsterCombatSnapshot[monsters.Length];
            for (int i = 0; i < monsters.Length; i++)
            {
                var value = monsters[i];
                monsterSnapshots[i] = new MonsterCombatSnapshot(value.id, value.enabled,
                    value.hitPoints, value.spawnIntervalSeconds, 1f, value.garnetReward,
                    value.gemstoneId, value.gemstoneAmount, value.gemstoneChancePercent,
                    value.productionCount, value.behaviorType);
            }
            var relicSnapshots = new RelicCombatSnapshot[relics.Length];
            for (int i = 0; i < relics.Length; i++)
            {
                RelicInformation value = relics[i];
                relicSnapshots[i] = new RelicCombatSnapshot(
                    value.id, value.owned, value.sourceMonsterId,
                    value.dropChancePercent, value.effectId,
                    value.effectValue, value.effectValue2);
            }
            var combat = new CombatSnapshot(stats.attackPower,
                stats.attackRadiusWorldUnits / BaseAttackRadiusWorldUnits,
                stats.attackCooldownSeconds, 1, stats.criticalChancePercent,
                stats.bossDamageMultiplier, true, stats.attackCooldownSeconds, rules.criticalDamageMultiplier);
            return new ProgressionCombatSnapshot(combat, skillSnapshots, monsterSnapshots,
                stats.normalFieldDurationSeconds, sourceJson, rules.globalAliveLimit,
                rules.perMonsterAliveLimit, rules.bossFieldDurationSeconds,
                cursorStatBonuses, relicSnapshots,
                new RelicDropRulesCombatSnapshot(relicDropRules.maxPerKill,
                    relicDropRules.candidatePolicy, relicDropRules.resolution,
                    relicDropRules.preserveBaseChanceAfterAcquisition));
        }

        public const float BaseAttackRadiusWorldUnits = 1.7253809f;
        private static bool Finite(float value, float minimum, float maximum) =>
            value >= minimum && value <= maximum && !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Positive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Percent(float value) => value >= 0f && value <= 100f && !float.IsNaN(value);
        private static bool Fail(string message, out string error) { error = message; return false; }
    }

    [Serializable]
    public sealed class CombatRulesInformation
    {
        public float criticalDamageMultiplier = 2f;
        public int globalAliveLimit = 80;
        public int perMonsterAliveLimit = 80;
        public float bossFieldDurationSeconds = 60f;
    }

    [Serializable]
    public sealed class StatInformation
    {
        public long attackPower;
        public float attackRadiusWorldUnits;
        public float attackCooldownSeconds;
        public float criticalChancePercent;
        public float bossDamageMultiplier;
        public float normalFieldDurationSeconds;
    }

    [Serializable]
    public sealed class GemstoneInformation
    {
        public string id;
        // Availability is derived from unlocked monster sources, never purchased.
        public bool enabled;
        public long amount;
    }

    [Serializable]
    public sealed class MonsterInformation
    {
        public string id;
        public bool enabled;
        // Exact count per spawn opportunity, not a label or percentage.
        public int productionCount;
        public long hitPoints;
        public float spawnIntervalSeconds;
        public long garnetReward;
        public string gemstoneId;
        public long gemstoneAmount;
        public float gemstoneChancePercent;
        public MonsterBehaviorType behaviorType;
    }

    [Serializable]
    public sealed class SkillInformation
    {
        public string id;
        public bool enabled;
        public SkillStatUpgradeInformation[] upgrades;
    }

    [Serializable]
    public sealed class SkillStatUpgradeInformation
    {
        public string statId;
        public float delta;
    }

    [Serializable]
    public sealed class RelicInformation
    {
        public string id;
        // True when the player has discovered this relic. Owned relic effects
        // are copied into the run snapshot; drop chances remain fixed.
        public bool owned;
        public string sourceMonsterId;
        public float dropChancePercent;
        public string effectId;
        // Units are defined by effectId. effectValue2 is reserved for
        // conditional thresholds such as Kraken Eye's low-health percent.
        public float effectValue;
        public float effectValue2;
    }

    [Serializable]
    public sealed class RelicDropRulesInformation
    {
        public int maxPerKill = 1;
        public string candidatePolicy = "unownedOnly";
        public string resolution = "sumFixedChanceThenWeightedPick";
        public bool preserveBaseChanceAfterAcquisition = true;
    }

}
