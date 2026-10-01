using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Data
{
    /// <summary>Loads separate combat baseline and growth configuration. Combat receives only resolved information.</summary>
    [Serializable]
    public sealed class GameDataDocument
    {
        public int documentVersion;
        public string locale;
        public string description;
        [NonSerialized] public GameInformation information;
        public ProgressionDefinition progression;
        public EntityTextDefinition[] entities;

        public const string RuntimeResourcePath = "GameData/game-data.en";
        public const string ProgressionResourcePath = "GameData/progression-config.en";

        public static GameDataDocument Load()
        {
            var asset = Resources.Load<TextAsset>(RuntimeResourcePath);
            if (asset == null) throw new InvalidOperationException("Missing English runtime game data.");
            var config = Resources.Load<TextAsset>(ProgressionResourcePath);
            if (config == null) throw new InvalidOperationException("Missing progression configuration.");
            if (!TryParse(asset.text, config.text, out var document, out var error)) throw new InvalidOperationException(error);
            return document;
        }

        public static bool TryParse(string informationJson, string configurationJson, out GameDataDocument document, out string error)
        {
            document = null;
            error = "";
            if (string.IsNullOrWhiteSpace(configurationJson) || configurationJson.Length > 4 * 1024 * 1024)
            { error = "Empty/oversized game data document."; return false; }
            try
            {
                if (!GameInformationJson.TryDeserialize(informationJson, out var information, out error)) return false;
                var candidate = JsonUtility.FromJson<GameDataDocument>(configurationJson);
                if (candidate == null) { error = "Missing progression configuration."; return false; }
                candidate.information = information;
                if (!candidate.TryValidate(out error)) return false;
                document = candidate;
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        public EntityTextDefinition FindEntity(string id)
        {
            if (entities != null) foreach (var entity in entities) if (entity != null && entity.id == id) return entity;
            return null;
        }

        public bool TryValidate(out string error)
        {
            error = "";
            if (documentVersion != 2 || locale != "en" || information == null ||
                progression == null || entities == null)
                return Fail("Missing game document sections/version/locale.", out error);
            if (!information.TryValidate(out error)) return false;
            if (progression.categories == null ||
                progression.categories.Length != 23 || progression.bossRewards == null || progression.bossRewards.Length != 5 ||
                (progression.savedProgressPolicy != "overlay" && progression.savedProgressPolicy != "fileOnly") ||
                progression.initialBossTier < 0 || progression.initialBossTier > 5)
                return Fail("Invalid progression configuration.", out error);
            var entityIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entity in entities)
                if (entity == null || string.IsNullOrWhiteSpace(entity.id) || string.IsNullOrWhiteSpace(entity.displayName) ||
                    !entityIds.Add(entity.id)) return Fail("Invalid/duplicate entity text.", out error);
            var categories = new HashSet<string>(StringComparer.Ordinal);
            var nodes = new Dictionary<string, UpgradeNodeData>(StringComparer.Ordinal);
            var currencies = new HashSet<string>(StringComparer.Ordinal);
            foreach (var gem in information.gemstones) currencies.Add(gem.id);
            foreach (var category in progression.categories)
            {
                if (category == null || string.IsNullOrWhiteSpace(category.id) || !categories.Add(category.id) ||
                    !entityIds.Contains(category.id) || category.nodes == null || category.nodes.Length == 0 ||
                    (category.tab != "stats" && category.tab != "skills" && category.tab != "monsters"))
                    return Fail("Invalid category.", out error);
                foreach (var node in category.nodes)
                {
                    if (node == null || string.IsNullOrWhiteSpace(node.id) || nodes.ContainsKey(node.id) ||
                        string.IsNullOrWhiteSpace(node.displayName) || node.cost < 0 || !currencies.Contains(node.currencyId) ||
                        node.requiredBossTier < 0 || node.requiredBossTier > 5 || !ValidEffect(category, node))
                        return Fail("Invalid node/effect in " + category.id, out error);
                    if (!string.IsNullOrEmpty(node.prerequisiteNodeId) && !nodes.ContainsKey(node.prerequisiteNodeId))
                        return Fail("Nodes must follow prerequisite order: " + node.id, out error);
                    nodes.Add(node.id, node);
                }
            }
            string[] statIds = { "stat.attack", "stat.radius", "stat.cooldown", "stat.critical", "stat.fieldDuration", "stat.boss" };
            foreach (var id in statIds)
                if (!categories.Contains(id)) return Fail("Missing stat category: " + id, out error);
            foreach (var category in progression.categories)
            {
                string expectedTab = Array.IndexOf(statIds, category.id) >= 0 ? "stats" :
                    category.id.StartsWith("skill.", StringComparison.Ordinal) ? "skills" :
                    category.id.StartsWith("monster.", StringComparison.Ordinal) ? "monsters" : string.Empty;
                if (category.tab != expectedTab) return Fail("Category in wrong tab.", out error);
            }
            if (nodes.Count > 1000) return Fail("Node budget exceeded.", out error);
            foreach (var node in nodes.Values)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal) { node.id };
                string prerequisite = node.prerequisiteNodeId;
                while (!string.IsNullOrEmpty(prerequisite))
                {
                    if (!visited.Add(prerequisite) || !nodes.TryGetValue(prerequisite, out var parent))
                        return Fail("Missing/cyclic prerequisite: " + node.id, out error);
                    prerequisite = parent.prerequisiteNodeId;
                }
            }
            foreach (var gem in information.gemstones)
                if (!entityIds.Contains(gem.id)) return Fail("Missing gemstone text.", out error);
            foreach (var monster in information.monsters)
                if (!categories.Contains(monster.id) || FindEntity(monster.id) == null ||
                    string.IsNullOrWhiteSpace(FindEntity(monster.id).prefabKey))
                    return Fail("Missing monster/category/visual key.", out error);
            foreach (var skill in information.skills)
                if (!categories.Contains(skill.id)) return Fail("Missing skill category.", out error);
            for (int i = 0; i < progression.bossRewards.Length; i++)
            {
                var boss = progression.bossRewards[i];
                if (boss == null || boss.tier != i + 1 || boss.id != "boss.v" + (i + 1) ||
                    boss.garnetReward < 0 || boss.gemstoneAmount < 0 || !currencies.Contains(boss.gemstoneId))
                    return Fail("Invalid boss rewards.", out error);
            }
            return true;
        }

        private static bool ValidEffect(UpgradeCategoryData category, UpgradeNodeData node)
        {
            if (!Finite(node.value, 0, 1000000000000d)) return false;
            switch (category.tab)
            {
                case "stats":
                    if (node.operation != "baseline" && node.operation != "set") return false;
                    if (node.operation == "baseline") return node.startsUnlocked;
                    switch (category.id)
                    {
                        case "stat.attack": return node.value >= 1 && node.value == Math.Floor(node.value);
                        case "stat.radius": return node.value > 0;
                        case "stat.cooldown": return node.value >= .05;
                        case "stat.critical": return node.value <= 100;
                        case "stat.fieldDuration": return node.value >= 15 && node.value <= 30;
                        case "stat.boss": return node.value > 0;
                        default: return false;
                    }
                case "skills":
                    return (node.id == category.id && node.operation == "enable") ||
                        ((node.id == category.id + ".damage" || node.id == category.id + ".radius" ||
                          node.id == category.id + ".cooldown") && node.operation == "multiply" && node.value > 0);
                case "monsters":
                    return (node.id == category.id && node.operation == "enable") ||
                        (node.id.StartsWith(category.id + ".production.", StringComparison.Ordinal) && node.operation == "set" &&
                         node.value >= 1 && node.value <= 16 && node.value == Math.Floor(node.value));
                default: return false;
            }
        }

        private static bool Finite(double value, double min, double max) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= min && value <= max;
        private static bool Fail(string message, out string error) { error = message; return false; }
    }

    [Serializable] public sealed class ProgressionDefinition
    {
        public string savedProgressPolicy;
        public int initialBossTier;
        public bool scaleSkillDamageWithAttack;
        public UpgradeCategoryData[] categories;
        public BossRewardData[] bossRewards;
    }
    [Serializable] public sealed class UpgradeCategoryData
    {
        public string id;
        public string tab;
        public UpgradeNodeData[] nodes;
    }
    [Serializable] public sealed class UpgradeNodeData
    {
        public string id;
        public string displayName;
        public string description;
        public string valueLabel;
        public int cost;
        public string currencyId;
        public int requiredBossTier;
        public string prerequisiteNodeId;
        public bool startsUnlocked;
        public string operation;
        public double value;
    }
    [Serializable] public sealed class BossRewardData
    {
        public string id;
        public int tier;
        public long garnetReward;
        public string gemstoneId;
        public long gemstoneAmount;
    }
    [Serializable] public sealed class EntityTextDefinition
    {
        public string id;
        public string displayName;
        public string description;
        public string prefabKey;
    }
}
