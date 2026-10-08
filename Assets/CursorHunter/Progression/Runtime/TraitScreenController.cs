using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using CursorHunter.Contracts;
using CursorHunter.Data;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

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
        [SerializeField] private bool loadSavedProgress = true;
        [SerializeField] private bool testModeUnlockAll = false;
        [SerializeField] private bool testModeFreeUpgrades = false;
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
        [SerializeField] private Sprite petIcon;
        [SerializeField] private Sprite[] gemstoneIcons = new Sprite[0];
        [SerializeField] private Sprite[] skillIcons = new Sprite[0];
        [SerializeField] private Sprite[] monsterIcons = new Sprite[0];
        [SerializeField] private Sprite[] petIcons = new Sprite[0];
        [SerializeField] private Font uiFont;

        [Header("Layout")]
        [SerializeField, Range(0, 4)] private int initialTab;
        [SerializeField, Min(0f)] private float transitionSeconds = 0.22f;
        [SerializeField] private Vector2 nodeCellSize = new Vector2(106f, 106f);
        [SerializeField] private Vector2 nodeGridSpacing = new Vector2(8f, 8f);
        [SerializeField, Min(12f)] private float connectorWidth = 22f;
        [SerializeField, Range(2f, 16f)] private float connectorThickness = 3f;
        [SerializeField, Min(24f)] private float categoryHeaderHeight = 28f;
        [SerializeField, Range(2, 12)] private int maxNodesPerLine = 5;
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
        private ProgressionCombatSnapshot _activeRunSnapshot;
        private GameDataDocument _document;
        private string _summaryJson;
        private CursorCombatStatDefaultsSnapshot? _cursorCombatStatDefaults;

        private readonly HashSet<string> _purchased =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _spentByNode =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _spentCurrencyByNode =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private RectTransform _runtimeRoot;
        private RectTransform _nodeContent;
        private RectTransform _gemstoneList;
        private TextMeshProUGUI _gemstoneUnlockMessage;
        private CanvasGroup _runtimeGroup;
        private CanvasGroup _nodeGroup;
        private TextMeshProUGUI _nodeHeading;
        private TextMeshProUGUI _garnetValue;
        private TextMeshProUGUI _selectedTitle;
        private TextMeshProUGUI _selectedDescription;
        private TextMeshProUGUI _selectedValue;
        private TextMeshProUGUI _selectedCost;
        private TextMeshProUGUI _selectedRequirement;
        private TextMeshProUGUI _statusMessage;
        private Button _upgradeButton;
        private Image _upgradeButtonImage;
        private TextMeshProUGUI _upgradeButtonLabel;
        private Button _resetNodeButton;
        private Image _resetNodeButtonImage;
        private TextMeshProUGUI _resetNodeButtonLabel;
        private GameObject _summaryOverlay;
        private RectTransform _summaryOverlayContent;
        private readonly List<TextMeshProUGUI> _summaryValues = new List<TextMeshProUGUI>();
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
            if (_activeCatalog == null)
            {
                _garnetBalance = startingGarnet;
                ResolveCatalog();
                InitializeGemstoneBalances();
                InitializePurchasedState();
                LoadSavedProgress();
            }
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
            CloseSummaryPopup();
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
            if (_summaryOverlay != null) Destroy(_summaryOverlay);
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
            RefreshAll();
            SaveProgressionState();
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


        public void ShowPetTab()
        {
            ShowTab(TraitTab.Skill);
        }

        /// <summary>
        /// Applies the immutable payout produced by one eligible run. Combat
        /// never edits these balances directly; App calls this once after its
        /// RunId/settlement-event guard accepts the result. Locked gemstones
        /// are ignored in normal mode so a stale result cannot reveal a future
        /// currency.
        /// </summary>
        public bool GrantResourceRewards(IReadOnlyList<ResourceRewardSnapshot> rewards)
        {
            if (rewards == null)
            {
                return true;
            }

            EnsureGemstoneBalanceCapacity();
            for (int i = 0; i < rewards.Count; i++)
            {
                ResourceRewardSnapshot reward = rewards[i];
                if (!reward.IsValid)
                {
                    continue;
                }

                if (reward.CurrencyId == "gem.garnet")
                {
                    _garnetBalance = AddSaturating(_garnetBalance, reward.Amount);
                    _gemstoneBalances[0] = _garnetBalance;
                    continue;
                }


                int gemstoneIndex = GetGemstoneIndexById(reward.CurrencyId);
                TraitGemstoneDefinition gemstone =
                    GetGemstoneDefinition(gemstoneIndex);
                if (gemstone == null || gemstone.Id != reward.CurrencyId ||
                    gemstoneIndex <= 0 ||
                    gemstoneIndex >= _gemstoneBalances.Length)
                {
                    continue;
                }

                _gemstoneBalances[gemstoneIndex] = AddSaturating(
                    _gemstoneBalances[gemstoneIndex],
                    reward.Amount);
            }

            RefreshAll();
            return SaveProgressionState();
        }


        /// <summary>
        /// Creates the resolved information document for the current trait
        /// state. App separately captures additive cursor bonuses for Combat;
        /// neither object exposes the live purchased-node collection.
        /// </summary>
        public string CreateGameInformationJson()
        {
            EnsureProgressionState();
            return GameInformationJson.Serialize(CreateGameInformation());
        }

        public CursorCombatStatBonusesSnapshot CreateCursorCombatStatBonusesSnapshot()
        {
            EnsureProgressionState();
            return GameInformationBuilder.CreateCursorCombatStatBonusesSnapshot(
                _document,
                _purchased);
        }

        public ProgressionCombatSnapshot CreateCombatSnapshot()
        {
            string json = CreateGameInformationJson();
            if (!GameInformationJson.TryDeserialize(json, out var information, out string error))
                throw new InvalidOperationException(error);
            return information.ToCombatSnapshot(
                json,
                CreateCursorCombatStatBonusesSnapshot());
        }

        private GameInformation CreateGameInformation()
        {
            return GameInformationBuilder.Build(_document, _purchased,
                id => GetGemstoneBalance(GetGemstoneIndexById(id)),
                _cursorCombatStatDefaults);
        }

        public void SetCursorCombatStatDefaults(
            CursorCombatStatDefaultsSnapshot defaults)
        {
            if (!defaults.IsValid)
            {
                Debug.LogError("Cursor combat defaults are invalid.", this);
                return;
            }

            _cursorCombatStatDefaults = defaults;
            if (_isBuilt)
            {
                RefreshAll();
            }
        }

        private void EnsureProgressionState()
        {
            if (_activeCatalog != null)
            {
                return;
            }

            ResolveCatalog();
            _garnetBalance = startingGarnet;
            InitializeGemstoneBalances();
            InitializePurchasedState();
            LoadSavedProgress();
        }

        /// <summary>
        /// Stores the immutable values copied at run start so a Field HUD
        /// summary describes the run that is currently on screen. Progression
        /// changes made after the run starts cannot silently alter this view.
        /// </summary>
        public void SetActiveRunSnapshot(ProgressionCombatSnapshot snapshot)
        {
            _activeRunSnapshot = snapshot;
        }

        /// <summary>
        /// Releases the run snapshot when the run is reset or the composition
        /// root leaves the scene. The result screen may keep the last snapshot
        /// until the next run so its information remains inspectable.
        /// </summary>
        public void ClearActiveRunSnapshot()
        {
            _activeRunSnapshot = null;
        }

        public void RequestBack()
        {
            backRequested?.Invoke();
        }

        private void ResolveCatalog()
        {
            _document = GameDataDocument.Load();
            // Data-file language does not select the player's UI locale.
            startingGarnet = _document.information.gemstones[0].amount;
            _garnetBalance = startingGarnet;
            startingGemstoneBalances = new long[_document.information.gemstones.Length];
            for (int i = 0; i < startingGemstoneBalances.Length; i++)
                startingGemstoneBalances[i] = _document.information.gemstones[i].amount;
            unlockedBossTier = _document.progression.initialBossTier;
            _runtimeCatalog = TraitCatalog.CreateFromDocument(_document);
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

        }

        private void LoadSavedProgress()
        {
            if (_document.progression.savedProgressPolicy == "fileOnly" || !loadSavedProgress ||
                !TraitProgressionStore.TryLoad(out TraitProgressionStore.SaveData data))
            {
                return;
            }

            _garnetBalance = Math.Max(0L, data.garnetBalance);
            EnsureGemstoneBalanceCapacity();
            if (data.gemstoneBalances != null)
            {
                int length = Mathf.Min(
                    data.gemstoneBalances.Count,
                    _gemstoneBalances.Length);
                for (int i = 0; i < length; i++)
                {
                    _gemstoneBalances[i] = Math.Max(0L, data.gemstoneBalances[i]);
                }
            }

            _gemstoneBalances[0] = _garnetBalance;
            // A saved wallet replaces the file baseline; only migrated refunds are additive.
            var loadedNodes = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < (data.purchasedNodeIds == null ? 0 : data.purchasedNodeIds.Count); i++)
            {
                string nodeId = data.version < 2 ? ProgressionMigration.NodeId(data.purchasedNodeIds[i]) : data.purchasedNodeIds[i];
                if (ProgressionMigration.IsRetiredNode(data.purchasedNodeIds[i])) continue;
                TraitNodeDefinition node = FindNode(nodeId);
                if (node == null || string.IsNullOrEmpty(node.Id) ||
                    (data.version < 2 && !loadedNodes.Add(node.Id)))
                {
                    // Removed gemstone gates, extra monsters and duration levels are refunded.
                    if (data.version < 2 && data.spentCosts != null && i < data.spentCosts.Count)
                    {
                        int refund = Math.Max(0, data.spentCosts[i]);
                        string currency = data.spentCurrencyIds != null && i < data.spentCurrencyIds.Count
                            ? ProgressionMigration.CurrencyId(data.spentCurrencyIds[i]) : "gem.garnet";
                        {
                            int index = GetGemstoneIndexById(currency);
                            if (index >= 0 && index < _gemstoneBalances.Length)
                                _gemstoneBalances[index] = AddSaturating(_gemstoneBalances[index], refund);
                        }
                    }
                    continue;
                }

                _purchased.Add(node.Id);
                if (data.spentCosts != null && i < data.spentCosts.Count)
                {
                    int cost = Mathf.Max(0, data.spentCosts[i]);
                    if (cost > 0)
                    {
                        _spentByNode.TryGetValue(node.Id, out int previousCost);
                        _spentByNode[node.Id] = (int)Math.Min(int.MaxValue, (long)previousCost + cost);
                    }
                }

                if (data.spentCurrencyIds != null &&
                    i < data.spentCurrencyIds.Count &&
                    GetGemstoneIndexById(data.spentCurrencyIds[i]) >= 0)
                {
                    _spentCurrencyByNode[node.Id] = ProgressionMigration.CurrencyId(data.spentCurrencyIds[i]);
                }
            }

            _garnetBalance = _gemstoneBalances[0];
        }

        private bool SaveProgressionState()
        {
            // File-only preview/prototyping must never overwrite the player's existing save.
            if (_document != null && _document.progression.savedProgressPolicy == "fileOnly") return true;
            bool saved = TraitProgressionStore.Save(
                _garnetBalance,
                _gemstoneBalances,
                _purchased,
                _spentByNode,
                _spentCurrencyByNode);
            if (!saved)
            {
                Debug.LogWarning(
                    "Trait progression changed in memory but could not be saved.",
                    this);
            }

            return saved;
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
            BuildSummaryPopup();

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
            ShowTab((TraitTab)Mathf.Clamp(initialTab, 0, 3), false);
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

                    bool visible = IsGemstoneAvailable(gemstone) || GetGemstoneBalance(i) > 0;
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
                    ? "일반 몬스터 처치로 획득"
                    : "등장한 일반 몬스터가 해당 젬스톤을 공급합니다";
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

            TextMeshProUGUI label = CreateText(
                CreateRect("Label", row, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(44f, 0f), new Vector2(-172f, 0f), new Vector2(0f, 0.5f)),
                gemstone.Title,
                14,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            label.color = Color.Lerp(inkColor, accent, 0.25f);

            long balance = GetGemstoneBalance(gemstoneIndex);
            TextMeshProUGUI value = CreateText(
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
                "몬스터 드롭",
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

        private bool IsGemstoneAvailable(TraitGemstoneDefinition gemstone)
        {
            if (gemstone == null) return false;
            if (gemstone.Id == "gem.garnet") return true;
            if (_document == null) return false;
            foreach (var monster in _document.information.monsters)
                if (monster.gemstoneId == gemstone.Id && monster.gemstoneAmount > 0 && monster.gemstoneChancePercent > 0 &&
                    (monster.enabled || _purchased.Contains(monster.id))) return true;
            return false;
        }

        /// <summary>
        /// Returns whether an enabled ordinary monster supplies the gemstone.
        /// Combat receives the same flag through the JSON snapshot.
        /// </summary>
        public bool IsGemstoneAvailable(int gemstoneIndex)
        {
            TraitGemstoneDefinition gemstone = GetGemstoneDefinition(gemstoneIndex);
            return IsGemstoneAvailable(gemstone);
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
                return -1;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                TraitGemstoneDefinition definition = definitions[i];
                if (definition != null && definition.Id == gemstoneId)
                {
                    return i;
                }
            }

            return -1;
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

            string[] labels = { "공격력", "자동 반경", "쿨타임", "치명타", "보스 피해" };
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
                TextMeshProUGUI value = CreateText(
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
                new Vector2(-10f, -51f),
                new Vector2(-760f, -402f),
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
            for (int i = 0; i < 3; i++)
            {
                int action = i;
                var button = CreateButton("GraphZoom" + i, heading, new Vector2(1f, .5f),
                    new Vector2(1f, .5f), new Vector2(-130f + i * 46f, 0f),
                    new Vector2(42f, 32f), null, panelColor, i == 0 ? "−" : i == 1 ? "+" : "1:1", 14, inkColor);
                button.onClick.AddListener(() => {
                    float zoom = action == 2 ? 1f : Mathf.Clamp(_nodeContent.localScale.x + (action == 0 ? -.15f : .15f), .5f, 1.5f);
                    _nodeContent.localScale = new Vector3(zoom, zoom, 1f);
                });
            }
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

            _selectedDescription.overflowMode = TextOverflowModes.Overflow;

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

            // Keep the two actions together in a centered action row. Edge
            // anchoring made the previous buttons read like unrelated footer
            // controls, especially on a narrow Mac Game view.
            _upgradeButton = CreateButton(
                "UpgradeButton",
                panel,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-72f, 46f),
                new Vector2(136f, 54f),
                null,
                new Color(0.96f, 0.64f, 0.20f, 0.98f),
                "강화/해금",
                19,
                inkColor);
            _upgradeButtonImage = _upgradeButton.GetComponent<Image>();
            _upgradeButtonLabel = _upgradeButton.GetComponentInChildren<TextMeshProUGUI>();
            _upgradeButton.onClick.AddListener(UpgradeSelected);

            _resetNodeButton = CreateButton(
                "ResetNodeButton",
                panel,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(72f, 46f),
                new Vector2(136f, 54f),
                null,
                new Color(0.32f, 0.26f, 0.34f, 0.98f),
                "노드 초기화",
                19,
                inkColor);
            _resetNodeButtonImage = _resetNodeButton.GetComponent<Image>();
            _resetNodeButtonLabel = _resetNodeButton.GetComponentInChildren<TextMeshProUGUI>();
            _resetNodeButton.onClick.AddListener(ResetSelectedNode);

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
                "전체 초기화",
                17,
                inkColor);
            reset.onClick.AddListener(ResetPreview);

            Button summary = CreateButton(
                "SummaryButton",
                _runtimeRoot,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-406f, 30f),
                new Vector2(170f, 52f),
                null,
                new Color(0.12f, 0.32f, 0.48f, 0.98f),
                "전체 정보",
                17,
                inkColor);
            summary.onClick.AddListener(ShowSummaryPopup);
        }

        private void BuildSummaryPopup()
        {
            _summaryOverlay = new GameObject(
                "TraitSummaryOverlay",
                typeof(RectTransform),
                typeof(Image));
            // The summary is also opened from the normal/boss field HUD. It
            // must live under the active canvas rather than the inactive trait
            // root, otherwise the field button can only open an invisible
            // child. The fallback keeps custom test scenes without a Canvas
            // usable.
            Canvas canvasHost = GetComponentInParent<Canvas>(true);
            Transform overlayParent = canvasHost != null
                ? canvasHost.transform
                : transform.root;
            _summaryOverlay.transform.SetParent(overlayParent, false);

            RectTransform overlayRect = _summaryOverlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.pivot = new Vector2(0.5f, 0.5f);

            Image overlayImage = _summaryOverlay.GetComponent<Image>();
            overlayImage.color = new Color(0.005f, 0.015f, 0.05f, 0.86f);
            overlayImage.raycastTarget = true;

            RectTransform panel = CreatePanel(
                "PopupPanel",
                overlayRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(900f, 720f),
                panelSprite,
                new Color(0.025f, 0.09f, 0.18f, 0.98f),
                new Vector2(0.5f, 0.5f));

            CreateText(
                CreateRect(
                    "Title",
                    panel,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, -26f),
                    new Vector2(-64f, 52f),
                    new Vector2(0.5f, 1f)),
                "현재 특성·해금 정보",
                24,
                TextAnchor.MiddleLeft,
                inkColor,
                true);

            RectTransform viewport = CreateRect(
                "Viewport",
                panel,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, -20f),
                new Vector2(-64f, -126f),
                new Vector2(0.5f, 0.5f));
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.12f);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect(
                "Content",
                viewport,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero,
                new Vector2(0f, 1f));
            ContentSizeFitter contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(18, 18, 14, 14);
            contentLayout.spacing = 0f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            _summaryOverlayContent = content;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45f;

            Button close = CreateButton(
                "CloseButton",
                panel,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 28f),
                new Vector2(200f, 52f),
                null,
                new Color(0.12f, 0.32f, 0.48f, 0.98f),
                "닫기",
                17,
                inkColor);
            close.onClick.AddListener(CloseSummaryPopup);
            _summaryOverlay.SetActive(false);
            _summaryOverlay.transform.SetAsLastSibling();
        }

        public bool IsSummaryOpen => _summaryOverlay != null && _summaryOverlay.activeSelf;

        public void ShowSummaryPopup()
        {
            if (!_isBuilt)
            {
                BuildUi();
            }

            if (_summaryOverlay == null)
            {
                return;
            }

            BuildSummaryContent();

            _summaryOverlay.SetActive(true);
            _summaryOverlay.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            ScrollRect scroll = _summaryOverlay.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null)
            {
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        public void CloseSummaryPopup()
        {
            if (_summaryOverlay != null)
            {
                _summaryOverlay.SetActive(false);
            }
        }

        /// <summary>
        /// Rebuilds the summary as explicit sections and key/value rows. A
        /// single TextMeshProUGUI blob made it difficult to tell a category, a node key,
        /// and its current value apart, especially once Monster1~20 were
        /// added. The content is intentionally rebuilt only when the popup is
        /// opened, so ordinary tab changes do not allocate summary rows.
        /// </summary>
        private void BuildSummaryContent()
        {
            if (_summaryOverlayContent == null) return;
            DestroyChildren(_summaryOverlayContent);
            _summaryJson = _activeRunSnapshot != null && !string.IsNullOrEmpty(_activeRunSnapshot.SourceJson)
                ? _activeRunSnapshot.SourceJson : CreateGameInformationJson();
            if (!GameInformationJson.TryDeserialize(_summaryJson, out var information, out string error))
            {
                AddSummaryRow(_summaryOverlayContent, "error", error);
                return;
            }
            CreateSummaryNote(_summaryOverlayContent, _activeRunSnapshot == null
                ? "현재 진행도 JSON · 전투 시작 시 이 값을 고정합니다."
                : "현재 런이 읽은 JSON · 재화 수량도 시작 시점 기준입니다.");
            var copy = CreateButton("CopyJson", _summaryOverlayContent, Vector2.zero, Vector2.zero,
                Vector2.zero, new Vector2(180f, 40f), null, panelColor, "JSON 복사", 15, inkColor);
            copy.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            copy.onClick.AddListener(() => GUIUtility.systemCopyBuffer = _summaryJson);
            CreateSummarySection(_summaryOverlayContent, "stats", Color.white);
            AddInfo("schemaVersion", information.schemaVersion);
            AddInfo("balanceVersion", information.balanceVersion);
            AddInfo("attackPower", information.stats.attackPower);
            AddInfo("attackRadiusWorldUnits", information.stats.attackRadiusWorldUnits);
            AddInfo("attackCooldownSeconds", information.stats.attackCooldownSeconds);
            AddInfo("criticalChancePercent", information.stats.criticalChancePercent);
            AddInfo("bossDamageMultiplier", information.stats.bossDamageMultiplier);
            AddInfo("normalFieldDurationSeconds", information.stats.normalFieldDurationSeconds);
            CreateSummarySection(_summaryOverlayContent, "rules", Color.white);
            AddInfo("criticalDamageMultiplier", information.rules.criticalDamageMultiplier);
            AddInfo("globalAliveLimit", information.rules.globalAliveLimit);
            AddInfo("perMonsterAliveLimit", information.rules.perMonsterAliveLimit);
            AddInfo("bossFieldDurationSeconds", information.rules.bossFieldDurationSeconds);
            CreateSummarySection(_summaryOverlayContent, "gemstones", Color.white);
            foreach (var gem in information.gemstones)
            {
                AddInfo(gem.id + ".enabled", gem.enabled);
                AddInfo(gem.id + ".amount", gem.amount);
            }
            CreateSummarySection(_summaryOverlayContent, "monsters", Color.white);
            foreach (var monster in information.monsters)
            {
                AddInfo(monster.id + ".enabled", monster.enabled);
                AddInfo(monster.id + ".productionCount", monster.productionCount);
                AddInfo(monster.id + ".hitPoints", monster.hitPoints);
                AddInfo(monster.id + ".spawnIntervalSeconds", monster.spawnIntervalSeconds);
                AddInfo(monster.id + ".garnetReward", monster.garnetReward);
                AddInfo(monster.id + ".gemstoneId", monster.gemstoneId);
                AddInfo(monster.id + ".gemstoneAmount", monster.gemstoneAmount);
                AddInfo(monster.id + ".gemstoneChancePercent", monster.gemstoneChancePercent);
                AddInfo(monster.id + ".behaviorType", (int)monster.behaviorType);
            }
            CreateSummarySection(_summaryOverlayContent, "skills", Color.white);
            foreach (var skill in information.skills)
            {
                AddInfo(skill.id + ".enabled", skill.enabled);
                AddInfo(skill.id + ".radiusWorldUnits", skill.radiusWorldUnits);
                AddInfo(skill.id + ".damage", skill.damage);
                AddInfo(skill.id + ".cooldownSeconds", skill.cooldownSeconds);
            }
        }

        private void AddInfo(string key, object value)
        {
            AddSummaryRow(_summaryOverlayContent, key, value is bool flag ? (flag ? "true" : "false") :
                Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private void CreateSummarySection(
            Transform parent,
            string title,
            Color accent)
        {
            RectTransform section = CreateRect(
                "Section_" + (title ?? "Summary"),
                parent,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(0f, 38f),
                new Vector2(0.5f, 0.5f));
            LayoutElement element = section.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 38f;
            element.minHeight = 38f;
            Image image = section.gameObject.AddComponent<Image>();
            image.color = new Color(accent.r, accent.g, accent.b, 0.16f);
            image.raycastTarget = false;
            CreateText(
                CreateRect(
                    "Label",
                    section,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(14f, 0f),
                    new Vector2(-28f, 0f),
                    new Vector2(0.5f, 0.5f)),
                title ?? "",
                16,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
        }

        private void CreateSummaryCategory(
            Transform parent,
            string title,
            Color accent)
        {
            RectTransform category = CreateRect(
                "Category_" + (title ?? "Category"),
                parent,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(0f, 30f),
                new Vector2(0.5f, 0.5f));
            LayoutElement element = category.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 30f;
            element.minHeight = 30f;
            CreateText(
                CreateRect(
                    "Label",
                    category,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(6f, 0f),
                    new Vector2(-12f, 0f),
                    new Vector2(0.5f, 0.5f)),
                "▸ " + (title ?? "종류"),
                14,
                TextAnchor.MiddleLeft,
                Color.Lerp(inkColor, accent, 0.32f),
                true);
        }

        private void CreateSummaryTableHeader(Transform parent)
        {
            RectTransform row = CreateRect(
                "TableHeader",
                parent,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(0f, 24f),
                new Vector2(0.5f, 0.5f));
            LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 24f;
            element.minHeight = 24f;
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(10, 10, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            TextMeshProUGUI key = CreateText(
                CreateRect("Key", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)),
                "항목 (Key)",
                11,
                TextAnchor.MiddleLeft,
                mutedInkColor,
                true);
            LayoutElement keyElement = key.gameObject.AddComponent<LayoutElement>();
            keyElement.preferredWidth = 250f;
            keyElement.minWidth = 160f;
            TextMeshProUGUI value = CreateText(
                CreateRect("Value", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)),
                "현재 값 (Value)",
                11,
                TextAnchor.MiddleLeft,
                mutedInkColor,
                true);
            LayoutElement valueElement = value.gameObject.AddComponent<LayoutElement>();
            valueElement.flexibleWidth = 1f;
        }

        private void AddSummaryRow(Transform parent, string key, string value)
        {
            RectTransform row = CreateRect(
                "Row_" + (key ?? "Value"),
                parent,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(0f, 32f),
                new Vector2(0.5f, 0.5f));
            LayoutElement rowElement = row.gameObject.AddComponent<LayoutElement>();
            rowElement.preferredHeight = 32f;
            rowElement.minHeight = 32f;
            Image background = row.gameObject.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.035f);
            background.raycastTarget = false;
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(10, 10, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            TextMeshProUGUI keyText = CreateText(
                CreateRect("Key", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)),
                key ?? "",
                13,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            LayoutElement keyElement = keyText.gameObject.AddComponent<LayoutElement>();
            keyElement.preferredWidth = 250f;
            keyElement.minWidth = 160f;
            TextMeshProUGUI valueText = CreateText(
                CreateRect("Value", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)),
                value ?? "",
                13,
                TextAnchor.MiddleLeft,
                mutedInkColor,
                false);
            valueText.overflowMode = TextOverflowModes.Truncate;
            LayoutElement valueElement = valueText.gameObject.AddComponent<LayoutElement>();
            valueElement.flexibleWidth = 1f;
        }

        private void CreateSummaryNote(Transform parent, string note)
        {
            RectTransform textRect = CreateRect(
                "Note",
                parent,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(0f, 44f),
                new Vector2(0.5f, 0.5f));
            LayoutElement element = textRect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 44f;
            element.minHeight = 44f;
            TextMeshProUGUI text = CreateText(
                textRect,
                note ?? "",
                12,
                TextAnchor.MiddleLeft,
                mutedInkColor,
                false);
            text.rectTransform.offsetMin = new Vector2(10f, 0f);
            text.rectTransform.offsetMax = new Vector2(-10f, 0f);
            text.overflowMode = TextOverflowModes.Overflow;
        }

        private Color GetTabAccent(TraitTab tab)
        {
            switch (tab)
            {
                case TraitTab.Skill:
                    return new Color(0.34f, 0.72f, 1f, 1f);
                case TraitTab.Monster:
                    return new Color(0.45f, 0.94f, 0.62f, 1f);
                default:
                    return new Color(1f, 0.82f, 0.30f, 1f);
            }
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

            TextMeshProUGUI[] texts = button.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (TextMeshProUGUI text in texts)
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
            if (_nodeContent == null) return;
            Vector2 previousPosition = _nodeContent.anchoredPosition;
            DestroyChildren(_nodeContent);
            var categories = GetCategories(_activeTab);
            if (categories == null) return;
            _nodeHeading.text = GetTabTitle(_activeTab) + " · 드래그로 이동";
            var positions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            var allNodes = new List<TraitNodeDefinition>();
            TraitNodeDefinition selected = null;
            int categoryIndex = 0, maxDepth = 1, flatIndex = 0;
            foreach (var category in categories)
            {
                int nodeIndex = 0;
                float lane = 110f + categoryIndex * 210f;
                var title = CreateRect("Branch_" + category.Id, _nodeContent, new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(32f, -lane + 70f), new Vector2(290f, 34f), new Vector2(0f, .5f));
                CreateText(title, category.Title, 16, TextAnchor.MiddleLeft, category.Accent, true);
                foreach (var node in category.Nodes)
                {
                    bool purchased = IsPurchased(node);
                    bool locked = !testModeUnlockAll &&
                        (node.RequiredBossTier > unlockedBossTier || !IsPrerequisiteMet(node));
                    float x = 90f + nodeIndex * 155f;
                    float y = -lane;
                    if (_activeTab == TraitTab.Skill && nodeIndex > 0)
                    {
                        x = 285f + (nodeIndex - 1) * 150f;
                        y += (nodeIndex - 2) * 52f;
                    }
                    else if (nodeIndex > 0) y += nodeIndex % 2 == 0 ? -22f : 22f;
                    positions[node.Id] = new Vector2(x, y);
                    allNodes.Add(node);
                    var button = CreateNodeButton(_nodeContent, category, node, categoryIndex, flatIndex++,
                        purchased, locked, !purchased && !locked && !node.AcquisitionOnly);
                    var rect = (RectTransform)button.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = positions[node.Id];
                    TraitNodeDefinition captured = node;
                    button.onClick.AddListener(() => SelectNode(captured));
                    if (selected == null || node.Id == _selectedNodeId) selected = node;
                    nodeIndex++;
                }
                maxDepth = Mathf.Max(maxDepth, nodeIndex);
                categoryIndex++;
            }
            foreach (var node in allNodes)
            {
                if (string.IsNullOrEmpty(node.PrerequisiteNodeId) ||
                    !positions.TryGetValue(node.PrerequisiteNodeId, out Vector2 start)) continue;
                Vector2 end = positions[node.Id], delta = end - start;
                var line = CreateRect("Edge_" + node.Id, _nodeContent, new Vector2(0f, 1f),
                    new Vector2(0f, 1f), (start + end) * .5f,
                    new Vector2(delta.magnitude, 3f), new Vector2(.5f, .5f));
                line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                var image = line.gameObject.AddComponent<Image>();
                image.color = IsPurchased(node) ? PurchasedColor :
                    IsPrerequisiteMet(node) ? AvailableColor : new Color(.16f, .23f, .33f);
                image.raycastTarget = false;
                line.SetAsFirstSibling();
            }
            _nodeContent.sizeDelta = new Vector2(Mathf.Max(700f, maxDepth * 155f + 150f),
                Mathf.Max(400f, categories.Count * 210f + 100f));
            _nodeContent.anchoredPosition = previousPosition;
            RefreshDetail(selected);
            if (animate) PlayNodeAnimation();
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
                GetNodeFrameColor(
                    _activeTab,
                    node,
                    category,
                    purchased,
                    locked,
                    available),
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
            TraitTab tab,
            TraitNodeDefinition node,
            TraitCategoryDefinition category,
            bool purchased,
            bool locked,
            bool available)
        {
            bool isMonsterProduction = tab == TraitTab.Monster &&
                node != null &&
                node.Id != null &&
                node.Id.IndexOf(".production.", StringComparison.Ordinal) >= 0;
            if (purchased)
            {
                return isMonsterProduction
                    ? new Color(1f, 0.67f, 0.25f, 0.98f)
                    : new Color(0.28f, 0.94f, 0.60f, 0.96f);
            }

            if (locked)
            {
                return isMonsterProduction
                    ? new Color(0.48f, 0.30f, 0.25f, 0.92f)
                    : new Color(0.20f, 0.42f, 0.68f, 0.92f);
            }

            if (available)
            {
                if (isMonsterProduction)
                {
                    return new Color(1f, 0.56f, 0.18f, 0.98f);
                }

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

            if (testModeFreeUpgrades)
            {

                TraitGemstoneDefinition testGemstone =
                    GetGemstoneDefinitionById(node.CostGemstoneId);
                string testCurrencyTitle = testGemstone == null
                    ? node.CostGemstoneId
                    : testGemstone.Title;
                return "테스트 무료  ·  원래 비용 " + node.Cost + " " + testCurrencyTitle;
            }


            TraitGemstoneDefinition gemstone = GetGemstoneDefinitionById(node.CostGemstoneId);
            string currencyTitle = gemstone == null ? node.CostGemstoneId : gemstone.Title;
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
            if (testModeFreeUpgrades || node == null || node.Cost <= 0)
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

        private void RefundNodeCost(
            TraitNodeDefinition node,
            int amount,
            string currencyId)
        {
            if (testModeFreeUpgrades || node == null || amount <= 0)
            {
                return;
            }

            string effectiveCurrencyId = string.IsNullOrEmpty(currencyId)
                ? node.CostGemstoneId
                : currencyId;

            int index = GetGemstoneIndexById(effectiveCurrencyId);
            if (index == 0)
            {
                _garnetBalance = AddSaturating(_garnetBalance, amount);
                EnsureGemstoneBalanceCapacity();
                _gemstoneBalances[0] = _garnetBalance;
                return;
            }

            EnsureGemstoneBalanceCapacity();
            if (index >= 0 && index < _gemstoneBalances.Length)
            {
                _gemstoneBalances[index] = AddSaturating(_gemstoneBalances[index], amount);
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
                if (_resetNodeButton != null)
                {
                    _resetNodeButton.interactable = false;
                }
                return;
            }

            _selectedTitle.text = node.Title;
            _selectedValue.text = node.ValueLabel;
            _selectedDescription.text = BuildNodeDescription(node);
            _selectedCost.text = GetNodeCostText(node);
            _selectedRequirement.text = GetNodeRequirementText(node);

            bool purchased = IsPurchased(node);
            bool lockedByBoss = !testModeUnlockAll &&
                node.RequiredBossTier > unlockedBossTier;
            bool lockedByPrerequisite = !testModeUnlockAll &&
                !IsPrerequisiteMet(node);
            bool currencyUnlocked = testModeUnlockAll || node.Cost <= 0 ||
                GetGemstoneDefinitionById(node.CostGemstoneId) != null;
            bool hasCurrency = testModeFreeUpgrades ||
                (currencyUnlocked && GetNodeBalance(node) >= node.Cost);
            bool canBuy = !purchased && !lockedByBoss && !lockedByPrerequisite &&
                !node.AcquisitionOnly && hasCurrency;
            bool canResetNode = purchased && !node.StartsUnlocked &&
                !HasPurchasedDependents(node);
            _upgradeButton.interactable = canBuy;
            _resetNodeButton.interactable = canResetNode;
            SetButtonLabel(
                _upgradeButtonLabel,
                node.Cost <= 0 ? "해금" : "강화");
            if (_upgradeButtonImage != null)
            {
                _upgradeButtonImage.color = canBuy ? AvailableColor : LockedColor;
            }
            if (_resetNodeButtonImage != null)
            {
                _resetNodeButtonImage.color = canResetNode
                    ? new Color(0.82f, 0.38f, 0.40f, 0.98f)
                    : LockedColor;
            }

            if (purchased)
            {
                _statusMessage.text = canResetNode
                    ? "적용됨 · 이 노드를 초기화할 수 있습니다"
                    : HasPurchasedDependents(node)
                        ? "적용됨 · 다음 강화가 연결되어 먼저 초기화해야 합니다"
                        : "이미 해금됨";
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
            bool currencyUnlocked = testModeUnlockAll || node == null || node.Cost <= 0 ||
                IsGemstoneAvailable(GetGemstoneDefinitionById(node.CostGemstoneId));
            if (node == null || IsPurchased(node) ||
                (!testModeUnlockAll && node.RequiredBossTier > unlockedBossTier) ||
                (!testModeUnlockAll && !IsPrerequisiteMet(node)) ||
                node.AcquisitionOnly || !currencyUnlocked ||
                (!testModeFreeUpgrades && GetNodeBalance(node) < node.Cost))
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
            SaveProgressionState();
        }

        private void ResetSelectedNode()
        {
            TraitNodeDefinition node = FindNode(_selectedNodeId);
            if (node == null || !IsPurchased(node) || node.StartsUnlocked)
            {
                RefreshDetail(node);
                return;
            }

            if (HasPurchasedDependents(node))
            {
                _statusMessage.text = "연결된 다음 강화가 있어 먼저 초기화해야 합니다";
                return;
            }

            if (_spentByNode.TryGetValue(node.Id, out int spent))
            {
                string spentCurrency = _spentCurrencyByNode.TryGetValue(
                    node.Id,
                    out string savedCurrency)
                    ? savedCurrency
                    : node.CostGemstoneId;
                RefundNodeCost(node, spent, spentCurrency);
            }

            _spentByNode.Remove(node.Id);
            _spentCurrencyByNode.Remove(node.Id);
            _purchased.Remove(node.Id);
            _statusMessage.text = "선택한 노드를 초기화했습니다";

            BuildNodes(false);
            _selectedNodeId = node.Id;
            RefreshSummary();
            RefreshGemstonePanel();
            RefreshDetail(node);
            SaveProgressionState();
        }

        private void ResetPreview()
        {
            foreach (KeyValuePair<string, int> spent in _spentByNode)
            {
                TraitNodeDefinition node = FindNode(spent.Key);
                string spentCurrency = _spentCurrencyByNode.TryGetValue(
                    spent.Key,
                    out string savedCurrency)
                    ? savedCurrency
                    : node == null ? string.Empty : node.CostGemstoneId;
                RefundNodeCost(node, spent.Value, spentCurrency);
            }

            InitializePurchasedState();
            _statusMessage.text = "전체 특성을 초기화했습니다";
            BuildCategories();
            BuildNodes(true);
            RefreshSummary();
            RefreshGemstonePanel();
            SaveProgressionState();
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

            string[] names = { "STATTab", "SKILLTab", "MONSTERTab" };
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
            if (_garnetValue != null) _garnetValue.text = _garnetBalance.ToString();
            if (_summaryValues.Count < 5 || _document == null) return;
            var info = CreateGameInformation();
            _summaryValues[0].text = info.stats.attackPower.ToString();
            _summaryValues[1].text = info.stats.attackRadiusWorldUnits.ToString("0.##") + "u";
            _summaryValues[2].text = info.stats.attackCooldownSeconds.ToString("0.##") + "s";
            _summaryValues[3].text = info.stats.criticalChancePercent.ToString("0.#") + "%";
            _summaryValues[4].text = info.stats.bossDamageMultiplier.ToString("0.##") + "×";
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
                TraitTab.Stat, TraitTab.Skill, TraitTab.Monster
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

        private TraitMonsterBalanceDefinition FindMonsterBalance(string monsterId)
        {
            if (_activeCatalog == null ||
                _activeCatalog.MonsterBalances == null ||
                string.IsNullOrEmpty(monsterId))
            {
                return null;
            }

            foreach (TraitMonsterBalanceDefinition balance in _activeCatalog.MonsterBalances)
            {
                if (balance != null &&
                    string.Equals(balance.Id, monsterId, StringComparison.Ordinal))
                {
                    return balance;
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


        private static long AddSaturating(long current, long amount)
        {
            if (amount <= 0L)
            {
                return Math.Max(0L, current);
            }

            if (current > long.MaxValue - amount)
            {
                return long.MaxValue;
            }

            return Math.Max(0L, current + amount);
        }

        private bool HasPurchasedDependents(TraitNodeDefinition node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id))
            {
                return false;
            }

            TraitTab[] tabs =
            {
                TraitTab.Stat, TraitTab.Skill, TraitTab.Monster
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

                    foreach (TraitNodeDefinition candidate in category.Nodes)
                    {
                        if (candidate != null &&
                            string.Equals(
                                candidate.PrerequisiteNodeId,
                                node.Id,
                                StringComparison.Ordinal) &&
                            IsPurchased(candidate))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static void SetButtonLabel(TextMeshProUGUI label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
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
            return node == null ? string.Empty : node.Description;
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

            if (categoryId.IndexOf("cooldown", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return timerIcon != null ? timerIcon : clickIcon;
            }

            if (categoryId.IndexOf("critical", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return criticalIcon;
            }

            if (categoryId.IndexOf("duration", StringComparison.OrdinalIgnoreCase) >= 0 ||
                categoryId.IndexOf("time", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return timerIcon;
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

            if (tab == TraitTab.Skill && category != null && category.Id == "skill.cursorAura")
                return radiusIcon != null ? radiusIcon : skillIcon;

            if (tab == TraitTab.Skill && skillIcons != null &&
                categoryIndex >= 0 && categoryIndex < skillIcons.Length &&
                skillIcons[categoryIndex] != null)
            {
                return skillIcons[categoryIndex];
            }

            if (tab == TraitTab.Monster && monsterIcons != null)
            {
                int monsterIndex = GetMonsterIconIndex(category == null ? null : category.Id);
                if (monsterIndex >= 0 && monsterIndex < monsterIcons.Length &&
                    monsterIcons[monsterIndex] != null)
                {
                    return monsterIcons[monsterIndex];
                }

                if (monsterIcons.Length > 0 && monsterIcons[0] != null)
                {
                    return monsterIcons[0];
                }
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
                // Every node in a skill row deliberately reuses that skill's
                // base icon. Damage/range/cooldown are still distinguished by
                // their labels and values, while the row reads as one skill.
                return GetCategoryHeaderIcon(tab, category, categoryIndex);
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

            // The CanvasScaler uses a width/height midpoint so 16:10 editor
            // windows do not apply a fractional width-only stretch. Pixel
            // perfect snapping is disabled because the dynamic tree is
            // intentionally allowed to land on fractional scale values;
            // snapping those vertices made small labels look softer.
            canvas.pixelPerfect = false;
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

        private TextMeshProUGUI CreateText(
            RectTransform rect,
            string content,
            int fontSize,
            TextAnchor alignment,
            Color color,
            bool bold)
        {
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content ?? string.Empty;
            // Use the serialized NanumGothic asset for Hangul. The builtin
            // fallback keeps custom catalogs usable when a scene has not yet
            // assigned a project font.
            text.font = LocalizedTypography.GetFont(uiFont);
            text.fontSize = Mathf.Max(
                12,
                Mathf.RoundToInt(fontSize * Mathf.Clamp(textScale, 1f, 1.5f)));
            text.alignment = LocalizedTypography.Alignment(alignment);
            text.color = color;
            text.raycastTarget = false;

            text.overflowMode = TextOverflowModes.Truncate;
            text.enableAutoSizing = false;
            text.richText = false;
            if (bold && text.font != null)
            {
                text.fontStyle = FontStyles.Bold;
            }

            LocalizationCatalog.BindKnown(text, content);
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
