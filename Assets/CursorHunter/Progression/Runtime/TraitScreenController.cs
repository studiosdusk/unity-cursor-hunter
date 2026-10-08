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
        [SerializeField, Range(0, 2)] private int initialTab;
        [SerializeField, Min(0f)] private float transitionSeconds = 0.14f;
        [SerializeField] private Vector2 nodeCellSize = new Vector2(106f, 106f);
        [SerializeField] private Vector2 nodeGridSpacing = new Vector2(18f, 18f);
        [SerializeField, Min(12f)] private float connectorWidth = 22f;
        [SerializeField, Range(2f, 16f)] private float connectorThickness = 3f;
        [SerializeField, Min(24f)] private float categoryHeaderHeight = 28f;
        [SerializeField, Range(1f, 1.5f)] private float textScale = 1.05f;
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
        private string _fullDocumentJson;
        private CursorCombatStatDefaultsSnapshot? _cursorCombatStatDefaults;

        private readonly HashSet<string> _purchased =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _spentByNode =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _spentCurrencyByNode =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2> _boardPositionsByNodeId =
            new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private RectTransform _runtimeRoot;
        private RectTransform _nodeContent;
        private RectTransform _nodeViewport;
        private RectTransform _gemstoneList;
        private RectTransform _gemstonePanel;
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
        private RectTransform _tooltipRoot;
        private CanvasGroup _tooltipGroup;
        private TextMeshProUGUI _tooltipType;
        private TextMeshProUGUI _tooltipTitle;
        private TextMeshProUGUI _tooltipValue;
        private TextMeshProUGUI _tooltipDescription;
        private TextMeshProUGUI _tooltipCost;
        private TextMeshProUGUI _tooltipStatus;
        private Button _tooltipUpgradeButton;
        private TextMeshProUGUI _tooltipUpgradeLabel;
        private Button _tooltipResetButton;
        private string _tooltipNodeId;
        private bool _pointerOverTooltip;
        private bool _tooltipPinned;
        private Coroutine _tooltipHideRoutine;
        private Coroutine _tooltipAnimation;
        private Button _upgradeButton;
        private Image _upgradeButtonImage;
        private TextMeshProUGUI _upgradeButtonLabel;
        private Button _resetNodeButton;
        private Image _resetNodeButtonImage;
        private TextMeshProUGUI _resetNodeButtonLabel;
        private GameObject _summaryOverlay;
        private RectTransform _summaryOverlayContent;
        private RectTransform _testToolsPanel;
        private TMP_InputField _testGrantAmountInput;
        private TextMeshProUGUI _testBossTierHeading;
        private Button[] _testBossTierButtons = Array.Empty<Button>();
        private TextMeshProUGUI[] _testBossTierLabels = Array.Empty<TextMeshProUGUI>();
        private bool _testToolsOpen;
        private Texture2D _dragHandCursor;
        private Coroutine _fitGraphRoutine;
        private readonly List<TextMeshProUGUI> _summaryValues = new List<TextMeshProUGUI>();
        private Coroutine _transition;
        private Coroutine _nodeTransition;
        private TraitTab _lastBuiltTab;
        private bool _hasBuiltNodeLayout;

        private sealed class BoardNodeEntry
        {
            public TraitTab Tab;
            public TraitCategoryDefinition Category;
            public TraitNodeDefinition Node;
            public int CategoryIndex;
            public int NodeIndex;
            public int FlatIndex;
            public Vector2Int Cell;
            public Vector2 Position;
        }

        [Serializable]
        private sealed class GameDocumentExportSnapshot
        {
            public int documentVersion;
            public string locale;
            public string description;
            public GameInformation information;
            public ProgressionDefinition progression;
            public EntityTextDefinition[] entities;
            public string[] purchasedNodeIds;
        }

        private const float StatGraphDefaultZoom = 0.62f;
        private const float StatGraphWidth = 1600f;
        private const float StatGraphHeight = 1120f;
        private const float BoardNodeSize = 76f;
        private const float BoardNodePitchX = 98f;
        private const float BoardNodePitchY = 98f;
        private const float MinimumGraphZoom = 0.12f;
        private const string ResearchCoreNodeId = "stat.attack.01";

        private static readonly Color LockedColor =
            new Color(0.12f, 0.07f, 0.16f, 0.98f);
        private static readonly Color AvailableColor =
            new Color(0.98f, 0.70f, 0.27f, 1f);
        private static readonly Color PurchasedColor =
            new Color(0.96f, 0.98f, 1f, 1f);

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

            if (_fitGraphRoutine != null)
            {
                StopCoroutine(_fitGraphRoutine);
            }
            _fitGraphRoutine = StartCoroutine(FitGraphAfterActivation());
            PlayEnterAnimation();
        }

        private void OnDisable()
        {
            if (_fitGraphRoutine != null)
            {
                StopCoroutine(_fitGraphRoutine);
                _fitGraphRoutine = null;
            }
            CloseSummaryPopup();
            HideNodeTooltip();
            RestoreDragCursor();
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
            RestoreDragCursor();
            if (_dragHandCursor != null)
            {
                Destroy(_dragHandCursor);
                _dragHandCursor = null;
            }
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
            RefreshTestBossTierButtons();
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
            ShowTab(TraitTab.Stat);
            TraitNodeDefinition petRoot = FindNode("skill.cursorAura");
            if (petRoot != null)
            {
                SelectNode(petRoot);
            }
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
            // A new or reset research profile starts at the available central
            // core. Nothing is purchased until the player activates it.
            _purchased.Clear();
            _spentByNode.Clear();
            _spentCurrencyByNode.Clear();
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

                // Versions through v3 serialized the authored baseline nodes
                // as purchased. v4 makes those nodes real research choices;
                // let the player start from the central core instead.
                if (data.version < 4 && IsLegacyImplicitStartNode(node.Id))
                {
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

        private static bool IsLegacyImplicitStartNode(string nodeId)
        {
            switch (nodeId)
            {
                case "stat.attack.01":
                case "stat.radius.01":
                case "stat.cooldown.01":
                case "stat.critical.01":
                case "stat.fieldDuration.01":
                case "skill.fireball":
                case "monster.01":
                    return true;
                default:
                    return false;
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
                : new Color(0.20f, 0.31f, 0.52f, 0.24f);
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
            fieldShade.color = new Color(0.014f, 0.026f, 0.078f, 0.82f);
            fieldShade.raycastTarget = false;

            _runtimeGroup = _runtimeRoot.gameObject.AddComponent<CanvasGroup>();
            _runtimeGroup.interactable = true;
            _runtimeGroup.blocksRaycasts = true;

            BuildNodePanel();
            BuildHeader();
            BuildGemstonePanel();
            BuildFooter();
            // Keep the full-information overlay available to the App HUD,
            // while omitting the always-visible summary and detail columns.
            BuildSummaryPopup();
            BuildNodeTooltip();

            _isBuilt = true;
            ShowTab((TraitTab)Mathf.Clamp(initialTab, 0, 2), false);
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
            RectTransform subtitle = CreateRect(
                "BoardGuide",
                _runtimeRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(44f, -62f),
                new Vector2(560f, 26f),
                new Vector2(0f, 1f));
            int nodeCount = CollectBoardEntries().Count;
            CreateText(
                subtitle,
                nodeCount.ToString("N0", CultureInfo.InvariantCulture) +
                    "개 연구 · 휠 확대/축소 · 드래그 이동 · 노드 클릭 후 해금/강화",
                13,
                TextAnchor.MiddleLeft,
                mutedInkColor,
                false);

            CreateBoardLegend(_runtimeRoot);
            BuildTopCategoryTabs(_runtimeRoot);
            BuildTestTools(_runtimeRoot);
            RectTransform status = CreateRect(
                "ResearchStatusMessage",
                _runtimeRoot,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 62f),
                new Vector2(760f, 28f),
                new Vector2(0.5f, 0f));
            _statusMessage = CreateText(
                status,
                string.Empty,
                13,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.84f, 0.50f, 1f),
                true);
        }

        private void BuildTopCategoryTabs(RectTransform parent)
        {
            RectTransform tabs = CreateRect(
                "ResearchCategoryTabs",
                parent,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-110f, -28f),
                new Vector2(348f, 54f),
                new Vector2(0.5f, 1f));

            Button research = CreateButton(
                "ResearchTreeTab",
                tabs,
                new Vector2(0.25f, 0.5f),
                new Vector2(0.25f, 0.5f),
                Vector2.zero,
                new Vector2(172f, 48f),
                tabFocusSprite,
                new Color(1f, 0.73f, 0.24f, 1f),
                "통합 연구",
                18,
                inkColor);
            research.interactable = false;
            ColorBlock researchColors = research.colors;
            researchColors.disabledColor = new Color(1f, 0.73f, 0.24f, 1f);
            research.colors = researchColors;

            Button relic = CreateButton(
                "RelicTab",
                tabs,
                new Vector2(0.75f, 0.5f),
                new Vector2(0.75f, 0.5f),
                Vector2.zero,
                new Vector2(164f, 48f),
                tabSprite,
                new Color(0.28f, 0.31f, 0.40f, 0.92f),
                "유물 · 기획 중",
                16,
                mutedInkColor);
            relic.interactable = false;
            ColorBlock relicColors = relic.colors;
            relicColors.disabledColor = new Color(0.28f, 0.31f, 0.40f, 0.92f);
            relic.colors = relicColors;
        }

        private void CreateBoardLegend(RectTransform parent)
        {
            RectTransform legend = CreateRect(
                "BoardLegend",
                parent,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -28f),
                new Vector2(388f, 28f),
                new Vector2(1f, 1f));
            CreateLegendChip(legend, 0f, "강화 완료", GetBoardStateColor(NodePurchaseState.Purchased));
            CreateLegendChip(legend, 96f, "구매 가능", GetBoardStateColor(NodePurchaseState.Available));
            CreateLegendChip(legend, 192f, "재화 부족", GetBoardStateColor(NodePurchaseState.Unaffordable));
            CreateLegendChip(legend, 292f, "잠김", GetBoardStateColor(NodePurchaseState.Locked));
        }

        private void CreateLegendChip(
            RectTransform parent,
            float x,
            string label,
            Color color)
        {
            RectTransform marker = CreateImageRect(
                "Marker_" + label,
                parent,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(x, 0f),
                new Vector2(9f, 9f),
                null,
                color,
                new Vector2(0.5f, 0.5f));
            marker.GetComponent<Image>().raycastTarget = false;
            CreateText(
                CreateRect(
                    "Label_" + label,
                    parent,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(x + 12f, 0f),
                    new Vector2(82f, 24f),
                    new Vector2(0f, 0.5f)),
                label,
                11,
                TextAnchor.MiddleLeft,
                mutedInkColor,
                true);
        }

        private void BuildTestTools(RectTransform parent)
        {
            Button toggle = CreateButton(
                "TestToolsToggle",
                parent,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-98f, -72f),
                new Vector2(148f, 36f),
                null,
                new Color(0.10f, 0.20f, 0.32f, 0.98f),
                "테스트 도구",
                14,
                inkColor);
            toggle.onClick.AddListener(() => SetTestToolsOpen(!_testToolsOpen));

            _testToolsPanel = CreatePanel(
                "TestToolsPanel",
                parent,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -116f),
                new Vector2(360f, 620f),
                panelSprite,
                new Color(0.018f, 0.043f, 0.105f, 0.98f),
                new Vector2(1f, 1f));
            Outline outline = _testToolsPanel.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(0.35f, 0.74f, 1f, 0.72f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            CreateText(
                CreateRect(
                    "TestToolsTitle",
                    _testToolsPanel,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(18f, -12f),
                    new Vector2(-60f, 30f),
                    new Vector2(0.5f, 1f)),
                "진행도 테스트 도구",
                19,
                TextAnchor.MiddleLeft,
                inkColor,
                true);

            Button close = CreateButton(
                "TestToolsClose",
                _testToolsPanel,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-31f, -14f),
                new Vector2(30f, 30f),
                null,
                new Color(0.14f, 0.21f, 0.32f, 0.98f),
                "×",
                17,
                inkColor);
            close.onClick.AddListener(() => SetTestToolsOpen(false));

            CreateText(
                CreateRect(
                    "TestToolsHint",
                    _testToolsPanel,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(18f, -48f),
                    new Vector2(-36f, 22f),
                    new Vector2(0.5f, 1f)),
                "수량을 입력하고 원하는 젬스톤을 지급하세요.",
                12,
                TextAnchor.MiddleLeft,
                mutedInkColor,
                false);

            RectTransform amountRect = CreateImageRect(
                "TestGrantAmount",
                _testToolsPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -78f),
                new Vector2(118f, 40f),
                null,
                new Color(0.035f, 0.075f, 0.13f, 1f),
                new Vector2(0f, 1f));
            Image amountBackground = amountRect.GetComponent<Image>();
            amountBackground.raycastTarget = true;
            _testGrantAmountInput = amountRect.gameObject.AddComponent<TMP_InputField>();
            _testGrantAmountInput.targetGraphic = amountBackground;
            _testGrantAmountInput.textViewport = amountRect;
            _testGrantAmountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            _testGrantAmountInput.lineType = TMP_InputField.LineType.SingleLine;
            _testGrantAmountInput.keyboardType = TouchScreenKeyboardType.NumberPad;
            _testGrantAmountInput.characterLimit = 18;
            _testGrantAmountInput.selectionColor = new Color(0.30f, 0.65f, 1f, 0.45f);
            TextMeshProUGUI amountText = CreateText(
                CreateRect(
                    "InputText",
                    amountRect,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(10f, 0f),
                    new Vector2(-20f, -4f),
                    new Vector2(0.5f, 0.5f)),
                "100",
                17,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            amountText.raycastTarget = false;
            _testGrantAmountInput.textComponent = amountText;
            _testGrantAmountInput.text = "100";

            Button grantAll = CreateButton(
                "GrantAllGemstones",
                _testToolsPanel,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-119f, -78f),
                new Vector2(202f, 40f),
                null,
                new Color(0.20f, 0.46f, 0.60f, 0.98f),
                "모든 젬스톤 지급",
                14,
                inkColor);
            grantAll.onClick.AddListener(GrantAllTestGemstones);

            CreateText(
                CreateRect(
                    "GemstoneGrantHeading",
                    _testToolsPanel,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(18f, -126f),
                    new Vector2(-36f, 22f),
                    new Vector2(0.5f, 1f)),
                "종류별 지급",
                13,
                TextAnchor.MiddleLeft,
                new Color(0.58f, 0.79f, 1f, 1f),
                true);

            RectTransform rows = CreateRect(
                "TestGemstoneRows",
                _testToolsPanel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -152f),
                new Vector2(-36f, 222f),
                new Vector2(0.5f, 1f));
            VerticalLayoutGroup rowsLayout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 4f;
            rowsLayout.padding = new RectOffset(0, 0, 0, 0);
            rowsLayout.childAlignment = TextAnchor.UpperLeft;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = false;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;

            IReadOnlyList<TraitGemstoneDefinition> definitions =
                _activeCatalog == null ? null : _activeCatalog.Gemstones;
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    TraitGemstoneDefinition gemstone = definitions[i];
                    if (gemstone == null) continue;
                    CreateTestGemstoneGrantRow(rows, gemstone, i);
                }
            }

            _testBossTierHeading = CreateText(
                CreateRect(
                    "TestBossTierHeading",
                    _testToolsPanel,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(18f, -384f),
                    new Vector2(-36f, 22f),
                    new Vector2(0.5f, 1f)),
                "보스 단계 해금 · 현재 0단계",
                13,
                TextAnchor.MiddleLeft,
                new Color(0.82f, 0.90f, 1f, 1f),
                true);
            BuildTestBossTierSelector();

            Button resetGemstones = CreateButton(
                "ResetTestGemstones",
                _testToolsPanel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -459f),
                new Vector2(-36f, 38f),
                null,
                new Color(0.42f, 0.18f, 0.24f, 0.98f),
                "젬스톤 잔액 0으로 초기화",
                13,
                inkColor);
            resetGemstones.onClick.AddListener(ResetTestGemstones);

            Button unlockAll = CreateButton(
                "TestUnlockAllConditions",
                _testToolsPanel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -503f),
                new Vector2(-36f, 38f),
                null,
                new Color(0.42f, 0.22f, 0.50f, 0.98f),
                "테스트: 잠금 조건 해제",
                14,
                inkColor);
            unlockAll.onClick.AddListener(UnlockAllConditionsForTesting);

            Button upgradeAll = CreateButton(
                "TestUpgradeAllNodes",
                _testToolsPanel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -547f),
                new Vector2(-36f, 38f),
                null,
                new Color(0.88f, 0.48f, 0.18f, 0.98f),
                "테스트: 전체 강화",
                14,
                inkColor);
            upgradeAll.onClick.AddListener(UpgradeAllNodesForTesting);

            CreateText(
                CreateRect(
                    "TestResetHint",
                    _testToolsPanel,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(18f, -578f),
                    new Vector2(-36f, 34f),
                    new Vector2(0.5f, 1f)),
                "테스트 구매는 저장됩니다. 구매 상태는 화면 하단의 전체 초기화로 되돌릴 수 있습니다.",
                10,
                TextAnchor.MiddleLeft,
                mutedInkColor,
                false);

            _testToolsPanel.gameObject.SetActive(false);
        }

        private void CreateTestGemstoneGrantRow(
            Transform parent,
            TraitGemstoneDefinition gemstone,
            int gemstoneIndex)
        {
            RectTransform row = CreateRect(
                "Grant_" + gemstone.Id,
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
            background.color = new Color(1f, 1f, 1f, 0.045f);
            background.raycastTarget = false;

            Sprite icon = GetGemstoneIcon(gemstoneIndex);
            if (icon != null)
            {
                RectTransform iconRect = CreateImageRect(
                    "Icon",
                    row,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(18f, 0f),
                    new Vector2(23f, 23f),
                    icon,
                    Color.white,
                    new Vector2(0.5f, 0.5f));
                iconRect.GetComponent<Image>().preserveAspect = true;
            }

            CreateText(
                CreateRect(
                    "Label",
                    row,
                    new Vector2(0f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(50f, 0f),
                    new Vector2(-150f, 28f),
                    new Vector2(0f, 0.5f)),
                GetGemstoneLocalizedName(gemstone.Id, gemstone.Title),
                13,
                TextAnchor.MiddleLeft,
                inkColor,
                true);

            int index = gemstoneIndex;
            Button grant = CreateButton(
                "Grant",
                row,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-52f, 0f),
                new Vector2(92f, 28f),
                null,
                GetGemstoneColor(gemstoneIndex),
                "+ 지급",
                12,
                new Color(0.06f, 0.08f, 0.12f, 1f));
            grant.onClick.AddListener(() => GrantTestGemstone(index));
        }

        private void BuildTestBossTierSelector()
        {
            RectTransform row = CreateRect(
                "TestBossTierButtons",
                _testToolsPanel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -411f),
                new Vector2(-36f, 34f),
                new Vector2(0.5f, 1f));
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            _testBossTierButtons = new Button[6];
            _testBossTierLabels = new TextMeshProUGUI[6];
            for (int tier = 0; tier <= 5; tier++)
            {
                Button button = CreateButton(
                    "TestBossTier_" + tier,
                    row,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    new Vector2(46f, 34f),
                    null,
                    new Color(0.10f, 0.15f, 0.24f, 1f),
                    tier.ToString(CultureInfo.InvariantCulture),
                    13,
                    inkColor);
                LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
                element.minWidth = 36f;
                element.preferredWidth = 46f;
                element.minHeight = 32f;
                element.preferredHeight = 34f;
                _testBossTierButtons[tier] = button;
                _testBossTierLabels[tier] = button.GetComponentInChildren<TextMeshProUGUI>();
                int capturedTier = tier;
                button.onClick.AddListener(() => SetTestBossTier(capturedTier));
            }

            RefreshTestBossTierButtons();
        }

        private void SetTestToolsOpen(bool open)
        {
            _testToolsOpen = open;
            if (_testToolsPanel != null)
            {
                _testToolsPanel.gameObject.SetActive(open);
                if (open) _testToolsPanel.SetAsLastSibling();
            }
        }

        private void SetTestBossTier(int tier)
        {
            unlockedBossTier = Mathf.Clamp(tier, 0, 5);
            RefreshTestBossTierButtons();
            RefreshAll();
            RefreshNodeTooltip(FindNode(_tooltipNodeId));
            SetTestStatus("테스트 보스 단계: " + unlockedBossTier +
                "단계까지의 해금 조건을 적용했습니다.");
        }

        private void RefreshTestBossTierButtons()
        {
            if (_testBossTierHeading != null)
            {
                _testBossTierHeading.text = "보스 단계 해금 · 현재 " +
                    Mathf.Clamp(unlockedBossTier, 0, 5) + "단계";
            }

            if (_testBossTierButtons == null || _testBossTierLabels == null)
            {
                return;
            }

            int count = Mathf.Min(_testBossTierButtons.Length, _testBossTierLabels.Length);
            for (int i = 0; i < count; i++)
            {
                Button button = _testBossTierButtons[i];
                if (button == null)
                {
                    continue;
                }

                bool selected = i == Mathf.Clamp(unlockedBossTier, 0, 5);
                Color color = selected
                    ? new Color(0.94f, 0.97f, 1f, 1f)
                    : new Color(0.10f, 0.15f, 0.24f, 1f);
                Image image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.color = color;
                }

                if (_testBossTierLabels[i] != null)
                {
                    _testBossTierLabels[i].color = selected
                        ? new Color(0.04f, 0.08f, 0.13f, 1f)
                        : inkColor;
                }

                ColorBlock colors = button.colors;
                colors.normalColor = color;
                colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = Color.Lerp(color, Color.black, 0.12f);
                colors.disabledColor = color;
                button.colors = colors;
            }
        }

        private void ResetTestGemstones()
        {
            EnsureGemstoneBalanceCapacity();
            for (int i = 0; i < _gemstoneBalances.Length; i++)
            {
                _gemstoneBalances[i] = 0L;
            }

            _garnetBalance = 0L;
            RefreshAll();
            RefreshNodeTooltip(FindNode(_tooltipNodeId));
            SaveProgressionState();
            SetTestStatus("테스트: 모든 젬스톤 잔액을 0개로 초기화했습니다.");
        }

        private bool TryGetTestGrantAmount(out long amount)
        {
            return long.TryParse(
                _testGrantAmountInput == null ? string.Empty : _testGrantAmountInput.text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out amount) && amount > 0L;
        }

        private void GrantTestGemstone(int gemstoneIndex)
        {
            if (!TryGetTestGrantAmount(out long amount))
            {
                SetTestStatus("지급 수량은 1 이상의 정수로 입력하세요.");
                return;
            }

            EnsureGemstoneBalanceCapacity();
            if (gemstoneIndex < 0 || gemstoneIndex >= _gemstoneBalances.Length)
            {
                return;
            }

            _gemstoneBalances[gemstoneIndex] = AddSaturating(
                GetGemstoneBalance(gemstoneIndex),
                amount);
            _garnetBalance = _gemstoneBalances[0];
            RefreshAll();
            RefreshNodeTooltip(FindNode(_tooltipNodeId));
            SaveProgressionState();
            TraitGemstoneDefinition gemstone = GetGemstoneDefinition(gemstoneIndex);
            SetTestStatus(GetGemstoneLocalizedName(
                gemstone == null ? string.Empty : gemstone.Id,
                gemstone == null ? "젬스톤" : gemstone.Title) +
                " " + amount.ToString("N0", CultureInfo.InvariantCulture) + "개 지급 완료");
        }

        private void GrantAllTestGemstones()
        {
            if (!TryGetTestGrantAmount(out long amount))
            {
                SetTestStatus("지급 수량은 1 이상의 정수로 입력하세요.");
                return;
            }

            EnsureGemstoneBalanceCapacity();
            for (int i = 0; i < _gemstoneBalances.Length; i++)
            {
                _gemstoneBalances[i] = AddSaturating(
                    GetGemstoneBalance(i),
                    amount);
            }
            _garnetBalance = _gemstoneBalances[0];
            RefreshAll();
            RefreshNodeTooltip(FindNode(_tooltipNodeId));
            SaveProgressionState();
            SetTestStatus("모든 젬스톤에 " + amount.ToString("N0", CultureInfo.InvariantCulture) + "개씩 지급했습니다.");
        }

        private void UnlockAllConditionsForTesting()
        {
            testModeUnlockAll = true;
            BuildNodes(false);
            RefreshNodeTooltip(FindNode(_tooltipNodeId));
            SetTestStatus("테스트: 선행 조건과 젬스톤 공급 잠금을 해제했습니다.");
        }

        private void UpgradeAllNodesForTesting()
        {
            testModeUnlockAll = true;
            foreach (TraitNodeDefinition node in EnumerateAllNodes())
            {
                if (node == null || string.IsNullOrEmpty(node.Id))
                {
                    continue;
                }

                if (_purchased.Add(node.Id))
                {
                    _spentByNode[node.Id] = 0;
                    _spentCurrencyByNode.Remove(node.Id);
                }
            }

            BuildNodes(false);
            RefreshSummary();
            RefreshGemstonePanel();
            RefreshNodeTooltip(FindNode(_tooltipNodeId));
            SaveProgressionState();
            SetTestStatus("테스트: 모든 노드를 해금·강화했습니다. 전체 초기화로 되돌릴 수 있습니다.");
        }

        private IEnumerable<TraitNodeDefinition> EnumerateAllNodes()
        {
            if (_activeCatalog == null) yield break;
            IReadOnlyList<TraitCategoryDefinition>[] collections =
            {
                _activeCatalog.StatCategories,
                _activeCatalog.SkillCategories,
                _activeCatalog.MonsterCategories
            };
            foreach (IReadOnlyList<TraitCategoryDefinition> categories in collections)
            {
                if (categories == null) continue;
                foreach (TraitCategoryDefinition category in categories)
                {
                    if (category == null || category.Nodes == null) continue;
                    foreach (TraitNodeDefinition node in category.Nodes)
                    {
                        yield return node;
                    }
                }
            }
        }

        private void SetTestStatus(string message)
        {
            if (_statusMessage != null) _statusMessage.text = message ?? string.Empty;
        }

        private static string GetGemstoneLocalizedName(string gemstoneId, string fallback)
        {
            switch (gemstoneId)
            {
                case "gem.garnet": return "가넷";
                case "gem.topaz": return "토파즈";
                case "gem.amethyst": return "자수정";
                case "gem.sapphire": return "사파이어";
                case "gem.diamond": return "다이아몬드";
                case "gem.dragon": return "드래곤 스톤";
                default: return string.IsNullOrEmpty(fallback) ? "젬스톤" : fallback;
            }
        }

        private void BuildGemstonePanel()
        {
            RectTransform panel = CreatePanel(
                "GemstonePanel",
                _runtimeRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, -94f),
                new Vector2(300f, 230f),
                null,
                new Color(0.015f, 0.045f, 0.12f, 0.88f),
                new Vector2(0f, 1f));
            _gemstonePanel = panel;

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

            if (_gemstonePanel != null)
            {
                float listHeight = Mathf.Max(38f, visibleCount * 38f);
                _gemstoneList.sizeDelta = new Vector2(_gemstoneList.sizeDelta.x, listHeight);
                Vector2 panelSize = _gemstonePanel.sizeDelta;
                panelSize.y = Mathf.Clamp(108f + visibleCount * 38f, 148f, 300f);
                _gemstonePanel.sizeDelta = panelSize;
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
                GetGemstoneLocalizedName(gemstone.Id, gemstone.Title),
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
                Vector2.zero,
                Vector2.zero,
                null,
                Color.clear,
                new Vector2(0.5f, 0.5f));
            RectTransform viewport = CreateRect(
                "NodeViewport",
                panel,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero,
                new Vector2(0.5f, 0.5f));
            _nodeViewport = viewport;
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;

            _nodeContent = CreateRect(
                "NodeContent",
                viewport,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(0f, 0f),
                new Vector2(0.5f, 0.5f));
            TraitGraphScrollRect scroll = viewport.gameObject.AddComponent<TraitGraphScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _nodeContent;
            // Dragging pans the research board in either direction. The stat
            // graph is centered on its shared hub when first opened.
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 0f;
            scroll.Bind(this);

            _nodeGroup = viewport.gameObject.AddComponent<CanvasGroup>();
            Image panelImage = panel.GetComponent<Image>();
            if (panelImage != null) panelImage.color = Color.clear;
            Outline panelOutline = panel.GetComponent<Outline>();
            if (panelOutline != null) panelOutline.enabled = false;
        }

        private void CreateTreeLegendItem(
            RectTransform parent,
            float x,
            string key,
            string fallback,
            Color color)
        {
            RectTransform marker = CreateImageRect(
                "LegendMarker_" + key,
                parent,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(x, 0f),
                new Vector2(8f, 8f),
                null,
                color,
                new Vector2(0.5f, 0.5f));
            marker.GetComponent<Image>().raycastTarget = false;

            RectTransform labelRect = CreateRect(
                "LegendLabel_" + key,
                parent,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(x + 10f, 0f),
                new Vector2(82f, 26f),
                new Vector2(0f, 0.5f));
            CreateLocalizedText(
                labelRect,
                key,
                fallback,
                11,
                TextAnchor.MiddleLeft,
                mutedInkColor,
                false);
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
                new Vector2(24f, 18f),
                new Vector2(148f, 44f),
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
                new Vector2(-24f, 18f),
                new Vector2(148f, 44f),
                null,
                new Color(0.04f, 0.12f, 0.24f, 0.96f),
                "전체 초기화",
                17,
                inkColor);
            reset.onClick.AddListener(ResetPreview);
        }

        private void BuildNodeTooltip()
        {
            _tooltipRoot = CreatePanel(
                "NodeTooltip",
                _runtimeRoot,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(392f, 372f),
                null,
                new Color(0.008f, 0.014f, 0.028f, 1f),
                Vector2.zero);
            _tooltipRoot.pivot = Vector2.zero;
            Image background = _tooltipRoot.GetComponent<Image>();
            background.sprite = null;
            background.type = Image.Type.Simple;
            background.color = new Color(0.008f, 0.014f, 0.028f, 1f);
            background.raycastTarget = true;
            Outline outline = _tooltipRoot.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(1f, 0.74f, 0.31f, 0.72f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            TraitNodeTooltipPointerRelay relay =
                _tooltipRoot.gameObject.AddComponent<TraitNodeTooltipPointerRelay>();
            relay.Bind(this);
            _tooltipGroup = _tooltipRoot.gameObject.AddComponent<CanvasGroup>();

            CreateImageRect(
                "AccentRail",
                _tooltipRoot,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -3f),
                new Vector2(-12f, 4f),
                null,
                AvailableColor,
                new Vector2(0.5f, 0.5f));

            _tooltipType = CreateText(
                CreateRect(
                    "TooltipType",
                    _tooltipRoot,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(18f, -16f),
                    new Vector2(210f, 24f),
                    new Vector2(0f, 1f)),
                "스탯",
                13,
                TextAnchor.MiddleLeft,
                new Color(0.83f, 0.91f, 1f, 1f),
                true);

            Button close = CreateButton(
                "TooltipClose",
                _tooltipRoot,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-19f, -17f),
                new Vector2(28f, 28f),
                null,
                new Color(0.12f, 0.18f, 0.29f, 0.92f),
                "×",
                17,
                inkColor);
            close.onClick.AddListener(HideNodeTooltip);

            _tooltipTitle = CreateText(
                CreateRect(
                    "TooltipTitle",
                    _tooltipRoot,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, -48f),
                    new Vector2(-36f, 42f),
                    new Vector2(0.5f, 1f)),
                "항목",
                23,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            _tooltipValue = CreateText(
                CreateRect(
                    "TooltipValue",
                    _tooltipRoot,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, -91f),
                    new Vector2(-36f, 26f),
                    new Vector2(0.5f, 1f)),
                "",
                16,
                TextAnchor.MiddleLeft,
                new Color(1f, 0.77f, 0.38f, 1f),
                true);
            _tooltipDescription = CreateText(
                CreateRect(
                    "TooltipDescription",
                    _tooltipRoot,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, -126f),
                    new Vector2(-36f, 118f),
                    new Vector2(0.5f, 1f)),
                "상세 설명",
                16,
                TextAnchor.UpperLeft,
                new Color(0.91f, 0.95f, 1f, 1f),
                false);
            _tooltipDescription.textWrappingMode = TextWrappingModes.Normal;
            _tooltipDescription.overflowMode = TextOverflowModes.Ellipsis;

            CreateImageRect(
                "TooltipSeparator",
                _tooltipRoot,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 146f),
                new Vector2(-32f, 1f),
                null,
                new Color(0.44f, 0.60f, 0.80f, 0.30f),
                new Vector2(0.5f, 0f));
            _tooltipCost = CreateText(
                CreateRect(
                    "TooltipCost",
                    _tooltipRoot,
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 111f),
                    new Vector2(-36f, 28f),
                    new Vector2(0.5f, 0f)),
                "비용",
                15,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            _tooltipStatus = CreateText(
                CreateRect(
                    "TooltipStatus",
                    _tooltipRoot,
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 81f),
                    new Vector2(-36f, 26f),
                    new Vector2(0.5f, 0f)),
                "구매 가능",
                14,
                TextAnchor.MiddleLeft,
                AvailableColor,
                true);

            _tooltipUpgradeButton = CreateButton(
                "TooltipUpgradeButton",
                _tooltipRoot,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 43f),
                new Vector2(348f, 48f),
                null,
                AvailableColor,
                "강화하기",
                17,
                new Color(0.10f, 0.07f, 0.025f, 1f));
            _tooltipUpgradeLabel = _tooltipUpgradeButton.GetComponentInChildren<TextMeshProUGUI>();
            Outline actionOutline = _tooltipUpgradeButton.gameObject.AddComponent<Outline>();
            actionOutline.effectColor = new Color(1f, 0.88f, 0.55f, 0.88f);
            actionOutline.effectDistance = new Vector2(1.5f, -1.5f);
            _tooltipUpgradeButton.onClick.AddListener(() =>
                TryUpgradeNode(FindNode(_tooltipNodeId)));

            _tooltipResetButton = CreateButton(
                "TooltipResetButton",
                _tooltipRoot,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-48f, 43f),
                new Vector2(82f, 44f),
                null,
                new Color(0.36f, 0.20f, 0.30f, 0.98f),
                "초기화",
                14,
                inkColor);
            _tooltipResetButton.onClick.AddListener(() =>
                ResetNode(FindNode(_tooltipNodeId)));
            _tooltipResetButton.gameObject.SetActive(false);
            _tooltipRoot.gameObject.SetActive(false);
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
                AddSummaryRow(_summaryOverlayContent, "정보를 읽을 수 없습니다", error);
                return;
            }
            CreateSummaryNote(_summaryOverlayContent, _activeRunSnapshot == null
                ? "전체 게임 정보를 한국어로 표시합니다. 영문 키와 ID는 내부 데이터에서 그대로 유지됩니다."
                : "전투 중인 런의 시작 시점 정보입니다. 영문 키와 ID는 실제 데이터 구조와 동일합니다.");
            var copy = CreateButton("CopyJson", _summaryOverlayContent, Vector2.zero, Vector2.zero,
                Vector2.zero, new Vector2(204f, 40f), null, panelColor, "전투 정보 JSON 복사", 14, inkColor);
            copy.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            copy.onClick.AddListener(() => GUIUtility.systemCopyBuffer = _summaryJson);
            var copyFull = CreateButton("CopyFullDocumentJson", _summaryOverlayContent,
                Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(250f, 40f), null,
                new Color(0.08f, 0.21f, 0.34f, 0.98f), "전체 게임·트리 JSON 복사", 14, inkColor);
            copyFull.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            copyFull.onClick.AddListener(() => GUIUtility.systemCopyBuffer = _fullDocumentJson);
            _fullDocumentJson = CreateFullGameDocumentJson(information);
            CreateSummarySection(_summaryOverlayContent, "데이터 버전", new Color(0.35f, 0.70f, 1f, 1f));
            AddLocalizedInfo("정보 스키마", "schemaVersion", information.schemaVersion);
            AddLocalizedInfo("밸런스 버전", "balanceVersion", information.balanceVersion);

            CreateSummarySection(_summaryOverlayContent, "플레이어 스탯", GetTabAccent(TraitTab.Stat));
            AddLocalizedInfo("공격력", "attackPower", information.stats.attackPower);
            AddLocalizedInfo("자동 공격 반경", "attackRadiusWorldUnits", information.stats.attackRadiusWorldUnits);
            AddLocalizedInfo("공격 간격", "attackCooldownSeconds", information.stats.attackCooldownSeconds);
            AddLocalizedInfo("치명타 확률", "criticalChancePercent", information.stats.criticalChancePercent);
            AddLocalizedInfo("보스 피해 배율", "bossDamageMultiplier", information.stats.bossDamageMultiplier);
            AddLocalizedInfo("일반 필드 지속 시간", "normalFieldDurationSeconds", information.stats.normalFieldDurationSeconds);

            CreateSummarySection(_summaryOverlayContent, "전투 규칙", new Color(0.88f, 0.48f, 0.34f, 1f));
            AddLocalizedInfo("치명타 피해 배율", "criticalDamageMultiplier", information.rules.criticalDamageMultiplier);
            AddLocalizedInfo("동시 등장 몬스터 제한", "globalAliveLimit", information.rules.globalAliveLimit);
            AddLocalizedInfo("종류별 동시 등장 제한", "perMonsterAliveLimit", information.rules.perMonsterAliveLimit);
            AddLocalizedInfo("보스 필드 지속 시간", "bossFieldDurationSeconds", information.rules.bossFieldDurationSeconds);

            CreateSummarySection(_summaryOverlayContent, "젬스톤 보유량과 공급 상태", new Color(0.38f, 0.78f, 1f, 1f));
            foreach (var gem in information.gemstones)
            {
                CreateSummaryCategory(
                    _summaryOverlayContent,
                    GetGemstoneLocalizedName(gem.id, gem.id) + " · " + gem.id,
                    GetGemstoneColor(GetGemstoneIndexById(gem.id)));
                AddLocalizedInfo("공급 상태", "enabled", gem.enabled ? "해금" : "잠김");
                AddLocalizedInfo("보유 수량", "amount", gem.amount);
            }

            CreateSummarySection(_summaryOverlayContent, "몬스터 정보", new Color(0.54f, 0.88f, 0.50f, 1f));
            foreach (var monster in information.monsters)
            {
                TraitNodeDefinition monsterNode = FindNode(monster.id);
                string monsterTitle = monsterNode == null || string.IsNullOrEmpty(monsterNode.Title)
                    ? monster.id
                    : GetLocalizedNodeTitle(monsterNode);
                CreateSummaryCategory(
                    _summaryOverlayContent,
                    monsterTitle + " · " + monster.id,
                    GetTabAccent(TraitTab.Monster));
                AddLocalizedInfo("해금 상태", "enabled", monster.enabled ? "해금" : "잠김");
                AddLocalizedInfo("생성 수", "productionCount", monster.productionCount);
                AddLocalizedInfo("체력", "hitPoints", monster.hitPoints);
                AddLocalizedInfo("생성 간격(초)", "spawnIntervalSeconds", monster.spawnIntervalSeconds);
                AddLocalizedInfo("처치 가넷 보상", "garnetReward", monster.garnetReward);
                AddLocalizedInfo("추가 젬스톤", "gemstoneId", GetGemstoneLocalizedName(monster.gemstoneId, monster.gemstoneId));
                AddLocalizedInfo("드롭 수량", "gemstoneAmount", monster.gemstoneAmount);
                AddLocalizedInfo("드롭 확률(%)", "gemstoneChancePercent", monster.gemstoneChancePercent);
                AddLocalizedInfo("행동 유형", "behaviorType", GetMonsterBehaviorLabel(monster.behaviorType));
            }

            CreateSummarySection(_summaryOverlayContent, "스킬과 펫 정보", GetTabAccent(TraitTab.Skill));
            foreach (var skill in information.skills)
            {
                TraitNodeDefinition skillNode = FindNode(skill.id);
                string skillTitle = skillNode == null || string.IsNullOrEmpty(skillNode.Title)
                    ? skill.id
                    : GetLocalizedNodeTitle(skillNode);
                CreateSummaryCategory(
                    _summaryOverlayContent,
                    skillTitle + " · " + skill.id,
                    GetTabAccent(TraitTab.Skill));
                AddLocalizedInfo("해금 상태", "enabled", skill.enabled ? "해금" : "잠김");
                AddLocalizedInfo("범위", "radiusWorldUnits", skill.radiusWorldUnits);
                AddLocalizedInfo("피해량", "damage", skill.damage);
                AddLocalizedInfo("재사용 대기 시간(초)", "cooldownSeconds", skill.cooldownSeconds);
            }

            CreateSummarySection(_summaryOverlayContent, "통합 연구 트리 전체 진행도", AvailableColor);
            foreach (TraitNodeDefinition node in EnumerateAllNodes())
            {
                if (node == null) continue;
                NodePurchaseState state = GetNodePurchaseState(node);
                string stateLabel = state == NodePurchaseState.Purchased
                    ? "강화 완료"
                    : state == NodePurchaseState.Available
                        ? "강화 가능"
                        : state == NodePurchaseState.Unaffordable
                            ? "재화 부족"
                            : "잠김";
                TraitGemstoneDefinition currency = GetGemstoneDefinitionById(node.CostGemstoneId);
                string currencyTitle = currency == null
                    ? GetGemstoneLocalizedName(node.CostGemstoneId, node.CostGemstoneId)
                    : GetGemstoneLocalizedName(currency.Id, currency.Title);
                string prerequisite = string.IsNullOrEmpty(node.PrerequisiteNodeId)
                    ? "없음"
                    : GetNodeDisplayName(node.PrerequisiteNodeId);
                AddSummaryRow(
                    _summaryOverlayContent,
                    GetLocalizedNodeTitle(node) + " · " + node.Id,
                    stateLabel + "  |  비용 " + node.Cost.ToString("N0", CultureInfo.InvariantCulture) +
                    " " + currencyTitle + "  |  선행 " + prerequisite +
                    "  |  효과 " + GetLocalizedNodeValueLabel(node));
            }
        }

        private string CreateFullGameDocumentJson(GameInformation information)
        {
            if (_document == null || information == null)
            {
                return _summaryJson ?? string.Empty;
            }

            var purchasedNodeIds = new List<string>();
            foreach (TraitNodeDefinition node in EnumerateAllNodes())
            {
                if (node != null && _purchased.Contains(node.Id))
                {
                    purchasedNodeIds.Add(node.Id);
                }
            }

            var export = new GameDocumentExportSnapshot
            {
                documentVersion = _document.documentVersion,
                locale = _document.locale,
                description = _document.description,
                information = information,
                progression = _document.progression,
                entities = _document.entities,
                purchasedNodeIds = purchasedNodeIds.ToArray()
            };
            return JsonUtility.ToJson(export, true);
        }

        private void AddLocalizedInfo(string koreanLabel, string englishKey, object value)
        {
            string formatted;
            if (value is bool flag)
            {
                formatted = flag ? "예" : "아니요";
            }
            else if (value is float floatValue)
            {
                formatted = floatValue.ToString("0.##", CultureInfo.InvariantCulture);
            }
            else if (value is double doubleValue)
            {
                formatted = doubleValue.ToString("0.##", CultureInfo.InvariantCulture);
            }
            else if (value is IFormattable formattable)
            {
                formatted = formattable.ToString("N0", CultureInfo.InvariantCulture);
            }
            else
            {
                formatted = Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            AddSummaryRow(
                _summaryOverlayContent,
                koreanLabel,
                formatted + "  ·  " + englishKey);
        }

        private string GetNodeDisplayName(string nodeId)
        {
            TraitNodeDefinition node = FindNode(nodeId);
            return node == null || string.IsNullOrEmpty(node.Title)
                ? nodeId
                : GetLocalizedNodeTitle(node);
        }

        private static string GetMonsterBehaviorLabel(MonsterBehaviorType behaviorType)
        {
            switch (behaviorType)
            {
                case MonsterBehaviorType.None:
                default:
                    return "기본 행동";
            }
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
            AddButtonIcon(button.transform as RectTransform, GetTabIcon(tab), new Vector2(16f, 0f));
            button.onClick.AddListener(() => ShowTab(tab));
        }

        private void ShowTab(TraitTab tab, bool animate = true)
        {
            _activeTab = tab;
            _selectedNodeId = null;
            BuildCategories();
            BuildNodes(animate);
            RefreshSummary();
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
            BuildUnifiedBoard(animate);
        }

        private void BuildUnifiedBoard(bool animate)
        {
            if (_nodeContent == null || _nodeViewport == null)
            {
                return;
            }

            bool preserveView = _hasBuiltNodeLayout;
            Vector2 previousPosition = _nodeContent.anchoredPosition;
            float previousZoom = Mathf.Max(0.01f, _nodeContent.localScale.x);
            DestroyChildren(_nodeContent);
            _boardPositionsByNodeId.Clear();

            List<BoardNodeEntry> entries = CollectBoardEntries();
            if (entries.Count == 0)
            {
                _nodeContent.sizeDelta = new Vector2(100f, 100f);
                _nodeContent.anchoredPosition = Vector2.zero;
                _nodeContent.localScale = Vector3.one;
                return;
            }

            List<BoardNodeEntry> ordered = OrderBoardEntries(entries);
            List<Vector2Int> cells = BuildBoardCells(ordered.Count);
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            var occupied = new Dictionary<Vector2Int, BoardNodeEntry>();
            for (int i = 0; i < ordered.Count; i++)
            {
                BoardNodeEntry entry = ordered[i];
                entry.Cell = cells[i];
                entry.Position = new Vector2(
                    entry.Cell.x * BoardNodePitchX,
                    entry.Cell.y * BoardNodePitchY);
                occupied[entry.Cell] = entry;
                _boardPositionsByNodeId[entry.Node.Id] = entry.Position;
                minX = Mathf.Min(minX, entry.Cell.x);
                maxX = Mathf.Max(maxX, entry.Cell.x);
                minY = Mathf.Min(minY, entry.Cell.y);
                maxY = Mathf.Max(maxY, entry.Cell.y);
            }

            _nodeContent.sizeDelta = new Vector2(
                (maxX - minX) * BoardNodePitchX + BoardNodeSize + 120f,
                (maxY - minY) * BoardNodePitchY + BoardNodeSize + 120f);

            // Every visible connection follows the square grid. Drawing each
            // east/north edge once creates a single open network around the
            // center core without diagonal shortcuts between nodes.
            Vector2Int[] edgeDirections =
            {
                Vector2Int.right,
                Vector2Int.up
            };
            for (int i = 0; i < ordered.Count; i++)
            {
                BoardNodeEntry from = ordered[i];
                for (int directionIndex = 0;
                    directionIndex < edgeDirections.Length;
                    directionIndex++)
                {
                    Vector2Int neighborCell = from.Cell + edgeDirections[directionIndex];
                    if (occupied.TryGetValue(neighborCell, out BoardNodeEntry to))
                    {
                        CreateBoardConnection(
                            from.Position,
                            to.Position,
                            GetBoardConnectionColor(from, to));
                    }
                }
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                BoardNodeEntry entry = ordered[i];
                NodePurchaseState state = GetNodePurchaseState(entry.Node);
                Button button = CreateBoardNode(entry, state);
                RectTransform rect = button.transform as RectTransform;
                rect.anchoredPosition = entry.Position;
                TraitNodeDefinition captured = entry.Node;
                button.onClick.AddListener(() => SelectNode(captured));
                TraitUiButtonFeedback feedback = button.GetComponent<TraitUiButtonFeedback>();
                if (feedback != null)
                {
                    feedback.SetScaleFeedback(1.14f, 0.94f, 0.14f);
                    feedback.PointerEntered += data => HandleNodePointerEnter(captured, data);
                    feedback.PointerExited += data => HandleNodePointerExit(captured, data);
                }
            }

            _selectedNodeId = string.IsNullOrEmpty(_tooltipNodeId)
                ? _selectedNodeId
                : _tooltipNodeId;
            UpdateNodeSelectionFrame();
            Canvas.ForceUpdateCanvases();
            if (preserveView)
            {
                _nodeContent.localScale = Vector3.one * previousZoom;
                _nodeContent.anchoredPosition = previousPosition;
            }
            else
            {
                CenterUnifiedGraph();
            }

            _lastBuiltTab = _activeTab;
            _hasBuiltNodeLayout = true;
            if (animate)
            {
                PlayNodeAnimation();
            }

            if (!string.IsNullOrEmpty(_tooltipNodeId))
            {
                RefreshNodeTooltip(FindNode(_tooltipNodeId));
            }
        }

        private List<BoardNodeEntry> CollectBoardEntries()
        {
            var result = new List<BoardNodeEntry>();
            TraitTab[] tabs = { TraitTab.Stat, TraitTab.Skill, TraitTab.Monster };
            int flatIndex = 0;
            for (int tabIndex = 0; tabIndex < tabs.Length; tabIndex++)
            {
                TraitTab tab = tabs[tabIndex];
                IReadOnlyList<TraitCategoryDefinition> categories = GetCategories(tab);
                if (categories == null)
                {
                    continue;
                }

                for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
                {
                    TraitCategoryDefinition category = categories[categoryIndex];
                    if (category == null || category.Nodes == null)
                    {
                        continue;
                    }

                    for (int nodeIndex = 0; nodeIndex < category.Nodes.Count; nodeIndex++)
                    {
                        TraitNodeDefinition node = category.Nodes[nodeIndex];
                        if (node == null || string.IsNullOrEmpty(node.Id))
                        {
                            continue;
                        }

                        result.Add(new BoardNodeEntry
                        {
                            Tab = tab,
                            Category = category,
                            Node = node,
                            CategoryIndex = categoryIndex,
                            NodeIndex = nodeIndex,
                            FlatIndex = flatIndex++
                        });
                    }
                }
            }

            return result;
        }

        private List<BoardNodeEntry> OrderBoardEntries(List<BoardNodeEntry> entries)
        {
            var ordered = new List<BoardNodeEntry>(entries.Count);
            var byId = new Dictionary<string, BoardNodeEntry>(StringComparer.Ordinal);
            var remaining = new List<BoardNodeEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                BoardNodeEntry entry = entries[i];
                if (entry == null || entry.Node == null)
                {
                    continue;
                }

                byId[entry.Node.Id] = entry;
                remaining.Add(entry);
            }

            BoardNodeEntry center = RemoveEntryById(remaining, byId, "stat.attack.01");
            if (center == null)
            {
                center = RemoveEasiestEntry(remaining, false);
            }
            if (center != null)
            {
                ordered.Add(center);
            }

            // These four first stat options occupy the cardinal spokes. The
            // center gates them until the player activates the research core.
            string[] cardinalStartIds =
            {
                "stat.radius.01",
                "stat.cooldown.01",
                "stat.critical.01",
                "stat.fieldDuration.01"
            };
            for (int i = 0; i < cardinalStartIds.Length; i++)
            {
                BoardNodeEntry entry = RemoveEntryById(
                    remaining,
                    byId,
                    cardinalStartIds[i]);
                if (entry == null)
                {
                    entry = RemoveEasiestEntry(remaining, false);
                }
                if (entry != null)
                {
                    ordered.Add(entry);
                }
            }

            while (remaining.Count > 0)
            {
                BoardNodeEntry next = null;
                for (int i = 0; i < remaining.Count; i++)
                {
                    BoardNodeEntry candidate = remaining[i];
                    string prerequisiteId = candidate.Node.PrerequisiteNodeId;
                    bool prerequisitePlaced = string.IsNullOrEmpty(prerequisiteId) ||
                        IsOrderedEntry(ordered, prerequisiteId);
                    if (!prerequisitePlaced)
                    {
                        continue;
                    }

                    if (next == null || CompareBoardDifficulty(candidate, next) < 0)
                    {
                        next = candidate;
                    }
                }

                if (next == null)
                {
                    next = RemoveEasiestEntry(remaining, false);
                }
                else
                {
                    remaining.Remove(next);
                }

                if (next != null)
                {
                    ordered.Add(next);
                }
            }

            return ordered;
        }

        private BoardNodeEntry RemoveEntryById(
            List<BoardNodeEntry> remaining,
            Dictionary<string, BoardNodeEntry> byId,
            string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || !byId.TryGetValue(nodeId, out BoardNodeEntry entry) ||
                !remaining.Remove(entry))
            {
                return null;
            }

            return entry;
        }

        private BoardNodeEntry RemoveEasiestEntry(
            List<BoardNodeEntry> remaining,
            bool startsUnlockedOnly)
        {
            BoardNodeEntry next = null;
            for (int i = 0; i < remaining.Count; i++)
            {
                BoardNodeEntry candidate = remaining[i];
                if (startsUnlockedOnly && !candidate.Node.StartsUnlocked)
                {
                    continue;
                }
                if (next == null || CompareBoardDifficulty(candidate, next) < 0)
                {
                    next = candidate;
                }
            }

            if (next != null)
            {
                remaining.Remove(next);
            }

            return next;
        }

        private bool IsOrderedEntry(List<BoardNodeEntry> ordered, string nodeId)
        {
            for (int i = 0; i < ordered.Count; i++)
            {
                if (string.Equals(ordered[i].Node.Id, nodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private int CompareBoardDifficulty(BoardNodeEntry left, BoardNodeEntry right)
        {
            int leftCurrency = Mathf.Max(0, GetGemstoneIndexById(left.Node.CostGemstoneId));
            int rightCurrency = Mathf.Max(0, GetGemstoneIndexById(right.Node.CostGemstoneId));
            long leftScore = left.Node.StartsUnlocked ? 0L :
                left.Node.RequiredBossTier * 1000000L +
                leftCurrency * 100000L +
                (left.Node.AcquisitionOnly ? 50000L : 0L) +
                left.Node.Cost;
            long rightScore = right.Node.StartsUnlocked ? 0L :
                right.Node.RequiredBossTier * 1000000L +
                rightCurrency * 100000L +
                (right.Node.AcquisitionOnly ? 50000L : 0L) +
                right.Node.Cost;
            int scoreOrder = leftScore.CompareTo(rightScore);
            return scoreOrder != 0
                ? scoreOrder
                : string.Compare(left.Node.Id, right.Node.Id, StringComparison.Ordinal);
        }

        private static List<Vector2Int> BuildBoardCells(int count)
        {
            var cells = new List<Vector2Int>(count);
            if (count <= 0)
            {
                return cells;
            }

            cells.Add(Vector2Int.zero);
            Vector2Int[] spokes =
            {
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };
            for (int i = 0; i < spokes.Length && cells.Count < count; i++)
            {
                cells.Add(spokes[i]);
            }

            var candidates = new List<Vector2Int>();
            for (int y = -12; y <= 12; y++)
            {
                for (int x = -18; x <= 18; x++)
                {
                    Vector2Int candidate = new Vector2Int(x, y);
                    if (!cells.Contains(candidate))
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            candidates.Sort((a, b) =>
            {
                float aRadius = Mathf.Abs(a.x) / 9f + Mathf.Abs(a.y) / 6f;
                float bRadius = Mathf.Abs(b.x) / 9f + Mathf.Abs(b.y) / 6f;
                int radiusOrder = aRadius.CompareTo(bRadius);
                if (radiusOrder != 0) return radiusOrder;
                int manhattanOrder = (Mathf.Abs(a.x) + Mathf.Abs(a.y)).CompareTo(
                    Mathf.Abs(b.x) + Mathf.Abs(b.y));
                if (manhattanOrder != 0) return manhattanOrder;
                int verticalOrder = b.y.CompareTo(a.y);
                return verticalOrder != 0 ? verticalOrder : a.x.CompareTo(b.x);
            });

            for (int i = 0; cells.Count < count && i < candidates.Count; i++)
            {
                cells.Add(candidates[i]);
            }

            return cells;
        }

        private void CreateBoardConnection(Vector2 from, Vector2 to, Color color)
        {
            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= BoardNodeSize)
            {
                return;
            }

            Vector2 direction = delta.normalized;
            Vector2 start = from + direction * (BoardNodeSize * 0.5f);
            Vector2 end = to - direction * (BoardNodeSize * 0.5f);
            Vector2 edge = end - start;
            bool horizontal = Mathf.Abs(edge.x) > Mathf.Abs(edge.y);
            Vector2 size = horizontal
                ? new Vector2(edge.magnitude, 5f)
                : new Vector2(5f, edge.magnitude);
            Vector2 center = (start + end) * 0.5f;
            CreateImageRect(
                "EdgeGlow",
                _nodeContent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                center,
                horizontal ? new Vector2(size.x, 12f) : new Vector2(12f, size.y),
                null,
                new Color(color.r, color.g, color.b, color.a * 0.20f),
                new Vector2(0.5f, 0.5f));
            CreateImageRect(
                "Edge",
                _nodeContent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                center,
                size,
                null,
                color,
                new Vector2(0.5f, 0.5f));
        }

        private Color GetBoardConnectionColor(BoardNodeEntry from, BoardNodeEntry to)
        {
            NodePurchaseState fromState = GetNodePurchaseState(from.Node);
            NodePurchaseState toState = GetNodePurchaseState(to.Node);
            if (fromState == NodePurchaseState.Purchased &&
                toState == NodePurchaseState.Purchased)
            {
                return new Color(0.24f, 0.82f, 0.70f, 0.90f);
            }
            if ((fromState == NodePurchaseState.Purchased &&
                    toState == NodePurchaseState.Available) ||
                (toState == NodePurchaseState.Purchased &&
                    fromState == NodePurchaseState.Available))
            {
                return new Color(1f, 0.71f, 0.30f, 0.96f);
            }
            if (fromState == NodePurchaseState.Locked || toState == NodePurchaseState.Locked)
            {
                return new Color(0.39f, 0.26f, 0.49f, 0.75f);
            }

            return new Color(0.22f, 0.34f, 0.52f, 0.72f);
        }

        private enum NodePurchaseState
        {
            Purchased,
            Available,
            Unaffordable,
            Locked
        }

        private NodePurchaseState GetNodePurchaseState(TraitNodeDefinition node)
        {
            if (node == null)
            {
                return NodePurchaseState.Locked;
            }
            if (!string.Equals(node.Id, ResearchCoreNodeId, StringComparison.Ordinal) &&
                !testModeUnlockAll && !_purchased.Contains(ResearchCoreNodeId))
            {
                return NodePurchaseState.Locked;
            }
            if (IsPurchased(node))
            {
                return NodePurchaseState.Purchased;
            }
            if (node.AcquisitionOnly ||
                (!testModeUnlockAll && node.RequiredBossTier > unlockedBossTier) ||
                (!testModeUnlockAll && !IsPrerequisiteMet(node)))
            {
                return NodePurchaseState.Locked;
            }

            TraitGemstoneDefinition gemstone = GetGemstoneDefinitionById(node.CostGemstoneId);
            bool currencyUnlocked = testModeUnlockAll || node.Cost <= 0 ||
                IsGemstoneAvailable(gemstone);
            bool hasCurrency = currencyUnlocked &&
                (testModeFreeUpgrades || node.Cost <= 0 ||
                    GetNodeBalance(node) >= node.Cost);
            return hasCurrency
                ? NodePurchaseState.Available
                : NodePurchaseState.Unaffordable;
        }

        private static Color GetBoardStateColor(NodePurchaseState state)
        {
            switch (state)
            {
                case NodePurchaseState.Purchased:
                    return PurchasedColor;
                case NodePurchaseState.Available:
                    return new Color(1f, 0.75f, 0.30f, 1f);
                case NodePurchaseState.Unaffordable:
                    return new Color(1f, 0.42f, 0.36f, 1f);
                default:
                    return new Color(0.91f, 0.37f, 0.74f, 1f);
            }
        }

        private static Color GetCategoryAccent(TraitTab tab)
        {
            switch (tab)
            {
                case TraitTab.Skill:
                    return new Color(1f, 0.55f, 0.27f, 1f);
                case TraitTab.Monster:
                    return new Color(0.75f, 0.53f, 1f, 1f);
                default:
                    return new Color(0.34f, 0.78f, 1f, 1f);
            }
        }

        private static Color GetBoardNodeColor(
            BoardNodeEntry entry,
            NodePurchaseState state)
        {
            if (state == NodePurchaseState.Purchased)
            {
                return PurchasedColor;
            }

            Color category = GetCategoryAccent(entry == null ? TraitTab.Stat : entry.Tab);
            switch (state)
            {
                case NodePurchaseState.Available:
                    return category;
                case NodePurchaseState.Unaffordable:
                    return Color.Lerp(category, new Color(1f, 0.31f, 0.34f, 1f), 0.58f);
                default:
                    return Color.Lerp(category, new Color(0.24f, 0.27f, 0.36f, 1f), 0.68f);
            }
        }

        private Button CreateBoardNode(BoardNodeEntry entry, NodePurchaseState state)
        {
            Color stateColor = GetBoardNodeColor(entry, state);
            bool purchased = state == NodePurchaseState.Purchased;
            bool available = state == NodePurchaseState.Available;
            bool locked = state == NodePurchaseState.Locked ||
                state == NodePurchaseState.Unaffordable;
            bool isCore = string.Equals(
                entry.Node.Id,
                "stat.attack.01",
                StringComparison.Ordinal);
            Color fill = purchased
                ? new Color(0.14f, 0.16f, 0.20f, 0.98f)
                : available
                    ? Color.Lerp(new Color(0.025f, 0.035f, 0.055f, 0.98f), stateColor, 0.22f)
                    : state == NodePurchaseState.Unaffordable
                        ? Color.Lerp(new Color(0.035f, 0.025f, 0.045f, 0.98f), stateColor, 0.12f)
                        : Color.Lerp(new Color(0.025f, 0.03f, 0.05f, 0.98f), stateColor, 0.10f);

            Button button = CreateButton(
                "Node_" + entry.Node.Id,
                _nodeContent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                entry.Position,
                new Vector2(BoardNodeSize, BoardNodeSize),
                null,
                fill,
                string.Empty,
                12,
                inkColor);
            Image buttonImage = button.GetComponent<Image>();
            buttonImage.raycastTarget = true;

            RectTransform buttonRect = button.transform as RectTransform;
            CreateSquareNodeFrame(
                buttonRect,
                new Color(stateColor.r, stateColor.g, stateColor.b, 0.17f),
                isCore ? 11f : 9f);
            CreateSquareNodeFrame(buttonRect, stateColor, isCore ? 5f : 4.5f);
            RectTransform selectionFrame = CreateSquareNodeSelectionFrame(buttonRect);
            selectionFrame.gameObject.SetActive(
                string.Equals(entry.Node.Id, _selectedNodeId, StringComparison.Ordinal));

            Sprite icon = GetNodeIcon(
                entry.Tab,
                entry.Category,
                entry.Node,
                entry.CategoryIndex,
                entry.FlatIndex);
            if (icon != null)
            {
                float iconSize = isCore ? 34f : 37f;
                RectTransform iconRect = CreateImageRect(
                    "Icon",
                    buttonRect,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, isCore ? 5f : 1f),
                    new Vector2(iconSize, iconSize),
                    icon,
                    locked ? new Color(0.80f, 0.82f, 0.92f, 0.60f) : Color.white,
                    new Vector2(0.5f, 0.5f));
                iconRect.GetComponent<Image>().preserveAspect = true;
            }

            string marker = isCore
                ? "CORE"
                : entry.Tab == TraitTab.Monster
                    ? "M"
                    : entry.Category != null && entry.Category.Id == "skill.cursorAura"
                        ? "PET"
                        : entry.Tab == TraitTab.Skill
                            ? "SK"
                            : GetNodeRankLabel(entry.Node);
            if (!string.IsNullOrEmpty(marker))
            {
                CreateText(
                    CreateRect(
                        "NodeMarker",
                        buttonRect,
                        new Vector2(0f, 1f),
                        new Vector2(0f, 1f),
                        new Vector2(7f, -5f),
                        new Vector2(31f, 15f),
                        new Vector2(0f, 1f)),
                    marker,
                    isCore ? 8 : 8,
                    TextAnchor.UpperLeft,
                    new Color(stateColor.r, stateColor.g, stateColor.b, 0.96f),
                    true);
            }

            if (isCore)
            {
                CreateText(
                    CreateRect(
                        "CoreLabel",
                        buttonRect,
                        new Vector2(0f, 0f),
                        new Vector2(1f, 0f),
                        new Vector2(0f, 5f),
                        new Vector2(-8f, 15f),
                        new Vector2(0.5f, 0f)),
                    "시작점",
                    9,
                    TextAnchor.MiddleCenter,
                    new Color(1f, 0.89f, 0.62f, 1f),
                    true);
            }

            Sprite stateIcon = purchased ? checkIcon : locked ? lockIcon : null;
            if (stateIcon != null)
            {
                RectTransform badge = CreateImageRect(
                    "StateBadge",
                    buttonRect,
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(-8f, -8f),
                    new Vector2(18f, 18f),
                    stateIcon,
                    Color.white,
                    new Vector2(0.5f, 0.5f));
                badge.GetComponent<Image>().preserveAspect = true;
            }
            else if (available)
            {
                RectTransform plus = CreateRect(
                    "UpgradeMark",
                    buttonRect,
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(-9f, -9f),
                    new Vector2(21f, 21f),
                    new Vector2(0.5f, 0.5f));
                Image plusBack = plus.gameObject.AddComponent<Image>();
                plusBack.color = new Color(0.08f, 0.07f, 0.09f, 1f);
                plusBack.raycastTarget = false;
                CreateText(
                    CreateRect(
                        "UpgradeMarkLabel",
                        plus,
                        Vector2.zero,
                        Vector2.one,
                        Vector2.zero,
                        Vector2.zero,
                        new Vector2(0.5f, 0.5f)),
                    "+",
                    15,
                    TextAnchor.MiddleCenter,
                    stateColor,
                    true);
            }

            if (isCore)
            {
                CreateSquareNodeFrame(
                    buttonRect,
                    new Color(1f, 0.85f, 0.48f, 0.28f),
                    1.5f);
            }

            return button;
        }

        private void BuildStatTreeNodes(bool animate)
        {
            if (_nodeContent == null)
            {
                return;
            }

            bool preserveView = _hasBuiltNodeLayout && _lastBuiltTab == TraitTab.Stat;
            Vector2 previousPosition = _nodeContent.anchoredPosition;
            float previousZoom = _nodeContent.localScale.x;
            DestroyChildren(_nodeContent);

            IReadOnlyList<TraitCategoryDefinition> categories = GetCategories(TraitTab.Stat);
            if (categories == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            _nodeHeading.text = LocalizationCatalog.Get(
                "ui.graph.stats",
                "스탯 연구 트리");
            _nodeContent.sizeDelta = new Vector2(StatGraphWidth, StatGraphHeight);

            Vector2 cellSize = GetSafeNodeCellSize();
            Vector2 spacing = GetSafeNodeGridSpacing();
            float pitchX = cellSize.x + spacing.x;
            float pitchY = cellSize.y + spacing.y;
            float hubSize = Mathf.Max(cellSize.x, cellSize.y) + 10f;
            float rootY = hubSize * 0.5f + cellSize.y * 0.5f + 30f;
            int topCount = (categories.Count + 1) / 2;
            int bottomCount = categories.Count - topCount;
            float clusterStride = pitchX * 2f + cellSize.x + 54f;

            var positions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            var allNodes = new List<TraitNodeDefinition>();
            var categoryRoots = new List<Vector2>(categories.Count);
            var categoryRootNodes = new List<TraitNodeDefinition>(categories.Count);
            var categoryCenters = new List<float>(categories.Count);
            var categoryIsTop = new List<bool>(categories.Count);
            TraitNodeDefinition selected = null;
            Vector2Int[] path =
            {
                new Vector2Int(1, 0), new Vector2Int(2, 0),
                new Vector2Int(2, 1), new Vector2Int(1, 1), new Vector2Int(0, 1),
                new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2),
                new Vector2Int(2, 3), new Vector2Int(1, 3), new Vector2Int(0, 3)
            };

            for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
            {
                TraitCategoryDefinition category = categories[categoryIndex];
                if (category == null || category.Nodes == null || category.Nodes.Count == 0)
                {
                    continue;
                }

                bool isTop = categoryIndex < topCount;
                int columnIndex = isTop ? categoryIndex : categoryIndex - topCount;
                int columnCount = isTop ? topCount : bottomCount;
                float centerX = (columnIndex - (columnCount - 1) * 0.5f) * clusterStride;
                float branchY = isTop ? rootY : -rootY;
                categoryCenters.Add(centerX);
                categoryIsTop.Add(isTop);
                categoryRoots.Add(new Vector2(centerX, branchY));
                categoryRootNodes.Add(category.Nodes[0]);

                for (int nodeIndex = 0; nodeIndex < category.Nodes.Count; nodeIndex++)
                {
                    TraitNodeDefinition node = category.Nodes[nodeIndex];
                    if (node == null)
                    {
                        continue;
                    }

                    Vector2Int gridCell = category.Id == "skill.cursorAura" &&
                        nodeIndex < 4
                        ? GetPetStatTreeCell(nodeIndex)
                        : nodeIndex < path.Length
                            ? path[nodeIndex]
                            : GetExtendedStatTreeCell(nodeIndex);
                    float x = centerX + (gridCell.x - 1) * pitchX;
                    float y = branchY + (isTop ? 1f : -1f) * gridCell.y * pitchY;
                    Vector2 position = new Vector2(x, y);
                    positions[node.Id] = position;
                    allNodes.Add(node);

                    bool purchased = IsPurchased(node);
                    bool locked = !testModeUnlockAll &&
                        (node.RequiredBossTier > unlockedBossTier || !IsPrerequisiteMet(node));
                    Button button = CreateNodeButton(
                        _nodeContent,
                        category,
                        node,
                        categoryIndex,
                        nodeIndex,
                        purchased,
                        locked,
                        !purchased && !locked && !node.AcquisitionOnly);
                    RectTransform rect = (RectTransform)button.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = WorldToGraphPosition(position);
                    TraitNodeDefinition captured = node;
                    button.onClick.AddListener(() => SelectNode(captured));

                    if (selected == null || node.Id == _selectedNodeId)
                    {
                        selected = node;
                    }
                }
            }

            float minTrunkX = float.MaxValue;
            float maxTrunkX = float.MinValue;
            for (int i = 0; i < categoryRoots.Count; i++)
            {
                minTrunkX = Mathf.Min(minTrunkX, categoryRoots[i].x);
                maxTrunkX = Mathf.Max(maxTrunkX, categoryRoots[i].x);
            }

            Color trunkColor = new Color(0.86f, 0.59f, 0.25f, 0.96f);
            CreateTreeLine(new Vector2(minTrunkX, 0f), new Vector2(maxTrunkX, 0f), 3.5f, trunkColor);

            for (int i = 0; i < categoryRoots.Count; i++)
            {
                Vector2 root = categoryRoots[i];
                TraitNodeDefinition rootNode = categoryRootNodes[i];
                bool isTop = categoryIsTop[i];
                Color rootColor = GetTreeEdgeColor(rootNode);
                Vector2 branchStart = new Vector2(root.x, 0f);
                if (Mathf.Abs(root.x) < 0.01f)
                {
                    branchStart.y = isTop ? hubSize * 0.5f : -hubSize * 0.5f;
                }

                Vector2 branchEnd = new Vector2(
                    root.x,
                    root.y - Mathf.Sign(root.y) * cellSize.y * 0.5f);
                CreateTreeLine(branchStart, branchEnd, 3f, rootColor);
            }

            foreach (TraitNodeDefinition node in allNodes)
            {
                if (node == null || string.IsNullOrEmpty(node.PrerequisiteNodeId) ||
                    !positions.TryGetValue(node.PrerequisiteNodeId, out Vector2 from) ||
                    !positions.TryGetValue(node.Id, out Vector2 to))
                {
                    continue;
                }

                CreateTreeNodeLink(from, to, cellSize, GetTreeEdgeColor(node));
            }

            for (int i = 0; i < categories.Count; i++)
            {
                TraitCategoryDefinition category = categories[i];
                if (category == null || i >= categoryCenters.Count)
                {
                    continue;
                }

                bool isTop = categoryIsTop[i];
                float labelY = categoryRoots[i].y + (isTop ? 1f : -1f) *
                    (pitchY * 3f + cellSize.y * 0.5f + 44f);
                Vector2 labelPosition = WorldToGraphPosition(
                    new Vector2(categoryCenters[i], labelY));
                RectTransform labelRect = CreateRect(
                    "Branch_" + category.Id,
                    _nodeContent,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    labelPosition,
                    new Vector2(300f, 30f),
                    new Vector2(0.5f, 0.5f));
                CreateLocalizedText(
                    labelRect,
                    GetCategoryTitleKey(category),
                    GetCategoryTitle(category),
                    15,
                    TextAnchor.MiddleCenter,
                    category.Accent,
                    true);

                int purchasedCount = CountPurchased(category.Id);
                RectTransform countRect = CreateRect(
                    "BranchProgress_" + category.Id,
                    _nodeContent,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    WorldToGraphPosition(new Vector2(categoryCenters[i], labelY - 22f)),
                    new Vector2(150f, 20f),
                    new Vector2(0.5f, 0.5f));
                CreateText(
                    countRect,
                    purchasedCount.ToString() + " / " + category.Nodes.Count,
                    11,
                    TextAnchor.MiddleCenter,
                    mutedInkColor,
                    false);
            }

            CreateStatTreeHub(hubSize);
            if (selected != null)
            {
                _selectedNodeId = selected.Id;
                UpdateNodeSelectionFrame();
            }
            RefreshDetail(selected);

            if (preserveView)
            {
                _nodeContent.localScale = Vector3.one * previousZoom;
                _nodeContent.anchoredPosition = previousPosition;
            }
            else
            {
                CenterStatGraph(StatGraphDefaultZoom);
            }

            _lastBuiltTab = TraitTab.Stat;
            _hasBuiltNodeLayout = true;
            if (animate)
            {
                PlayNodeAnimation();
            }
        }

        private void BuildLinearNodes(bool animate)
        {
            if (_nodeContent == null) return;
            bool preserveView = _hasBuiltNodeLayout && _lastBuiltTab == _activeTab;
            Vector2 previousPosition = _nodeContent.anchoredPosition;
            float previousZoom = _nodeContent.localScale.x;
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
                CreateText(title, GetCategoryTitle(category), 16, TextAnchor.MiddleLeft, category.Accent, true);
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
            if (preserveView)
            {
                _nodeContent.localScale = Vector3.one * previousZoom;
                _nodeContent.anchoredPosition = previousPosition;
            }
            else
            {
                _nodeContent.localScale = Vector3.one;
                _nodeContent.anchoredPosition = Vector2.zero;
            }
            RefreshDetail(selected);
            _lastBuiltTab = _activeTab;
            _hasBuiltNodeLayout = true;
            if (animate) PlayNodeAnimation();
        }

        private Vector2 WorldToGraphPosition(Vector2 worldPosition)
        {
            return new Vector2(
                StatGraphWidth * 0.5f + worldPosition.x,
                -(StatGraphHeight * 0.5f + worldPosition.y));
        }

        private static Vector2Int GetExtendedStatTreeCell(int nodeIndex)
        {
            int extraIndex = nodeIndex - 11;
            int row = 4 + extraIndex / 3;
            int column = extraIndex % 3;
            if ((row & 1) == 1)
            {
                column = 2 - column;
            }

            return new Vector2Int(column, row);
        }

        private static Vector2Int GetPetStatTreeCell(int nodeIndex)
        {
            // Keep the three independent pet upgrades on direct cardinal
            // spokes from the companion unlock. This avoids diagonal elbows
            // crossing another square node in the compact tree layout.
            switch (nodeIndex)
            {
                case 0: return new Vector2Int(1, 1);
                case 1: return new Vector2Int(2, 1);
                case 2: return new Vector2Int(0, 1);
                default: return new Vector2Int(1, 2);
            }
        }

        private void CreateTreeNodeLink(
            Vector2 from,
            Vector2 to,
            Vector2 cellSize,
            Color color)
        {
            Vector2 delta = to - from;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                float direction = Mathf.Sign(delta.x);
                from.x += direction * cellSize.x * 0.5f;
                to.x -= direction * cellSize.x * 0.5f;
            }
            else
            {
                float direction = Mathf.Sign(delta.y);
                from.y += direction * cellSize.y * 0.5f;
                to.y -= direction * cellSize.y * 0.5f;
            }

            CreateTreeLine(from, to, 3f, color);
        }

        private void CreateTreeLine(Vector2 fromWorld, Vector2 toWorld, float thickness, Color color)
        {
            Vector2 from = WorldToGraphPosition(fromWorld);
            Vector2 to = WorldToGraphPosition(toWorld);
            if (Mathf.Abs(from.x - to.x) < 0.01f || Mathf.Abs(from.y - to.y) < 0.01f)
            {
                CreateTreeSegment(from, to, thickness, color);
                return;
            }

            Vector2 elbow = new Vector2(to.x, from.y);
            CreateTreeSegment(from, elbow, thickness, color);
            CreateTreeSegment(elbow, to, thickness, color);
        }

        private void CreateTreeSegment(Vector2 from, Vector2 to, float thickness, Color color)
        {
            bool horizontal = Mathf.Abs(from.y - to.y) < 0.01f;
            float length = horizontal
                ? Mathf.Abs(to.x - from.x)
                : Mathf.Abs(to.y - from.y);
            if (length < 1f)
            {
                return;
            }

            Vector2 center = (from + to) * 0.5f;
            Vector2 mainSize = horizontal
                ? new Vector2(length, thickness)
                : new Vector2(thickness, length);
            Vector2 shadowSize = horizontal
                ? new Vector2(length, thickness + 5f)
                : new Vector2(thickness + 5f, length);
            Color shadowColor = new Color(0.006f, 0.012f, 0.035f, 0.92f);
            CreateImageRect(
                "TreeLineShadow",
                _nodeContent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                center,
                shadowSize,
                null,
                shadowColor,
                new Vector2(0.5f, 0.5f));
            CreateImageRect(
                "TreeLine",
                _nodeContent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                center,
                mainSize,
                null,
                color,
                new Vector2(0.5f, 0.5f));
        }

        private Color GetTreeEdgeColor(TraitNodeDefinition node)
        {
            if (node == null)
            {
                return new Color(0.32f, 0.38f, 0.53f, 0.84f);
            }

            if (IsPurchased(node))
            {
                return PurchasedColor;
            }

            bool locked = !testModeUnlockAll &&
                (node.RequiredBossTier > unlockedBossTier || !IsPrerequisiteMet(node));
            if (locked)
            {
                return new Color(0.43f, 0.29f, 0.48f, 0.86f);
            }

            return AvailableColor;
        }

        private void CreateStatTreeHub(float size)
        {
            RectTransform hub = CreateRect(
                "GrowthCore",
                _nodeContent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                WorldToGraphPosition(Vector2.zero),
                new Vector2(size, size),
                new Vector2(0.5f, 0.5f));
            Image background = hub.gameObject.AddComponent<Image>();
            background.color = new Color(0.035f, 0.055f, 0.11f, 1f);
            background.raycastTarget = false;
            CreateSquareNodeFrame(hub, new Color(0.96f, 0.70f, 0.32f, 1f), 3.5f);

            if (attackIcon != null)
            {
                RectTransform icon = CreateImageRect(
                    "CoreIcon",
                    hub,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 8f),
                    new Vector2(34f, 34f),
                    attackIcon,
                    Color.white,
                    new Vector2(0.5f, 0.5f));
                icon.GetComponent<Image>().preserveAspect = true;
            }

            CreateText(
                CreateRect(
                    "CoreLabel",
                    hub,
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 10f),
                    new Vector2(-8f, 18f),
                    new Vector2(0.5f, 0f)),
                "성장 코어",
                10,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.84f, 0.59f, 1f),
                true);
        }

        private void CenterStatGraph(float zoom)
        {
            CenterUnifiedGraph(zoom);
        }

        private void CenterCurrentGraph()
        {
            CenterUnifiedGraph();
        }

        private void CenterUnifiedGraph(float requestedZoom = -1f)
        {
            if (_nodeContent == null || _nodeViewport == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            float fitZoom = Mathf.Min(
                (_nodeViewport.rect.width - 120f) / Mathf.Max(1f, _nodeContent.sizeDelta.x),
                (_nodeViewport.rect.height - 112f) / Mathf.Max(1f, _nodeContent.sizeDelta.y));
            float zoom = requestedZoom > 0f
                ? Mathf.Clamp(requestedZoom, MinimumGraphZoom, 1.25f)
                : Mathf.Clamp(fitZoom, MinimumGraphZoom, 1.05f);
            _nodeContent.localScale = Vector3.one * zoom;
            _nodeContent.anchoredPosition = Vector2.zero;
        }

        private IEnumerator FitGraphAfterActivation()
        {
            // The canvas can still report its previous or zero size in Awake.
            // Fit after layout has been resolved so a saved zoom from another
            // visit cannot leave most of the unified board outside the view.
            yield return null;
            Canvas.ForceUpdateCanvases();
            CenterUnifiedGraph();
            _fitGraphRoutine = null;
        }

        private void SetGraphZoom(float zoom)
        {
            if (_nodeContent == null || _nodeViewport == null)
            {
                return;
            }

            Vector2 viewportCenter = Vector2.zero;
            float oldZoom = Mathf.Max(0.01f, _nodeContent.localScale.x);
            Vector2 contentPointAtCenter = (viewportCenter - _nodeContent.anchoredPosition) / oldZoom;
            float safeZoom = Mathf.Clamp(zoom, MinimumGraphZoom, 1.25f);
            _nodeContent.localScale = Vector3.one * safeZoom;
            _nodeContent.anchoredPosition = viewportCenter - contentPointAtCenter * safeZoom;
        }

        internal void ZoomGraphAt(Vector2 screenPosition, float wheelDelta)
        {
            if (_nodeContent == null || _nodeViewport == null || Mathf.Approximately(wheelDelta, 0f))
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _nodeViewport,
                    screenPosition,
                    GetUiCamera(),
                    out Vector2 viewportPoint))
            {
                return;
            }

            float previousZoom = Mathf.Max(0.01f, _nodeContent.localScale.x);
            Vector2 contentPoint =
                (viewportPoint - _nodeContent.anchoredPosition) / previousZoom;
            float nextZoom = Mathf.Clamp(
                previousZoom * Mathf.Pow(1.12f, wheelDelta),
                MinimumGraphZoom,
                1.35f);
            _nodeContent.localScale = Vector3.one * nextZoom;
            _nodeContent.anchoredPosition = viewportPoint - contentPoint * nextZoom;
        }

        internal void SetGraphDragCursor(bool dragging)
        {
            if (dragging)
            {
                if (_dragHandCursor == null)
                {
                    _dragHandCursor = CreateDragHandCursor();
                }
                Cursor.SetCursor(_dragHandCursor, new Vector2(7f, 7f), CursorMode.Auto);
            }
            else
            {
                RestoreDragCursor();
            }
        }

        private void RestoreDragCursor()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        private static Texture2D CreateDragHandCursor()
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "TraitGraphDragHandCursor";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color outline = new Color(0.025f, 0.055f, 0.10f, 1f);
            Color fill = new Color(0.96f, 0.98f, 1f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!IsDragHandPixel(x, y))
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    bool edge = false;
                    for (int offsetY = -1; offsetY <= 1 && !edge; offsetY++)
                    {
                        for (int offsetX = -1; offsetX <= 1; offsetX++)
                        {
                            if (!IsDragHandPixel(x + offsetX, y + offsetY))
                            {
                                edge = true;
                                break;
                            }
                        }
                    }
                    texture.SetPixel(x, y, edge ? outline : fill);
                }
            }

            texture.Apply();
            return texture;
        }

        private static bool IsDragHandPixel(int x, int y)
        {
            bool fingers =
                (x >= 8 && x <= 12 && y >= 13 && y <= 27) ||
                (x >= 13 && x <= 17 && y >= 15 && y <= 29) ||
                (x >= 18 && x <= 21 && y >= 13 && y <= 26) ||
                (x >= 22 && x <= 25 && y >= 10 && y <= 21);
            bool palm = x >= 8 && x <= 24 && y >= 5 && y <= 17;
            bool thumb = x >= 3 && x <= 10 && y >= 10 && y <= 15;
            return fingers || palm || thumb;
        }

        private static string GetCategoryTitle(TraitCategoryDefinition category)
        {
            if (category == null)
            {
                return string.Empty;
            }

            switch (category.Id)
            {
                case "stat.attack": return LocalizationCatalog.Get("ui.attack", "공격력");
                case "stat.radius": return LocalizationCatalog.Get("ui.radius", "자동 반경");
                case "stat.cooldown": return LocalizationCatalog.Get("ui.cooldown", "쿨타임");
                case "stat.critical": return LocalizationCatalog.Get("ui.critical", "치명타");
                case "stat.boss": return LocalizationCatalog.Get("ui.boss", "보스 피해");
                case "stat.fieldDuration": return LocalizationCatalog.Get("ui.category.fieldDuration", "필드 지속 시간");
                case "skill.cursorAura": return LocalizationCatalog.Get("ui.category.petAura", "펫 동료");
                default: return GetLocalizedEntityName(category.Id, category.Title);
            }
        }

        private static string GetCategoryTitleKey(TraitCategoryDefinition category)
        {
            if (category == null)
            {
                return string.Empty;
            }

            switch (category.Id)
            {
                case "stat.attack": return "ui.attack";
                case "stat.radius": return "ui.radius";
                case "stat.cooldown": return "ui.cooldown";
                case "stat.critical": return "ui.critical";
                case "stat.boss": return "ui.boss";
                case "stat.fieldDuration": return "ui.category.fieldDuration";
                case "skill.cursorAura": return "ui.category.petAura";
                default: return string.Empty;
            }
        }

        private Sprite GetTabIcon(TraitTab tab)
        {
            switch (tab)
            {
                case TraitTab.Skill:
                    return skillIcon != null ? skillIcon : attackIcon;
                case TraitTab.Monster:
                    return monsterIcon != null ? monsterIcon : bossIcon;
                default:
                    return attackIcon;
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
                3.5f);

            bool compactTreeNode = _activeTab == TraitTab.Stat;
            RectTransform buttonRect = button.transform as RectTransform;
            RectTransform selectionFrame = CreateSquareNodeSelectionFrame(buttonRect);
            selectionFrame.gameObject.SetActive(node != null && node.Id == _selectedNodeId);

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
                float iconSize = compactTreeNode ? GetSafeNodeCellSize().x * 0.46f : 30f;
                Vector2 iconPosition = compactTreeNode
                    ? new Vector2(0f, -2f)
                    : new Vector2(0f, -23f);
                Vector2 iconAnchor = compactTreeNode
                    ? new Vector2(0.5f, 0.5f)
                    : new Vector2(0.5f, 1f);
                RectTransform iconRect = CreateImageRect(
                    "Icon",
                    buttonRect,
                    iconAnchor,
                    iconAnchor,
                    iconPosition,
                    new Vector2(iconSize, iconSize),
                    icon,
                    purchased ? Color.white : new Color(1f, 1f, 1f, locked ? 0.48f : 0.92f),
                    new Vector2(0.5f, 0.5f));
                iconRect.GetComponent<Image>().preserveAspect = true;
            }

            if (compactTreeNode)
            {
                string rank = GetNodeRankLabel(node);
                CreateText(
                    CreateRect(
                        "Rank",
                        buttonRect,
                        new Vector2(0f, 1f),
                        new Vector2(0f, 1f),
                        new Vector2(8f, -7f),
                        new Vector2(24f, 16f),
                        new Vector2(0f, 1f)),
                    rank,
                    9,
                    TextAnchor.UpperLeft,
                    locked ? new Color(0.60f, 0.59f, 0.72f, 1f) : category.Accent,
                    true);
            }
            else
            {
                CreateText(
                    CreateRect("Title", buttonRect, Vector2.zero, Vector2.one, new Vector2(0f, -4f), new Vector2(-12f, -56f), new Vector2(0.5f, 0.5f)),
                    node.Title,
                    11,
                    TextAnchor.MiddleCenter,
                    locked ? new Color(0.63f, 0.72f, 0.84f, 1f) : inkColor,
                    true);
                CreateText(
                    CreateRect("Value", buttonRect, Vector2.zero, Vector2.one, new Vector2(0f, -29f), new Vector2(-12f, -77f), new Vector2(0.5f, 0.5f)),
                    node.ValueLabel,
                    11,
                    TextAnchor.MiddleCenter,
                    locked ? new Color(0.50f, 0.62f, 0.76f, 1f) : category.Accent,
                    true);
            }

            if (purchased && checkIcon != null)
            {
                RectTransform check = CreateImageRect(
                    "Check",
                    button.transform as RectTransform,
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(compactTreeNode ? -10f : -14f, compactTreeNode ? -10f : -14f),
                    new Vector2(compactTreeNode ? 15f : 20f, compactTreeNode ? 15f : 20f),
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
                    new Vector2(compactTreeNode ? -10f : -14f, compactTreeNode ? -10f : -14f),
                    new Vector2(compactTreeNode ? 15f : 20f, compactTreeNode ? 15f : 20f),
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

        private RectTransform CreateSquareNodeSelectionFrame(RectTransform parent)
        {
            RectTransform frame = CreateRect(
                "SelectionFrame",
                parent,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(-12f, -12f),
                new Vector2(0.5f, 0.5f));
            Color color = new Color(1f, 0.91f, 0.68f, 0.96f);
            const float thickness = 1.5f;
            CreateImageRect("SelectTop", frame, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -thickness * 0.5f), new Vector2(0f, thickness), null, color, new Vector2(0.5f, 0.5f));
            CreateImageRect("SelectBottom", frame, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, thickness * 0.5f), new Vector2(0f, thickness), null, color, new Vector2(0.5f, 0.5f));
            CreateImageRect("SelectLeft", frame, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(thickness * 0.5f, 0f), new Vector2(thickness, 0f), null, color, new Vector2(0.5f, 0.5f));
            CreateImageRect("SelectRight", frame, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-thickness * 0.5f, 0f), new Vector2(thickness, 0f), null, color, new Vector2(0.5f, 0.5f));
            return frame;
        }

        private static string GetNodeRankLabel(TraitNodeDefinition node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id))
            {
                return string.Empty;
            }

            int separator = node.Id.LastIndexOf('.');
            if (separator < 0 || separator >= node.Id.Length - 1)
            {
                return string.Empty;
            }

            string suffix = node.Id.Substring(separator + 1);
            return int.TryParse(suffix, out int rank) && rank > 0
                ? rank.ToString("D2", CultureInfo.InvariantCulture)
                : string.Empty;
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
                return new Color(0.32f, 0.88f, 0.70f, 0.98f);
            }

            if (locked)
            {
                return new Color(0.64f, 0.32f, 0.57f, 0.94f);
            }

            if (available)
            {
                return isMonsterProduction
                    ? new Color(1f, 0.64f, 0.25f, 0.98f)
                    : new Color(1f, 0.76f, 0.35f, 0.98f);
            }

            return new Color(0.42f, 0.29f, 0.48f, 0.86f);
        }

        private void SelectNode(TraitNodeDefinition node)
        {
            if (node == null)
            {
                return;
            }

            _tooltipPinned = true;
            _selectedNodeId = node.Id;
            UpdateNodeSelectionFrame();
            RectTransform nodeRect = _nodeContent == null
                ? null
                : _nodeContent.Find("Node_" + node.Id) as RectTransform;
            Vector2 screenPosition = nodeRect == null
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : RectTransformUtility.WorldToScreenPoint(
                    GetUiCamera(),
                    nodeRect.position);
            ShowNodeTooltip(node, screenPosition);
        }

        private Camera GetUiCamera()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
        }

        private void HandleNodePointerEnter(
            TraitNodeDefinition node,
            PointerEventData eventData)
        {
            if (node == null)
            {
                return;
            }

            if (!_tooltipPinned || _tooltipNodeId != node.Id)
            {
                _tooltipPinned = false;
            }
            if (_tooltipHideRoutine != null)
            {
                StopCoroutine(_tooltipHideRoutine);
                _tooltipHideRoutine = null;
            }

            _selectedNodeId = node.Id;
            UpdateNodeSelectionFrame();
            ShowNodeTooltip(node, eventData == null ? Input.mousePosition : eventData.position);
        }

        private void HandleNodePointerExit(
            TraitNodeDefinition node,
            PointerEventData eventData)
        {
            ScheduleNodeTooltipHide();
        }

        internal void HandleTooltipPointerEnter()
        {
            _pointerOverTooltip = true;
            if (_tooltipHideRoutine != null)
            {
                StopCoroutine(_tooltipHideRoutine);
                _tooltipHideRoutine = null;
            }
        }

        internal void HandleTooltipPointerExit()
        {
            _pointerOverTooltip = false;
            ScheduleNodeTooltipHide();
        }

        private void ScheduleNodeTooltipHide()
        {
            if (_tooltipRoot == null || !_tooltipRoot.gameObject.activeSelf ||
                _pointerOverTooltip || _tooltipPinned)
            {
                return;
            }

            if (_tooltipHideRoutine != null)
            {
                StopCoroutine(_tooltipHideRoutine);
            }
            _tooltipHideRoutine = StartCoroutine(HideTooltipAfterPointerLeaves());
        }

        private IEnumerator HideTooltipAfterPointerLeaves()
        {
            yield return new WaitForSecondsRealtime(0.55f);
            _tooltipHideRoutine = null;
            if (!_pointerOverTooltip && !_tooltipPinned)
            {
                HideNodeTooltip();
            }
        }

        private void ShowNodeTooltip(TraitNodeDefinition node, Vector2 screenPosition)
        {
            if (node == null || _tooltipRoot == null)
            {
                return;
            }

            bool animate = !_tooltipRoot.gameObject.activeSelf || _tooltipNodeId != node.Id;
            _tooltipNodeId = node.Id;
            if (_tooltipHideRoutine != null)
            {
                StopCoroutine(_tooltipHideRoutine);
                _tooltipHideRoutine = null;
            }

            _tooltipRoot.gameObject.SetActive(true);
            PositionNodeTooltip(screenPosition);
            RefreshNodeTooltip(node);
            if (animate)
            {
                if (_tooltipAnimation != null)
                {
                    StopCoroutine(_tooltipAnimation);
                }
                _tooltipAnimation = StartCoroutine(AnimateTooltipIn());
            }
        }

        private IEnumerator AnimateTooltipIn()
        {
            if (_tooltipGroup == null || _tooltipRoot == null)
            {
                yield break;
            }

            _tooltipGroup.alpha = 0f;
            _tooltipRoot.localScale = Vector3.one * 0.94f;
            const float duration = 0.13f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                _tooltipGroup.alpha = eased;
                _tooltipRoot.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, eased);
                yield return null;
            }

            _tooltipGroup.alpha = 1f;
            _tooltipRoot.localScale = Vector3.one;
            _tooltipAnimation = null;
        }

        private void PositionNodeTooltip(Vector2 screenPosition)
        {
            if (_tooltipRoot == null || _runtimeRoot == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _runtimeRoot,
                screenPosition,
                GetUiCamera(),
                out Vector2 localPoint);
            Rect rootRect = _runtimeRoot.rect;
            float cursorX = localPoint.x - rootRect.xMin;
            float cursorY = localPoint.y - rootRect.yMin;
            Vector2 size = _tooltipRoot.sizeDelta;
            float x = cursorX + 24f;
            float y = cursorY + 18f;
            if (x + size.x > rootRect.width - 16f)
            {
                x = cursorX - size.x - 24f;
            }
            if (y + size.y > rootRect.height - 16f)
            {
                y = cursorY - size.y - 18f;
            }

            _tooltipRoot.anchoredPosition = new Vector2(
                Mathf.Clamp(x, 16f, Mathf.Max(16f, rootRect.width - size.x - 16f)),
                Mathf.Clamp(y, 16f, Mathf.Max(16f, rootRect.height - size.y - 16f)));
        }

        private void RefreshNodeTooltip(TraitNodeDefinition node)
        {
            if (node == null || _tooltipType == null)
            {
                return;
            }

            NodePurchaseState state = GetNodePurchaseState(node);
            TraitTab tab = GetNodeTab(node);
            bool concealed = state == NodePurchaseState.Locked;
            _tooltipType.text = concealed ? "잠긴 연구" : GetNodeTypeLabel(node, tab);
            _tooltipType.color = concealed
                ? new Color(0.88f, 0.89f, 0.94f, 1f)
                : GetCategoryAccent(tab);
            _tooltipTitle.text = concealed ? "???" : GetLocalizedNodeTitle(node);
            _tooltipValue.text = concealed ? "???" : GetLocalizedNodeValueLabel(node);
            _tooltipDescription.text = concealed
                ? "이 항목은 잠겨 있습니다.\n해금 조건을 충족하면 내용이 공개됩니다."
                : BuildNodeDescription(node);

            TraitGemstoneDefinition gemstone = GetGemstoneDefinitionById(node.CostGemstoneId);
            string currencyName = gemstone == null
                ? GetGemstoneLocalizedName(node.CostGemstoneId, node.CostGemstoneId)
                : GetGemstoneLocalizedName(gemstone.Id, gemstone.Title);
            if (concealed)
            {
                _tooltipCost.text = "비용 · 해금 후 공개";
            }
            else if (node.AcquisitionOnly || node.Cost <= 0)
            {
                _tooltipCost.text = GetNodeCostText(node);
            }
            else if (testModeFreeUpgrades)
            {
                _tooltipCost.text = GetNodeCostText(node) + " " + currencyName +
                    " · 보유 " + GetNodeBalance(node).ToString("N0", CultureInfo.InvariantCulture);
            }
            else
            {
                _tooltipCost.text = "비용  " +
                    node.Cost.ToString("N0", CultureInfo.InvariantCulture) + "  " +
                    currencyName + "  ·  보유 " +
                    GetNodeBalance(node).ToString("N0", CultureInfo.InvariantCulture);
            }

            bool purchased = state == NodePurchaseState.Purchased;
            bool canBuy = state == NodePurchaseState.Available && !node.AcquisitionOnly;
            bool canReset = purchased && !node.StartsUnlocked && !HasPurchasedDependents(node);
            _tooltipStatus.text = GetTooltipStatus(node, state);
            _tooltipStatus.color = GetBoardStateColor(state);
            _tooltipUpgradeButton.gameObject.SetActive(canBuy);
            _tooltipUpgradeButton.interactable = canBuy;
            if (canBuy)
            {
                SetButtonLabel(_tooltipUpgradeLabel, GetTooltipActionLabel(node, state));
                _tooltipUpgradeLabel.color = new Color(0.06f, 0.07f, 0.09f, 1f);
            }

            RectTransform actionRect = _tooltipUpgradeButton.transform as RectTransform;
            if (actionRect != null)
            {
                actionRect.anchoredPosition = new Vector2(canReset ? -57f : 0f, 36f);
                actionRect.sizeDelta = new Vector2(canReset ? 210f : 320f, 46f);
            }

            Color actionColor = canBuy
                ? AvailableColor
                : state == NodePurchaseState.Purchased
                    ? new Color(0.10f, 0.38f, 0.32f, 1f)
                    : state == NodePurchaseState.Unaffordable
                        ? new Color(0.42f, 0.16f, 0.18f, 1f)
                        : new Color(0.30f, 0.21f, 0.35f, 1f);
            Image upgradeImage = _tooltipUpgradeButton.GetComponent<Image>();
            if (upgradeImage != null)
            {
                upgradeImage.color = actionColor;
            }
            ColorBlock actionColors = _tooltipUpgradeButton.colors;
            actionColors.normalColor = actionColor;
            actionColors.highlightedColor = Color.Lerp(actionColor, Color.white, 0.16f);
            actionColors.pressedColor = Color.Lerp(actionColor, Color.black, 0.10f);
            actionColors.selectedColor = actionColors.highlightedColor;
            actionColors.disabledColor = actionColor;
            _tooltipUpgradeButton.colors = actionColors;

            _tooltipResetButton.gameObject.SetActive(canReset);
        }

        private static string GetTooltipActionLabel(
            TraitNodeDefinition node,
            NodePurchaseState state)
        {
            if (node == null)
            {
                return "항목을 선택하세요";
            }

            if (string.Equals(node.Id, ResearchCoreNodeId, StringComparison.Ordinal))
            {
                return state == NodePurchaseState.Purchased
                    ? string.Empty
                    : "연구 시작하기";
            }

            if (state == NodePurchaseState.Purchased)
            {
                return string.Empty;
            }

            if (node.AcquisitionOnly)
            {
                return "전투에서 획득";
            }

            if (state == NodePurchaseState.Unaffordable)
            {
                return "젬스톤 부족";
            }

            if (state == NodePurchaseState.Locked)
            {
                return "선행 조건 필요";
            }

            if (IsEntityUnlockNode(node.Id))
            {
                return node.Id.StartsWith("skill.", StringComparison.Ordinal)
                    ? "스킬 해금하기"
                    : "몬스터 해금하기";
            }

            return string.IsNullOrEmpty(node.PrerequisiteNodeId) || node.Cost <= 0
                ? "해금하기"
                : "강화하기";
        }

        private static bool IsEntityUnlockNode(string nodeId)
        {
            return IsRootEntityNode(nodeId, "skill.") ||
                IsRootEntityNode(nodeId, "monster.");
        }

        private static bool IsRootEntityNode(string nodeId, string prefix)
        {
            return !string.IsNullOrEmpty(nodeId) &&
                nodeId.StartsWith(prefix, StringComparison.Ordinal) &&
                nodeId.IndexOf('.', prefix.Length) < 0;
        }

        private string GetTooltipStatus(TraitNodeDefinition node, NodePurchaseState state)
        {
            if (node != null && string.Equals(
                    node.Id,
                    ResearchCoreNodeId,
                    StringComparison.Ordinal))
            {
                return state == NodePurchaseState.Purchased
                    ? "시작점 활성화 · 동서남북의 첫 연구가 열렸습니다"
                    : "중앙 시작점 · 선택하면 첫 연구 분기가 열립니다";
            }

            if (state == NodePurchaseState.Purchased)
            {
                return node.StartsUnlocked
                    ? "기본 항목 · 효과 적용 중"
                    : "효과 적용 중";
            }
            if (state == NodePurchaseState.Available)
            {
                return "구매 가능 · 아래 [" + GetTooltipActionLabel(node, state) +
                    "] 버튼을 누르세요";
            }
            if (state == NodePurchaseState.Unaffordable)
            {
                TraitGemstoneDefinition gemstone = GetGemstoneDefinitionById(node.CostGemstoneId);
                if (!IsGemstoneAvailable(gemstone))
                {
                    return (gemstone == null
                        ? GetGemstoneLocalizedName(node.CostGemstoneId, node.CostGemstoneId)
                        : GetGemstoneLocalizedName(gemstone.Id, gemstone.Title)) +
                        " 공급원을 먼저 해금하세요";
                }
                return "재화 부족 · 필요 " + node.Cost.ToString("N0", CultureInfo.InvariantCulture) +
                    " / 보유 " + GetNodeBalance(node).ToString("N0", CultureInfo.InvariantCulture);
            }
            if (node.AcquisitionOnly)
            {
                return node.RequiredBossTier > unlockedBossTier
                    ? "보스 " + node.RequiredBossTier + "단계 이후 전투 중 획득"
                    : "전투 중 확률로 획득할 수 있습니다";
            }
            if (!testModeUnlockAll && node.RequiredBossTier > unlockedBossTier)
            {
                return "보스 " + node.RequiredBossTier + "단계를 먼저 격파하세요";
            }
            if (!testModeUnlockAll && !IsPrerequisiteMet(node))
            {
                TraitNodeDefinition prerequisite = FindNode(node.PrerequisiteNodeId);
                return "선행 항목 해금 필요 · " +
                    (prerequisite == null ? node.PrerequisiteNodeId : GetLocalizedNodeTitle(prerequisite));
            }

            return "현재는 잠겨 있습니다";
        }

        private static string GetNodeTypeLabel(TraitNodeDefinition node, TraitTab tab)
        {
            if (node != null && node.Id != null &&
                node.Id.StartsWith("skill.cursorAura", StringComparison.Ordinal))
            {
                return "펫 동료";
            }
            switch (tab)
            {
                case TraitTab.Skill: return "스킬";
                case TraitTab.Monster: return "몬스터";
                default: return "스탯";
            }
        }

        private static TraitTab GetNodeTab(TraitNodeDefinition node)
        {
            if (node != null && node.Id != null &&
                node.Id.StartsWith("monster.", StringComparison.Ordinal))
            {
                return TraitTab.Monster;
            }
            if (node != null && node.Id != null &&
                node.Id.StartsWith("skill.", StringComparison.Ordinal))
            {
                return TraitTab.Skill;
            }
            return TraitTab.Stat;
        }

        private void HideNodeTooltip()
        {
            if (_tooltipHideRoutine != null)
            {
                StopCoroutine(_tooltipHideRoutine);
                _tooltipHideRoutine = null;
            }
            if (_tooltipAnimation != null)
            {
                StopCoroutine(_tooltipAnimation);
                _tooltipAnimation = null;
            }
            _pointerOverTooltip = false;
            _tooltipPinned = false;
            _tooltipNodeId = null;
            if (_tooltipRoot != null)
            {
                _tooltipRoot.gameObject.SetActive(false);
            }
            _selectedNodeId = null;
            UpdateNodeSelectionFrame();
        }

        private void UpdateNodeSelectionFrame()
        {
            if (_nodeContent == null)
            {
                return;
            }

            for (int i = 0; i < _nodeContent.childCount; i++)
            {
                Transform child = _nodeContent.GetChild(i);
                if (child == null || !child.name.StartsWith("Node_", StringComparison.Ordinal))
                {
                    continue;
                }

                Transform frame = child.Find("SelectionFrame");
                if (frame != null)
                {
                    frame.gameObject.SetActive(child.name == "Node_" + _selectedNodeId);
                }
            }
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
            string currencyTitle = gemstone == null
                ? GetGemstoneLocalizedName(node.CostGemstoneId, node.CostGemstoneId)
                : GetGemstoneLocalizedName(gemstone.Id, gemstone.Title);
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
                _upgradeButton.gameObject.SetActive(false);
                if (_resetNodeButton != null)
                {
                    _resetNodeButton.interactable = false;
                    _resetNodeButton.gameObject.SetActive(false);
                }
                return;
            }

            _selectedTitle.text = GetLocalizedNodeTitle(node);
            _selectedValue.text = GetLocalizedNodeValueLabel(node);
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
            _upgradeButton.gameObject.SetActive(canBuy);
            _resetNodeButton.interactable = canResetNode;
            _resetNodeButton.gameObject.SetActive(canResetNode);
            if (canBuy)
            {
                SetButtonLabel(
                    _upgradeButtonLabel,
                    node.Cost <= 0 ? "해금" : "강화");
            }
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
                        : "효과 적용 중";
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
            TryUpgradeNode(FindNode(_selectedNodeId));
        }

        private void ResetSelectedNode()
        {
            ResetNode(FindNode(_selectedNodeId));
        }

        private void TryUpgradeNode(TraitNodeDefinition node)
        {
            if (node == null || GetNodePurchaseState(node) != NodePurchaseState.Available ||
                node.AcquisitionOnly)
            {
                RefreshNodeTooltip(node);
                return;
            }

            var lockedBeforePurchase = new HashSet<string>(StringComparer.Ordinal);
            foreach (TraitNodeDefinition candidate in EnumerateAllNodes())
            {
                if (candidate != null && GetNodePurchaseState(candidate) == NodePurchaseState.Locked)
                {
                    lockedBeforePurchase.Add(candidate.Id);
                }
            }

            SpendNodeCost(node);
            _purchased.Add(node.Id);
            _spentByNode[node.Id] = testModeFreeUpgrades ? 0 : node.Cost;
            _spentCurrencyByNode[node.Id] = node.CostGemstoneId;
            _selectedNodeId = node.Id;
            BuildNodes(false);
            RefreshSummary();
            RefreshGemstonePanel();
            RefreshNodeTooltip(node);
            SaveProgressionState();
            PlayNodePurchaseEffect(node.Id);

            var newlyOpened = new List<TraitNodeDefinition>();
            foreach (TraitNodeDefinition candidate in EnumerateAllNodes())
            {
                if (candidate == null || !lockedBeforePurchase.Contains(candidate.Id)) continue;
                NodePurchaseState nextState = GetNodePurchaseState(candidate);
                if (nextState == NodePurchaseState.Available ||
                    nextState == NodePurchaseState.Unaffordable)
                {
                    newlyOpened.Add(candidate);
                    PlayNodeAvailableEffect(candidate.Id);
                }
            }

            if (newlyOpened.Count > 0)
            {
                int shownCount = Mathf.Min(2, newlyOpened.Count);
                string names = string.Empty;
                for (int i = 0; i < shownCount; i++)
                {
                    if (i > 0) names += ", ";
                    names += GetLocalizedNodeTitle(newlyOpened[i]);
                }
                if (newlyOpened.Count > shownCount) names += " 외 " + (newlyOpened.Count - shownCount) + "개";
                SetTestStatus("다음 연구가 열렸습니다: " + names);
            }
            else
            {
                SetTestStatus(GetLocalizedNodeTitle(node) + " 강화 완료");
            }
        }

        private void ResetNode(TraitNodeDefinition node)
        {
            if (node == null || !IsPurchased(node) || node.StartsUnlocked)
            {
                RefreshNodeTooltip(node);
                return;
            }

            if (HasPurchasedDependents(node))
            {
                if (_tooltipStatus != null)
                {
                    _tooltipStatus.text = "연결된 다음 강화가 있어 먼저 초기화해야 합니다";
                    _tooltipStatus.color = GetBoardStateColor(NodePurchaseState.Locked);
                }
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
            _selectedNodeId = node.Id;
            BuildNodes(false);
            RefreshSummary();
            RefreshGemstonePanel();
            RefreshNodeTooltip(node);
            SaveProgressionState();
        }

        private void PlayNodePurchaseEffect(string nodeId)
        {
            PlayNodeEffect(nodeId, PurchasedColor);
        }

        private void PlayNodeAvailableEffect(string nodeId)
        {
            PlayNodeEffect(nodeId, AvailableColor);
        }

        private void PlayNodeEffect(string nodeId, Color effectColor)
        {
            if (_nodeContent == null || string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            string objectName = "Node_" + nodeId;
            Transform node = null;
            for (int i = 0; i < _nodeContent.childCount; i++)
            {
                Transform candidate = _nodeContent.GetChild(i);
                if (candidate != null && candidate.gameObject.activeInHierarchy &&
                    string.Equals(candidate.name, objectName, StringComparison.Ordinal))
                {
                    node = candidate;
                    break;
                }
            }

            // Rebuilds defer Destroy until the end of the frame, so an inactive
            // node with the same name can still be found while its replacement
            // is already in the graph. Only animate the active replacement.
            if (node == null || !node.gameObject.activeInHierarchy)
            {
                return;
            }

            TraitNodePurchaseBurst burst = node.gameObject.AddComponent<TraitNodePurchaseBurst>();
            burst.Play(effectColor);
        }

        private void ResetPreview()
        {
            testModeUnlockAll = false;
            testModeFreeUpgrades = false;
            unlockedBossTier = _document == null || _document.progression == null
                ? 0
                : Mathf.Clamp(_document.progression.initialBossTier, 0, 5);
            RefreshTestBossTierButtons();
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
            if (_statusMessage != null)
            {
                _statusMessage.text = "시작점만 남기고 연구를 초기화했습니다";
            }
            BuildCategories();
            BuildNodes(true);
            CenterUnifiedGraph();
            RefreshSummary();
            RefreshGemstonePanel();
            SelectNode(FindNode(ResearchCoreNodeId));
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
                    : GetLocalizedNodeTitle(prerequisite);
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
            if (node == null || string.IsNullOrEmpty(node.Id)) return string.Empty;
            string id = node.Id;

            if (string.Equals(id, ResearchCoreNodeId, StringComparison.Ordinal))
            {
                return "연구의 중앙 시작점입니다. 활성화하면 동서남북의 첫 연구 선택지가 열립니다.";
            }

            if (id.StartsWith("stat.", StringComparison.Ordinal))
            {
                if (id.EndsWith(".01", StringComparison.Ordinal) &&
                    (id.StartsWith("stat.attack.", StringComparison.Ordinal) ||
                     id.StartsWith("stat.radius.", StringComparison.Ordinal) ||
                     id.StartsWith("stat.cooldown.", StringComparison.Ordinal) ||
                     id.StartsWith("stat.critical.", StringComparison.Ordinal)))
                {
                    return "기본 능력치는 플레이어 전투 설정의 기준값을 사용합니다.";
                }
                if (id.StartsWith("stat.fieldDuration.", StringComparison.Ordinal) &&
                    id.EndsWith(".01", StringComparison.Ordinal))
                {
                    return "일반 필드 지속 시간의 기준값은 현재 게임 정보에서 가져옵니다.";
                }

                string value = node.ValueLabel ?? string.Empty;
                if (id.StartsWith("stat.attack.", StringComparison.Ordinal))
                    return "공격력이 " + value + "만큼 증가합니다.";
                if (id.StartsWith("stat.radius.", StringComparison.Ordinal))
                    return "자동 공격 반경이 " + value.TrimStart('+') + "만큼 넓어집니다.";
                if (id.StartsWith("stat.cooldown.", StringComparison.Ordinal))
                    return "자동 공격 대기 시간이 " + value.TrimStart('-').Replace("s", "초") + " 줄어듭니다.";
                if (id.StartsWith("stat.critical.", StringComparison.Ordinal))
                    return "치명타 확률이 " + value + "p 증가합니다.";
                if (id.StartsWith("stat.boss.", StringComparison.Ordinal))
                    return "보스 피해 배율이 " + value.Replace("x", "배") + " 증가합니다.";
                if (id.StartsWith("stat.fieldDuration.", StringComparison.Ordinal))
                    return "일반 필드가 " + value.Replace("s", "초") + " 동안 지속됩니다.";
            }

            if (id.StartsWith("skill.", StringComparison.Ordinal))
            {
                string skillId = GetEntityRootId(id, "skill.");
                string skillName = GetLocalizedEntityName(skillId, skillId);
                if (string.Equals(id, skillId, StringComparison.Ordinal))
                    return skillName + " 스킬을 해금합니다. 스킬의 전투 수치는 전체 게임 정보에서 확인할 수 있습니다.";
                if (id.EndsWith(".damage", StringComparison.Ordinal))
                    return "피해량이 " + (node.ValueLabel ?? string.Empty).Replace("x", "배") + "로 증가합니다.";
                if (id.EndsWith(".radius", StringComparison.Ordinal))
                    return "효과 범위가 " + (node.ValueLabel ?? string.Empty).Replace("x", "배") + "로 증가합니다.";
                if (id.EndsWith(".cooldown", StringComparison.Ordinal))
                    return "재사용 대기 시간이 " + (node.ValueLabel ?? string.Empty).Replace("x", "배") + "로 줄어듭니다.";
            }

            if (id.StartsWith("monster.", StringComparison.Ordinal))
            {
                string monsterId = GetEntityRootId(id, "monster.");
                string monsterName = GetLocalizedEntityName(monsterId, monsterId);
                if (string.Equals(id, monsterId, StringComparison.Ordinal))
                    return "이 몬스터를 전투에 해금합니다. 처치 보상과 젬스톤 드롭 정보는 게임 정보에서 확인할 수 있습니다.";
                if (id.IndexOf(".production.", StringComparison.Ordinal) >= 0)
                    return "한 번의 생성 주기마다 " + (node.ValueLabel ?? string.Empty) + "마리가 등장합니다.";
            }

            return "현재 게임 데이터에 정의된 효과를 적용합니다. 상세 수치는 전체 게임 정보에서 확인할 수 있습니다.";
        }

        private string GetLocalizedNodeTitle(TraitNodeDefinition node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id)) return string.Empty;
            string id = node.Id;
            if (string.Equals(id, ResearchCoreNodeId, StringComparison.Ordinal))
            {
                return "연구 시작점";
            }
            if (id.StartsWith("stat.", StringComparison.Ordinal))
            {
                string group = id.StartsWith("stat.attack.", StringComparison.Ordinal) ? "공격력" :
                    id.StartsWith("stat.radius.", StringComparison.Ordinal) ? "자동 공격 반경" :
                    id.StartsWith("stat.cooldown.", StringComparison.Ordinal) ? "공격 간격" :
                    id.StartsWith("stat.critical.", StringComparison.Ordinal) ? "치명타 확률" :
                    id.StartsWith("stat.boss.", StringComparison.Ordinal) ? "보스 피해" :
                    id.StartsWith("stat.fieldDuration.", StringComparison.Ordinal) ? "필드 지속 시간" : "능력치";
                int rank = GetTrailingRank(id);
                return rank <= 1 ? group + " 기본값" : group + " 강화 " + rank;
            }

            if (id.StartsWith("skill.", StringComparison.Ordinal))
            {
                string skillId = GetEntityRootId(id, "skill.");
                string skillName = GetLocalizedEntityName(skillId, node.Title);
                if (string.Equals(id, skillId, StringComparison.Ordinal)) return skillName;
                if (id.EndsWith(".damage", StringComparison.Ordinal)) return skillName + " · 피해 강화";
                if (id.EndsWith(".radius", StringComparison.Ordinal)) return skillName + " · 범위 강화";
                if (id.EndsWith(".cooldown", StringComparison.Ordinal)) return skillName + " · 재사용 시간";
            }

            if (id.StartsWith("monster.", StringComparison.Ordinal))
            {
                string monsterId = GetEntityRootId(id, "monster.");
                string monsterName = GetLocalizedEntityName(monsterId, node.Title);
                if (string.Equals(id, monsterId, StringComparison.Ordinal)) return monsterName;
                return monsterName + " · 생성 수 " + (node.ValueLabel ?? string.Empty) + "마리";
            }

            return node.Title ?? node.Id;
        }

        private static string GetLocalizedNodeValueLabel(TraitNodeDefinition node)
        {
            if (node == null) return string.Empty;
            if (string.Equals(node.Id, ResearchCoreNodeId, StringComparison.Ordinal))
            {
                return "동서남북 첫 연구 연결";
            }
            string value = node.ValueLabel ?? string.Empty;
            if (value == "Component baseline" || value == "JSON baseline") return "기본값";
            if (value == "Unlock skill") return "스킬 해금";
            if (value == "Enable monster") return "몬스터 해금";
            return value;
        }

        private static int GetTrailingRank(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            int lastDot = id.LastIndexOf('.');
            if (lastDot < 0 || lastDot + 1 >= id.Length) return 0;
            return int.TryParse(id.Substring(lastDot + 1), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int rank) ? rank : 0;
        }

        private static string GetEntityRootId(string id, string prefix)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith(prefix, StringComparison.Ordinal)) return id;
            int suffixStart = id.IndexOf('.', prefix.Length);
            return suffixStart < 0 ? id : id.Substring(0, suffixStart);
        }

        private static string GetLocalizedEntityName(string id, string fallback)
        {
            switch (id)
            {
                case "skill.fireball": return "파이어볼";
                case "skill.lightning": return "라이트닝 볼트";
                case "skill.freeze": return "빙결";
                case "skill.hurricane": return "허리케인";
                case "skill.meteor": return "메테오";
                case "skill.dragonBreath": return "드래곤 브레스";
                case "skill.cursorAura": return "펫 동료";
                case "monster.01": return "가넷 그루터기";
                case "monster.02": return "토파즈 동굴박쥐";
                case "monster.03": return "호박 버섯";
                case "monster.04": return "자수정 가면유령";
                case "monster.05": return "수정 전갈";
                case "monster.06": return "사파이어 눈요정";
                case "monster.07": return "빙결 거미";
                case "monster.08": return "다이아 철골렘";
                case "monster.09": return "드래곤 불꽃봉오리";
                case "monster.10": return "고대 해골군주";
                default: return string.IsNullOrEmpty(fallback) ? id : fallback;
            }
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

            if (string.Equals(categoryId, "skill.cursorAura", StringComparison.Ordinal))
            {
                if (petIcon != null)
                {
                    return petIcon;
                }

                return petIcons != null && petIcons.Length > 0
                    ? petIcons[0]
                    : skillIcon;
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
                    return "스킬 연구";
                case TraitTab.Monster:
                    return "몬스터 연구";
                default:
                    return "스탯 연구 트리";
            }
        }

        private Vector2 GetSafeNodeCellSize()
        {
            if (_activeTab == TraitTab.Stat)
            {
                return new Vector2(82f, 82f);
            }

            return new Vector2(
                Mathf.Max(72f, nodeCellSize.x),
                Mathf.Max(72f, nodeCellSize.y));
        }

        private Vector2 GetSafeNodeGridSpacing()
        {
            if (_activeTab == TraitTab.Stat)
            {
                return new Vector2(18f, 18f);
            }

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

            if (category != null && category.Id == "skill.cursorAura")
            {
                return GetCategoryIcon(category.Id);
            }

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
            if (category != null &&
                category.Id == "skill.cursorAura" && node != null)
            {
                if (node.Id == "skill.cursorAura")
                {
                    return GetCategoryHeaderIcon(tab, category, categoryIndex);
                }

                if (node.Id.EndsWith(".damage", StringComparison.Ordinal))
                {
                    return attackIcon;
                }

                if (node.Id.EndsWith(".radius", StringComparison.Ordinal))
                {
                    return radiusIcon;
                }

                if (node.Id.EndsWith(".cooldown", StringComparison.Ordinal))
                {
                    return timerIcon != null ? timerIcon : clickIcon;
                }
            }

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
            _runtimeRoot.localScale = Vector3.one;
            float elapsed = 0f;
            float duration = Mathf.Clamp(transitionSeconds, 0.08f, 0.18f);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _runtimeGroup.alpha = 1f - Mathf.Pow(1f - t, 2f);
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
            float duration = Mathf.Clamp(transitionSeconds * 0.7f, 0.08f, 0.14f);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _nodeGroup.alpha = 1f - Mathf.Pow(1f - t, 2f);
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
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.38f, 0.54f, 0.82f, 0.16f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
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

        private TextMeshProUGUI CreateLocalizedText(
            RectTransform rect,
            string key,
            string fallback,
            int fontSize,
            TextAnchor alignment,
            Color color,
            bool bold)
        {
            TextMeshProUGUI text = CreateText(
                rect,
                LocalizationCatalog.Get(key, fallback),
                fontSize,
                alignment,
                color,
                bold);
            if (!string.IsNullOrEmpty(key))
            {
                LocalizedTmpLabel localized = text.GetComponent<LocalizedTmpLabel>();
                if (localized == null)
                {
                    localized = text.gameObject.AddComponent<LocalizedTmpLabel>();
                }

                localized.Bind(key, fallback);
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

    /// <summary>Uses the mouse wheel for zoom and preserves ScrollRect drag panning.</summary>
    public sealed class TraitGraphScrollRect : ScrollRect
    {
        private TraitScreenController _owner;

        public void Bind(TraitScreenController owner)
        {
            _owner = owner;
        }

        public override void OnScroll(PointerEventData eventData)
        {
            if (_owner != null && eventData != null)
            {
                _owner.ZoomGraphAt(eventData.position, eventData.scrollDelta.y);
                eventData.Use();
            }
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            base.OnBeginDrag(eventData);
            if (_owner != null) _owner.SetGraphDragCursor(true);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            base.OnEndDrag(eventData);
            if (_owner != null) _owner.SetGraphDragCursor(false);
        }
    }

    /// <summary>Keeps the hover card open while the pointer moves onto it.</summary>
    public sealed class TraitNodeTooltipPointerRelay : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private TraitScreenController _owner;

        public void Bind(TraitScreenController owner)
        {
            _owner = owner;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_owner != null) _owner.HandleTooltipPointerEnter();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_owner != null) _owner.HandleTooltipPointerExit();
        }
    }

    /// <summary>
    /// A short square pulse with outward sparks makes purchases readable
    /// without introducing a full-screen particle overlay.
    /// </summary>
    public sealed class TraitNodePurchaseBurst : MonoBehaviour
    {
        private RectTransform _target;
        private RectTransform _ring;
        private CanvasGroup _ringGroup;
        private readonly List<RectTransform> _sparks = new List<RectTransform>();
        private readonly List<CanvasGroup> _sparkGroups = new List<CanvasGroup>();
        private readonly List<Vector2> _directions = new List<Vector2>();

        public void Play(Color color)
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                Destroy(this);
                return;
            }

            _target = transform as RectTransform;
            if (_target == null)
            {
                Destroy(this);
                return;
            }

            _ring = CreateRing(color);
            _ringGroup = _ring.GetComponent<CanvasGroup>();
            Vector2[] directions =
            {
                Vector2.up,
                Vector2.right,
                Vector2.down,
                Vector2.left,
                new Vector2(1f, 1f).normalized,
                new Vector2(1f, -1f).normalized,
                new Vector2(-1f, -1f).normalized,
                new Vector2(-1f, 1f).normalized
            };
            for (int i = 0; i < directions.Length; i++)
            {
                RectTransform spark = CreateSpark(color, i);
                _sparks.Add(spark);
                _sparkGroups.Add(spark.GetComponent<CanvasGroup>());
                _directions.Add(directions[i]);
            }

            StartCoroutine(AnimateBurst());
        }

        private RectTransform CreateRing(Color color)
        {
            GameObject ringObject = new GameObject(
                "UnlockPulse",
                typeof(RectTransform),
                typeof(CanvasGroup));
            ringObject.transform.SetParent(transform, false);
            RectTransform ring = ringObject.GetComponent<RectTransform>();
            ring.anchorMin = Vector2.zero;
            ring.anchorMax = Vector2.one;
            ring.pivot = new Vector2(0.5f, 0.5f);
            ring.offsetMin = new Vector2(-1f, -1f);
            ring.offsetMax = new Vector2(1f, 1f);
            ring.localScale = Vector3.one * 0.82f;
            CreateRingBar(ring, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -2f), new Vector2(0f, 4f), color);
            CreateRingBar(ring, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 2f), new Vector2(0f, 4f), color);
            CreateRingBar(ring, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(2f, 0f), new Vector2(4f, 0f), color);
            CreateRingBar(ring, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-2f, 0f), new Vector2(4f, 0f), color);
            return ring;
        }

        private static void CreateRingBar(
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            GameObject barObject = new GameObject("PulseEdge", typeof(RectTransform), typeof(Image));
            barObject.transform.SetParent(parent, false);
            RectTransform rect = barObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = barObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private RectTransform CreateSpark(Color color, int index)
        {
            GameObject sparkObject = new GameObject(
                "UnlockSpark_" + index,
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup));
            sparkObject.transform.SetParent(transform, false);
            RectTransform spark = sparkObject.GetComponent<RectTransform>();
            spark.anchorMin = spark.anchorMax = new Vector2(0.5f, 0.5f);
            spark.pivot = new Vector2(0.5f, 0.5f);
            spark.sizeDelta = index % 2 == 0
                ? new Vector2(7f, 7f)
                : new Vector2(5f, 9f);
            Image image = sparkObject.GetComponent<Image>();
            image.color = Color.Lerp(color, Color.white, 0.32f);
            image.raycastTarget = false;
            return spark;
        }

        private IEnumerator AnimateBurst()
        {
            const float duration = 0.48f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                if (_ring != null)
                {
                    _ring.localScale = Vector3.one * Mathf.Lerp(0.82f, 1.85f, eased);
                    _ringGroup.alpha = 1f - t;
                }
                if (_target != null)
                {
                    _target.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.16f);
                }
                for (int i = 0; i < _sparks.Count; i++)
                {
                    if (_sparks[i] == null) continue;
                    _sparks[i].anchoredPosition = _directions[i] * Mathf.Lerp(0f, 47f, eased);
                    _sparks[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.35f, t);
                    _sparkGroups[i].alpha = 1f - t;
                }
                yield return null;
            }

            if (_target != null) _target.localScale = Vector3.one;
            if (_ring != null) Destroy(_ring.gameObject);
            for (int i = 0; i < _sparks.Count; i++)
            {
                if (_sparks[i] != null) Destroy(_sparks[i].gameObject);
            }
            Destroy(this);
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
        [SerializeField, Min(0f)] private float hoverScale = 1.04f;
        [SerializeField, Min(0f)] private float pressedScale = 0.94f;
        [SerializeField, Min(0f)] private float duration = 0.12f;

        private RectTransform _rect;
        private Coroutine _animation;
        public event Action<PointerEventData> PointerEntered;
        public event Action<PointerEventData> PointerExited;

        public void SetScaleFeedback(float hover, float pressed, float seconds)
        {
            hoverScale = Mathf.Max(1f, hover);
            pressedScale = Mathf.Clamp(pressed, 0.8f, 1f);
            duration = Mathf.Clamp(seconds, 0.05f, 0.25f);
        }

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
            PointerEntered?.Invoke(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            AnimateTo(1f);
            PointerExited?.Invoke(eventData);
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
            // Rebuilding the board disables the old node objects before Unity
            // dispatches the matching pointer-exit event. Do not start a
            // coroutine from that late event on an inactive node.
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy || _rect == null)
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
                float eased = t * t * (3f - 2f * t);
                _rect.localScale = Vector3.LerpUnclamped(start, end, eased);
                yield return null;
            }

            _rect.localScale = end;
            _animation = null;
        }
    }
}
