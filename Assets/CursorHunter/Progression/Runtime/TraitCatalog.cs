using System;
using System.Collections.Generic;
using UnityEngine;
using CursorHunter.Data;

namespace CursorHunter.Progression
{
    /// <summary>
    /// Top-level collections shown by the progression screen.
    /// </summary>
    public enum TraitTab
    {
        Stat = 0,
        Skill = 1,
        Monster = 2
    }

    /// <summary>
    /// A data-only purchase, unlock, or drop entry. Designers can extend a
    /// branch without changing the screen code.
    /// </summary>
    [Serializable]
    public sealed class TraitNodeDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private string valueLabel;
        [SerializeField, Min(0)] private int cost = 10;
        [SerializeField] private string costGemstoneId = "gem.garnet";
        [SerializeField, Min(0)] private int requiredBossTier;
        [SerializeField] private string prerequisiteNodeId;
        [SerializeField] private bool startsUnlocked;
        [SerializeField] private bool acquisitionOnly;

        public string Id => id;
        public string Title => title;
        public string Description => description;
        public string ValueLabel => valueLabel;
        public int Cost => Mathf.Max(0, cost);
        public string CostGemstoneId => string.IsNullOrEmpty(costGemstoneId)
            ? "gem.garnet"
            : costGemstoneId;
        public int RequiredBossTier => Mathf.Max(0, requiredBossTier);
        public string PrerequisiteNodeId => prerequisiteNodeId;
        public bool StartsUnlocked => startsUnlocked;
        public bool AcquisitionOnly => acquisitionOnly;

        public TraitNodeDefinition()
        {
        }

        public TraitNodeDefinition(
            string nodeId,
            string nodeTitle,
            string nodeDescription,
            string nodeValueLabel,
            int nodeCost,
            int nodeRequiredBossTier,
            bool nodeStartsUnlocked = false)
        {
            id = nodeId;
            title = nodeTitle;
            description = nodeDescription;
            valueLabel = nodeValueLabel;
            cost = Mathf.Max(0, nodeCost);
            costGemstoneId = "gem.garnet";
            requiredBossTier = Mathf.Max(0, nodeRequiredBossTier);
            startsUnlocked = nodeStartsUnlocked;
        }

        public TraitNodeDefinition WithPrerequisite(string prerequisiteId)
        {
            prerequisiteNodeId = prerequisiteId;
            return this;
        }

        public TraitNodeDefinition WithCostGemstone(string gemstoneId)
        {
            costGemstoneId = string.IsNullOrEmpty(gemstoneId)
                ? "gem.garnet"
                : gemstoneId;
            return this;
        }

        public TraitNodeDefinition AsDropOnly()
        {
            acquisitionOnly = true;
            return this;
        }
    }

    /// <summary>Gemstone identity only. Supply comes from ordinary monsters.</summary>
    [Serializable]
    public sealed class TraitGemstoneDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField] private string description;
        [SerializeField] private bool startsUnlocked;
        public string Id => id;
        public string Title => title;
        public string Description => description;
        public bool StartsUnlocked => startsUnlocked;
        public TraitGemstoneDefinition() {}
        public TraitGemstoneDefinition(string gemstoneId, string gemstoneTitle,
            string gemstoneDescription, bool gemstoneStartsUnlocked = false)
        {
            id = gemstoneId;
            title = gemstoneTitle;
            description = gemstoneDescription;
            startsUnlocked = gemstoneStartsUnlocked;
        }
    }

    [Serializable]
    public sealed class TraitCategoryDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField] private Color accent = new Color(0.35f, 0.72f, 0.45f, 1f);
        [SerializeField] private Sprite icon;
        [SerializeField] private List<TraitNodeDefinition> nodes =
            new List<TraitNodeDefinition>();

        public string Id => id;
        public string Title => title;
        public Color Accent => accent;
        public Sprite Icon => icon;
        public IReadOnlyList<TraitNodeDefinition> Nodes => nodes;

        public TraitCategoryDefinition()
        {
        }

        public TraitCategoryDefinition(
            string categoryId,
            string categoryTitle,
            Color categoryAccent,
            Sprite categoryIcon = null)
        {
            id = categoryId;
            title = categoryTitle;
            accent = categoryAccent;
            icon = categoryIcon;
        }

        public void AddNode(TraitNodeDefinition node)
        {
            if (node == null)
            {
                return;
            }

            if (nodes == null)
            {
                nodes = new List<TraitNodeDefinition>();
            }

            nodes.Add(node);
        }
    }

    /// <summary>
    /// Combat-facing first-pass balance data for a named monster. Progression
    /// owns the immutable values and the production nodes; Combat receives a
    /// copied snapshot at run start and never reads this ScriptableObject live.
    /// </summary>
    [Serializable]
    public sealed class TraitMonsterBalanceDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string featureLabel;
        [SerializeField, Min(1)] private long hitPoints = 1L;
        [SerializeField, Min(0.1f)] private float spawnInterval = 1f;
        [SerializeField, Min(1)] private int baseBatch = 1;
        [SerializeField, Min(0)] private long garnetReward = 3L;
        [SerializeField] private string bonusDropCurrencyId;
        [SerializeField, Min(0)] private long bonusDropAmount;
        [SerializeField, Range(0f, 100f)] private float bonusDropChancePercent;

        public string Id => id;
        public string FeatureLabel => featureLabel;
        public long HitPoints => Math.Max(1L, hitPoints);
        public float SpawnInterval => Mathf.Max(0.1f, spawnInterval);
        public int BaseBatch => Mathf.Max(1, baseBatch);
        public long GarnetReward => Math.Max(0L, garnetReward);
        public string BonusDropCurrencyId => bonusDropCurrencyId ?? string.Empty;
        public long BonusDropAmount => Math.Max(0L, bonusDropAmount);
        public float BonusDropChancePercent => Mathf.Clamp(bonusDropChancePercent, 0f, 100f);

        public TraitMonsterBalanceDefinition()
        {
        }

        public TraitMonsterBalanceDefinition(
            string monsterId,
            string feature,
            long hp,
            float interval,
            int batch)
            : this(
                monsterId,
                feature,
                hp,
                interval,
                batch,
                3L,
                string.Empty,
                0L,
                0f)
        {
        }

        public TraitMonsterBalanceDefinition(
            string monsterId,
            string feature,
            long hp,
            float interval,
            int batch,
            long reward,
            string dropCurrencyId,
            long dropAmount,
            float dropChancePercent)
        {
            id = monsterId;
            featureLabel = feature;
            hitPoints = Math.Max(1L, hp);
            spawnInterval = Mathf.Max(0.1f, interval);
            baseBatch = Mathf.Max(1, batch);
            garnetReward = Math.Max(0L, reward);
            bonusDropCurrencyId = dropCurrencyId ?? string.Empty;
            bonusDropAmount = Math.Max(0L, dropAmount);
            bonusDropChancePercent = Mathf.Clamp(dropChancePercent, 0f, 100f);
        }
    }

    /// <summary>
    /// Fixed first-pass boss settlement values. Combat owns the encounter;
    /// App/Progression owns applying these immutable rewards after success.
    /// </summary>
    [Serializable]
    public sealed class TraitBossRewardDefinition
    {
        [SerializeField] private int bossTier;
        [SerializeField, Min(0)] private long guaranteedGarnet;
        [SerializeField] private string bonusGemstoneId;
        [SerializeField, Min(0)] private long bonusGemstoneAmount;

        public int BossTier => Mathf.Clamp(bossTier, 1, 5);
        public long GuaranteedGarnet => Math.Max(0L, guaranteedGarnet);
        public string BonusGemstoneId => bonusGemstoneId ?? string.Empty;
        public long BonusGemstoneAmount => Math.Max(0L, bonusGemstoneAmount);

        public TraitBossRewardDefinition()
        {
        }

        public TraitBossRewardDefinition(
            int tier,
            long garnet,
            string gemstoneId,
            long gemstoneAmount)
        {
            bossTier = Mathf.Clamp(tier, 1, 5);
            guaranteedGarnet = Math.Max(0L, garnet);
            bonusGemstoneId = gemstoneId ?? string.Empty;
            bonusGemstoneAmount = Math.Max(0L, gemstoneAmount);
        }
    }

    [CreateAssetMenu(
        fileName = "TraitCatalog",
        menuName = "Cursor Hunter/Progression/Trait Catalog")]
    public sealed class TraitCatalog : ScriptableObject
    {
        [SerializeField] private List<TraitCategoryDefinition> statCategories =
            new List<TraitCategoryDefinition>();
        [SerializeField] private List<TraitCategoryDefinition> skillCategories =
            new List<TraitCategoryDefinition>();
        [SerializeField] private List<TraitCategoryDefinition> monsterCategories =
            new List<TraitCategoryDefinition>();
        [SerializeField] private List<TraitMonsterBalanceDefinition> monsterBalances =
            new List<TraitMonsterBalanceDefinition>();
        [SerializeField] private List<TraitGemstoneDefinition> gemstones =
            new List<TraitGemstoneDefinition>();
        [SerializeField] private List<TraitBossRewardDefinition> bossRewards =
            new List<TraitBossRewardDefinition>();

        public IReadOnlyList<TraitCategoryDefinition> StatCategories => statCategories;
        public IReadOnlyList<TraitCategoryDefinition> SkillCategories => skillCategories;
        public IReadOnlyList<TraitCategoryDefinition> MonsterCategories => monsterCategories;
        public IReadOnlyList<TraitMonsterBalanceDefinition> MonsterBalances => monsterBalances;
        public IReadOnlyList<TraitGemstoneDefinition> Gemstones => gemstones;
        public IReadOnlyList<TraitBossRewardDefinition> BossRewards => bossRewards;

        public bool HasAnyCategory =>
            HasEntries(statCategories) || HasEntries(skillCategories) ||
            HasEntries(monsterCategories) ||
            HasEntries(monsterBalances) ||
            HasEntries(gemstones) || HasEntries(bossRewards);

        public void Replace(
            IEnumerable<TraitCategoryDefinition> stats,
            IEnumerable<TraitCategoryDefinition> skills,
            IEnumerable<TraitCategoryDefinition> monsters)
        {
            statCategories = Copy(stats);
            skillCategories = Copy(skills);
            monsterCategories = Copy(monsters);
        }


        public void ReplaceMonsterBalances(
            IEnumerable<TraitMonsterBalanceDefinition> balances)
        {
            monsterBalances = balances == null
                ? new List<TraitMonsterBalanceDefinition>()
                : new List<TraitMonsterBalanceDefinition>(balances);
        }

        public void ReplaceGemstones(IEnumerable<TraitGemstoneDefinition> source)
        {
            gemstones = source == null
                ? new List<TraitGemstoneDefinition>()
                : new List<TraitGemstoneDefinition>(source);
        }

        private static bool HasEntries<T>(IReadOnlyList<T> source)
        {
            return source != null && source.Count > 0;
        }

        private static List<TraitCategoryDefinition> Copy(
            IEnumerable<TraitCategoryDefinition> source)
        {
            return source == null
                ? new List<TraitCategoryDefinition>()
                : new List<TraitCategoryDefinition>(source);
        }

        /// <summary>
        /// Runtime fallback used by the prototype scene. The node IDs and
        /// currency IDs are the contracts consumed by Combat and Save later.
        /// </summary>
        public static TraitCatalog CreateRuntimeDemo()
        {
            return CreateFromDocument(GameDataDocument.Load());
        }

        public static TraitCatalog CreateFromDocument(GameDataDocument document)
        {
            if (document == null || !document.TryValidate(out _))
                throw new ArgumentException("Invalid game data document.");
            var catalog = CreateInstance<TraitCatalog>();
            catalog.name = "TraitCatalog_File_" + document.locale;
            catalog.hideFlags = HideFlags.HideAndDontSave;
            foreach (var source in document.progression.categories)
            {
                Color accent = source.tab == "stats" ? new Color(.95f, .53f, .48f) :
                    source.tab == "skills" ? new Color(.4f, .7f, .95f) :
                    source.tab == "monsters" ? new Color(.46f, .79f, .6f) : new Color(.98f, .76f, .24f);
                var category = new TraitCategoryDefinition(source.id, document.FindEntity(source.id).displayName, accent);
                foreach (var entry in source.nodes)
                    category.AddNode(new TraitNodeDefinition(entry.id, entry.displayName, entry.description,
                        entry.valueLabel, entry.cost, entry.requiredBossTier,
                        GameInformationBuilder.IsInitiallyPurchased(document, source, entry))
                        .WithCostGemstone(entry.currencyId).WithPrerequisite(entry.prerequisiteNodeId));
                switch (source.tab)
                {
                    case "stats": catalog.statCategories.Add(category); break;
                    case "skills":
                        // Cursor Aura is the existing pet companion system. Keep
                        // its stable skill.* IDs and GameInformation ownership,
                        // while presenting its upgrades as a branch in Stats.
                        if (string.Equals(source.id, "skill.cursorAura", StringComparison.Ordinal))
                            catalog.statCategories.Add(category);
                        else
                            catalog.skillCategories.Add(category);
                        break;
                    case "monsters": catalog.monsterCategories.Add(category); break;
                }
            }
            foreach (var monster in document.information.monsters)
                catalog.monsterBalances.Add(new TraitMonsterBalanceDefinition(monster.id,
                    document.FindEntity(monster.id).description, monster.hitPoints, monster.spawnIntervalSeconds,
                    monster.productionCount, monster.garnetReward, monster.gemstoneId, monster.gemstoneAmount,
                    monster.gemstoneChancePercent));
            foreach (var gem in document.information.gemstones)
            {
                var text = document.FindEntity(gem.id);
                catalog.gemstones.Add(new TraitGemstoneDefinition(gem.id, text.displayName, text.description, gem.enabled));
            }
            foreach (var boss in document.progression.bossRewards)
                catalog.bossRewards.Add(new TraitBossRewardDefinition(boss.tier, boss.garnetReward,
                    boss.gemstoneId, boss.gemstoneAmount));
            return catalog;
        }
    }
}
