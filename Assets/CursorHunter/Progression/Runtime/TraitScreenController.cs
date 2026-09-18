using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CursorHunter.Progression
{
    /// <summary>
    /// Runtime-owned presentation for the trait screen. The controller builds
    /// its children from TraitCatalog, so category and node counts can change
    /// without changing this layout code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TraitScreenController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private TraitCatalog catalog;
        [SerializeField, Min(0)] private long startingGarnet = 125;
        [SerializeField] private long[] startingGemstoneBalances =
            new long[] { 125L, 0L, 0L, 0L, 0L, 0L };
        [SerializeField, Range(0, 5)] private int unlockedBossTier;
        [SerializeField] private UnityEvent backRequested = new UnityEvent();

        [Header("Casual Fantasy sprites")]
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite combatBackgroundSprite;
        [SerializeField] private Sprite tabSprite;
        [SerializeField] private Sprite tabFocusSprite;
        [SerializeField] private Sprite categorySprite;
        [SerializeField] private Sprite categoryFocusSprite;
        [SerializeField] private Sprite nodeSprite;
        [SerializeField] private Sprite nodeAccentSprite;
        [SerializeField] private Sprite nodePurchasedSprite;
        [SerializeField] private Sprite nodeLockedSprite;
        [SerializeField] private Sprite gemIcon;
        [SerializeField] private Sprite lockIcon;
        [SerializeField] private Sprite checkIcon;
        [SerializeField] private Sprite backIcon;
        [SerializeField] private Sprite attackIcon;
        [SerializeField] private Sprite radiusIcon;
        [SerializeField] private Sprite clickIcon;
        [SerializeField] private Sprite timerIcon;
        [SerializeField] private Sprite criticalIcon;
        [SerializeField] private Sprite gemstoneIcon;
        [SerializeField] private Sprite bossIcon;
        [SerializeField] private Sprite skillIcon;
        [SerializeField] private Sprite monsterIcon;
        [SerializeField] private Sprite lootIcon;
        [SerializeField] private Sprite petIcon;
        [SerializeField] private Sprite[] gemstoneIcons = new Sprite[0];
        [SerializeField] private Sprite[] skillIcons = new Sprite[0];
        [SerializeField] private Sprite[] monsterIcons = new Sprite[0];
        [SerializeField] private Sprite[] lootIcons = new Sprite[0];
        [SerializeField] private Sprite[] petIcons = new Sprite[0];
        [SerializeField] private Font uiFont;

        [Header("Layout")]
        [SerializeField, Range(0, 4)] private int initialTab;
        [SerializeField, Min(0f)] private float transitionSeconds = 0.22f;
        [SerializeField] private Vector2 nodeCellSize = new Vector2(84f, 84f);
        [SerializeField] private Vector2 nodeGridSpacing = new Vector2(8f, 8f);
        [SerializeField, Min(12f)] private float connectorWidth = 22f;
        [SerializeField, Range(2f, 16f)] private float connectorThickness = 3f;
        [SerializeField, Min(24f)] private float categoryHeaderHeight = 28f;
        [SerializeField, Range(1f, 1.5f)] private float textScale = 1.18f;
        [SerializeField] private Color backgroundColor =
            new Color(0.015f, 0.045f, 0.11f, 0.98f);
        [SerializeField] private Color panelColor =
            new Color(0.02f, 0.06f, 0.14f, 0.84f);
        [SerializeField] private Color inkColor =
            new Color(0.96f, 0.98f, 1f, 1f);
        [SerializeField] private Color mutedInkColor =
            new Color(0.64f, 0.75f, 0.88f, 1f);

        private TraitCatalog _runtimeCatalog;
        private TraitCatalog _activeCatalog;
        private bool _ownsRuntimeCatalog;
        private bool _isBuilt;
        private TraitTab _activeTab;
        private string _selectedNodeId;
        private long _garnetBalance;
        private long[] _gemstoneBalances;

        private readonly HashSet<string> _purchased =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _spentByNode =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _spentCurrencyByNode =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private RectTransform _runtimeRoot;
        private RectTransform _nodeContent;
        private RectTransform _gemstoneList;
        private Text _gemstoneUnlockMessage;
        private CanvasGroup _runtimeGroup;
        private CanvasGroup _nodeGroup;
        private Text _nodeHeading;
        private Text _garnetValue;
        private Text _selectedTitle;
        private Text _selectedDescription;
        private Text _selectedValue;
        private Text _selectedCost;
        private Text _selectedRequirement;
        private Text _statusMessage;
        private Button _upgradeButton;
        private Image _upgradeButtonImage;
        private readonly List<Text> _summaryValues = new List<Text>();
        private Coroutine _transition;
        private Coroutine _nodeTransition;

        private static readonly Color LockedColor =
            new Color(0.06f, 0.10f, 0.18f, 0.96f);
        private static readonly Color AvailableColor =
            new Color(1f, 0.74f, 0.27f, 1f);
        private static readonly Color PurchasedColor =
            new Color(0.35f, 0.90f, 0.64f, 1f);

        private static readonly Color[] GemstoneColors =
        {
            new Color(0.98f, 0.28f, 0.36f, 1f),
            new Color(1.00f, 0.70f, 0.22f, 1f),
            new Color(0.80f, 0.43f, 0.96f, 1f),
            new Color(0.28f, 0.62f, 1.00f, 1f),
            new Color(0.42f, 0.94f, 0.96f, 1f),
            new Color(0.95f, 0.48f, 0.22f, 1f)
        };

        private void Awake()
        {
            HideAuthoredPlaceholders();
            EnsureCanvasScale();
            _garnetBalance = startingGarnet;
            ResolveCatalog();
            InitializeGemstoneBalances();
            InitializePurchasedState();
            BuildUi();
        }

        private void OnEnable()
        {
            if (!_isBuilt)
            {
                BuildUi();
            }

            PlayEnterAnimation();
        }

        private void OnDisable()
        {
            if (_transition != null)
            {
                StopCoroutine(_transition);
                _transition = null;
            }

            if (_nodeTransition != null)
            {
                StopCoroutine(_nodeTransition);
                _nodeTransition = null;
            }
        }

        private void OnDestroy()
        {
            if (_ownsRuntimeCatalog && _runtimeCatalog != null)
            {
                Destroy(_runtimeCatalog);
                _runtimeCatalog = null;
            }
        }

        /// <summary>
        /// Allows the app or a test scene to provide a fresh balance without
        /// coupling the UI to a save/profile implementation.
        /// </summary>
        public void SetProgressionSnapshot(long garnet, int bossTier)
        {
            SetProgressionSnapshot(garnet, null, bossTier);
        }

        /// <summary>
        /// Applies a read-only profile snapshot. The two-argument overload is
        /// kept for existing App bindings; the full overload also carries
        /// balances for every gemstone that is already unlocked.
        /// </summary>
        public void SetProgressionSnapshot(
            long garnet,
            IReadOnlyList<long> gemstoneBalances,
            int bossTier)
        {
            _garnetBalance = Math.Max(0L, garnet);
            if (gemstoneBalances != null)
            {
                int requiredLength = Mathf.Max(6, gemstoneBalances.Count);
                if (_gemstoneBalances == null ||
                    _gemstoneBalances.Length < requiredLength)
                {
                    Array.Resize(ref _gemstoneBalances, requiredLength);
                }

                Array.Clear(_gemstoneBalances, 0, _gemstoneBalances.Length);
                for (int i = 0; i < gemstoneBalances.Count; i++)
                {
                    _gemstoneBalances[i] = Math.Max(0L, gemstoneBalances[i]);
                }
            }
            else
            {
                EnsureGemstoneBalanceCapacity();
            }

            if (_gemstoneBalances != null && _gemstoneBalances.Length > 0)
            {
                _gemstoneBalances[0] = _garnetBalance;
            }
            unlockedBossTier = Mathf.Clamp(bossTier, 0, 5);
            AddStartingNodes(_activeCatalog == null ? null : _activeCatalog.StatCategories);
            AddStartingNodes(_activeCatalog == null ? null : _activeCatalog.SkillCategories);
            AddStartingNodes(_activeCatalog == null ? null : _activeCatalog.MonsterCategories);
            AddStartingNodes(_activeCatalog == null ? null : _activeCatalog.LootCategories);
            AddStartingNodes(_activeCatalog == null ? null : _activeCatalog.PetCategories);
            RefreshAll();
        }

        private void InitializeGemstoneBalances()
        {
            int sourceLength = startingGemstoneBalances == null
                ? 0
                : startingGemstoneBalances.Length;
            _gemstoneBalances = new long[Mathf.Max(6, sourceLength)];
            for (int i = 0; i < _gemstoneBalances.Length; i++)
            {
                long value = i < sourceLength ? startingGemstoneBalances[i] : 0L;
                _gemstoneBalances[i] = Math.Max(0L, value);
            }

            _gemstoneBalances[0] = Math.Max(0L, _garnetBalance);
        }

        private void EnsureGemstoneBalanceCapacity()
        {
            if (_gemstoneBalances != null && _gemstoneBalances.Length >= 6)
            {
                return;
            }

            long[] resized = new long[6];
            if (_gemstoneBalances != null)
            {
                Array.Copy(
                    _gemstoneBalances,
                    resized,
                    Mathf.Min(_gemstoneBalances.Length, resized.Length));
            }

            _gemstoneBalances = resized;
        }

        public void ShowStatTab()
        {
            ShowTab(TraitTab.Stat);
        }

        public void ShowSkillTab()
        {
            ShowTab(TraitTab.Skill);
        }

        public void ShowMonsterTab()
        {
            ShowTab(TraitTab.Monster);
        }

        public void ShowLootTab()
        {
            ShowTab(TraitTab.Loot);
        }

        public void ShowPetTab()
        {
            ShowTab(TraitTab.Pet);
        }

        public void RequestBack()
        {
            backRequested?.Invoke();
        }

        private void ResolveCatalog()
        {
            if (catalog != null)
            {
                _activeCatalog = catalog;
                return;
            }

            _runtimeCatalog = TraitCatalog.CreateRuntimeDemo();
            _activeCatalog = _runtimeCatalog;
            _ownsRuntimeCatalog = true;
        }

        private void InitializePurchasedState()
        {
            _purchased.Clear();
            _spentByNode.Clear();
            _spentCurrencyByNode.Clear();

            AddStartingNodes(_activeCatalog.StatCategories);
            AddStartingNodes(_activeCatalog.SkillCategories);
            AddStartingNodes(_activeCatalog.MonsterCategories);
            AddStartingNodes(_activeCatalog.LootCategories);
            AddStartingNodes(_activeCatalog.PetCategories);
        }

        private void AddStartingNodes(
            IReadOnlyList<TraitCategoryDefinition> categories)
        {
            if (categories == null)
            {
                return;
            }

            foreach (TraitCategoryDefinition category in categories)
            {
                if (category == null || category.Nodes == null)
                {
                    continue;
                }

                foreach (TraitNodeDefinition node in category.Nodes)
                {
                    if (node != null && node.StartsUnlocked &&
                        node.RequiredBossTier <= unlockedBossTier &&
                        !string.IsNullOrEmpty(node.Id))
                    {
                        _purchased.Add(node.Id);
                    }
                }
            }
        }

        private void BuildUi()
        {
            if (_isBuilt)
            {
                return;
            }

            _runtimeRoot = CreateRect(
                "TraitScreen_Runtime",
                transform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Vector2(0.5f, 0.5f));

            Image background = _runtimeRoot.gameObject.GetComponent<Image>();
            if (background == null)
            {
                background = _runtimeRoot.gameObject.AddComponent<Image>();
            }
            background.sprite = combatBackgroundSprite;
            // field_a.png is a 202x111 combat tile. Stretching it to the
            // entire canvas magnifies its texels and produces the blurred
            // screenshot seen at non-native resolutions. Tiling keeps the
            // texture at its imported pixel density; the source is configured
            // with Repeat wrap mode, so Unity can draw it as a single tiled
            // quad instead of allocating one quad per tile.
            background.type = combatBackgroundSprite == null
                ? Image.Type.Simple
                : Image.Type.Tiled;
            background.pixelsPerUnitMultiplier = 1f;
            // Keep the authored combat texture as a translucent blue-tinted
            // layer. The combat map remains underneath it, so props and
            // ambient details continue to read through the trait screen while
            // the palette matches the dark skill-tree reference.
            background.color = combatBackgroundSprite == null
                ? backgroundColor
                : new Color(0.22f, 0.36f, 0.58f, 0.38f);
            background.preserveAspect = false;
            background.raycastTarget = true;

            // The same field texture used by the combat scene remains visible
            // beneath the tree, while this shade keeps white connectors and
            // labels readable without adding a grey frame around the UI.
            RectTransform shadeRect = CreateRect(
                "FieldShade",
                _runtimeRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Vector2(0.5f, 0.5f));
            Image fieldShade = shadeRect.gameObject.AddComponent<Image>();
            fieldShade.color = new Color(0.006f, 0.018f, 0.065f, 0.80f);
            fieldShade.raycastTarget = false;

            _runtimeGroup = _runtimeRoot.gameObject.AddComponent<CanvasGroup>();
            _runtimeGroup.interactable = true;
            _runtimeGroup.blocksRaycasts = true;

            BuildHeader();
            BuildGemstonePanel();
            BuildSummaryPanel();
            BuildCategoryPanel();
            BuildNodePanel();
            BuildDetailPanel();
            BuildFooter();

            // The resource panel occupies the upper-left corner. Keep it on
            // top of the translucent node surface, while the node content's
            // left inset reserves that space so the two systems never stack
            // over the same controls.
            Transform gemstonePanel = _runtimeRoot.Find("GemstonePanel");
            if (gemstonePanel != null)
            {
                gemstonePanel.SetAsLastSibling();
            }

            _isBuilt = true;
            ShowTab((TraitTab)Mathf.Clamp(initialTab, 0, 4), false);
            Canvas.ForceUpdateCanvases();
        }

        private void BuildHeader()
        {
            RectTransform title = CreateRect(
                "Title",
                _runtimeRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(42f, -36f),
                new Vector2(180f, 70f),
                new Vector2(0f, 0.5f));
            CreateText(title, "특성", 32, TextAnchor.MiddleLeft, inkColor, true);

            RectTransform tabs = CreateRect(
                "Tabs",
                _runtimeRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -42f),
                new Vector2(820f, 72f),
                new Vector2(0.5f, 0.5f));
            HorizontalLayoutGroup layout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            CreateTabButton(tabs, "STATTab", "스탯", TraitTab.Stat);
            CreateTabButton(tabs, "SKILLTab", "스킬", TraitTab.Skill);
            CreateTabButton(tabs, "MONSTERTab", "몬스터", TraitTab.Monster);
            CreateTabButton(tabs, "LOOTTab", "전리품", TraitTab.Loot);
            CreateTabButton(tabs, "PETTab", "펫", TraitTab.Pet);
        }

        private void BuildGemstonePanel()
        {
            RectTransform panel = CreatePanel(
                "GemstonePanel",
                _runtimeRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, -132f),
                new Vector2(320f, 300f),
                null,
                new Color(0.015f, 0.045f, 0.12f, 0.88f),
                new Vector2(0f, 1f));

            RectTransform heading = CreateRect(
                "GemstoneHeading",
                panel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -10f),
                new Vector2(-32f, 28f),
                new Vector2(0.5f, 1f));
            CreateText(heading, "보유 젬스톤", 18, TextAnchor.MiddleLeft, inkColor, true);

            _gemstoneList = CreateRect(
                "GemstoneList",
                panel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -44f),
                new Vector2(-32f, 206f),
                new Vector2(0.5f, 1f));
            LayoutElement listElement = _gemstoneList.gameObject.AddComponent<LayoutElement>();
            listElement.preferredHeight = 206f;
            listElement.minHeight = 30f;
            VerticalLayoutGroup listLayout = _gemstoneList.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 4f;
            listLayout.childAlignment = TextAnchor.UpperLeft;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = false;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;

            RectTransform unlockMessage = CreateRect(
                "GemstoneUnlockMessage",
                panel,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 10f),
                new Vector2(-32f, 24f),
                new Vector2(0.5f, 0f));
            _gemstoneUnlockMessage = CreateText(
                unlockMessage,
                "",
                11,
                TextAnchor.MiddleLeft,
                mutedInkColor,
                false);

            RefreshGemstonePanel();
        }

        private void RefreshGemstonePanel()
        {
            if (_gemstoneList == null)
            {
                return;
            }

            DestroyChildren(_gemstoneList);
            _garnetValue = null;

            IReadOnlyList<TraitGemstoneDefinition> definitions =
                _activeCatalog == null ? null : _activeCatalog.Gemstones;
            int visibleCount = 0;
            bool hasLockedDefinition = false;
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    TraitGemstoneDefinition gemstone = definitions[i];
                    if (gemstone == null)
                    {
                        continue;
                    }

                    bool visible = IsGemstoneUnlocked(gemstone);
                    if (!visible)
                    {
                        hasLockedDefinition = true;
                        continue;
                    }

                    CreateGemstoneRow(_gemstoneList, gemstone, i, visibleCount);
                    visibleCount++;
                }
            }

            if (visibleCount == 0)
            {
                CreateText(
                    CreateRect("Empty", _gemstoneList, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 32f), new Vector2(0.5f, 0.5f)),
                    "젬스톤 정보를 불러오는 중...",
                    12,
                    TextAnchor.MiddleLeft,
                    mutedInkColor,
                    false);
            }

            if (_gemstoneUnlockMessage != null)
            {
                _gemstoneUnlockMessage.text = hasLockedDefinition
                    ? "스탯에서 해금 · 발견 강화로 등장 확률 증가"
                    : "해금한 젬스톤은 랜덤으로 등장합니다";
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_gemstoneList);
        }

        private void CreateGemstoneRow(
            Transform parent,
            TraitGemstoneDefinition gemstone,
            int gemstoneIndex,
            int visibleIndex)
        {
            RectTransform row = CreateRect(
                "Gemstone_" + (gemstone.Id ?? visibleIndex.ToString()),
                parent,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(0f, 34f),
                new Vector2(0.5f, 0.5f));
            LayoutElement rowElement = row.gameObject.AddComponent<LayoutElement>();
            rowElement.preferredHeight = 34f;
            rowElement.minHeight = 34f;

            Image rowImage = row.gameObject.AddComponent<Image>();
            Color accent = GetGemstoneColor(gemstoneIndex);
            rowImage.color = new Color(accent.r * 0.20f, accent.g * 0.20f, accent.b * 0.24f, 0.78f);
            rowImage.raycastTarget = false;

            Sprite iconSprite = GetGemstoneIcon(gemstoneIndex);
            if (iconSprite != null)
            {
                RectTransform icon = CreateImageRect(
                    "Icon",
                    row,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(18f, 0f),
                    new Vector2(28f, 28f),
                    iconSprite,
                    Color.white,
                    new Vector2(0.5f, 0.5f));
                Image iconImage = icon.GetComponent<Image>();
                iconImage.preserveAspect = true;
            }

            Text label = CreateText(
                CreateRect("Label", row, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(44f, 0f), new Vector2(-172f, 0f), new Vector2(0f, 0.5f)),
                gemstone.Title,
                14,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            label.color = Color.Lerp(inkColor, accent, 0.25f);

            long balance = GetGemstoneBalance(gemstoneIndex);
            Text value = CreateText(
                CreateRect("Value", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-112f, 0f), new Vector2(62f, 30f), new Vector2(1f, 0.5f)),
                balance.ToString(),
                15,
                TextAnchor.MiddleRight,
                inkColor,
                true);
            if (gemstoneIndex == 0)
            {
                _garnetValue = value;
            }

            CreateText(
                CreateRect("Chance", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(88f, 30f), new Vector2(1f, 0.5f)),
                FormatGemstoneChance(GetGemstoneDropChance(gemstoneIndex)),
                12,
                TextAnchor.MiddleRight,
                accent,
                true);
        }

        private Sprite GetGemstoneIcon(int index)
        {
            if (gemstoneIcons != null && index >= 0 && index < gemstoneIcons.Length &&
                gemstoneIcons[index] != null)
            {
                return gemstoneIcons[index];
            }

            return gemstoneIcon != null ? gemstoneIcon : gemIcon;
        }

        private long GetGemstoneBalance(int index)
        {
            if (index == 0)
            {
                return _garnetBalance;
            }

            if (_gemstoneBalances == null || index < 0 || index >= _gemstoneBalances.Length)
            {
                return 0L;
            }

            return Math.Max(0L, _gemstoneBalances[index]);
        }

        private static Color GetGemstoneColor(int index)
        {
            if (index < 0 || index >= GemstoneColors.Length)
            {
                return new Color(0.55f, 0.70f, 0.90f, 1f);
            }

            return GemstoneColors[index];
        }

        private bool IsGemstoneUnlocked(TraitGemstoneDefinition gemstone)
        {
            if (gemstone == null)
            {
                return false;
            }

            if (gemstone.StartsUnlocked)
            {
                return true;
            }

            // New definitions are unlocked by a Stat node. The boss-tier
            // check remains a compatibility fallback for older catalogs that
            // do not yet contain an unlockNodeId.
            if (!string.IsNullOrEmpty(gemstone.UnlockNodeId))
            {
                return _purchased.Contains(gemstone.UnlockNodeId);
            }

            return gemstone.RequiredBossTier <= unlockedBossTier;
        }

        /// <summary>
        /// Returns whether the indexed gemstone is currently in the random
        /// drop table. Combat can use this without knowing the UI layout.
        /// </summary>
        public bool IsGemstoneUnlocked(int gemstoneIndex)
        {
            TraitGemstoneDefinition gemstone = GetGemstoneDefinition(gemstoneIndex);
            return IsGemstoneUnlocked(gemstone);
        }

        /// <summary>
        /// Returns the current relative drop weight. A weight is normalized
        /// against all unlocked gemstones, so adding a discovery upgrade for
        /// one type raises its actual chance while keeping the table at 100%.
        /// </summary>
        public int GetGemstoneDropWeight(int gemstoneIndex)
        {
            TraitGemstoneDefinition gemstone = GetGemstoneDefinition(gemstoneIndex);
            if (!IsGemstoneUnlocked(gemstone))
            {
                return 0;
            }

            int upgradeCount = CountGemstoneRateUpgrades(gemstone);
            return gemstone.GetDropWeight(upgradeCount);
        }

        public float GetGemstoneDropChance(int gemstoneIndex)
        {
            int weight = GetGemstoneDropWeight(gemstoneIndex);
            if (weight <= 0)
            {
                return 0f;
            }

            int totalWeight = GetTotalGemstoneDropWeight();
            return totalWeight <= 0
                ? 0f
                : (float)weight / totalWeight * 100f;
        }

        /// <summary>
        /// Rolls one unlocked gemstone from a caller-provided normalized
        /// value. Supplying the random value from Combat keeps random state in
        /// one owner and makes deterministic run seeds possible.
        /// </summary>
        public bool TryRollGemstoneDrop(float normalizedRoll, out int gemstoneIndex)
        {
            gemstoneIndex = -1;
            int totalWeight = GetTotalGemstoneDropWeight();
            if (totalWeight <= 0)
            {
                return false;
            }

            // A malformed caller value should not create a NaN cursor that
            // silently falls through the weighted loop. Treat NaN as the
            // first bucket; infinities are handled by Clamp01 below.
            float safeRoll = float.IsNaN(normalizedRoll) ? 0f : normalizedRoll;
            float clampedRoll = Mathf.Clamp01(safeRoll);
            if (clampedRoll >= 1f)
            {
                clampedRoll = 0.99999994f;
            }

            float cursor = clampedRoll * totalWeight;
            IReadOnlyList<TraitGemstoneDefinition> definitions = _activeCatalog == null
                ? null
                : _activeCatalog.Gemstones;
            if (definitions == null)
            {
                return false;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                int weight = GetGemstoneDropWeight(i);
                if (weight <= 0)
                {
                    continue;
                }

                if (cursor < weight)
                {
                    gemstoneIndex = i;
                    return true;
                }

                cursor -= weight;
            }

            // Floating point rounding can leave a tiny remainder. Returning
            // the final weighted entry keeps the method total and avoids a
            // silent no-drop at the upper edge of the range.
            for (int i = definitions.Count - 1; i >= 0; i--)
            {
                if (GetGemstoneDropWeight(i) > 0)
                {
                    gemstoneIndex = i;
                    return true;
                }
            }

            return false;
        }

        private TraitGemstoneDefinition GetGemstoneDefinition(int gemstoneIndex)
        {
            IReadOnlyList<TraitGemstoneDefinition> definitions = _activeCatalog == null
                ? null
                : _activeCatalog.Gemstones;
            if (definitions == null || gemstoneIndex < 0 ||
                gemstoneIndex >= definitions.Count)
            {
                return null;
            }

            return definitions[gemstoneIndex];
        }

        private TraitGemstoneDefinition GetGemstoneDefinitionById(string gemstoneId)
        {
            IReadOnlyList<TraitGemstoneDefinition> definitions = _activeCatalog == null
                ? null
                : _activeCatalog.Gemstones;
            if (definitions == null || string.IsNullOrEmpty(gemstoneId))
            {
                return null;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                TraitGemstoneDefinition definition = definitions[i];
                if (definition != null && definition.Id == gemstoneId)
                {
                    return definition;
                }
            }

            return null;
        }

        private int GetGemstoneIndexById(string gemstoneId)
        {
            IReadOnlyList<TraitGemstoneDefinition> definitions = _activeCatalog == null
                ? null
                : _activeCatalog.Gemstones;
            if (definitions == null || string.IsNullOrEmpty(gemstoneId))
            {
                return 0;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                TraitGemstoneDefinition definition = definitions[i];
                if (definition != null && definition.Id == gemstoneId)
                {
                    return i;
                }
            }

            return 0;
        }

        private int GetTotalGemstoneDropWeight()
        {
            IReadOnlyList<TraitGemstoneDefinition> definitions = _activeCatalog == null
                ? null
                : _activeCatalog.Gemstones;
            if (definitions == null)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < definitions.Count; i++)
            {
                total = AddClamped(total, GetGemstoneDropWeight(i));
            }

            return total;
        }

        private int CountGemstoneRateUpgrades(TraitGemstoneDefinition gemstone)
        {
            if (gemstone == null)
            {
                return 0;
            }

            string prefix = gemstone.DropRateNodePrefix;
            if (string.IsNullOrEmpty(prefix))
            {
                return 0;
            }

            return CountPurchasedPrefix(prefix);
        }

        private int CountPurchasedPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return 0;
            }

            int count = 0;
            foreach (string id in _purchased)
            {
                if (id.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static int AddClamped(int current, int value)
        {
            if (value <= 0)
            {
                return current;
            }

            if (current > int.MaxValue - value)
            {
                return int.MaxValue;
            }

            return current + value;
        }

        private static string FormatGemstoneChance(float chance)
        {
            if (chance <= 0f)
            {
                return "0%";
            }

            if (chance >= 99.95f)
            {
                return "100%";
            }

            return chance.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        private void BuildSummaryPanel()
        {
            RectTransform panel = CreatePanel(
                "SummaryPanel",
                _runtimeRoot,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-28f, -132f),
                new Vector2(640f, 100f),
                null,
                new Color(0.015f, 0.045f, 0.12f, 0.88f),
                new Vector2(1f, 1f));

            string[] labels = { "공격력", "반경", "클릭", "치명타", "보스 피해" };
            for (int i = 0; i < labels.Length; i++)
            {
                RectTransform card = CreateRect(
                    labels[i],
                    panel,
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(16f + i * 126f, 0f),
                    new Vector2(116f, 100f),
                    new Vector2(0f, 0.5f));
                CreateText(
                    CreateRect("Label", card, Vector2.zero, Vector2.one, new Vector2(0f, 16f), new Vector2(-8f, -42f), new Vector2(0.5f, 0.5f)),
                    labels[i],
                    13,
                    TextAnchor.MiddleCenter,
                    mutedInkColor,
                    true);
                Text value = CreateText(
                    CreateRect("Value", card, Vector2.zero, Vector2.one, new Vector2(0f, -15f), new Vector2(-8f, -36f), new Vector2(0.5f, 0.5f)),
                    "-",
                    23,
                    TextAnchor.MiddleCenter,
                    inkColor,
                    true);
                _summaryValues.Add(value);
            }
        }

        private void BuildCategoryPanel()
        {
            // Categories remain data-owned while BuildNodes renders one
            // horizontal chain per category.
        }

        private void BuildNodePanel()
        {
            RectTransform panel = CreatePanel(
                "NodePanel",
                _runtimeRoot,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(-170f, -51f),
                new Vector2(-396f, -402f),
                null,
                new Color(0.012f, 0.038f, 0.10f, 0.48f),
                new Vector2(0.5f, 0.5f));

            RectTransform heading = CreateRect(
                "NodeHeading",
                panel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -26f),
                new Vector2(-32f, 42f),
                new Vector2(0.5f, 1f));
            _nodeHeading = CreateText(
                heading,
                "전체 목록",
                16,
                TextAnchor.MiddleLeft,
                mutedInkColor,
                true);

            RectTransform viewport = CreateRect(
                "NodeViewport",
                panel,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, -24f),
                new Vector2(-40f, -72f),
                new Vector2(0.5f, 0.5f));
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;

            _nodeContent = CreateRect(
                "NodeContent",
                viewport,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                Vector2.zero,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f));
            VerticalLayoutGroup layout = _nodeContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = Mathf.Max(6f, nodeGridSpacing.y);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            // Leave a clear lane for the vertical gemstone inventory panel.
            // The ScrollRect still exposes the full chain when a custom
            // catalog has wider rows.
            layout.padding = new RectOffset(340, 14, 12, 12);
            ContentSizeFitter fitter = _nodeContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _nodeContent;
            // Monster uses one long chain, so horizontal scrolling is enabled
            // for every tab. Rows that fit the viewport simply do not move.
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 55f;

            _nodeGroup = viewport.gameObject.AddComponent<CanvasGroup>();
        }

        private void BuildDetailPanel()
        {
            RectTransform panel = CreatePanel(
                "DetailPanel",
                _runtimeRoot,
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(-28f, -51f),
                new Vector2(320f, -402f),
                null,
                new Color(0.015f, 0.045f, 0.12f, 0.88f),
                new Vector2(1f, 0.5f));

            RectTransform heading = CreateRect(
                "DetailHeading",
                panel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -28f),
                new Vector2(-36f, 44f),
                new Vector2(0.5f, 1f));
            CreateText(heading, "선택한 항목", 15, TextAnchor.MiddleLeft, mutedInkColor, true);

            _selectedTitle = CreateText(
                CreateRect("SelectedTitle", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -88f), new Vector2(-36f, 54f), new Vector2(0.5f, 1f)),
                "Trait",
                25,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            _selectedValue = CreateText(
                CreateRect("SelectedValue", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -132f), new Vector2(-36f, 32f), new Vector2(0.5f, 1f)),
                "",
                18,
                TextAnchor.MiddleLeft,
                new Color(0.90f, 0.50f, 0.15f, 1f),
                true);
            _selectedDescription = CreateText(
                CreateRect("SelectedDescription", panel, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 42f), new Vector2(-36f, 120f), new Vector2(0.5f, 0.5f)),
                "",
                16,
                TextAnchor.UpperLeft,
                mutedInkColor,
                false);
            _selectedDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            _selectedDescription.verticalOverflow = VerticalWrapMode.Overflow;

            _selectedCost = CreateText(
                CreateRect("SelectedCost", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 120f), new Vector2(-36f, 32f), new Vector2(0.5f, 0f)),
                "Cost",
                16,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            _selectedRequirement = CreateText(
                CreateRect("SelectedRequirement", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 86f), new Vector2(-36f, 28f), new Vector2(0.5f, 0f)),
                "",
                14,
                TextAnchor.MiddleLeft,
                mutedInkColor,
                false);

            _upgradeButton = CreateButton(
                "UpgradeButton",
                panel,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 28f),
                new Vector2(-36f, 54f),
                null,
                new Color(0.96f, 0.64f, 0.20f, 0.98f),
                "강화",
                19,
                inkColor);
            _upgradeButtonImage = _upgradeButton.GetComponent<Image>();
            _upgradeButton.onClick.AddListener(UpgradeSelected);

            _statusMessage = CreateText(
                CreateRect("StatusMessage", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 3f), new Vector2(-36f, 22f), new Vector2(0.5f, 0f)),
                "",
                12,
                TextAnchor.MiddleCenter,
                mutedInkColor,
                false);
        }

        private void BuildFooter()
        {
            Button back = CreateButton(
                "BackButton",
                _runtimeRoot,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(34f, 30f),
                new Vector2(170f, 52f),
                null,
                new Color(0.04f, 0.12f, 0.24f, 0.96f),
                "뒤로",
                17,
                inkColor);
            AddButtonIcon(back.transform as RectTransform, backIcon, new Vector2(28f, 0f));
            back.onClick.AddListener(RequestBack);

            Button reset = CreateButton(
                "ResetButton",
                _runtimeRoot,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-220f, 30f),
                new Vector2(170f, 52f),
                null,
                new Color(0.04f, 0.12f, 0.24f, 0.96f),
                "초기화",
                17,
                inkColor);
            reset.onClick.AddListener(ResetPreview);
        }

        private void CreateTabButton(
            RectTransform parent,
            string objectName,
            string label,
            TraitTab tab)
        {
            Button button = CreateButton(
                objectName,
                parent,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(154f, 62f),
                tabSprite,
                new Color(1f, 1f, 1f, 0.95f),
                label,
                19,
                inkColor);
            button.onClick.AddListener(() => ShowTab(tab));
        }

        private void ShowTab(TraitTab tab, bool animate = true)
        {
            _activeTab = tab;
            _selectedNodeId = null;
            BuildCategories();
            BuildNodes(animate);
            RefreshSummary();
            RefreshTabButtons();
        }

        private void BuildCategories()
        {
            // Category rows are rebuilt with the active tab in BuildNodes. This
            // hook remains so a future filter can be added without changing
            // the screen lifecycle.
        }

        private void AddButtonIcon(
            RectTransform button,
            Sprite icon,
            Vector2 anchoredPosition)
        {
            if (button == null || icon == null)
            {
                return;
            }

            RectTransform iconRect = CreateImageRect(
                "Icon",
                button,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                anchoredPosition,
                new Vector2(28f, 28f),
                icon,
                Color.white,
                new Vector2(0.5f, 0.5f));
            iconRect.GetComponent<Image>().preserveAspect = true;

            Text[] texts = button.GetComponentsInChildren<Text>();
            foreach (Text text in texts)
            {
                if (text.gameObject.name == "Label")
                {
                    RectTransform label = text.rectTransform;
                    label.offsetMin = new Vector2(42f, label.offsetMin.y);
                    label.offsetMax = new Vector2(-10f, label.offsetMax.y);
                    break;
                }
            }
        }

        private void BuildNodes(bool animate)
        {
            if (_nodeContent == null)
            {
                return;
            }

            DestroyChildren(_nodeContent);
            _selectedNodeId = null;

            IReadOnlyList<TraitCategoryDefinition> categories = GetCategories(_activeTab);
            int nodeCount = CountNodes(categories);

            if (_nodeHeading != null)
            {
                _nodeHeading.text = GetTabTitle(_activeTab) + " · " + nodeCount + "개";
            }

            if (nodeCount == 0)
            {
                RefreshDetail(null);
                return;
            }

            TraitNodeDefinition firstNode = null;
            int flatIndex = 0;
            int categoryIndex = 0;
            if (categories != null)
            {
                foreach (TraitCategoryDefinition category in categories)
                {
                    if (category == null || category.Nodes == null)
                    {
                        continue;
                    }

                    List<TraitNodeDefinition> nodes = new List<TraitNodeDefinition>();
                    foreach (TraitNodeDefinition node in category.Nodes)
                    {
                        if (node != null)
                        {
                            nodes.Add(node);
                        }
                    }

                    if (nodes.Count == 0)
                    {
                        continue;
                    }

                    CreateCategoryRow(
                        _nodeContent,
                        category,
                        categoryIndex,
                        nodes,
                        ref flatIndex);
                    firstNode = firstNode ?? nodes[0];
                    categoryIndex++;
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_nodeContent);
            Canvas.ForceUpdateCanvases();
            ScrollRect scroll = _nodeContent.GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                scroll.horizontalNormalizedPosition = 0f;
                scroll.verticalNormalizedPosition = 1f;
            }

            RefreshDetail(firstNode);

            if (animate)
            {
                PlayNodeAnimation();
            }
        }

        private int CountNodes(IReadOnlyList<TraitCategoryDefinition> categories)
        {
            if (categories == null)
            {
                return 0;
            }

            int count = 0;
            foreach (TraitCategoryDefinition category in categories)
            {
                if (category == null || category.Nodes == null)
                {
                    continue;
                }

                foreach (TraitNodeDefinition node in category.Nodes)
                {
                    if (node != null)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private RectTransform CreateCategoryRow(
            Transform parent,
            TraitCategoryDefinition category,
            int categoryIndex,
            List<TraitNodeDefinition> nodes,
            ref int flatIndex)
        {
            float nodeWidth = GetSafeNodeCellSize().x;
            float chainWidth = nodeWidth * nodes.Count +
                GetSafeConnectorWidth() * Mathf.Max(0, nodes.Count - 1);
            float rowWidth = chainWidth + 28f;
            // The row padding is included here so six stat rows fit in the
            // default viewport without clipping. Skill and monster lists keep
            // their ScrollRect when their data exceeds the available height.
            float rowHeight = GetSafeCategoryHeaderHeight() +
                GetSafeNodeCellSize().y + 18f;

            RectTransform row = CreateRect(
                "CategoryRow_" + (category.Id ?? categoryIndex.ToString()),
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                Vector2.zero,
                new Vector2(rowWidth, rowHeight),
                new Vector2(0f, 1f));
            Image rowImage = row.gameObject.AddComponent<Image>();
            // Rows are flat translucent strips. The square node sprite carries
            // the only frame, so no grey category/card border is introduced.
            rowImage.sprite = null;
            rowImage.color = new Color(0.015f, 0.04f, 0.10f, 0.06f);
            rowImage.raycastTarget = false;

            LayoutElement rowElement = row.gameObject.AddComponent<LayoutElement>();
            rowElement.preferredWidth = rowWidth;
            rowElement.minWidth = rowWidth;
            rowElement.preferredHeight = rowHeight;
            rowElement.minHeight = rowHeight;

            RectTransform header = CreateRect(
                "CategoryHeader",
                row,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(14f, -8f),
                new Vector2(rowWidth - 28f, GetSafeCategoryHeaderHeight()),
                new Vector2(0f, 1f));
            LayoutElement headerElement = header.gameObject.AddComponent<LayoutElement>();
            headerElement.preferredHeight = GetSafeCategoryHeaderHeight();
            headerElement.minHeight = GetSafeCategoryHeaderHeight();

            Sprite headerIcon = GetCategoryHeaderIcon(_activeTab, category, categoryIndex);
            if (headerIcon != null)
            {
                RectTransform icon = CreateImageRect(
                    "CategoryIcon",
                    header,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(4f, 0f),
                    new Vector2(24f, 24f),
                    headerIcon,
                    Color.white,
                    new Vector2(0.5f, 0.5f));
                icon.GetComponent<Image>().preserveAspect = true;
            }

            RectTransform headerLabel = CreateRect(
                "Label",
                header,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Vector2(0.5f, 0.5f));
            Text headerText = CreateText(
                headerLabel,
                BuildCategoryHeaderText(category, nodes.Count),
                16,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            headerText.rectTransform.offsetMin = new Vector2(headerIcon == null ? 4f : 36f, 0f);
            headerText.rectTransform.offsetMax = new Vector2(-4f, 0f);

            RectTransform chain = CreateRect(
                "Chain",
                row,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(14f, -8f - GetSafeCategoryHeaderHeight() - 2f),
                new Vector2(chainWidth, GetSafeNodeCellSize().y),
                new Vector2(0f, 1f));
            LayoutElement chainElement = chain.gameObject.AddComponent<LayoutElement>();
            chainElement.preferredWidth = chainWidth;
            chainElement.minWidth = chainWidth;
            chainElement.preferredHeight = GetSafeNodeCellSize().y;
            chainElement.minHeight = GetSafeNodeCellSize().y;

            HorizontalLayoutGroup chainLayout = chain.gameObject.AddComponent<HorizontalLayoutGroup>();
            chainLayout.spacing = 0f;
            chainLayout.childAlignment = TextAnchor.MiddleLeft;
            chainLayout.childControlWidth = false;
            chainLayout.childControlHeight = false;
            chainLayout.childForceExpandWidth = false;
            chainLayout.childForceExpandHeight = false;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (i > 0)
                {
                    CreateConnector(chain, category.Accent);
                }

                TraitNodeDefinition node = nodes[i];
                bool purchased = IsPurchased(node);
                bool lockedByBoss = node.RequiredBossTier > unlockedBossTier;
                bool lockedByPrerequisite = !IsPrerequisiteMet(node);
                bool lockedByRequirement = lockedByBoss || lockedByPrerequisite;
                bool available = !purchased && !lockedByRequirement;
                Button button = CreateNodeButton(
                    chain,
                    category,
                    node,
                    categoryIndex,
                    flatIndex,
                    purchased,
                    lockedByRequirement,
                    available);
                TraitNodeDefinition capturedNode = node;
                button.onClick.AddListener(() => SelectNode(capturedNode));
                flatIndex++;
            }

            return row;
        }

        private string BuildCategoryHeaderText(
            TraitCategoryDefinition category,
            int nodeCount)
        {
            string title = category == null ? "목록" : category.Title;
            if (_activeTab == TraitTab.Monster)
            {
                return title + "  ·  순서 연결 " + nodeCount + "종";
            }

            if (_activeTab == TraitTab.Loot)
            {
                return title + "  ·  확률 획득 " + nodeCount + "종";
            }

            if (_activeTab == TraitTab.Pet)
            {
                return title + "  ·  자동 공격 강화 " + nodeCount + "단계";
            }

            return title + "  ·  " + nodeCount + "단계";
        }

        private RectTransform CreateConnector(Transform parent, Color accent)
        {
            RectTransform connector = CreateRect(
                "Connector",
                parent,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(GetSafeConnectorWidth(), GetSafeConnectorThickness()),
                new Vector2(0.5f, 0.5f));
            Image image = connector.gameObject.AddComponent<Image>();
            image.color = Color.Lerp(Color.white, accent, 0.14f);
            image.raycastTarget = false;

            LayoutElement element = connector.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = GetSafeConnectorWidth();
            element.minWidth = GetSafeConnectorWidth();
            element.preferredHeight = GetSafeConnectorThickness();
            element.minHeight = GetSafeConnectorThickness();
            return connector;
        }

        private Button CreateNodeButton(
            RectTransform parent,
            TraitCategoryDefinition category,
            TraitNodeDefinition node,
            int categoryIndex,
            int nodeIndex,
            bool purchased,
            bool locked,
            bool available)
        {
            // The authored demo node sprites are rounded card samples. The
            // trait tree uses a single rectangular fill plus one shared
            // square accent frame so the icon itself never gets a separate
            // card or a grey circular border.
            Sprite sprite = null;
            Color color = purchased
                ? PurchasedColor
                : available
                    ? AvailableColor
                    : LockedColor;
            Color nodeFill = Color.Lerp(
                new Color(0.008f, 0.025f, 0.07f, 0.96f),
                color,
                purchased ? 0.26f : available ? 0.12f : 0.04f);
            Button button = CreateButton(
                "Node_" + node.Id,
                parent,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                GetSafeNodeCellSize(),
                sprite,
                nodeFill,
                string.Empty,
                15,
                inkColor);
            LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = GetSafeNodeCellSize().x;
            element.preferredHeight = GetSafeNodeCellSize().y;

            // Draw four crisp Image bars around the rectangular button. Some
            // sample frame sprites contain an opaque rounded centre; using
            // bars keeps the icon free of a second card and makes every node
            // a true square at any canvas scale.
            CreateSquareNodeFrame(
                button.transform as RectTransform,
                GetNodeFrameColor(category, purchased, locked, available),
                3f);

            Shadow nodeShadow = button.GetComponent<Shadow>();
            if (nodeShadow != null)
            {
                // The black sample shadow reads as a grey card border at
                // small resolutions. Keep the interaction feedback on the
                // button scale, but remove the extra mesh around each node.
                nodeShadow.enabled = false;
            }

            Sprite icon = GetNodeIcon(
                _activeTab,
                category,
                node,
                categoryIndex,
                nodeIndex);
            if (icon != null)
            {
                RectTransform iconRect = CreateImageRect(
                    "Icon",
                    button.transform as RectTransform,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -23f),
                    new Vector2(30f, 30f),
                    icon,
                    purchased ? Color.white : new Color(1f, 1f, 1f, locked ? 0.48f : 0.90f),
                    new Vector2(0.5f, 0.5f));
                iconRect.GetComponent<Image>().preserveAspect = true;
            }

            CreateText(
                CreateRect("Title", button.transform as RectTransform, Vector2.zero, Vector2.one, new Vector2(0f, -4f), new Vector2(-12f, -56f), new Vector2(0.5f, 0.5f)),
                node.Title,
                11,
                TextAnchor.MiddleCenter,
                locked ? new Color(0.63f, 0.72f, 0.84f, 1f) : inkColor,
                true);
            CreateText(
                CreateRect("Value", button.transform as RectTransform, Vector2.zero, Vector2.one, new Vector2(0f, -29f), new Vector2(-12f, -77f), new Vector2(0.5f, 0.5f)),
                node.ValueLabel,
                11,
                TextAnchor.MiddleCenter,
                locked ? new Color(0.50f, 0.62f, 0.76f, 1f) : category.Accent,
                true);

            if (purchased && checkIcon != null)
            {
                RectTransform check = CreateImageRect(
                    "Check",
                    button.transform as RectTransform,
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(-14f, -14f),
                    new Vector2(20f, 20f),
                    checkIcon,
                    Color.white,
                    new Vector2(0.5f, 0.5f));
                check.GetComponent<Image>().preserveAspect = true;
            }
            else if (locked && lockIcon != null)
            {
                RectTransform lockRect = CreateImageRect(
                    "Lock",
                    button.transform as RectTransform,
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(-14f, -14f),
                    new Vector2(20f, 20f),
                    lockIcon,
                    Color.white,
                    new Vector2(0.5f, 0.5f));
                lockRect.GetComponent<Image>().preserveAspect = true;
            }

            return button;
        }

        private void CreateSquareNodeFrame(
            RectTransform parent,
            Color color,
            float thickness)
        {
            if (parent == null)
            {
                return;
            }

            float safeThickness = Mathf.Max(1f, thickness);
            CreateImageRect(
                "FrameTop",
                parent,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -safeThickness * 0.5f),
                new Vector2(0f, safeThickness),
                null,
                color,
                new Vector2(0.5f, 0.5f));
            CreateImageRect(
                "FrameBottom",
                parent,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, safeThickness * 0.5f),
                new Vector2(0f, safeThickness),
                null,
                color,
                new Vector2(0.5f, 0.5f));
            CreateImageRect(
                "FrameLeft",
                parent,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(safeThickness * 0.5f, 0f),
                new Vector2(safeThickness, 0f),
                null,
                color,
                new Vector2(0.5f, 0.5f));
            CreateImageRect(
                "FrameRight",
                parent,
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(-safeThickness * 0.5f, 0f),
                new Vector2(safeThickness, 0f),
                null,
                color,
                new Vector2(0.5f, 0.5f));
        }

        private static Color GetNodeFrameColor(
            TraitCategoryDefinition category,
            bool purchased,
            bool locked,
            bool available)
        {
            if (purchased)
            {
                return new Color(0.28f, 0.94f, 0.60f, 0.96f);
            }

            if (locked)
            {
                return new Color(0.20f, 0.42f, 0.68f, 0.92f);
            }

            if (available)
            {
                Color accent = category == null
                    ? AvailableColor
                    : category.Accent;
                return Color.Lerp(accent, Color.white, 0.24f);
            }

            return new Color(0.30f, 0.52f, 0.76f, 0.86f);
        }

        private void SelectNode(TraitNodeDefinition node)
        {
            _selectedNodeId = node == null ? null : node.Id;
            RefreshDetail(node);
        }

        private string GetNodeCostText(TraitNodeDefinition node)
        {
            if (node == null || node.AcquisitionOnly)
            {
                return node != null && node.AcquisitionOnly
                    ? "비용  랜덤 드롭"
                    : string.Empty;
            }

            if (node.Cost <= 0)
            {
                return "비용 없음";
            }

            TraitGemstoneDefinition gemstone = GetGemstoneDefinitionById(node.CostGemstoneId);
            string currencyTitle = IsGemstoneUnlocked(gemstone)
                ? gemstone.Title
                : "해금된 젬스톤";
            return "비용  " + node.Cost + "  " + currencyTitle;
        }

        private long GetNodeBalance(TraitNodeDefinition node)
        {
            if (node == null)
            {
                return 0L;
            }

            int index = GetGemstoneIndexById(node.CostGemstoneId);
            return GetGemstoneBalance(index);
        }

        private void SpendNodeCost(TraitNodeDefinition node)
        {
            if (node == null || node.Cost <= 0)
            {
                return;
            }

            int index = GetGemstoneIndexById(node.CostGemstoneId);
            if (index == 0)
            {
                _garnetBalance = Math.Max(0L, _garnetBalance - node.Cost);
                EnsureGemstoneBalanceCapacity();
                _gemstoneBalances[0] = _garnetBalance;
                return;
            }

            EnsureGemstoneBalanceCapacity();
            if (index >= 0 && index < _gemstoneBalances.Length)
            {
                _gemstoneBalances[index] = Math.Max(
                    0L,
                    _gemstoneBalances[index] - node.Cost);
            }
        }

        private void RefundNodeCost(TraitNodeDefinition node, int amount)
        {
            if (node == null || amount <= 0)
            {
                return;
            }

            int index = GetGemstoneIndexById(node.CostGemstoneId);
            if (index == 0)
            {
                _garnetBalance = Math.Max(0L, _garnetBalance + amount);
                EnsureGemstoneBalanceCapacity();
                _gemstoneBalances[0] = _garnetBalance;
                return;
            }

            EnsureGemstoneBalanceCapacity();
            if (index >= 0 && index < _gemstoneBalances.Length)
            {
                _gemstoneBalances[index] = Math.Max(
                    0L,
                    _gemstoneBalances[index] + amount);
            }
        }

        private void RefreshDetail(TraitNodeDefinition node)
        {
            if (_selectedTitle == null)
            {
                return;
            }

            if (node == null)
            {
                _selectedTitle.text = "선택한 항목 없음";
                _selectedValue.text = string.Empty;
                _selectedDescription.text = "표시할 항목이 없습니다.";
                _selectedCost.text = string.Empty;
                _selectedRequirement.text = string.Empty;
                _upgradeButton.interactable = false;
                return;
            }

            _selectedTitle.text = node.Title;
            _selectedValue.text = node.ValueLabel;
            _selectedDescription.text = BuildNodeDescription(node);
            _selectedCost.text = GetNodeCostText(node);
            _selectedRequirement.text = GetNodeRequirementText(node);

            bool purchased = IsPurchased(node);
            bool lockedByBoss = node.RequiredBossTier > unlockedBossTier;
            bool lockedByPrerequisite = !IsPrerequisiteMet(node);
            bool currencyUnlocked = node.Cost <= 0 ||
                IsGemstoneUnlocked(GetGemstoneDefinitionById(node.CostGemstoneId));
            bool hasCurrency = currencyUnlocked && GetNodeBalance(node) >= node.Cost;
            bool canBuy = !purchased && !lockedByBoss && !lockedByPrerequisite &&
                !node.AcquisitionOnly && hasCurrency;
            _upgradeButton.interactable = canBuy;
            if (_upgradeButtonImage != null)
            {
                _upgradeButtonImage.color = canBuy ? AvailableColor : LockedColor;
            }

            if (purchased)
            {
                _statusMessage.text = "이미 해금됨";
            }
            else if (lockedByPrerequisite)
            {
                _statusMessage.text = "선행 특성을 먼저 해금하세요";
            }
            else if (lockedByBoss)
            {
                _statusMessage.text = "필요한 보스를 먼저 격파하세요";
            }
            else if (node.AcquisitionOnly)
            {
                _statusMessage.text = "전투 중 확률로 획득하는 항목입니다";
            }
            else if (!hasCurrency)
            {
                _statusMessage.text = "젬스톤이 부족합니다";
            }
            else
            {
                _statusMessage.text = "강화할 수 있습니다";
            }
        }

        private void UpgradeSelected()
        {
            TraitNodeDefinition node = FindNode(_selectedNodeId);
            bool currencyUnlocked = node == null || node.Cost <= 0 ||
                IsGemstoneUnlocked(GetGemstoneDefinitionById(node.CostGemstoneId));
            if (node == null || IsPurchased(node) ||
                node.RequiredBossTier > unlockedBossTier ||
                !IsPrerequisiteMet(node) || node.AcquisitionOnly ||
                !currencyUnlocked || GetNodeBalance(node) < node.Cost)
            {
                RefreshDetail(node);
                return;
            }

            SpendNodeCost(node);
            _purchased.Add(node.Id);
            _spentByNode[node.Id] = node.Cost;
            _spentCurrencyByNode[node.Id] = node.CostGemstoneId;
            _statusMessage.text = node.Cost <= 0 ? "해금되었습니다" : "강화되었습니다";

            BuildNodes(false);
            _selectedNodeId = node.Id;
            RefreshSummary();
            RefreshGemstonePanel();
            RefreshDetail(node);
        }

        private void ResetPreview()
        {
            foreach (KeyValuePair<string, int> spent in _spentByNode)
            {
                TraitNodeDefinition node = FindNode(spent.Key);
                RefundNodeCost(node, spent.Value);
            }

            InitializePurchasedState();
            _statusMessage.text = "미리보기를 초기화했습니다";
            BuildCategories();
            BuildNodes(true);
            RefreshSummary();
        }

        private void RefreshAll()
        {
            if (!_isBuilt)
            {
                return;
            }

            BuildCategories();
            BuildNodes(false);
            RefreshSummary();
            RefreshGemstonePanel();
        }

        private void RefreshTabButtons()
        {
            if (_runtimeRoot == null)
            {
                return;
            }

            string[] names = { "STATTab", "SKILLTab", "MONSTERTab", "LOOTTab", "PETTab" };
            for (int i = 0; i < names.Length; i++)
            {
                Transform child = _runtimeRoot.Find("Tabs/" + names[i]);
                if (child == null)
                {
                    continue;
                }

                Image image = child.GetComponent<Image>();
                if (image == null)
                {
                    continue;
                }

                bool selected = (int)_activeTab == i;
                image.sprite = selected && tabFocusSprite != null ? tabFocusSprite : tabSprite;
                image.color = selected
                    ? new Color(1f, 0.84f, 0.48f, 1f)
                    : new Color(1f, 1f, 1f, 0.90f);
            }
        }

        private void RefreshSummary()
        {
            if (_garnetValue != null)
            {
                _garnetValue.text = _garnetBalance.ToString();
            }

            if (_summaryValues.Count < 5)
            {
                return;
            }

            int attack = CountPurchased("stat.attack");
            int radius = CountPurchased("stat.radius");
            int clicks = CountPurchased("stat.multiClick");
            int critical = CountPurchased("stat.critical");
            int boss = CountPurchased("stat.boss");

            _summaryValues[0].text = GetAttackSummary(attack);
            _summaryValues[1].text = GetRadiusSummary(radius);
            _summaryValues[2].text = GetClickSummary(clicks);
            _summaryValues[3].text = GetCriticalSummary(critical);
            _summaryValues[4].text = GetBossSummary(boss);
        }

        private string GetAttackSummary(int level)
        {
            int[] values = { 1, 3, 6, 10, 16, 24, 35, 50, 70, 100 };
            return values[Mathf.Clamp(level - 1, 0, values.Length - 1)].ToString();
        }

        private string GetRadiusSummary(int level)
        {
            string[] values =
            {
                "작은 원", "+1", "+2", "+3", "+4", "+5",
                "1/32", "1/16", "1/8", "1/4", "전체 화면"
            };
            return values[Mathf.Clamp(level - 1, 0, values.Length - 1)];
        }

        private string GetClickSummary(int level)
        {
            string[] values = { "1회", "2회", "3회", "4회", "무한" };
            return values[Mathf.Clamp(level - 1, 0, values.Length - 1)];
        }

        private string GetCriticalSummary(int level)
        {
            string[] values = { "0%", "5%", "10%", "20%", "30%", "50%", "75%", "100%" };
            return values[Mathf.Clamp(level - 1, 0, values.Length - 1)];
        }

        private string GetBossSummary(int level)
        {
            int[] values = { 0, 25, 60, 110, 180, 300 };
            return "+" + values[Mathf.Clamp(level, 0, values.Length - 1)] + "%";
        }

        private int CountPurchased(string categoryId)
        {
            IReadOnlyList<TraitCategoryDefinition> categories = GetCategories(TraitTab.Stat);
            if (categories == null)
            {
                return 0;
            }

            foreach (TraitCategoryDefinition category in categories)
            {
                if (category == null || category.Id != categoryId || category.Nodes == null)
                {
                    continue;
                }

                int count = 0;
                foreach (TraitNodeDefinition node in category.Nodes)
                {
                    if (IsPurchased(node))
                    {
                        count++;
                    }
                }

                return count;
            }

            return 0;
        }

        private TraitNodeDefinition FindNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return null;
            }

            TraitTab[] tabs =
            {
                TraitTab.Stat, TraitTab.Skill, TraitTab.Monster,
                TraitTab.Loot, TraitTab.Pet
            };
            foreach (TraitTab tab in tabs)
            {
                IReadOnlyList<TraitCategoryDefinition> categories = GetCategories(tab);
                if (categories == null)
                {
                    continue;
                }

                foreach (TraitCategoryDefinition category in categories)
                {
                    if (category == null || category.Nodes == null)
                    {
                        continue;
                    }

                    foreach (TraitNodeDefinition node in category.Nodes)
                    {
                        if (node != null && node.Id == nodeId)
                        {
                            return node;
                        }
                    }
                }
            }

            return null;
        }

        private bool IsPurchased(TraitNodeDefinition node)
        {
            return node != null && !string.IsNullOrEmpty(node.Id) && _purchased.Contains(node.Id);
        }

        private bool IsPrerequisiteMet(TraitNodeDefinition node)
        {
            return node == null || string.IsNullOrEmpty(node.PrerequisiteNodeId) ||
                _purchased.Contains(node.PrerequisiteNodeId);
        }

        private string GetNodeRequirementText(TraitNodeDefinition node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            if (!IsPrerequisiteMet(node))
            {
                TraitNodeDefinition prerequisite = FindNode(node.PrerequisiteNodeId);
                string title = prerequisite == null
                    ? node.PrerequisiteNodeId
                    : prerequisite.Title;
                return "먼저 " + title + " 해금 필요";
            }

            if (node.AcquisitionOnly)
            {
                return node.RequiredBossTier <= unlockedBossTier
                    ? "전투 중 확률로 획득"
                    : "보스 " + node.RequiredBossTier + "단계 해금 후 전투 중 확률로 획득";
            }

            return node.RequiredBossTier <= unlockedBossTier
                ? "현재 진행 단계에서 해금 가능"
                : "보스 " + node.RequiredBossTier + "단계 격파 후 해금";
        }

        private string BuildNodeDescription(TraitNodeDefinition node)
        {
            string description = node == null ? string.Empty : node.Description;
            if (node == null || _activeTab != TraitTab.Stat ||
                node.Id == null || node.Id.IndexOf("stat.gemstone.rate.",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                return description;
            }

            int gemstoneIndex = GetGemstoneIndexFromNodeId(node.Id);
            if (gemstoneIndex < 0)
            {
                return description;
            }

            return description + "\n현재 등장 확률 " +
                FormatGemstoneChance(GetGemstoneDropChance(gemstoneIndex));
        }

        private IReadOnlyList<TraitCategoryDefinition> GetCategories(TraitTab tab)
        {
            if (_activeCatalog == null)
            {
                return null;
            }

            switch (tab)
            {
                case TraitTab.Skill:
                    return _activeCatalog.SkillCategories;
                case TraitTab.Monster:
                    return _activeCatalog.MonsterCategories;
                case TraitTab.Loot:
                    return _activeCatalog.LootCategories;
                case TraitTab.Pet:
                    return _activeCatalog.PetCategories;
                default:
                    return _activeCatalog.StatCategories;
            }
        }

        private Sprite GetCategoryIcon(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId))
            {
                return null;
            }

            if (categoryId.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return attackIcon;
            }

            if (categoryId.IndexOf("radius", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return radiusIcon;
            }

            if (categoryId.IndexOf("click", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return clickIcon;
            }

            if (categoryId.IndexOf("critical", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return criticalIcon;
            }

            if (categoryId.IndexOf("gem", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return gemstoneIcon != null ? gemstoneIcon : gemIcon;
            }

            if (categoryId.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return bossIcon;
            }

            if (categoryId.IndexOf("skill", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return skillIcon != null ? skillIcon : attackIcon;
            }

            if (categoryId.IndexOf("monster", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return monsterIcon != null ? monsterIcon : bossIcon;
            }

            if (categoryId.IndexOf("loot", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return lootIcon != null ? lootIcon : gemstoneIcon;
            }

            if (categoryId.IndexOf("pet", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return petIcon != null ? petIcon : skillIcon;
            }

            return attackIcon;
        }

        private string GetTabTitle(TraitTab tab)
        {
            switch (tab)
            {
                case TraitTab.Skill:
                    return "스킬 목록";
                case TraitTab.Monster:
                    return "몬스터 도감";
                case TraitTab.Loot:
                    return "전리품 목록";
                case TraitTab.Pet:
                    return "펫 목록";
                default:
                    return "스탯 목록";
            }
        }

        private Vector2 GetSafeNodeCellSize()
        {
            return new Vector2(
                Mathf.Max(72f, nodeCellSize.x),
                Mathf.Max(72f, nodeCellSize.y));
        }

        private Vector2 GetSafeNodeGridSpacing()
        {
            return new Vector2(
                Mathf.Max(4f, nodeGridSpacing.x),
                Mathf.Max(4f, nodeGridSpacing.y));
        }

        private float GetSafeConnectorWidth()
        {
            return Mathf.Max(12f, connectorWidth);
        }

        private float GetSafeConnectorThickness()
        {
            return Mathf.Clamp(connectorThickness, 2f, 16f);
        }

        private float GetSafeCategoryHeaderHeight()
        {
            return Mathf.Max(24f, categoryHeaderHeight);
        }

        private Sprite GetCategoryHeaderIcon(
            TraitTab tab,
            TraitCategoryDefinition category,
            int categoryIndex)
        {
            if (category != null && category.Icon != null)
            {
                return category.Icon;
            }

            if (tab == TraitTab.Skill && skillIcons != null &&
                categoryIndex >= 0 && categoryIndex < skillIcons.Length &&
                skillIcons[categoryIndex] != null)
            {
                return skillIcons[categoryIndex];
            }

            if (tab == TraitTab.Monster && monsterIcons != null &&
                monsterIcons.Length > 0 && monsterIcons[0] != null)
            {
                return monsterIcons[0];
            }

            if (tab == TraitTab.Loot && lootIcons != null &&
                categoryIndex >= 0 && categoryIndex < lootIcons.Length &&
                lootIcons[categoryIndex] != null)
            {
                return lootIcons[categoryIndex];
            }

            if (tab == TraitTab.Pet && petIcons != null &&
                categoryIndex >= 0 && categoryIndex < petIcons.Length &&
                petIcons[categoryIndex] != null)
            {
                return petIcons[categoryIndex];
            }

            return category == null ? null : GetCategoryIcon(category.Id);
        }

        private Sprite GetNodeIcon(
            TraitTab tab,
            TraitCategoryDefinition category,
            TraitNodeDefinition node,
            int categoryIndex,
            int flatIndex)
        {
            if (tab == TraitTab.Skill && node != null)
            {
                // The first card in a row carries the skill-specific icon;
                // modifier cards reuse the matching stat icon so their effect
                // is clear even before bespoke skill art is available.
                if (category != null && string.Equals(
                    node.Id,
                    category.Id,
                    StringComparison.Ordinal))
                {
                    return GetCategoryHeaderIcon(tab, category, categoryIndex);
                }

                string nodeId = node.Id ?? string.Empty;
                if (nodeId.IndexOf(".damage", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return attackIcon;
                }

                if (nodeId.IndexOf(".radius", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return radiusIcon;
                }

                if (nodeId.IndexOf(".cooldown", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return timerIcon != null ? timerIcon : clickIcon;
                }

                return skillIcon != null ? skillIcon : attackIcon;
            }

            if (tab == TraitTab.Monster && monsterIcons != null)
            {
                int monsterIndex = GetMonsterIconIndex(node == null ? null : node.Id);
                if (monsterIndex < 0)
                {
                    monsterIndex = flatIndex;
                }

                if (monsterIndex >= 0 && monsterIndex < monsterIcons.Length &&
                    monsterIcons[monsterIndex] != null)
                {
                    return monsterIcons[monsterIndex];
                }
            }

            if (tab == TraitTab.Loot && lootIcons != null &&
                flatIndex >= 0 && flatIndex < lootIcons.Length &&
                lootIcons[flatIndex] != null)
            {
                return lootIcons[flatIndex];
            }

            if (tab == TraitTab.Pet)
            {
                if (petIcons != null && flatIndex >= 0 &&
                    flatIndex < petIcons.Length && petIcons[flatIndex] != null)
                {
                    return petIcons[flatIndex];
                }

                return petIcon != null ? petIcon : skillIcon;
            }

            if (tab == TraitTab.Stat && category != null &&
                string.Equals(category.Id, "stat.gemstone", StringComparison.Ordinal) &&
                node != null)
            {
                int gemstoneIndex = GetGemstoneIndexFromNodeId(node.Id);
                if (gemstoneIndex >= 0)
                {
                    return GetGemstoneIcon(gemstoneIndex);
                }

                // Collection behavior uses the sack icon, while type unlock
                // and discovery nodes use the actual color-matched gem above.
                return gemstoneIcon != null ? gemstoneIcon : gemIcon;
            }

            if (category != null && category.Icon != null)
            {
                return category.Icon;
            }

            return category == null ? null : GetCategoryIcon(category.Id);
        }

        private static int GetMonsterIconIndex(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) ||
                !nodeId.StartsWith("monster.", StringComparison.Ordinal))
            {
                return -1;
            }

            int separator = nodeId.IndexOf('.', "monster.".Length);
            string number = separator < 0
                ? nodeId.Substring("monster.".Length)
                : nodeId.Substring("monster.".Length, separator - "monster.".Length);
            int parsed;
            return int.TryParse(number, out parsed) ? parsed - 1 : -1;
        }

        private static int GetGemstoneIndexFromNodeId(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return -1;
            }

            if (nodeId.IndexOf(".topaz", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 1;
            }

            if (nodeId.IndexOf(".amethyst", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 2;
            }

            if (nodeId.IndexOf(".sapphire", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 3;
            }

            if (nodeId.IndexOf(".diamond", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 4;
            }

            if (nodeId.IndexOf(".dragon", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 5;
            }

            if (nodeId.IndexOf(".garnet", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 0;
            }

            return -1;
        }

        private void HideAuthoredPlaceholders()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name != "TraitScreen_Runtime")
                {
                    child.gameObject.SetActive(false);
                }
            }

            Image rootImage = GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.color = backgroundColor;
                rootImage.raycastTarget = true;
            }
        }

        private void EnsureCanvasScale()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            Transform canvasTransform = canvas.transform;
            if (canvasTransform.localScale.sqrMagnitude < 0.0001f)
            {
                canvasTransform.localScale = Vector3.one;
            }

            // Dynamic UI text is rasterized by the Canvas. Pixel-perfect
            // overlay rendering prevents half-pixel placement from softening
            // the glyph edges when the editor or a windowed build is scaled.
            canvas.pixelPerfect = true;
        }

        private void PlayEnterAnimation()
        {
            if (_runtimeRoot == null || _runtimeGroup == null)
            {
                return;
            }

            if (_transition != null)
            {
                StopCoroutine(_transition);
            }

            _transition = StartCoroutine(AnimateRoot());
        }

        private IEnumerator AnimateRoot()
        {
            _runtimeGroup.alpha = 0f;
            _runtimeRoot.localScale = Vector3.one * 0.96f;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, transitionSeconds);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                _runtimeGroup.alpha = t;
                _runtimeRoot.localScale = Vector3.LerpUnclamped(
                    Vector3.one * 0.96f,
                    Vector3.one,
                    t);
                yield return null;
            }

            _runtimeGroup.alpha = 1f;
            _runtimeRoot.localScale = Vector3.one;
            _transition = null;
        }

        private void PlayNodeAnimation()
        {
            if (_nodeGroup == null)
            {
                return;
            }

            if (_nodeTransition != null)
            {
                StopCoroutine(_nodeTransition);
            }

            _nodeTransition = StartCoroutine(AnimateNodeGroup());
        }

        private IEnumerator AnimateNodeGroup()
        {
            _nodeGroup.alpha = 0f;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, transitionSeconds * 0.75f);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _nodeGroup.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }

            _nodeGroup.alpha = 1f;
            _nodeTransition = null;
        }

        private static void DestroyChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null)
                {
                    // Destroy is deferred until the end of the frame. Disable
                    // first so a rapid tab/category switch never lets the
                    // outgoing nodes participate in the next layout pass.
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }
        }

        private RectTransform CreateRect(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 pivot)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        private RectTransform CreateImageRect(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            Sprite sprite,
            Color color,
            Vector2 pivot)
        {
            RectTransform rect = CreateRect(
                objectName,
                parent,
                anchorMin,
                anchorMax,
                anchoredPosition,
                size,
                pivot);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null)
            {
                image.type = Image.Type.Simple;
            }

            return rect;
        }

        private RectTransform CreatePanel(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            Sprite sprite,
            Color color,
            Vector2 pivot)
        {
            RectTransform rect = CreateRect(
                objectName,
                parent,
                anchorMin,
                anchorMax,
                anchoredPosition,
                size,
                pivot);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null)
            {
                image.type = Image.Type.Sliced;
            }

            return rect;
        }

        private Text CreateText(
            RectTransform rect,
            string content,
            int fontSize,
            TextAnchor alignment,
            Color color,
            bool bold)
        {
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = content ?? string.Empty;
            // Use the serialized NanumGothic asset for Hangul. The builtin
            // fallback keeps custom catalogs usable when a scene has not yet
            // assigned a project font.
            text.font = uiFont != null
                ? uiFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.Max(
                12,
                Mathf.RoundToInt(fontSize * Mathf.Clamp(textScale, 1f, 1.5f)));
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = false;
            text.alignByGeometry = true;
            text.supportRichText = false;
            if (bold && text.font != null)
            {
                text.fontStyle = FontStyle.Bold;
            }

            return text;
        }

        private Button CreateButton(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            Sprite sprite,
            Color color,
            string label,
            int fontSize,
            Color textColor)
        {
            RectTransform rect = CreateRect(
                objectName,
                parent,
                anchorMin,
                anchorMax,
                anchoredPosition,
                size,
                new Vector2(0.5f, 0.5f));
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = true;
            if (sprite != null)
            {
                image.type = Image.Type.Sliced;
            }

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.58f);
            shadow.effectDistance = new Vector2(2f, -3f);
            shadow.useGraphicAlpha = true;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.20f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.10f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.10f, 0.16f, 0.26f, 0.68f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.gameObject.AddComponent<TraitUiButtonFeedback>();

            if (!string.IsNullOrEmpty(label))
            {
                RectTransform labelRect = CreateRect(
                    "Label",
                    rect,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    new Vector2(-20f, -10f),
                    new Vector2(0.5f, 0.5f));
                CreateText(labelRect, label, fontSize, TextAnchor.MiddleCenter, textColor, true);
            }

            return button;
        }
    }

    /// <summary>
    /// Lightweight pointer feedback shared by dynamically generated buttons.
    /// It uses unscaled time so the trait screen still animates while a run is
    /// paused.
    /// </summary>
    public sealed class TraitUiButtonFeedback : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField, Min(0f)] private float hoverScale = 1.035f;
        [SerializeField, Min(0f)] private float pressedScale = 0.96f;
        [SerializeField, Min(0f)] private float duration = 0.10f;

        private RectTransform _rect;
        private Coroutine _animation;

        private void Awake()
        {
            _rect = transform as RectTransform;
        }

        private void OnDisable()
        {
            if (_animation != null)
            {
                StopCoroutine(_animation);
                _animation = null;
            }

            if (_rect != null)
            {
                _rect.localScale = Vector3.one;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            AnimateTo(hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            AnimateTo(1f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            AnimateTo(pressedScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            AnimateTo(hoverScale);
        }

        private void AnimateTo(float scale)
        {
            if (_rect == null)
            {
                return;
            }

            if (_animation != null)
            {
                StopCoroutine(_animation);
            }

            _animation = StartCoroutine(ScaleTo(scale));
        }

        private IEnumerator ScaleTo(float scale)
        {
            Vector3 start = _rect.localScale;
            Vector3 end = Vector3.one * scale;
            float elapsed = 0f;
            float length = Mathf.Max(0.01f, duration);
            while (elapsed < length)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / length);
                _rect.localScale = Vector3.LerpUnclamped(start, end, t);
                yield return null;
            }

            _rect.localScale = end;
            _animation = null;
        }
    }
}
