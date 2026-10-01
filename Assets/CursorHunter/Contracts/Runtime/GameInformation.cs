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

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (schemaVersion != 3 || balanceVersion < 1 || stats == null ||
                gemstones == null || monsters == null || skills == null)
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
            if (gemstones.Length != 6 || monsters.Length != 10 || skills.Length != 7)
                return Fail("Expected 6 gemstones, 10 monsters and 7 skills.", out error);
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
            string[] skillIds = { "skill.fireball", "skill.lightning", "skill.freeze", "skill.hurricane", "skill.meteor", "skill.dragonBreath", "skill.cursorAura" };
            for (int i = 0; i < skills.Length; i++)
            {
                var skill = skills[i];
                if (skill == null || skill.id != skillIds[i] || skill.damage < 1 ||
                    !Positive(skill.radiusWorldUnits) || !Positive(skill.cooldownSeconds) ||
                    skill.cooldownSeconds < 0.05f)
                    return Fail("Invalid skill data.", out error);
            }
            return true;
        }

        public ProgressionCombatSnapshot ToCombatSnapshot(string sourceJson)
        {
            if (!TryValidate(out string error))
                throw new ArgumentException(error);
            var skillSnapshots = new SkillCombatSnapshot[skills.Length];
            for (int i = 0; i < skills.Length; i++)
            {
                var value = skills[i];
                skillSnapshots[i] = new SkillCombatSnapshot(value.id, value.enabled, 1f, 1f, 1f,
                    value.damage, value.radiusWorldUnits, value.cooldownSeconds);
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
            var combat = new CombatSnapshot(stats.attackPower,
                stats.attackRadiusWorldUnits / BaseAttackRadiusWorldUnits,
                stats.attackCooldownSeconds, 1, stats.criticalChancePercent,
                stats.bossDamageMultiplier, true, stats.attackCooldownSeconds, rules.criticalDamageMultiplier);
            return new ProgressionCombatSnapshot(combat, skillSnapshots, monsterSnapshots,
                stats.normalFieldDurationSeconds, sourceJson, rules.globalAliveLimit,
                rules.perMonsterAliveLimit, rules.bossFieldDurationSeconds);
        }

        public const float BaseAttackRadiusWorldUnits = 1.7253809f;
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
        public long damage;
        public float radiusWorldUnits;
        public float cooldownSeconds;
    }

}
