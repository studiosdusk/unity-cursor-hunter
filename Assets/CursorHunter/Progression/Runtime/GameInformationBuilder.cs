using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using CursorHunter.Data;
using UnityEngine;

namespace CursorHunter.Progression
{
    /// <summary>Resolves explicit node effects against a copied authored baseline.</summary>
    public static class GameInformationBuilder
    {
        public static GameInformation Build(GameDataDocument document, ISet<string> purchased,
            Func<string, long> wallet)
        {
            return Build(document, purchased, wallet, null);
        }

        public static GameInformation Build(
            GameDataDocument document,
            ISet<string> purchased,
            Func<string, long> wallet,
            CursorCombatStatDefaultsSnapshot? cursorStatDefaults)
        {
            if (document == null || purchased == null || wallet == null)
                throw new ArgumentNullException("Game data builder inputs cannot be null.");
            var result = JsonUtility.FromJson<GameInformation>(JsonUtility.ToJson(document.information));
            if (cursorStatDefaults.HasValue)
            {
                ApplyCursorStatDefaults(result, cursorStatDefaults.Value);
            }

            ApplyCursorStatBonuses(
                result,
                CreateCursorCombatStatBonusesSnapshot(document, purchased));

            var monsters = new Dictionary<string, MonsterInformation>(StringComparer.Ordinal);
            var skills = new Dictionary<string, SkillInformation>(StringComparer.Ordinal);
            var skillDamageMultipliers = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var monster in result.monsters) monsters.Add(monster.id, monster);
            foreach (var skill in result.skills) { skills.Add(skill.id, skill); skillDamageMultipliers.Add(skill.id, 1d); }

            foreach (var category in document.progression.categories)
            {
                foreach (var node in category.nodes)
                {
                    if (!purchased.Contains(node.id) || node.operation == "baseline") continue;
                    switch (category.tab)
                    {
                        case "stats":
                            switch (category.id)
                            {
                                case "stat.fieldDuration": result.stats.normalFieldDurationSeconds = (float)node.value; break;
                            }
                            break;
                        case "monsters":
                            var monster = monsters[category.id];
                            if (node.operation == "enable") monster.enabled = true;
                            else monster.productionCount = Math.Max(monster.productionCount, (int)node.value);
                            break;
                        case "skills":
                            var skill = skills[category.id];
                            if (node.operation == "enable") skill.enabled = true;
                            else if (node.id.EndsWith(".damage", StringComparison.Ordinal)) skillDamageMultipliers[skill.id] *= node.value;
                            else if (node.id.EndsWith(".radius", StringComparison.Ordinal)) skill.radiusWorldUnits *= (float)node.value;
                            else if (node.id.EndsWith(".cooldown", StringComparison.Ordinal)) skill.cooldownSeconds *= (float)node.value;
                            break;
                    }
                }
            }
            long baseAttackPower = cursorStatDefaults.HasValue
                ? cursorStatDefaults.Value.AttackPower
                : document.information.stats.attackPower;
            double attackRatio = document.progression.scaleSkillDamageWithAttack
                ? (double)result.stats.attackPower / baseAttackPower
                : 1d;
            foreach (var skill in result.skills)
                skill.damage = ScaleDamage(skill.damage, skillDamageMultipliers[skill.id] * attackRatio);

            foreach (var gem in result.gemstones)
            {
                gem.amount = Math.Max(0, wallet(gem.id));
                gem.enabled = gem.id == "gem.garnet";
                foreach (var monster in result.monsters)
                    if (monster.enabled && monster.gemstoneId == gem.id &&
                        monster.gemstoneAmount > 0 && monster.gemstoneChancePercent > 0) gem.enabled = true;
            }
            if (!result.TryValidate(out string error)) throw new InvalidOperationException("Resolved game data: " + error);
            return result;
        }

        public static CursorCombatStatBonusesSnapshot CreateCursorCombatStatBonusesSnapshot(
            GameDataDocument document,
            ISet<string> purchased)
        {
            if (document == null || purchased == null)
            {
                throw new ArgumentNullException("Game data and purchased nodes cannot be null.");
            }

            long attackPowerDelta = 0L;
            float radiusDelta = 0f;
            float cooldownDelta = 0f;
            float criticalChanceDelta = 0f;
            float bossDamageDelta = 0f;

            foreach (UpgradeCategoryData category in document.progression.categories)
            {
                if (category == null || category.tab != "stats" || category.nodes == null)
                {
                    continue;
                }

                foreach (UpgradeNodeData node in category.nodes)
                {
                    if (node == null || node.operation != "add" ||
                        !purchased.Contains(node.id))
                    {
                        continue;
                    }

                    switch (category.id)
                    {
                        case "stat.attack":
                            attackPowerDelta = AddNonNegative(
                                attackPowerDelta,
                                (long)Math.Round(node.value, MidpointRounding.AwayFromZero));
                            break;
                        case "stat.radius":
                            radiusDelta += (float)node.value;
                            break;
                        case "stat.cooldown":
                            cooldownDelta += (float)node.value;
                            break;
                        case "stat.critical":
                            criticalChanceDelta += (float)node.value;
                            break;
                        case "stat.boss":
                            bossDamageDelta += (float)node.value;
                            break;
                    }
                }
            }

            var bonuses = new CursorCombatStatBonusesSnapshot(
                attackPowerDelta,
                radiusDelta,
                cooldownDelta,
                criticalChanceDelta,
                0f,
                bossDamageDelta);
            if (!bonuses.IsValid)
            {
                throw new InvalidOperationException("Cursor combat trait bonuses are invalid.");
            }

            return bonuses;
        }

        private static void ApplyCursorStatDefaults(
            GameInformation information,
            CursorCombatStatDefaultsSnapshot defaults)
        {
            if (!defaults.IsValid)
            {
                throw new InvalidOperationException("Cursor combat defaults are invalid.");
            }

            information.stats.attackPower = defaults.AttackPower;
            information.stats.attackRadiusWorldUnits = defaults.AttackRadiusWorldUnits;
            information.stats.attackCooldownSeconds = defaults.AttackCooldownSeconds;
            information.stats.criticalChancePercent = defaults.CriticalChancePercent;
            information.stats.bossDamageMultiplier = defaults.BossDamageMultiplier;
            information.rules.criticalDamageMultiplier =
                defaults.CriticalDamageMultiplier;
        }

        private static void ApplyCursorStatBonuses(
            GameInformation information,
            CursorCombatStatBonusesSnapshot bonuses)
        {
            information.stats.attackPower = AddNonNegative(
                information.stats.attackPower,
                bonuses.AttackPowerDelta);
            information.stats.attackRadiusWorldUnits +=
                bonuses.AttackRadiusWorldUnitsDelta;
            information.stats.attackCooldownSeconds = Mathf.Max(
                CursorCombatStatLimits.MinimumAttackCooldownSeconds,
                information.stats.attackCooldownSeconds +
                bonuses.AttackCooldownSecondsDelta);
            information.stats.criticalChancePercent = Mathf.Clamp(
                information.stats.criticalChancePercent +
                bonuses.CriticalChancePercentDelta,
                0f,
                CursorCombatStatLimits.MaximumCriticalChancePercent);
            information.stats.bossDamageMultiplier +=
                bonuses.BossDamageMultiplierDelta;
            information.rules.criticalDamageMultiplier +=
                bonuses.CriticalDamageMultiplierDelta;
        }

        private static long AddNonNegative(long value, long delta)
        {
            return delta > long.MaxValue - value ? long.MaxValue : value + delta;
        }

        private static long ScaleDamage(long value, double multiplier)
        {
            double scaled = value * multiplier;
            if (double.IsNaN(scaled) || scaled <= 0) throw new InvalidOperationException("Invalid damage calculation.");
            return scaled >= long.MaxValue ? long.MaxValue : Math.Max(1L, (long)Math.Round(scaled, MidpointRounding.AwayFromZero));
        }

        public static bool IsInitiallyPurchased(GameDataDocument document, UpgradeCategoryData category, UpgradeNodeData node)
        {
            // Combat defaults describe the authored run snapshot; they do not
            // mean the player has purchased those nodes in the research tree.
            return document != null && category != null && node != null && node.startsUnlocked;
        }
    }
}
