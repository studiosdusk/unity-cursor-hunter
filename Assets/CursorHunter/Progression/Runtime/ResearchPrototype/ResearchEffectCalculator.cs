using System;
using System.Collections.Generic;
using System.Linq;
using CursorHunter.Contracts;

namespace CursorHunter.Progression.ResearchPrototype
{
    /// <summary>Resolves saved placement ranks into additive combat deltas without reading UI state.</summary>
    public static class ResearchEffectCalculator
    {
        /// <summary>Checks the scene-authored map before purchases or combat snapshots.</summary>
        public static void ValidateMap(ResearchTestNode[] nodes)
        {
            if (nodes == null) throw new InvalidOperationException("Research map is missing.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var members = new HashSet<ResearchTestNode>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (ResearchTestNode node in nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.placementId) || !ids.Add(node.placementId))
                    throw new InvalidOperationException("Research map contains a missing or duplicate placement ID.");
                members.Add(node);
                if (node.definition == null) throw new InvalidOperationException("Research placement has no definition: " + node.placementId);
                if (!node.definition.TryValidateGameplay(out string error))
                    throw new InvalidOperationException("Invalid research definition " + node.placementId + ": " + error);
                var game = node.definition.gameplay;
                if (game.kind == ResearchEffectKind.SkillUnlock || game.kind == ResearchEffectKind.MonsterUnlock ||
                    game.kind == ResearchEffectKind.MonsterProduction || game.kind == ResearchEffectKind.FieldDuration)
                {
                    string key = game.kind + ":" + game.targetId;
                    if (!unique.Add(key)) throw new InvalidOperationException("Duplicate unique research effect: " + key);
                }
            }
            foreach (ResearchTestNode node in nodes)
            {
                int count = node.prerequisites == null ? 0 : node.prerequisites.Length;
                if (node.requiredPrerequisites < 0 || node.requiredPrerequisites > count)
                    throw new InvalidOperationException("Research prerequisite count is invalid: " + node.placementId);
                var seen = new HashSet<ResearchTestNode>();
                if (node.prerequisites != null)
                    foreach (ResearchTestNode prerequisite in node.prerequisites)
                        if (prerequisite == null || !members.Contains(prerequisite) || !seen.Add(prerequisite))
                            throw new InvalidOperationException("Research prerequisite is missing or duplicated: " + node.placementId);
            }
            var visiting = new HashSet<ResearchTestNode>();
            var visited = new HashSet<ResearchTestNode>();
            void Visit(ResearchTestNode node)
            {
                if (visited.Contains(node)) return;
                if (!visiting.Add(node)) throw new InvalidOperationException("Research prerequisites contain a cycle: " + node.placementId);
                if (node.prerequisites != null)
                    foreach (ResearchTestNode prerequisite in node.prerequisites) Visit(prerequisite);
                visiting.Remove(node);
                visited.Add(node);
            }
            foreach (ResearchTestNode node in nodes) Visit(node);
        }

        public static CursorCombatStatBonusesSnapshot Calculate(ResearchTestNode[] nodes, Func<string, int> rankByPlacement)
        {
            if (nodes == null || rankByPlacement == null) throw new ArgumentNullException("Research map or rank lookup is missing.");
            ValidateMap(nodes);
            long attack = 0L;
            float radius = 0f, interval = 0f, chance = 0f, criticalDamage = 0f, bossDamage = 0f;
            foreach (ResearchTestNode node in nodes)
            {
                ResearchDefinition definition = node.definition;
                if (definition.gameplay.kind != ResearchEffectKind.CursorStat) continue;
                int rank = Math.Max(0, Math.Min(rankByPlacement(node.placementId), definition.gameplay.ranks.Length));
                if (rank == 0) continue;
                float delta = definition.GetEffectValue(rank);
                switch (definition.gameplay.target)
                {
                    case ResearchStat.Attack:
                        long add = (long)delta;
                        attack = add > long.MaxValue - attack ? long.MaxValue : attack + add;
                        break;
                    case ResearchStat.Radius: radius += delta; break;
                    case ResearchStat.Interval: interval += delta; break;
                    case ResearchStat.CriticalChance: chance += delta; break;
                    case ResearchStat.CriticalDamage: criticalDamage += delta; break;
                    case ResearchStat.BossDamage: bossDamage += delta; break;
                    default: throw new InvalidOperationException("Unsupported research target.");
                }
            }
            var result = new CursorCombatStatBonusesSnapshot(attack, radius, interval, chance, criticalDamage, bossDamage);
            if (!result.IsValid) throw new InvalidOperationException("Research effects exceed supported combat values.");
            return result;
        }

        public static void ApplyAdditional(ResearchTestNode[] nodes, Func<string, int> rankByPlacement, GameInformation information)
        {
            if (nodes == null || rankByPlacement == null || information == null) throw new ArgumentNullException();
            ValidateMap(nodes);
            foreach (var node in nodes)
            {
                var game = node.definition.gameplay;
                int rank = Math.Max(0, Math.Min(rankByPlacement(node.placementId), game.ranks.Length));
                if (rank == 0 || game.kind == ResearchEffectKind.CursorStat) continue;
                if (!RequiredTargetUnlocked(nodes, node.definition, rankByPlacement))
                    throw new InvalidOperationException("Research target is not unlocked: " + game.targetId);
                float value = node.definition.GetEffectValue(rank);
                switch (game.kind)
                {
                    case ResearchEffectKind.SkillUnlock:
                    case ResearchEffectKind.SkillStat:
                        var skill = information.skills?.FirstOrDefault(x => x != null && x.id == game.targetId);
                        if (skill == null) throw new InvalidOperationException("Missing skill: " + game.targetId);
                        if (game.kind == ResearchEffectKind.SkillUnlock) skill.enabled = true;
                        else
                        {
                            var upgrades = skill.upgrades?.ToList() ?? new List<SkillStatUpgradeInformation>();
                            var entry = upgrades.FirstOrDefault(x => x != null && x.statId == game.statId);
                            if (entry == null) { entry = new SkillStatUpgradeInformation { statId = game.statId }; upgrades.Add(entry); }
                            entry.delta += value;
                            skill.upgrades = upgrades.ToArray();
                        }
                        break;
                    case ResearchEffectKind.MonsterUnlock:
                    case ResearchEffectKind.MonsterProduction:
                        var monster = information.monsters?.FirstOrDefault(x => x != null && x.id == game.targetId);
                        if (monster == null) throw new InvalidOperationException("Missing monster: " + game.targetId);
                        if (game.kind == ResearchEffectKind.MonsterUnlock) monster.enabled = true;
                        else monster.productionCount = Math.Max(monster.productionCount, (int)value);
                        break;
                    case ResearchEffectKind.FieldDuration:
                        information.stats.normalFieldDurationSeconds = Math.Max(information.stats.normalFieldDurationSeconds, value);
                        break;
                    default: throw new InvalidOperationException("Unsupported research effect.");
                }
            }
            foreach (var gem in information.gemstones ?? Array.Empty<GemstoneInformation>())
                if (gem != null) gem.enabled = gem.id == "gem.garnet" ||
                    (information.monsters != null && information.monsters.Any(x => x != null && x.enabled && x.gemstoneId == gem.id &&
                        x.gemstoneAmount > 0 && x.gemstoneChancePercent > 0));
        }
        public static bool RequiredTargetUnlocked(ResearchTestNode[] nodes, ResearchDefinition definition, Func<string, int> rankByPlacement)
        {
            if (nodes == null || definition == null || definition.gameplay == null || rankByPlacement == null) return false;
            var game = definition.gameplay;
            ResearchEffectKind unlock;
            if (game.kind == ResearchEffectKind.SkillStat) unlock = ResearchEffectKind.SkillUnlock;
            else if (game.kind == ResearchEffectKind.MonsterProduction && game.targetId != "monster.01") unlock = ResearchEffectKind.MonsterUnlock;
            else return true;
            return nodes.Any(x => x != null && x.definition != null && x.definition.gameplay != null &&
                x.definition.gameplay.kind == unlock && x.definition.gameplay.targetId == game.targetId && rankByPlacement(x.placementId) > 0);
        }
    }
}
