using System;
using System.Collections.Generic;
using UnityEngine;

namespace CursorHunter.Progression
{
    /// <summary>
    /// Top-level collections shown by the progression screen.
    /// </summary>
    public enum TraitTab
    {
        Stat = 0,
        Skill = 1,
        Monster = 2,
        Loot = 3,
        Pet = 4
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

    /// <summary>
    /// Persistent gemstone definition. A gemstone is hidden until its Stat
    /// unlock node is purchased (or its legacy boss gate is reached), then its
    /// discovery upgrades increase its weighted random drop chance.
    /// </summary>
    [Serializable]
    public sealed class TraitGemstoneDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField, TextArea(1, 2)] private string description;
        [SerializeField, Min(0)] private int requiredBossTier;
        [SerializeField] private string unlockNodeId;
        [SerializeField, Min(0)] private int baseDropWeight;
        [SerializeField, Min(0)] private int dropWeightPerUpgrade;
        [SerializeField, Min(0)] private int maxDropWeight = 1000;
        [SerializeField] private string dropRateNodePrefix;
        [SerializeField] private bool startsUnlocked;

        public string Id => id;
        public string Title => title;
        public string Description => description;
        public int RequiredBossTier => Mathf.Max(0, requiredBossTier);
        public string UnlockNodeId => unlockNodeId;
        public int BaseDropWeight => Mathf.Max(0, baseDropWeight);
        public int DropWeightPerUpgrade => Mathf.Max(0, dropWeightPerUpgrade);
        public int MaxDropWeight => Mathf.Max(0, maxDropWeight);
        public string DropRateNodePrefix => dropRateNodePrefix;
        public bool StartsUnlocked => startsUnlocked;

        public TraitGemstoneDefinition()
        {
        }

        public TraitGemstoneDefinition(
            string gemstoneId,
            string gemstoneTitle,
            string gemstoneDescription,
            int gemstoneRequiredBossTier,
            bool gemstoneStartsUnlocked = false)
        {
            id = gemstoneId;
            title = gemstoneTitle;
            description = gemstoneDescription;
            requiredBossTier = Mathf.Max(0, gemstoneRequiredBossTier);
            unlockNodeId = string.Empty;
            baseDropWeight = 0;
            dropWeightPerUpgrade = 0;
            maxDropWeight = 1000;
            dropRateNodePrefix = string.Empty;
            startsUnlocked = gemstoneStartsUnlocked;
        }

        public TraitGemstoneDefinition(
            string gemstoneId,
            string gemstoneTitle,
            string gemstoneDescription,
            string gemstoneUnlockNodeId,
            int gemstoneBaseDropWeight,
            int gemstoneDropWeightPerUpgrade,
            int gemstoneMaxDropWeight = 1000,
            string gemstoneDropRateNodePrefix = null,
            bool gemstoneStartsUnlocked = false)
        {
            id = gemstoneId;
            title = gemstoneTitle;
            description = gemstoneDescription;
            requiredBossTier = 0;
            unlockNodeId = gemstoneUnlockNodeId;
            baseDropWeight = Mathf.Max(0, gemstoneBaseDropWeight);
            dropWeightPerUpgrade = Mathf.Max(0, gemstoneDropWeightPerUpgrade);
            maxDropWeight = Mathf.Max(0, gemstoneMaxDropWeight);
            dropRateNodePrefix = gemstoneDropRateNodePrefix;
            startsUnlocked = gemstoneStartsUnlocked;
        }

        public int GetDropWeight(int upgradeCount)
        {
            int safeCount = Mathf.Max(0, upgradeCount);
            long raw = (long)BaseDropWeight +
                (long)DropWeightPerUpgrade * safeCount;
            if (raw <= 0L)
            {
                return 0;
            }

            return raw >= MaxDropWeight ? MaxDropWeight : (int)raw;
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
        [SerializeField] private string lootFragmentCurrencyId;
        [SerializeField, Range(0f, 100f)] private float lootFragmentChancePercent;

        public string Id => id;
        public string FeatureLabel => featureLabel;
        public long HitPoints => Math.Max(1L, hitPoints);
        public float SpawnInterval => Mathf.Max(0.1f, spawnInterval);
        public int BaseBatch => Mathf.Max(1, baseBatch);
        public long GarnetReward => Math.Max(0L, garnetReward);
        public string BonusDropCurrencyId => bonusDropCurrencyId ?? string.Empty;
        public long BonusDropAmount => Math.Max(0L, bonusDropAmount);
        public float BonusDropChancePercent => Mathf.Clamp(bonusDropChancePercent, 0f, 100f);
        public string LootFragmentCurrencyId => lootFragmentCurrencyId ?? string.Empty;
        public float LootFragmentChancePercent => Mathf.Clamp(lootFragmentChancePercent, 0f, 100f);

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
                0f,
                string.Empty,
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
            float dropChancePercent,
            string fragmentCurrencyId,
            float fragmentChancePercent)
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
            lootFragmentCurrencyId = fragmentCurrencyId ?? string.Empty;
            lootFragmentChancePercent = Mathf.Clamp(fragmentChancePercent, 0f, 100f);
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
        [SerializeField, Range(0f, 100f)] private float lootFragmentChancePercent;
        [SerializeField, Min(0)] private int firstClearFragmentAmount;

        public int BossTier => Mathf.Clamp(bossTier, 1, 5);
        public long GuaranteedGarnet => Math.Max(0L, guaranteedGarnet);
        public string BonusGemstoneId => bonusGemstoneId ?? string.Empty;
        public long BonusGemstoneAmount => Math.Max(0L, bonusGemstoneAmount);
        public float LootFragmentChancePercent => Mathf.Clamp(lootFragmentChancePercent, 0f, 100f);
        public int FirstClearFragmentAmount => Mathf.Max(0, firstClearFragmentAmount);

        public TraitBossRewardDefinition()
        {
        }

        public TraitBossRewardDefinition(
            int tier,
            long garnet,
            string gemstoneId,
            long gemstoneAmount,
            float fragmentChancePercent,
            int firstClearFragments)
        {
            bossTier = Mathf.Clamp(tier, 1, 5);
            guaranteedGarnet = Math.Max(0L, garnet);
            bonusGemstoneId = gemstoneId ?? string.Empty;
            bonusGemstoneAmount = Math.Max(0L, gemstoneAmount);
            lootFragmentChancePercent = Mathf.Clamp(fragmentChancePercent, 0f, 100f);
            firstClearFragmentAmount = Mathf.Max(0, firstClearFragments);
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
        [SerializeField] private List<TraitCategoryDefinition> lootCategories =
            new List<TraitCategoryDefinition>();
        [SerializeField] private List<TraitCategoryDefinition> petCategories =
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
        public IReadOnlyList<TraitCategoryDefinition> LootCategories => lootCategories;
        public IReadOnlyList<TraitCategoryDefinition> PetCategories => petCategories;
        public IReadOnlyList<TraitMonsterBalanceDefinition> MonsterBalances => monsterBalances;
        public IReadOnlyList<TraitGemstoneDefinition> Gemstones => gemstones;
        public IReadOnlyList<TraitBossRewardDefinition> BossRewards => bossRewards;

        public bool HasAnyCategory =>
            HasEntries(statCategories) || HasEntries(skillCategories) ||
            HasEntries(monsterCategories) || HasEntries(lootCategories) ||
            HasEntries(petCategories) || HasEntries(monsterBalances) ||
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

        public void ReplaceAll(
            IEnumerable<TraitCategoryDefinition> stats,
            IEnumerable<TraitCategoryDefinition> skills,
            IEnumerable<TraitCategoryDefinition> monsters,
            IEnumerable<TraitCategoryDefinition> loot,
            IEnumerable<TraitCategoryDefinition> pets,
            IEnumerable<TraitMonsterBalanceDefinition> balances = null)
        {
            Replace(stats, skills, monsters);
            lootCategories = Copy(loot);
            petCategories = Copy(pets);
            monsterBalances = balances == null
                ? new List<TraitMonsterBalanceDefinition>()
                : new List<TraitMonsterBalanceDefinition>(balances);
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
            TraitCatalog catalog = CreateInstance<TraitCatalog>();
            catalog.name = "TraitCatalog_RuntimeDemo";
            catalog.hideFlags = HideFlags.HideAndDontSave;

            Color coral = new Color(0.95f, 0.53f, 0.48f, 1f);
            Color sky = new Color(0.40f, 0.70f, 0.95f, 1f);
            Color yellow = new Color(0.98f, 0.76f, 0.24f, 1f);
            Color pink = new Color(0.94f, 0.55f, 0.72f, 1f);
            Color mint = new Color(0.46f, 0.79f, 0.60f, 1f);
            Color violet = new Color(0.62f, 0.54f, 0.90f, 1f);

            TraitCategoryDefinition attack =
                new TraitCategoryDefinition("stat.attack", "공격력", coral);
            int[] attackValues = { 1, 3, 6, 10, 16, 24, 35, 50, 70, 100 };
            int[] attackCosts = { 0, 15, 30, 55, 90, 140, 210, 300, 420, 580 };
            int[] attackTiers = { 0, 0, 1, 1, 2, 2, 3, 3, 4, 5 };
            for (int i = 0; i < attackValues.Length; i++)
            {
                string id = "stat.attack." + (i + 1).ToString("00");
                TraitNodeDefinition node = new TraitNodeDefinition(
                    id,
                    "공격력 " + (i + 1),
                    "기본 커서 공격력을 " + attackValues[i] + "로 설정합니다.",
                    "공격력 " + attackValues[i],
                    attackCosts[i],
                    attackTiers[i],
                    i == 0);
                // 공격력은 초반 가넷에서 시작해 보스 구간별 젬스톤으로
                // 결제 재화를 넘긴다. 후반 노드가 다시 가넷으로 돌아가지
                // 않도록 비용 ID를 노드에 명시한다.
                string attackCurrency = i < 2
                    ? "gem.garnet"
                    : i < 4
                        ? "gem.topaz"
                        : i < 6
                            ? "gem.amethyst"
                            : i < 8
                                ? "gem.sapphire"
                                : i < 9
                                    ? "gem.diamond"
                                    : "gem.dragon";
                node.WithCostGemstone(attackCurrency);
                if (i > 0)
                {
                    node.WithPrerequisite("stat.attack." + i.ToString("00"));
                }
                attack.AddNode(node);
            }

            TraitCategoryDefinition radius =
                new TraitCategoryDefinition("stat.radius", "반경", sky);
            AddRadiusNode(radius, "01", "작은 원", "기본 커서 반경입니다.", "기본", 0, 0, true, "gem.garnet");
            AddRadiusNode(radius, "02", "커서 영역 증가 1", "작은 원에서 조금 넓어집니다.", "+1", 12, 0, false, "gem.garnet");
            AddRadiusNode(radius, "03", "커서 영역 증가 2", "커서 판정이 한 단계 넓어집니다.", "+2", 24, 0, false, "gem.garnet");
            AddRadiusNode(radius, "04", "커서 영역 증가 3", "커서 판정이 한 단계 넓어집니다.", "+3", 42, 1, false, "gem.topaz");
            AddRadiusNode(radius, "05", "커서 영역 증가 4", "커서 판정이 한 단계 넓어집니다.", "+4", 65, 1, false, "gem.topaz");
            AddRadiusNode(radius, "06", "커서 영역 증가 5", "커서 판정이 한 단계 넓어집니다.", "+5", 95, 2, false, "gem.amethyst");
            AddRadiusNode(radius, "07", "필드의 1/32", "원형 판정이 필드의 1/32까지 닿습니다.", "1/32", 130, 2, false, "gem.amethyst");
            AddRadiusNode(radius, "08", "필드의 1/16", "원형 판정이 필드의 1/16까지 닿습니다.", "1/16", 185, 3, false, "gem.sapphire");
            AddRadiusNode(radius, "09", "필드의 1/8", "원형 판정이 필드의 1/8까지 닿습니다.", "1/8", 255, 3, false, "gem.sapphire");
            AddRadiusNode(radius, "10", "필드의 1/4", "원형 판정이 필드의 1/4까지 닿습니다.", "1/4", 350, 4, false, "gem.diamond");
            AddRadiusNode(radius, "11", "화면 전체", "원형 판정이 화면 전체를 덮습니다.", "전체", 500, 5, false, "gem.dragon");
            ChainNodes(radius);

            TraitCategoryDefinition multiClick =
                new TraitCategoryDefinition("stat.multiClick", "다중 클릭", yellow);
            AddNode(multiClick, "stat.multiClick.01", "1회 클릭", "한 번 입력하면 한 번 타격합니다.", "1회", 0, 0, true);
            AddNode(multiClick, "stat.multiClick.02", "더블 클릭", "한 번 입력하면 두 번 병렬 타격합니다. 반복 입력과 중첩됩니다.", "2회", 40, 1, false).WithCostGemstone("gem.topaz");
            AddNode(multiClick, "stat.multiClick.03", "트리플 클릭", "한 번 입력하면 세 번 병렬 타격합니다.", "3회", 90, 2, false).WithCostGemstone("gem.amethyst");
            AddNode(multiClick, "stat.multiClick.04", "쿼드 클릭", "한 번 입력하면 네 번 병렬 타격합니다.", "4회", 180, 3, false).WithCostGemstone("gem.sapphire");
            // The v4 clear is the turning point into the late-game auto
            // build. Keeping this at tier 4 lets the player use automatic
            // clicking while learning the v5 fight, instead of making the
            // final unlock require the ending itself.
            AddNode(multiClick, "stat.multiClick.05", "자동 무한 클릭", "한 번 입력하면 자동 타격이 계속 이어집니다.", "무한", 360, 4, false).WithCostGemstone("gem.dragon");
            ChainNodes(multiClick);

            TraitCategoryDefinition critical =
                new TraitCategoryDefinition("stat.critical", "치명타", pink);
            AddNode(critical, "stat.critical.01", "치명타 0%", "치명타가 발생하지 않는 기본 확률입니다.", "0%", 0, 0, true);
            AddNode(critical, "stat.critical.02", "치명타 5%", "치명타 확률이 5%가 됩니다.", "5%", 20, 0, false).WithCostGemstone("gem.garnet");
            AddNode(critical, "stat.critical.03", "치명타 10%", "치명타 확률이 10%가 됩니다.", "10%", 40, 1, false).WithCostGemstone("gem.topaz");
            AddNode(critical, "stat.critical.04", "치명타 20%", "치명타 확률이 20%가 됩니다.", "20%", 75, 2, false).WithCostGemstone("gem.amethyst");
            AddNode(critical, "stat.critical.05", "치명타 30%", "치명타 확률이 30%가 됩니다.", "30%", 120, 2, false).WithCostGemstone("gem.amethyst");
            AddNode(critical, "stat.critical.06", "치명타 50%", "치명타 확률이 50%가 됩니다.", "50%", 190, 3, false).WithCostGemstone("gem.sapphire");
            AddNode(critical, "stat.critical.07", "치명타 75%", "치명타 확률이 75%가 됩니다.", "75%", 300, 4, false).WithCostGemstone("gem.diamond");
            AddNode(critical, "stat.critical.08", "치명타 100%", "모든 유효 타격이 치명타가 됩니다.", "100%", 480, 5, false).WithCostGemstone("gem.dragon");
            ChainNodes(critical);

            TraitCategoryDefinition gemstone =
                new TraitCategoryDefinition("stat.gemstone", "젬 수집", mint);
            AddNode(gemstone, "stat.gemstone.01", "접촉 클릭", "젬스톤에 커서를 닿게 한 뒤 클릭해야 수집합니다.", "접촉", 0, 0, true);
            AddNode(gemstone, "stat.gemstone.02", "접촉 자동 획득", "젬스톤이 커서에 닿으면 자동으로 획득합니다.", "자동 접촉", 35, 1, false).WithCostGemstone("gem.topaz");
            AddNode(gemstone, "stat.gemstone.03", "자석 효과", "커서 주변의 자석 반경 안에 들어온 젬스톤을 끌어옵니다.", "자석", 90, 2, false).WithCostGemstone("gem.amethyst");
            AddNode(gemstone, "stat.gemstone.04", "자석 반경 확대 1~2", "젬스톤 자석 반경을 두 단계 확대합니다.", "자석 +2", 240, 3, false).WithCostGemstone("gem.sapphire");
            AddNode(gemstone, "stat.gemstone.05", "화면 전체 자동 수집", "전장에 생성된 젬스톤을 자동으로 수집합니다.", "전체 자동", 420, 5, false).WithCostGemstone("gem.dragon");
            ChainNodes(gemstone, 5);

            TraitNodeDefinition topazUnlock = AddGemstoneChain(
                gemstone, "topaz", "토파즈", 1, 55, 20, 5, "토파즈", "gem.garnet");
            TraitNodeDefinition amethystUnlock = AddGemstoneChain(
                gemstone, "amethyst", "자수정", 2, 110, 12, 4, "자수정", "gem.topaz").WithPrerequisite(topazUnlock.Id);
            TraitNodeDefinition sapphireUnlock = AddGemstoneChain(
                gemstone, "sapphire", "사파이어", 2, 170, 8, 3, "사파이어", "gem.amethyst").WithPrerequisite(amethystUnlock.Id);
            TraitNodeDefinition diamondUnlock = AddGemstoneChain(
                gemstone, "diamond", "다이아몬드", 3, 250, 4, 2, "다이아몬드", "gem.sapphire").WithPrerequisite(sapphireUnlock.Id);
            TraitNodeDefinition dragonUnlock = AddGemstoneChain(
                gemstone, "dragon", "드래곤 젬", 4, 360, 2, 1, "드래곤 젬", "gem.diamond").WithPrerequisite(diamondUnlock.Id);

            TraitCategoryDefinition boss =
                new TraitCategoryDefinition("stat.boss", "보스 피해", violet);
            AddBossNode(boss, "01", "보스 공격력 1", "+25%", 25, 1, "gem.topaz");
            AddBossNode(boss, "02", "보스 공격력 2", "+60%", 60, 2, "gem.amethyst");
            AddBossNode(boss, "03", "보스 공격력 3", "+110%", 110, 3, "gem.sapphire");
            AddBossNode(boss, "04", "보스 공격력 4", "+180%", 180, 4, "gem.diamond");
            AddBossNode(boss, "05", "보스 공격력 5", "+300%", 300, 5, "gem.dragon");
            ChainNodes(boss);

            TraitCategoryDefinition fieldDuration =
                new TraitCategoryDefinition("stat.fieldDuration", "일반 필드 시간", sky);
            int[] durationCosts = { 0, 15, 35, 65, 110, 180, 280, 420, 600, 850 };
            int[] durationTiers = { 0, 0, 1, 1, 2, 2, 3, 3, 4, 5 };
            string[] durationCurrencies =
            {
                "gem.garnet", "gem.garnet", "gem.topaz", "gem.topaz", "gem.amethyst",
                "gem.amethyst", "gem.sapphire", "gem.sapphire", "gem.diamond", "gem.dragon"
            };
            for (int i = 0; i < 10; i++)
            {
                int seconds = 15 + i * 5;
                AddNode(
                    fieldDuration,
                    "stat.fieldDuration." + (i + 1).ToString("00"),
                    "일반 필드 " + seconds + "초",
                    "일반 필드 제한 시간을 " + seconds + "초로 설정합니다. 보스 필드는 항상 60초입니다.",
                    seconds + "초",
                    durationCosts[i],
                    durationTiers[i],
                    i == 0).WithCostGemstone(durationCurrencies[i]);
            }
            ChainNodes(fieldDuration);

            catalog.statCategories.Add(attack);
            catalog.statCategories.Add(radius);
            catalog.statCategories.Add(multiClick);
            catalog.statCategories.Add(critical);
            catalog.statCategories.Add(gemstone);
            catalog.statCategories.Add(boss);
            catalog.statCategories.Add(fieldDuration);

            catalog.skillCategories.Add(CreateSkillCategory(
                "skill.fireball", "파이어볼", coral, 0, 0, "gem.garnet", true));
            catalog.skillCategories.Add(CreateSkillCategory(
                "skill.lightning", "라이트닝 볼트", sky, 1, 2, "gem.topaz", false));
            catalog.skillCategories.Add(CreateSkillCategory(
                "skill.freeze", "프리즈닝", mint, 2, 3, "gem.amethyst", false));
            catalog.skillCategories.Add(CreateSkillCategory(
                "skill.hurricane", "허리케인", yellow, 2, 3, "gem.sapphire", false));
            catalog.skillCategories.Add(CreateSkillCategory(
                "skill.meteor", "메테오", coral, 3, 4, "gem.diamond", false));
            catalog.skillCategories.Add(CreateSkillCategory(
                "skill.dragonBreath", "드래곤 브레스", violet, 4, 1, "gem.dragon", false));

            int[] monsterTiers = { 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 5, 5, 5, 5 };
            string[] monsterFeatures =
            {
                "기본형", "빠른 이동", "나타났다가 사라짐", "체력이 많음", "방패 보유",
                "점프 이동", "크기가 작음", "크기가 크고 느림", "지그재그 이동", "짧은 순간이동",
                "이동 방향 급변", "원형 이동", "처치 시 분리", "잔상 생성", "이동 중 크기 변화",
                "화면 가장자리 재등장", "여러 겹 방패", "특정 구간에서 급가속", "무리를 지어 이동", "특징 무작위 조합"
            };
            for (int i = 0; i < monsterFeatures.Length; i++)
            {
                string number = (i + 1).ToString("00");
                string monsterId = "monster." + number;
                string title = "Monster" + (i + 1);
                TraitCategoryDefinition monsterCategory =
                    new TraitCategoryDefinition(monsterId, title, mint);
                TraitNodeDefinition monsterNode = new TraitNodeDefinition(
                    monsterId,
                    title,
                    monsterFeatures[i] + " 특징을 가진 일반 몬스터입니다.",
                    monsterFeatures[i],
                    0,
                    monsterTiers[i],
                    i == 0);
                monsterCategory.AddNode(monsterNode);

                string previous = monsterId;
                for (int level = 1; level <= 3; level++)
                {
                    string productionId = monsterId + ".production." + level.ToString("00");
                    TraitNodeDefinition production = new TraitNodeDefinition(
                        productionId,
                        title + " 생산량 " + level,
                        title + "의 동시 생성 묶음이 " + (level * 25) + "% 증가합니다.",
                        "+" + (level * 25) + "%",
                        20 + level * 25 + monsterTiers[i] * 10,
                        monsterTiers[i],
                        false).WithPrerequisite(previous).WithCostGemstone(GemstoneForTier(monsterTiers[i]));
                    monsterCategory.AddNode(production);
                    previous = productionId;
                }

                // Production levels live immediately after their monster's
                // unlock node. The UI can therefore use one icon and a
                // different border color for the three follow-up upgrades.
                catalog.monsterCategories.Add(monsterCategory);
            }
            long[] monsterHp =
            {
                20L, 45L, 70L, 140L, 220L, 500L, 350L, 900L, 1200L, 4000L,
                6000L, 8000L, 12000L, 50000L, 70000L, 90000L, 400000L,
                650000L, 900000L, 2200000L
            };
            long[] monsterGarnetRewards =
            {
                3L, 5L, 7L, 9L, 12L, 18L, 22L, 28L, 35L, 50L,
                65L, 80L, 110L, 160L, 200L, 260L, 380L, 450L, 520L, 600L
            };
            float[] monsterIntervals =
            {
                1.2f, 1.4f, 1.6f, 1.8f, 2.0f, 2.2f, 1.7f, 2.5f, 2.4f, 2.8f,
                2.6f, 3.0f, 3.2f, 3.5f, 3.2f, 3.8f, 4.0f, 3.6f, 4.2f, 4.8f
            };
            string[] monsterDropCurrencies =
            {
                string.Empty,
                "gem.topaz", "gem.topaz", "gem.topaz", "gem.topaz",
                "gem.amethyst", "gem.amethyst", "gem.amethyst", "gem.amethyst",
                "gem.sapphire", "gem.sapphire", "gem.sapphire", "gem.sapphire",
                "gem.diamond", "gem.diamond", "gem.diamond",
                "gem.dragon", "gem.dragon", "gem.dragon", "gem.dragon"
            };
            long[] monsterDropAmounts =
            {
                0L, 1L, 1L, 2L, 2L, 1L, 1L, 2L, 2L, 1L,
                1L, 2L, 2L, 1L, 1L, 2L, 1L, 1L, 2L, 3L
            };
            float[] monsterDropChances =
            {
                0f, 12f, 14f, 16f, 18f, 10f, 12f, 14f, 16f, 9f,
                11f, 13f, 15f, 8f, 10f, 12f, 7f, 9f, 11f, 15f
            };
            float[] monsterFragmentChances =
            {
                1.5f, 1.5f, 1.7f, 1.9f, 2.1f, 1.5f, 1.7f, 1.9f, 2.1f, 1.5f,
                1.7f, 1.9f, 2.1f, 1.6f, 1.8f, 2.0f, 1.7f, 1.9f, 2.1f, 2.5f
            };
            for (int i = 0; i < monsterFeatures.Length; i++)
            {
                string fragmentCurrency = "fragment.loot.monster." + (i + 1).ToString("00");
                catalog.monsterBalances.Add(new TraitMonsterBalanceDefinition(
                    "monster." + (i + 1).ToString("00"),
                    monsterFeatures[i],
                    monsterHp[i],
                    monsterIntervals[i],
                    1,
                    monsterGarnetRewards[i],
                    monsterDropCurrencies[i],
                    monsterDropAmounts[i],
                    monsterDropChances[i],
                    fragmentCurrency,
                    monsterFragmentChances[i]));
            }
            TraitCategoryDefinition monsterLoot =
                new TraitCategoryDefinition("loot.monster", "일반 몬스터 전리품", coral);
            for (int i = 0; i < monsterFeatures.Length; i++)
            {
                int tier = monsterTiers[i];
                string lootId = "loot.monster." + (i + 1).ToString("00");
                string target = "Monster" + (i + 1);
                string effect = LootEffectForIndex(i, 0, target);
                TraitNodeDefinition unlock = new TraitNodeDefinition(
                    lootId,
                    target + " 전리품 해금",
                    target + " 전리품 조각 1개로 효과를 해금합니다. " + effect,
                    effect,
                    1,
                    tier,
                    false).WithCostGemstone("fragment.loot.monster." + (i + 1).ToString("00"));
                monsterLoot.AddNode(unlock);
                string previous = unlock.Id;
                int[] fragmentCosts = { 2, 3, 5, 10 };
                for (int level = 1; level <= fragmentCosts.Length; level++)
                {
                    TraitNodeDefinition upgrade = new TraitNodeDefinition(
                        lootId + ".level." + level.ToString("00"),
                        target + " 전리품 Lv." + level,
                        "전리품 효과를 강화합니다. " + LootEffectForIndex(i, level, target),
                        LootEffectForIndex(i, level, target),
                        fragmentCosts[level - 1],
                        tier,
                        false).WithPrerequisite(previous)
                        .WithCostGemstone("fragment.loot.monster." + (i + 1).ToString("00"));
                    monsterLoot.AddNode(upgrade);
                    previous = upgrade.Id;
                }
            }
            TraitCategoryDefinition bossLoot =
                new TraitCategoryDefinition("loot.boss", "보스 전리품", violet);
            for (int tier = 1; tier <= 5; tier++)
            {
                string lootId = "loot.boss." + tier.ToString("00");
                string fragmentCurrency = "fragment.loot.boss." + tier.ToString("00");
                TraitNodeDefinition unlock = new TraitNodeDefinition(
                    lootId,
                    "보스 v" + tier + " 전리품 해금",
                    "보스 v" + tier + " 전리품 조각 1개로 효과를 해금합니다. 보스 피해 +5%.",
                    "보스 피해 +5%",
                    1,
                    tier,
                    false).WithCostGemstone(fragmentCurrency);
                bossLoot.AddNode(unlock);
                string previous = unlock.Id;
                int[] fragmentCosts = { 2, 3, 5, 10 };
                string[] effects = { "보스 피해 +10%", "보스 정산 +10%", "보스 보너스 드롭 +5%", "보스 피해 +20%" };
                for (int level = 1; level <= effects.Length; level++)
                {
                    TraitNodeDefinition upgrade = new TraitNodeDefinition(
                        lootId + ".level." + level.ToString("00"),
                        "보스 v" + tier + " 전리품 Lv." + level,
                        "보스 전리품 효과를 강화합니다. " + effects[level - 1],
                        effects[level - 1],
                        fragmentCosts[level - 1],
                        tier,
                        false).WithPrerequisite(previous)
                        .WithCostGemstone(fragmentCurrency);
                    bossLoot.AddNode(upgrade);
                    previous = upgrade.Id;
                }
            }
            catalog.lootCategories.Add(monsterLoot);
            catalog.lootCategories.Add(bossLoot);

            TraitCategoryDefinition pet =
                new TraitCategoryDefinition("pet.cursor", "커서 펫", sky);
            AddNode(pet, "pet.cursor.01", "펫 1 해금", "커서 주변에서 자동 공격하는 펫 1마리를 보유합니다.", "1마리", 0, 0, true);
            AddNode(pet, "pet.cursor.02", "펫 영역 I", "펫의 개별 커서 영역이 25% 증가합니다.", "+25% 영역", 45, 1, false).WithCostGemstone("gem.topaz");
            AddNode(pet, "pet.cursor.03", "펫 공격력 I", "펫 자동 공격력이 50% 증가합니다.", "+50% 피해", 80, 2, false).WithCostGemstone("gem.amethyst");
            AddNode(pet, "pet.cursor.04", "펫 공격 주기 I", "펫 자동 공격 주기가 20% 빨라집니다.", "-20% 주기", 120, 3, false).WithCostGemstone("gem.sapphire");
            AddNode(pet, "pet.cursor.05", "펫 영역 II", "펫의 개별 커서 영역이 추가로 50% 증가합니다.", "+50% 영역", 200, 4, false).WithCostGemstone("gem.diamond");
            catalog.petCategories.Add(pet);

            catalog.gemstones.Add(new TraitGemstoneDefinition(
                "gem.garnet", "가넷", "게임 시작부터 사용하는 기본 젬스톤입니다.",
                string.Empty, 70, 0, 1000, string.Empty, true));
            catalog.gemstones.Add(new TraitGemstoneDefinition(
                "gem.topaz", "토파즈", "보스 v1 이후 스탯에서 해금하면 등장합니다.",
                topazUnlock.Id, 20, 5, 1000, "stat.gemstone.rate.topaz."));
            catalog.gemstones.Add(new TraitGemstoneDefinition(
                "gem.amethyst", "자수정", "보스 v2 이후 스탯에서 해금하면 등장합니다.",
                amethystUnlock.Id, 12, 4, 1000, "stat.gemstone.rate.amethyst."));
            catalog.gemstones.Add(new TraitGemstoneDefinition(
                "gem.sapphire", "사파이어", "보스 v2 이후 스탯에서 해금하면 등장합니다.",
                sapphireUnlock.Id, 8, 3, 1000, "stat.gemstone.rate.sapphire."));
            catalog.gemstones.Add(new TraitGemstoneDefinition(
                "gem.diamond", "다이아몬드", "보스 v3 이후 스탯에서 해금하면 등장합니다.",
                diamondUnlock.Id, 4, 2, 1000, "stat.gemstone.rate.diamond."));
            catalog.gemstones.Add(new TraitGemstoneDefinition(
                "gem.dragon", "드래곤 젬", "보스 v4 이후 드래곤 브레스와 함께 등장합니다.",
                dragonUnlock.Id, 2, 1, 1000, "stat.gemstone.rate.dragon."));

            catalog.bossRewards.Add(new TraitBossRewardDefinition(
                1, 100, "gem.topaz", 20, 8f, 1));
            catalog.bossRewards.Add(new TraitBossRewardDefinition(
                2, 500, "gem.amethyst", 20, 10f, 2));
            catalog.bossRewards.Add(new TraitBossRewardDefinition(
                3, 3000, "gem.sapphire", 15, 12f, 3));
            catalog.bossRewards.Add(new TraitBossRewardDefinition(
                4, 20000, "gem.diamond", 8, 15f, 5));
            catalog.bossRewards.Add(new TraitBossRewardDefinition(
                5, 100000, "gem.dragon", 5, 20f, 10));

            return catalog;
        }

        private static TraitNodeDefinition AddNode(
            TraitCategoryDefinition category,
            string id,
            string title,
            string description,
            string value,
            int cost,
            int tier,
            bool startsUnlocked)
        {
            TraitNodeDefinition node = new TraitNodeDefinition(
                id, title, description, value, cost, tier, startsUnlocked);
            category.AddNode(node);
            return node;
        }

        private static void ChainNodes(TraitCategoryDefinition category, int count = -1)
        {
            if (category == null || category.Nodes == null)
            {
                return;
            }

            int limit = count < 0
                ? category.Nodes.Count
                : Mathf.Min(count, category.Nodes.Count);
            for (int i = 1; i < limit; i++)
            {
                TraitNodeDefinition previous = category.Nodes[i - 1];
                TraitNodeDefinition current = category.Nodes[i];
                if (previous != null && current != null)
                {
                    current.WithPrerequisite(previous.Id);
                }
            }
        }

        private static void AddRadiusNode(
            TraitCategoryDefinition category,
            string number,
            string title,
            string description,
            string value,
            int cost,
            int tier,
            bool startsUnlocked,
            string currencyId)
        {
            AddNode(category, "stat.radius." + number, title, description, value, cost, tier, startsUnlocked)
                .WithCostGemstone(currencyId);
        }

        private static string LootEffectForIndex(int monsterIndex, int level, string target)
        {
            int pattern = monsterIndex % 4;
            switch (pattern)
            {
                case 0:
                    return target + " 대상 피해 +" + (5 + level * 5) + "%";
                case 1:
                    return target + " 생산량 +" + (10 + level * 5) + "%";
                case 2:
                    return target + " 수확량 +" + (10 + level * 5) + "%";
                default:
                    return target + " 전리품 확률 +" + (2 + level * 2) + "%";
            }
        }

        private static TraitNodeDefinition AddGemstoneChain(
            TraitCategoryDefinition category,
            string gemstoneKey,
            string title,
            int tier,
            int unlockCost,
            int rateCost,
            int rateStep,
            string displayName,
            string unlockCurrencyId)
        {
            string unlockId = "stat.gemstone.unlock." + gemstoneKey;
            TraitNodeDefinition unlock = new TraitNodeDefinition(
                unlockId,
                displayName + " 해금",
                displayName + "을 스탯의 랜덤 젬 드롭 목록에 추가합니다.",
                "해금",
                unlockCost,
                tier).WithCostGemstone(unlockCurrencyId);
            category.AddNode(unlock);
            string previous = unlock.Id;
            for (int i = 1; i <= 2; i++)
            {
                string rateId = "stat.gemstone.rate." + gemstoneKey + "." + i.ToString("00");
                TraitNodeDefinition rate = new TraitNodeDefinition(
                    rateId,
                    displayName + " 발견 " + i,
                    displayName + "의 등장 가중치를 " + rateStep + " 올립니다.",
                    "+" + rateStep + " 가중치",
                    rateCost + (i - 1) * rateCost,
                    tier).WithPrerequisite(previous).WithCostGemstone("gem." + gemstoneKey);
                category.AddNode(rate);
                previous = rate.Id;
            }
            return unlock;
        }

        private static void AddBossNode(
            TraitCategoryDefinition category,
            string number,
            string title,
            string value,
            int cost,
            int tier,
            string gemstoneId)
        {
            category.AddNode(new TraitNodeDefinition(
                "stat.boss." + number,
                title,
                "보스 v" + tier + " 격파 이후 보스에게 주는 피해가 " + value + " 증가합니다.",
                value,
                cost,
                tier).WithCostGemstone(gemstoneId));
        }

        private static string GemstoneForTier(int tier)
        {
            switch (tier)
            {
                case 1: return "gem.topaz";
                case 2: return "gem.amethyst";
                case 3: return "gem.sapphire";
                case 4: return "gem.diamond";
                case 5: return "gem.dragon";
                default: return "gem.garnet";
            }
        }

        private static TraitCategoryDefinition CreateSkillCategory(
            string skillId,
            string skillTitle,
            Color accent,
            int requiredBossTier,
            int unlockCost,
            string currencyId,
            bool startsUnlocked)
        {
            TraitCategoryDefinition category =
                new TraitCategoryDefinition(skillId, skillTitle, accent);
            TraitNodeDefinition unlock = new TraitNodeDefinition(
                skillId,
                skillTitle,
                startsUnlocked
                    ? skillTitle + "은(는) 게임 시작부터 사용할 수 있습니다."
                    : skillTitle + " 스킬을 해금합니다.",
                startsUnlocked ? "기본 스킬" : "스킬 해금",
                startsUnlocked ? 0 : unlockCost,
                requiredBossTier,
                startsUnlocked).WithCostGemstone(currencyId);
            category.AddNode(unlock);
            TraitNodeDefinition damage = new TraitNodeDefinition(
                skillId + ".damage",
                "피해량 강화",
                skillTitle + "의 피해량이 35% 증가합니다.",
                "+35% 피해",
                startsUnlocked ? 10 : unlockCost + 1,
                requiredBossTier).WithPrerequisite(unlock.Id).WithCostGemstone(currencyId);
            TraitNodeDefinition radius = new TraitNodeDefinition(
                skillId + ".radius",
                "범위 강화",
                skillTitle + "의 효과 범위가 30% 증가합니다.",
                "+30% 범위",
                startsUnlocked ? 14 : unlockCost + 2,
                requiredBossTier).WithPrerequisite(damage.Id).WithCostGemstone(currencyId);
            TraitNodeDefinition cooldown = new TraitNodeDefinition(
                skillId + ".cooldown",
                "쿨타임 강화",
                skillTitle + "의 쿨타임이 20% 감소합니다.",
                "-20% 쿨타임",
                startsUnlocked ? 18 : unlockCost + 3,
                requiredBossTier).WithPrerequisite(radius.Id).WithCostGemstone(currencyId);
            category.AddNode(damage);
            category.AddNode(radius);
            category.AddNode(cooldown);
            return category;
        }
    }
}
