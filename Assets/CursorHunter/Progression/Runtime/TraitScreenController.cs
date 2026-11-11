using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using CursorHunter.Contracts;
using CursorHunter.Data;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Serialization;
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
        private const string BalanceInfoPreferenceKey = "cursor_hunter.balance_patch.game_data.v1";
        private const string BalanceProgressionPreferenceKey = "cursor_hunter.balance_patch.progression.v1";
        private const string BoardLayoutPreferenceKey = "cursor_hunter.research_layout.v2";

        private enum BalanceEditorTab
        {
            Monsters,
            Skills,
            Research
        }

        [Header("Data")]
        [SerializeField] private TraitCatalog catalog;
        [SerializeField, Min(0)] private long startingGarnet = 245;
        [SerializeField] private long[] startingGemstoneBalances =
            new long[] { 245L, 0L, 0L, 0L, 0L, 0L };
        [SerializeField, Range(0, 5)] private int unlockedBossTier;
        public int CurrentBossProgressTier => Mathf.Clamp(unlockedBossTier, 0, 5);
        [SerializeField] private bool loadSavedProgress = true;
        [SerializeField] private bool testModeUnlockAll = false;
        [SerializeField] private bool testModeFreeUpgrades = false;
        [SerializeField] private UnityEvent backRequested = new UnityEvent();
        [SerializeField] private UnityEvent combatRequested = new UnityEvent();
        [SerializeField] private UnityEvent bossCombatRequested = new UnityEvent();

        [Header("Casual Fantasy sprites")]
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite buttonFrameSprite;
        [SerializeField] private Sprite combatBackgroundSprite;
        [SerializeField] private Sprite tabSprite;
        [SerializeField] private Sprite tabFocusSprite;
        [SerializeField] private Sprite researchCategoryTabFrame;
        [SerializeField] private Sprite talentCategoryTabFrame;
        [SerializeField] private Sprite relicCategoryTabFrame;
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
        [SerializeField] private Sprite backButtonSprite;
        [SerializeField] private GameObject backButtonPrefab;
        [SerializeField] private Sprite attackIcon;
        [SerializeField] private Sprite radiusIcon;
        [SerializeField, FormerlySerializedAs("clickIcon")] private Sprite attackIntervalIcon;
        [SerializeField] private Sprite timerIcon;
        [SerializeField] private Sprite criticalIcon;
        [SerializeField] private Sprite gemstoneIcon;
        [SerializeField] private Sprite bossIcon;
        [SerializeField] private Sprite skillIcon;
        [SerializeField] private Sprite monsterIcon;
        [SerializeField] private Sprite petIcon;
        [SerializeField] private Sprite researchTabIcon;
        [SerializeField] private Sprite talentTabIcon;
        [SerializeField] private GemstoneIconCatalog gemstoneIconCatalog;
        // Retained as a fallback for older/custom scenes without a matching catalog entry.
        [SerializeField, HideInInspector] private Sprite[] gemstoneIcons = new Sprite[0];
        [SerializeField] private Sprite[] skillIcons = new Sprite[0];
        [SerializeField] private Sprite[] monsterIcons = new Sprite[0];
        [SerializeField] private Sprite[] petIcons = new Sprite[0];
        [SerializeField] private Font uiFont;

        [Header("Relic Collection")]
        [SerializeField] private List<RelicDisplayEntry> relicEntries =
            new List<RelicDisplayEntry>();
        [SerializeField] private Sprite relicRevealSparkleSprite;
        [SerializeField] private Sprite relicRevealGlowSprite;
        [SerializeField] private Sprite relicRevealShardSprite;

        [Header("Layout")]
        [SerializeField, Range(0, 2)] private int initialTab;
        [SerializeField] private bool initialShowRelicCollection;
        [SerializeField, Min(0f)] private float transitionSeconds = 0.14f;
        [SerializeField] private Vector2 nodeCellSize = new Vector2(106f, 106f);
        [SerializeField] private Vector2 nodeGridSpacing = new Vector2(27f, 27f);
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
        private bool _showRelicCollection;
        private bool _showTalentCollection;
        private bool _showFieldCollection;
        private TraitTab _activeTab;
        private string _selectedNodeId;
        private long _garnetBalance;
        private long[] _gemstoneBalances;
        private long _experiencePoints;
        private int _selectedFieldTier = 1;
        private int _availableTraitCards;
        private int _traitCardDrawCount;
        private readonly List<string> _ownedTalentIds = new List<string>();
        private readonly HashSet<string> _unseenRelicIds = new HashSet<string>(StringComparer.Ordinal);
        private bool _talentDraftRevealed;
        private bool _talentSelectionAnimating;
        private int _talentRerollsUsed;
        private GameObject _talentFlyCard;
        private readonly List<string> _talentDraftIds = new List<string>(3);
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
        private readonly Dictionary<string, List<TraitProgressionStore.SpentCostEntry>> _spentNodeCosts =
            new Dictionary<string, List<TraitProgressionStore.SpentCostEntry>>(StringComparer.Ordinal);
        private readonly Dictionary<string, MonsterDefinition> _monsterDefinitionsById =
            new Dictionary<string, MonsterDefinition>(StringComparer.Ordinal);
        private bool _monsterDefinitionsLoaded;
        private readonly Dictionary<string, Vector2> _boardPositionsByNodeId =
            new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private readonly Dictionary<string, RectTransform> _boardNodeRectsById =
            new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        private RectTransform _runtimeRoot;
        private Vector2 _lastHeaderViewportSize;
        private RectTransform _nodeContent;
        private RectTransform _nodeViewport;
        private TextMeshProUGUI _screenTitle;
        private Button _researchCategoryButton;
        private Button _relicCategoryButton;
        private Button _talentCategoryButton;
        private Button _generalCombatButton;
        private Button _bossCombatButton;
        private Button _relicTestModeButton;
        private TextMeshProUGUI _relicTestModeLabel;
        private Button _balanceEditorToggle;
        private RectTransform _gemstoneList;
        private RectTransform _gemstonePanel;
        private RectTransform _combatStatsPanel;
        private readonly List<TextMeshProUGUI> _combatStatValues =
            new List<TextMeshProUGUI>();
        private readonly List<RainbowFrameSegment> _rainbowFrameSegments =
            new List<RainbowFrameSegment>();
        private float _rainbowFramePhase;
        private CanvasGroup _runtimeGroup;
        private CanvasGroup _nodeGroup;
        private TextMeshProUGUI _nodeHeading;
        private TextMeshProUGUI _garnetValue;
        private Image _selectedHeaderBackground;
        private Image _selectedHeaderAccent;
        private TextMeshProUGUI _selectedTitle;
        private TextMeshProUGUI _selectedDescription;
        private TextMeshProUGUI _selectedCost;
        private TextMeshProUGUI _selectedRequirement;
        private TextMeshProUGUI _statusMessage;
        private RectTransform _tooltipRoot;
        private CanvasGroup _tooltipGroup;
        private TextMeshProUGUI _tooltipType;
        private TextMeshProUGUI _tooltipTitle;
        private TextMeshProUGUI _tooltipDescription;
        private TextMeshProUGUI _tooltipCost;
        private Image _tooltipCurrencyIcon;
        private readonly Image[] _tooltipCostIcons = new Image[5];
        private readonly TextMeshProUGUI[] _tooltipCostAmounts = new TextMeshProUGUI[5];
        private Button _tooltipCloseButton;
        private Button _tooltipUpgradeButton;
        private TextMeshProUGUI _tooltipUpgradeLabel;
        private Button _tooltipResetButton;
        private string _tooltipNodeId;
        private string _hoveredNodeId;
        private bool _pointerOverTooltip;
        private Coroutine _tooltipAnimation;
        private Coroutine _tooltipDismissRoutine;
        private GameObject _relicDetailOverlay;
        private int _selectedRelicIndex = -1;
        private int _lastViewedRelicIndex = -1;
        private bool _relicTestMode;
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
        private TMP_InputField _testRelicIndexInput;
        private Button _testUnlockAllButton;
        private TextMeshProUGUI _testUnlockAllLabel;
        private bool _testToolsOpen;
        private RectTransform _balanceEditorPanel;
        private bool _balanceEditorOpen;
        private BalanceEditorTab _balanceEditorTab;
        private int _balanceEditorIndex;
        private TextMeshProUGUI _balanceEditorTargetLabel;
        private RectTransform _balanceTargetListContent;
        private RectTransform _balanceTargetListViewport;
        private ScrollRect _balanceTargetListScroll;
        private readonly List<BalanceEditorTargetButton> _balanceTargetButtons =
            new List<BalanceEditorTargetButton>();
        private readonly TextMeshProUGUI[] _balanceEditorFieldLabels = new TextMeshProUGUI[7];
        private readonly TMP_InputField[] _balanceEditorFields = new TMP_InputField[7];
        private RectTransform _balanceEditorDescriptionRoot;
        private TMP_InputField _balanceEditorDescription;
        private TextMeshProUGUI _balanceEditorStatus;
        private string _balanceEditorTargetId;
        private readonly Dictionary<string, string> _balanceEditedDescriptions =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private Button _boardLayoutEditButton;
        private Button _boardLayoutResetButton;
        private bool _boardLayoutEditing;
        private string _draggingBoardNodeId;
        private Vector2 _boardNodeDragOffset;
        private readonly Dictionary<string, Vector2Int> _boardCellOverrides =
            new Dictionary<string, Vector2Int>(StringComparer.Ordinal);
        private bool _boardCellOverridesLoaded;
        private bool _graphDragCursorActive;
        private Button _bulkUpgradeButton;
        private Coroutine _bulkUpgradeRoutine;
        private Texture2D _traitBackdropTexture;
        private Sprite _traitBackdropSprite;
        private Coroutine _fitGraphRoutine;
        private readonly List<TextMeshProUGUI> _summaryValues = new List<TextMeshProUGUI>();
        private Coroutine _transition;
        private Coroutine _nodeTransition;
        private TraitTab _lastBuiltTab;
        private bool _hasBuiltNodeLayout;

        public bool CanStartPlayerCombat
        {
            get
            {
                EnsureProgressionState();
                return _purchased.Contains(ResearchCoreNodeId) && _purchased.Contains("monster.01");
            }
        }

        public long ExperiencePoints { get { EnsureProgressionState(); return _experiencePoints; } }
        public int PlayerLevel { get { EnsureProgressionState(); return ProgressionLevelCurve.GetLevel(_experiencePoints); } }
        public int SelectedFieldTier { get { EnsureProgressionState(); return Mathf.Clamp(_selectedFieldTier, 1, UnlockedFieldTier); } }
        public int UnlockedFieldTier => Mathf.Clamp(1 + PlayerLevel / 5, 1, 5);
        public int AvailableTraitCards { get { EnsureProgressionState(); return _availableTraitCards; } }
        internal bool CanNavigateResearchGraph => !_showRelicCollection && !_showTalentCollection && !_showFieldCollection;
        public int TraitCardDrawCost => _traitCardDrawCount == int.MaxValue
            ? int.MaxValue
            : Mathf.Max(1, _traitCardDrawCount + 1);

        public bool TryGetRelicIcon(string relicId, out Sprite icon)
        {
            EnsureRelicEntries();
            for (int i = 0; i < relicEntries.Count; i++)
            {
                RelicDisplayEntry entry = relicEntries[i];
                if (entry != null && string.Equals(entry.Id, relicId, StringComparison.Ordinal))
                {
                    icon = entry.Icon;
                    return icon != null;
                }
            }
            icon = null;
            return false;
        }

        public bool TryGetRelicPresentation(string relicId, out string title, out Sprite icon)
        {
            EnsureRelicEntries();
            for (int i = 0; i < relicEntries.Count; i++)
            {
                RelicDisplayEntry entry = relicEntries[i];
                if (entry == null || !string.Equals(entry.Id, relicId, StringComparison.Ordinal)) continue;
                title = entry.Title ?? string.Empty;
                icon = entry.Icon;
                return icon != null || !string.IsNullOrWhiteSpace(title);
            }
            title = string.Empty;
            icon = null;
            return false;
        }

        public void GetRelicDiscoveryEffectSprites(out Sprite glow, out Sprite sparkle, out Sprite shard)
        {
            glow = relicRevealGlowSprite;
            sparkle = relicRevealSparkleSprite;
            shard = relicRevealShardSprite;
        }

        public bool SelectFieldTier(int fieldTier)
        {
            EnsureProgressionState();
            int next = Mathf.Clamp(fieldTier, 1, UnlockedFieldTier);
            if (next != _selectedFieldTier)
            {
                int previous = _selectedFieldTier;
                _selectedFieldTier = next;
                if (!SaveProgressionState())
                {
                    _selectedFieldTier = previous;
                    SetTestStatus("필드 선택을 저장하지 못했습니다.");
                    return false;
                }
            }

            // Field cards are both the selection and entry action. Remember the
            // chosen field, then begin combat immediately without a second CTA.
            if (_showFieldCollection)
            {
                StartSelectedFieldCombat();
                return true;
            }

            SetTestStatus("필드 " + next.ToString(CultureInfo.InvariantCulture) + "을 선택했습니다.");
            return true;
        }

        public int GetOwnedTalentStackCount(string id)
        {
            EnsureProgressionState();
            int count = 0;
            for (int i = 0; i < _ownedTalentIds.Count; i++)
                if (string.Equals(_ownedTalentIds[i], id, StringComparison.Ordinal)) count++;
            return count;
        }

        [Serializable]
        private sealed class RelicDisplayEntry
        {
            [SerializeField] private string id;
            [SerializeField] private string title;
            [SerializeField] private Sprite icon;
            [SerializeField, TextArea(2, 4)] private string description;
            [SerializeField] private bool isObtained;

            public string Id => id;
            public string Title => title;
            public Sprite Icon => icon;
            public string Description => description;
            public bool IsObtained => isObtained;

            public void SetObtained(bool value)
            {
                isObtained = value;
            }

            public void ApplyPresentationDefaults(
                string relicId,
                string defaultTitle,
                Sprite defaultIcon,
                string defaultDescription)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    id = relicId;
                }

                bool hasGenericTitle = !string.IsNullOrWhiteSpace(title) &&
                    title.StartsWith("유물 ", StringComparison.Ordinal);
                if (string.IsNullOrWhiteSpace(title) || hasGenericTitle)
                {
                    title = defaultTitle;
                }

                if (icon == null)
                {
                    icon = defaultIcon;
                }

                if (string.IsNullOrWhiteSpace(description))
                {
                    description = defaultDescription;
                }
            }
        }

        private sealed class BoardNodeEntry
        {
            public TraitTab Tab;
            public TraitCategoryDefinition Category;
            public TraitNodeDefinition Node;
            public int CategoryIndex;
            public int NodeIndex;
            public int FlatIndex;
            public int DifficultyTier;
            public Vector2Int Cell;
            public Vector2 Position;
        }

        private sealed class BalanceEditorTargetButton
        {
            public BalanceEditorTab Tab;
            public int Index;
            public Button Button;
        }

        private sealed class RainbowFrameSegment
        {
            public Image Image;
            public float Hue;
            public float Alpha;
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

        private const float StatGraphDefaultZoom = 0.82f;
        private const float StatGraphWidth = 1600f;
        private const float StatGraphHeight = 1120f;
        private const float BoardNodeSize = 76f;
        // BoardNodeSize is 76 px. A 22 px gap was too tight for the number of
        // branches on the combined board, so increase the gap by 1.5x.
        private const float BoardNodePitchX = 109f;
        private const float BoardNodePitchY = 109f;
        private const float MinimumGraphZoom = 0.45f;
        private const float MaximumGraphZoom = 2.1f;
        private const float RelicGridCardAspect = 2.35f;
        private const float RelicDetailCardAspect = 0.68f;
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
            EnsureRelicEntries();
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

        private void OnValidate()
        {
            EnsureRelicEntries();
        }

        private void EnsureRelicEntries()
        {
            if (relicEntries == null)
            {
                relicEntries = new List<RelicDisplayEntry>();
            }

            while (relicEntries.Count < 20)
            {
                relicEntries.Add(new RelicDisplayEntry());
            }
        }

        private void ApplyRelicPresentationDefaults()
        {
            RelicCollectionCatalog defaults =
                Resources.Load<RelicCollectionCatalog>("RelicCollectionCatalog");
            if (defaults == null || defaults.Entries == null)
            {
                return;
            }

            EnsureRelicEntries();

            // Main scenes created before relic IDs were serialized still have the
            // original presentation order. Resolve that one-time migration by
            // title/icon first, using the old index only as a final fallback.
            for (int i = 0; i < relicEntries.Count; i++)
            {
                RelicDisplayEntry entry = relicEntries[i];
                if (entry == null || !string.IsNullOrWhiteSpace(entry.Id))
                {
                    continue;
                }

                RelicCollectionCatalogEntry legacyPresentation =
                    defaults.FindLegacyEntry(entry.Title, entry.Icon);
                if (legacyPresentation == null && i < defaults.Entries.Count)
                {
                    legacyPresentation = defaults.Entries[i];
                }

                if (legacyPresentation != null)
                {
                    entry.ApplyPresentationDefaults(
                        legacyPresentation.Id,
                        legacyPresentation.Title,
                        legacyPresentation.Icon,
                        legacyPresentation.Description);
                }
            }

            var entriesById = new Dictionary<string, RelicDisplayEntry>(StringComparer.Ordinal);
            for (int i = 0; i < relicEntries.Count; i++)
            {
                RelicDisplayEntry entry = relicEntries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Id) &&
                    !entriesById.ContainsKey(entry.Id))
                {
                    entriesById.Add(entry.Id, entry);
                }
            }

            RelicInformation[] definitions = _document == null ||
                _document.information == null
                ? null
                : _document.information.relics;
            if (definitions != null)
            {
                var orderedEntries = new List<RelicDisplayEntry>(definitions.Length);
                for (int i = 0; i < definitions.Length; i++)
                {
                    RelicInformation definition = definitions[i];
                    if (definition == null || string.IsNullOrWhiteSpace(definition.id))
                    {
                        continue;
                    }

                    if (!entriesById.TryGetValue(definition.id, out RelicDisplayEntry entry))
                    {
                        entry = new RelicDisplayEntry();
                        if (defaults.FindById(definition.id) is RelicCollectionCatalogEntry presentation)
                        {
                            entry.ApplyPresentationDefaults(
                                presentation.Id,
                                presentation.Title,
                                presentation.Icon,
                                presentation.Description);
                        }
                    }

                    orderedEntries.Add(entry);
                    entriesById.Remove(definition.id);
                }

                // Preserve unexpected authored entries at the end so a future
                // catalog extension does not silently discard scene data.
                foreach (RelicDisplayEntry entry in entriesById.Values)
                {
                    orderedEntries.Add(entry);
                }

                relicEntries = orderedEntries;
            }

            for (int i = 0; i < relicEntries.Count; i++)
            {
                RelicDisplayEntry entry = relicEntries[i];
                if (entry == null)
                {
                    continue;
                }

                RelicCollectionCatalogEntry presentation = defaults.FindById(entry.Id);
                if (presentation == null)
                {
                    continue;
                }

                entry.ApplyPresentationDefaults(
                    presentation.Id,
                    presentation.Title,
                    presentation.Icon,
                    presentation.Description);
            }
        }

        private void OnEnable()
        {
            LocalizationCatalog.Changed -= RefreshLocalizedContent;
            LocalizationCatalog.Changed += RefreshLocalizedContent;
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

        private void Update()
        {
            UpdateRainbowNodeFrames();
            RefreshResponsiveHeaderLayout(false);
        }

        private void OnDisable()
        {
            LocalizationCatalog.Changed -= RefreshLocalizedContent;
            if (_fitGraphRoutine != null)
            {
                StopCoroutine(_fitGraphRoutine);
                _fitGraphRoutine = null;
            }
            if (_bulkUpgradeRoutine != null)
            {
                SaveProgressionState();
                StopCoroutine(_bulkUpgradeRoutine);
                _bulkUpgradeRoutine = null;
                RestoreBulkResearchRevealState();
            }
            if (_bulkUpgradeButton != null)
            {
                _bulkUpgradeButton.interactable = true;
                SetButtonLabel(_bulkUpgradeButton.GetComponentInChildren<TextMeshProUGUI>(), "가능한 연구 모두 진행");
            }
            if (_runtimeGroup != null) _runtimeGroup.interactable = true;
            CloseSummaryPopup();
            HideNodeTooltip();
            CloseRelicDetail();
            if (_talentFlyCard != null) Destroy(_talentFlyCard);
            _talentFlyCard = null;
            if (_talentSelectionAnimating)
            {
                _talentSelectionAnimating = false;
                _talentDraftRevealed = false;
                _talentDraftIds.Clear();
                if (_showTalentCollection) BuildTalentCollection();
            }
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

        private void RefreshLocalizedContent()
        {
            if (!_isBuilt) return;
            RefreshAll();
            if (!string.IsNullOrEmpty(_tooltipNodeId))
                RefreshNodeTooltip(FindNode(_tooltipNodeId));
            RefreshDetail(FindNode(_selectedNodeId));
        }

        private void OnDestroy()
        {
            if (_summaryOverlay != null) Destroy(_summaryOverlay);
            RestoreDragCursor();
            if (_traitBackdropSprite != null)
            {
                Destroy(_traitBackdropSprite);
                _traitBackdropSprite = null;
            }
            if (_traitBackdropTexture != null)
            {
                Destroy(_traitBackdropTexture);
                _traitBackdropTexture = null;
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
            RefreshAll();
            SaveProgressionState();
        }

        /// <summary>
        /// Advances and persists one sequential boss milestone. App calls this
        /// only after a successful BossDefeated run result.
        /// </summary>
        public void RegisterBossVictory()
        {
            EnsureProgressionState();
            if (unlockedBossTier >= 5)
            {
                return;
            }

            unlockedBossTier = Mathf.Clamp(unlockedBossTier + 1, 0, 5);
            RefreshAll();
            SaveProgressionState();
            SetTestStatus("보스 " + unlockedBossTier + "단계 격파 기록을 저장했습니다.");
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
            TraitNodeDefinition petRoot = FindNode("skill.pet_baby_dragon");
            if (petRoot != null)
            {
                SelectNode(petRoot);
            }
        }

        /// <summary>
        /// Applies the immutable payout produced by one eligible run. Combat
        /// never edits these balances directly; App calls this once after its
        /// RunId/settlement-event guard accepts the result. Valid currency
        /// balances can exist before their supply monsters are unlocked;
        /// receiving currency does not unlock monsters or research.
        /// </summary>
        public bool GrantResourceRewards(IReadOnlyList<ResourceRewardSnapshot> rewards)
        {
            return GrantRunRewards(rewards, null, 0L);
        }

        /// <summary>
        /// Applies currency and relic awards from one run before writing the
        /// profile once, so newly found relics are available on the next run.
        /// </summary>
        public bool GrantRunRewards(
            IReadOnlyList<ResourceRewardSnapshot> rewards,
            IReadOnlyList<string> relicAwards,
            long experienceEarned = 0L,
            bool isBossRun = false)
        {
            if (experienceEarned < 0L) return false;
            EnsureProgressionState();
            EnsureRelicEntries();
            experienceEarned = ApplyTalentExperienceBonus(experienceEarned, isBossRun);

            var relicIndexes = new List<int>();
            if (relicAwards != null && relicAwards.Count > 0)
            {
                RelicInformation[] definitions = _document == null ||
                    _document.information == null
                    ? null
                    : _document.information.relics;
                if (definitions == null)
                    return false;

                var seenAwards = new HashSet<string>(StringComparer.Ordinal);
                for (int awardIndex = 0; awardIndex < relicAwards.Count; awardIndex++)
                {
                    string relicId = relicAwards[awardIndex];
                    if (string.IsNullOrWhiteSpace(relicId) || !seenAwards.Add(relicId))
                        return false;

                    int definitionIndex = -1;
                    for (int i = 0; i < definitions.Length; i++)
                    {
                        if (definitions[i] != null && definitions[i].id == relicId)
                        {
                            definitionIndex = i;
                            break;
                        }
                    }

                    if (definitionIndex < 0 || definitionIndex >= relicEntries.Count ||
                        relicEntries[definitionIndex] == null)
                    {
                        return false;
                    }

                    if (!relicEntries[definitionIndex].IsObtained)
                        relicIndexes.Add(definitionIndex);
                }
            }

            EnsureGemstoneBalanceCapacity();
            long previousGarnet = _garnetBalance;
            long[] previousGemstoneBalances = (long[])_gemstoneBalances.Clone();
            long previousExperience = _experiencePoints;
            int previousCardBalance = _availableTraitCards;
            var previousUnseenRelics = new HashSet<string>(_unseenRelicIds, StringComparer.Ordinal);
            for (int i = 0; rewards != null && i < rewards.Count; i++)
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

            for (int i = 0; i < relicIndexes.Count; i++)
            {
                int relicIndex = relicIndexes[i];
                relicEntries[relicIndex].SetObtained(true);
                _unseenRelicIds.Add(relicEntries[relicIndex].Id);
            }

            long experienceBefore = _experiencePoints;
            _experiencePoints = AddSaturating(_experiencePoints, experienceEarned);
            int previousLevel = ProgressionLevelCurve.GetLevel(experienceBefore);
            int currentLevel = ProgressionLevelCurve.GetLevel(_experiencePoints);
            long cardsEarned = Math.Max(0, currentLevel - previousLevel);
            int fiveLevelMilestones = Math.Max(
                0, currentLevel / 5 - previousLevel / 5);
            cardsEarned += (long)fiveLevelMilestones *
                GetOwnedTalentStackCount("talent.chronicle");
            _availableTraitCards = (int)Math.Min(int.MaxValue,
                (long)_availableTraitCards + cardsEarned);

            RefreshAll();
            if (SaveProgressionState())
                return true;

            _garnetBalance = previousGarnet;
            _gemstoneBalances = previousGemstoneBalances;
            _experiencePoints = previousExperience;
            _availableTraitCards = previousCardBalance;
            _unseenRelicIds.Clear();
            foreach (string id in previousUnseenRelics) _unseenRelicIds.Add(id);
            for (int i = 0; i < relicIndexes.Count; i++)
                relicEntries[relicIndexes[i]].SetObtained(false);
            RefreshAll();
            return false;
        }

        private long ApplyTalentExperienceBonus(long experience, bool isBossRun)
        {
            if (experience <= 0L) return Math.Max(0L, experience);
            int bonusPercent = GetOwnedTalentStackCount("talent.wisdom") * 10;
            if (isBossRun)
                bonusPercent += GetOwnedTalentStackCount("talent.ledger") * 10;
            if (bonusPercent <= 0) return experience;
            decimal increased = decimal.Round(
                (decimal)experience * (100 + bonusPercent) / 100m,
                0,
                MidpointRounding.AwayFromZero);
            return increased >= long.MaxValue ? long.MaxValue : (long)increased;
        }

        private void RestoreOwnedRelics(IReadOnlyList<string> ownedRelicIds)
        {
            EnsureRelicEntries();
            var owned = new HashSet<string>(StringComparer.Ordinal);
            if (ownedRelicIds != null)
            {
                for (int i = 0; i < ownedRelicIds.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(ownedRelicIds[i]))
                        owned.Add(ownedRelicIds[i]);
                }
            }

            RelicInformation[] definitions = _document == null ||
                _document.information == null
                ? null
                : _document.information.relics;
            for (int i = 0; i < relicEntries.Count; i++)
            {
                string id = definitions != null && i < definitions.Length &&
                    definitions[i] != null
                    ? definitions[i].id
                    : string.Empty;
                if (relicEntries[i] != null)
                    relicEntries[i].SetObtained(!string.IsNullOrEmpty(id) && owned.Contains(id));
            }
        }

        private int _highestBossStageCleared;
        private readonly HashSet<string> _settledBossRunIds = new HashSet<string>(StringComparer.Ordinal);
        public int HighestBossStageCleared
        {
            get { EnsureProgressionState(); return _highestBossStageCleared; }
        }

        /// <summary>Resets only boss stage progress, preserving purchases, rewards, and paid receipts.</summary>
        public bool TryResetBossProgress()
        {
            EnsureProgressionState();
            int previousStage = _highestBossStageCleared;
            _highestBossStageCleared = 0;
            if (SaveProgressionState()) return true;
            _highestBossStageCleared = previousStage;
            return false;
        }

        /// <summary>Save rewards, stage progress, and the receipt together. A replayed result is a no-op.</summary>
        public bool GrantBossClear(RunResult result)
        {
            EnsureProgressionState();
            if (!result.BossDefeated || result.SettlementPolicy != RunSettlementPolicy.Eligible ||
                !result.RunId.IsValid || result.BossStage < 1 || result.BossStage > 20 ||
                result.BossStage > _highestBossStageCleared + 1) return false;
            if (_settledBossRunIds.Contains(result.RunId.Value)) return true;
            int previousStage = _highestBossStageCleared;
            int previousTier = unlockedBossTier;
            _highestBossStageCleared = Math.Max(previousStage, result.BossStage);
            // Replaying a cleared stage must not unlock the next research tier.
            unlockedBossTier = Mathf.Max(previousTier, Mathf.Clamp(result.BossStage, 0, 5));
            _settledBossRunIds.Add(result.RunId.Value);
            if (GrantRunRewards(
                    result.Rewards, result.RelicAwards, result.ExperienceEarned,
                    isBossRun: true))
            {
                return true;
            }
            _highestBossStageCleared = previousStage;
            unlockedBossTier = previousTier;
            _settledBossRunIds.Remove(result.RunId.Value);
            RefreshAll();
            return false;
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
            GameInformation information = GameInformationBuilder.Build(_document, _purchased,
                id => GetGemstoneBalance(GetGemstoneIndexById(id)),
                _cursorCombatStatDefaults, GetOwnedRelicIds());
            ApplyOwnedTalentEffects(information);
            if (!information.TryValidate(out string error))
                throw new InvalidOperationException("Resolved talent effects: " + error);
            return information;
        }

        private void ApplyOwnedTalentEffects(GameInformation information)
        {
            if (information == null || information.stats == null) return;
            information.stats.attackPower = AddSaturating(information.stats.attackPower,
                GetOwnedTalentStackCount("talent.might"));
        }

        private HashSet<string> GetOwnedRelicIds()
        {
            EnsureRelicEntries();
            var ownedIds = new HashSet<string>(StringComparer.Ordinal);
            RelicInformation[] definitions = _document == null ||
                _document.information == null
                ? null
                : _document.information.relics;
            if (definitions == null) return ownedIds;
            int count = Mathf.Min(definitions.Length, relicEntries.Count);
            for (int i = 0; i < count; i++)
                if (definitions[i] != null && relicEntries[i] != null &&
                    relicEntries[i].IsObtained)
                    ownedIds.Add(definitions[i].id);
            return ownedIds;
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

#if UNITY_EDITOR
        /// <summary>Refresh authored combat data without reloading purchases or wallets.</summary>
        public bool ReloadAuthoredCombatDefaults(out string error)
        {
            error = string.Empty;
            if (_document == null) return true;
            TextAsset asset = Resources.Load<TextAsset>(GameDataDocument.RuntimeResourcePath);
            if (asset == null)
            { error = "Missing authored combat information."; return false; }
            if (!GameInformationJson.TryDeserialize(asset.text, out GameInformation information,
                out error)) return false;
            _document.information = information;
            return true;
        }
#endif

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
            if (_showFieldCollection)
            {
                ShowTab(_activeTab, false);
                return;
            }
            backRequested?.Invoke();
        }

        public void RequestCombat()
        {
            ShowFieldSelectionForCombat();
        }

        private void StartSelectedFieldCombat()
        {
            if (!_showFieldCollection) return;
            _showFieldCollection = false;
            SetGraphNavigationEnabled(true);
            UpdateHeaderForActiveView();
            SetCollectionChromeVisible(true);
            RefreshCollectionTabButtons();
            BuildNodes(false);
            RefreshSummary();
            combatRequested?.Invoke();
        }

        public void RequestBossCombat()
        {
            bossCombatRequested?.Invoke();
        }

        private void ResolveCatalog()
        {
            _highestBossStageCleared = 0;
            _settledBossRunIds.Clear();
            _document = GameDataDocument.Load();
            LoadBalancePatchOverrides();
            if (PlayerPrefs.HasKey(BalanceProgressionPreferenceKey)) RefreshBalanceDescriptionOverrides();
            // Data-file language does not select the player's UI locale.
            startingGarnet = _document.information.gemstones[0].amount;
            _garnetBalance = startingGarnet;
            startingGemstoneBalances = new long[_document.information.gemstones.Length];
            for (int i = 0; i < startingGemstoneBalances.Length; i++)
                startingGemstoneBalances[i] = _document.information.gemstones[i].amount;
            unlockedBossTier = 0;
            _runtimeCatalog = TraitCatalog.CreateFromDocument(_document);
            _activeCatalog = _runtimeCatalog;
            _ownsRuntimeCatalog = true;
            ApplyRelicPresentationDefaults();
        }

        private void InitializePurchasedState()
        {
            // A new or reset research profile starts at the available central
            // core. Nothing is purchased until the player activates it.
            _purchased.Clear();
            _spentByNode.Clear();
            _spentCurrencyByNode.Clear();
            _spentNodeCosts.Clear();
            EnsureRelicEntries();
            for (int i = 0; i < relicEntries.Count; i++)
                if (relicEntries[i] != null)
                    relicEntries[i].SetObtained(false);
            _unseenRelicIds.Clear();
            _experiencePoints = 0L;
            _selectedFieldTier = 1;
            _availableTraitCards = 0;
            _traitCardDrawCount = 0;
            _ownedTalentIds.Clear();
            _talentDraftIds.Clear();
            _talentDraftRevealed = false;
            _talentRerollsUsed = 0;
        }

        private void LoadSavedProgress()
        {
            if (_document.progression.savedProgressPolicy == "fileOnly" || !loadSavedProgress ||
                !TraitProgressionStore.TryLoad(out TraitProgressionStore.SaveData data))
            {
                return;
            }

            RestoreOwnedRelics(data.ownedRelicIds);
            _unseenRelicIds.Clear();
            if (data.unseenRelicIds != null)
                foreach (string id in data.unseenRelicIds)
                    if (!string.IsNullOrWhiteSpace(id)) _unseenRelicIds.Add(id);
            _garnetBalance = Math.Max(0L, data.garnetBalance);
            _experiencePoints = Math.Max(0L, data.experiencePoints);
            _selectedFieldTier = Mathf.Clamp(data.selectedFieldTier, 1, 5);
            _availableTraitCards = Math.Max(0, data.availableTraitCards);
            _traitCardDrawCount = Math.Max(0, data.traitCardDrawCount);
            _ownedTalentIds.Clear();
            if (data.ownedTraitCardIds != null)
                foreach (string id in data.ownedTraitCardIds)
                    if (!string.IsNullOrWhiteSpace(id)) _ownedTalentIds.Add(id);
            _highestBossStageCleared = data.highestBossStageCleared;
            _settledBossRunIds.Clear();
            foreach (string id in data.settledBossRunIds) _settledBossRunIds.Add(id);
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
            unlockedBossTier = Mathf.Max(
                unlockedBossTier,
                Mathf.Clamp(data.unlockedBossTier, 0, 5));
            _selectedFieldTier = Mathf.Clamp(_selectedFieldTier, 1,
                Mathf.Clamp(1 + ProgressionLevelCurve.GetLevel(_experiencePoints) / 5, 1, 5));
            _spentNodeCosts.Clear();
            if (data.spentCostEntries != null)
            {
                for (int i = 0; i < data.spentCostEntries.Count; i++)
                {
                    TraitProgressionStore.SpentCostEntry receipt = data.spentCostEntries[i];
                    if (receipt == null || string.IsNullOrWhiteSpace(receipt.nodeId) ||
                        string.IsNullOrWhiteSpace(receipt.currencyId) || receipt.amount <= 0)
                        continue;
                    if (!_spentNodeCosts.TryGetValue(receipt.nodeId, out var receipts))
                    {
                        receipts = new List<TraitProgressionStore.SpentCostEntry>();
                        _spentNodeCosts.Add(receipt.nodeId, receipts);
                    }
                    receipts.Add(new TraitProgressionStore.SpentCostEntry
                    {
                        nodeId = receipt.nodeId,
                        currencyId = receipt.currencyId,
                        amount = receipt.amount
                    });
                }
            }
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
                    // Retired skills have no replacement node. Return their
                    // recorded spend when loading a save from the old tree.
                    bool retiredSkill = !string.IsNullOrEmpty(data.purchasedNodeIds[i]) &&
                        (data.purchasedNodeIds[i].StartsWith("skill.", StringComparison.Ordinal) ||
                         data.purchasedNodeIds[i].StartsWith("pet.cursor.", StringComparison.Ordinal));
                    if ((data.version < 2 || retiredSkill) &&
                        data.spentCosts != null && i < data.spentCosts.Count)
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

                if (!_spentNodeCosts.ContainsKey(node.Id) &&
                    data.spentCosts != null && i < data.spentCosts.Count &&
                    data.spentCosts[i] > 0)
                {
                    string legacyCurrency = data.spentCurrencyIds != null && i < data.spentCurrencyIds.Count
                        ? ProgressionMigration.CurrencyId(data.spentCurrencyIds[i])
                        : node.CostGemstoneId;
                    _spentNodeCosts[node.Id] = new List<TraitProgressionStore.SpentCostEntry>
                    {
                        new TraitProgressionStore.SpentCostEntry
                        {
                            nodeId = node.Id,
                            currencyId = legacyCurrency,
                            amount = data.spentCosts[i]
                        }
                    };
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
                _spentCurrencyByNode,
                unlockedBossTier: unlockedBossTier,
                ownedRelicIds: GetOwnedRelicIds(),
                highestBossStageCleared: _highestBossStageCleared,
                settledBossRunIds: _settledBossRunIds,
                unseenRelicIds: _unseenRelicIds,
                experiencePoints: _experiencePoints,
                availableTraitCards: _availableTraitCards,
                traitCardDrawCount: _traitCardDrawCount,
                selectedFieldTier: _selectedFieldTier,
                ownedTraitCardIds: _ownedTalentIds,
                spentCostEntries: _spentNodeCosts);
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
            background.sprite = null;
            background.type = Image.Type.Simple;
            background.color = backgroundColor;
            background.preserveAspect = false;
            background.raycastTarget = true;
            _runtimeRoot.gameObject.AddComponent<TraitTooltipDismissRelay>().Bind(this);

            // Use the project's castle landscape as the board artwork. The
            // subdued tint and the translucent atmosphere layer below keep
            // the art visible without competing with nodes or text.
            if (combatBackgroundSprite != null)
            {
                RectTransform artworkRect = CreateRect(
                    "TraitBackdropArtwork",
                    _runtimeRoot,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero,
                    new Vector2(0.5f, 0.5f));
                Image artwork = artworkRect.gameObject.AddComponent<Image>();
                artwork.sprite = combatBackgroundSprite;
                artwork.type = Image.Type.Simple;
                artwork.color = new Color(0.46f, 0.53f, 0.65f, 1f);
                artwork.preserveAspect = false;
                artwork.raycastTarget = false;
            }

            // A charcoal-blue veil blends the artwork toward a neutral tone
            // before the navy gradient, reducing color noise behind the nodes.
            RectTransform desaturationRect = CreateRect(
                "TraitBackdropDesaturation",
                _runtimeRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Vector2(0.5f, 0.5f));
            RawImage desaturation = desaturationRect.gameObject.AddComponent<RawImage>();
            desaturation.texture = Texture2D.whiteTexture;
            desaturation.color = new Color(0.13f, 0.14f, 0.16f, 0.34f);
            desaturation.raycastTarget = false;

            // A translucent navy gradient darkens the artwork, while a soft
            // edge vignette focuses attention on the research board.
            RectTransform gradientRect = CreateRect(
                "TraitBackdropAtmosphere",
                _runtimeRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Vector2(0.5f, 0.5f));
            Image gradient = gradientRect.gameObject.AddComponent<Image>();
            gradient.sprite = CreateTraitBackdropSprite();
            gradient.type = Image.Type.Simple;
            gradient.color = Color.white;
            gradient.preserveAspect = false;
            gradient.raycastTarget = false;

            _runtimeGroup = _runtimeRoot.gameObject.AddComponent<CanvasGroup>();
            _runtimeGroup.interactable = true;
            _runtimeGroup.blocksRaycasts = true;

            BuildNodePanel();
            BuildHeader();
            BuildGemstonePanel();
            BuildCombatStatsPanel();
            BuildFooter();
            // Keep the full-information overlay available to the App HUD,
            // while omitting the always-visible summary and detail columns.
            BuildSummaryPopup();
            BuildNodeTooltip();

            _isBuilt = true;
            if (initialShowRelicCollection)
            {
                ShowRelicCollection(false);
            }
            else
            {
                ShowTab((TraitTab)Mathf.Clamp(initialTab, 0, 2), false);
            }
            UpdateHeaderForActiveView();
            Canvas.ForceUpdateCanvases();
            RefreshResponsiveHeaderLayout(true);
        }

        private Sprite CreateTraitBackdropSprite()
        {
            const int textureSize = 512;
            _traitBackdropTexture = new Texture2D(
                textureSize,
                textureSize,
                TextureFormat.RGBA32,
                false,
                false)
            {
                name = "TraitScreen_NavyAtmosphereVignette",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color[] pixels = new Color[textureSize * textureSize];
            Color upperNavy = new Color32(5, 11, 27, 255);
            Color lowerNavy = new Color32(10, 17, 38, 255);

            for (int y = 0; y < textureSize; y++)
            {
                float v = y / (float)(textureSize - 1);
                Color vertical = Color.Lerp(upperNavy, lowerNavy, v);

                for (int x = 0; x < textureSize; x++)
                {
                    float u = x / (float)(textureSize - 1);
                    Color color = vertical;
                    float edge = Mathf.Max(
                        Mathf.Abs(u - 0.5f) * 2f,
                        Mathf.Abs(v - 0.5f) * 2f);
                    float vignette = Mathf.SmoothStep(0.42f, 1f, edge);
                    color.a = Mathf.Lerp(0.52f, 0.63f, v) + vignette * 0.25f;
                    pixels[y * textureSize + x] = color;
                }
            }

            _traitBackdropTexture.SetPixels(pixels);
            _traitBackdropTexture.Apply(false, true);
            _traitBackdropSprite = Sprite.Create(
                _traitBackdropTexture,
                new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                100f);
            _traitBackdropSprite.name = "TraitScreen_NavyAtmosphereVignetteSprite";
            _traitBackdropSprite.hideFlags = HideFlags.DontSave;
            return _traitBackdropSprite;
        }

        private void BuildHeader()
        {
            BuildTopCategoryTabs(_runtimeRoot);
            float tabsWidth = GetTopCategoryTabsWidth(_runtimeRoot);
            float titleWidth = (GetLogicalCanvasSize(_runtimeRoot).x - tabsWidth) * 0.5f - 48f;
            RectTransform title = CreateRect(
                "Title",
                _runtimeRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -66f),
                new Vector2(Mathf.Max(0f, titleWidth), 54f),
                new Vector2(0f, 1f));
            _screenTitle = CreateText(title, "특성", 28, TextAnchor.MiddleLeft, inkColor, true);
            title.gameObject.SetActive(titleWidth >= 96f);
            BuildTestTools(_runtimeRoot);
            BuildRelicTestModeToggle(_runtimeRoot);
            BuildBalancePatchEditor(_runtimeRoot);
            BuildBoardLayoutEditorToggle(_runtimeRoot);
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
            float tabsWidth = GetTopCategoryTabsWidth(parent);
            const float tabGap = 8f;
            float buttonWidth = Mathf.Max(1f, (tabsWidth - tabGap * 2f) / 3f);
            RectTransform tabs = CreatePanel(
                "ResearchCategoryTabs",
                parent,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -24f),
                new Vector2(tabsWidth, 90f),
                panelSprite,
                new Color(0.025f, 0.055f, 0.105f, 0.96f),
                new Vector2(0.5f, 1f));
            Image strip = tabs.GetComponent<Image>();
            strip.raycastTarget = false;
            Outline stripOutline = tabs.GetComponent<Outline>();
            if (stripOutline != null)
            {
                // The sliced frame sprite already carries the visible edge.
                // A second vertex-offset outline softened the edge at scaled
                // Game-view resolutions, so keep the authored border crisp.
                stripOutline.enabled = false;
            }

            _researchCategoryButton = CreateButton(
                "ResearchTreeTab",
                tabs,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-(buttonWidth + tabGap), 0f),
                new Vector2(buttonWidth, 68f),
                researchCategoryTabFrame != null
                    ? researchCategoryTabFrame
                    : tabFocusSprite != null ? tabFocusSprite : buttonFrameSprite,
                Color.white,
                "연구",
                24,
                new Color(0.08f, 0.11f, 0.16f, 1f));
            AddButtonIcon(
                _researchCategoryButton.transform as RectTransform,
                petIcon != null ? petIcon : researchTabIcon,
                new Vector2(18f, 0f));
            AddCollectionTabSelectionAccent(_researchCategoryButton);
            _researchCategoryButton.onClick.AddListener(() => ShowTab(TraitTab.Stat));

            _talentCategoryButton = CreateButton(
                "TalentTab",
                tabs,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2((buttonWidth + tabGap), 0f),
                new Vector2(buttonWidth, 68f),
                talentCategoryTabFrame != null
                    ? talentCategoryTabFrame
                    : tabSprite != null ? tabSprite : buttonFrameSprite,
                Color.white,
                "특성",
                24,
                new Color(0.12f, 0.15f, 0.18f, 1f));
            AddButtonIcon(
                _talentCategoryButton.transform as RectTransform,
                talentTabIcon != null ? talentTabIcon : skillIcon,
                new Vector2(18f, 0f));
            AddCollectionTabSelectionAccent(_talentCategoryButton);
            _talentCategoryButton.onClick.AddListener(() => ShowTalentCollection());

            _relicCategoryButton = CreateButton(
                "RelicTab",
                tabs,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(buttonWidth, 68f),
                relicCategoryTabFrame != null
                    ? relicCategoryTabFrame
                    : tabSprite != null ? tabSprite : buttonFrameSprite,
                Color.white,
                "유물",
                24,
                new Color(0.12f, 0.15f, 0.18f, 1f));
            Sprite relicTabIcon = null;
            TryGetRelicPresentation("relic.red_potion", out _, out relicTabIcon);
            AddButtonIcon(
                _relicCategoryButton.transform as RectTransform,
                relicTabIcon != null ? relicTabIcon : gemstoneIcon != null ? gemstoneIcon : gemIcon,
                new Vector2(18f, 0f));
            AddCollectionTabSelectionAccent(_relicCategoryButton);
            _relicCategoryButton.onClick.AddListener(() => ShowRelicCollection());
            RefreshCollectionTabButtons();
        }

        private void AddCollectionTabSelectionAccent(Button button)
        {
            if (button == null) return;
            RectTransform accent = CreateRect(
                "SelectedAccent",
                button.transform,
                new Vector2(0.18f, 0f),
                new Vector2(0.82f, 0f),
                new Vector2(0f, 5f),
                new Vector2(0f, 3f),
                new Vector2(0.5f, 0f));
            Image image = accent.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 0.82f, 0.42f, 1f);
            image.raycastTarget = false;
            Shadow glow = accent.gameObject.AddComponent<Shadow>();
            glow.effectColor = new Color(1f, 0.56f, 0.17f, 0.52f);
            glow.effectDistance = new Vector2(0f, -1f);
            glow.useGraphicAlpha = true;
            accent.gameObject.SetActive(false);
        }

        private static float GetTopCategoryTabsWidth(RectTransform parent)
        {
            if (parent == null) return 0f;
            float width = GetLogicalCanvasSize(parent).x;
            float availableWidth = Mathf.Max(0f, width - 48f);
            float preferredWidth = Mathf.Max(680f, width * 0.56f);
            return Mathf.Min(1280f, availableWidth, preferredWidth);
        }

        private static Vector2 GetLogicalCanvasSize(RectTransform parent)
        {
            if (parent == null) return Vector2.zero;
            Vector2 size = parent.rect.size;
            if (size.x > 1f && size.y > 1f) return size;

            // Awake can build the screen before the Canvas has completed its
            // first layout pass. Use its logical pixel scale until the real
            // RectTransform dimensions become available.
            Canvas canvas = parent.GetComponentInParent<Canvas>();
            float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
            return new Vector2(Screen.width / scale, Screen.height / scale);
        }

        private void RefreshResponsiveHeaderLayout(bool force)
        {
            if (_runtimeRoot == null) return;
            Vector2 viewportSize = GetLogicalCanvasSize(_runtimeRoot);
            if (viewportSize.x <= 1f || viewportSize.y <= 1f) return;
            if (!force && (viewportSize - _lastHeaderViewportSize).sqrMagnitude < 1f) return;
            _lastHeaderViewportSize = viewportSize;

            float tabsWidth = GetTopCategoryTabsWidth(_runtimeRoot);
            float toolWidth = GetHeaderToolButtonWidth(_runtimeRoot);
            RectTransform tabs = _runtimeRoot.Find("ResearchCategoryTabs") as RectTransform;
            if (tabs != null)
            {
                const float tabGap = 8f;
                float buttonWidth = Mathf.Max(1f, (tabsWidth - tabGap * 2f) / 3f);
                tabs.sizeDelta = new Vector2(tabsWidth, 90f);
                ResizeHeaderTab(_researchCategoryButton, buttonWidth, -(buttonWidth + tabGap));
                ResizeHeaderTab(_relicCategoryButton, buttonWidth, 0f);
                ResizeHeaderTab(_talentCategoryButton, buttonWidth, buttonWidth + tabGap);
            }

            if (_screenTitle != null)
            {
                RectTransform title = _screenTitle.rectTransform;
                float titleWidth = (viewportSize.x - tabsWidth) * 0.5f - 48f;
                title.sizeDelta = new Vector2(Mathf.Max(0f, titleWidth), 54f);
                title.gameObject.SetActive(titleWidth >= 96f);
            }

            ResizeHeaderTool(_relicTestModeButton, toolWidth);
            ResizeHeaderTool(_balanceEditorToggle, toolWidth);
            ResizeHeaderTool(_boardLayoutEditButton, toolWidth);
            ResizeHeaderTool(_boardLayoutResetButton, toolWidth);
            PositionHeaderTool(
                _runtimeRoot.Find("TestToolsToggle")?.GetComponent<Button>(),
                24f + 148f * 0.5f,
                42f);
            float balanceCenterX = 24f + 148f + 12f + toolWidth * 0.5f;
            PositionHeaderTool(_balanceEditorToggle, balanceCenterX, 42f);
            PositionHeaderTool(_relicTestModeButton, balanceCenterX, 42f);
            float layoutCenterX = 24f + 148f + 12f + toolWidth + 12f + toolWidth * 0.5f;
            PositionHeaderTool(_boardLayoutEditButton, layoutCenterX, 42f);
            PositionHeaderTool(_boardLayoutResetButton, layoutCenterX, 88f);

        }

        private static void ResizeHeaderTab(Button button, float width, float x)
        {
            if (button == null) return;
            RectTransform rect = button.transform as RectTransform;
            rect.sizeDelta = new Vector2(width, 68f);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        private static void ResizeHeaderTool(Button button, float width)
        {
            if (button == null) return;
            RectTransform rect = button.transform as RectTransform;
            Vector2 size = rect.sizeDelta;
            rect.sizeDelta = new Vector2(width, size.y);
        }

        private static void PositionHeaderTool(Button button, float x, float y)
        {
            if (button == null) return;
            RectTransform rect = button.transform as RectTransform;
            rect.anchoredPosition = new Vector2(x, y);
        }

        private void BuildRelicTestModeToggle(RectTransform parent)
        {
            _relicTestModeButton = CreateButton(
                "RelicTestModeToggle",
                parent,
                Vector2.zero,
                Vector2.zero,
                new Vector2(24f + 148f + 12f + GetHeaderToolButtonWidth(parent) * 0.5f, 42f),
                new Vector2(GetHeaderToolButtonWidth(parent), 36f),
                null,
                new Color(0.28f, 0.20f, 0.10f, 0.98f),
                "유물 개별 테스트",
                13,
                inkColor);
            _relicTestModeLabel = _relicTestModeButton.GetComponentInChildren<TextMeshProUGUI>();
            _relicTestModeButton.onClick.AddListener(() => SetRelicTestMode(!_relicTestMode));
            _relicTestModeButton.gameObject.SetActive(false);
        }

        private void BuildBoardLayoutEditorToggle(RectTransform parent)
        {
            _boardLayoutEditButton = CreateButton(
                "ResearchLayoutEditorToggle",
                parent,
                Vector2.zero,
                Vector2.zero,
                new Vector2(24f + 148f + 12f + GetHeaderToolButtonWidth(parent) + 12f + GetHeaderToolButtonWidth(parent) * 0.5f, 42f),
                new Vector2(GetHeaderToolButtonWidth(parent), 36f),
                null,
                new Color(0.12f, 0.22f, 0.31f, 0.98f),
                "배치 편집: 끔",
                13,
                inkColor);
            _boardLayoutEditButton.onClick.AddListener(ToggleBoardLayoutEditor);
            _boardLayoutResetButton = CreateButton(
                "ResearchLayoutReset", parent, Vector2.zero, Vector2.zero,
                new Vector2(24f + 148f + 12f + GetHeaderToolButtonWidth(parent) + 12f + GetHeaderToolButtonWidth(parent) * 0.5f, 88f), new Vector2(GetHeaderToolButtonWidth(parent), 32f),
                buttonFrameSprite, new Color(0.28f, 0.31f, 0.38f, 1f),
                "배치 원본 복원", 13, inkColor);
            _boardLayoutResetButton.onClick.AddListener(RestoreBoardLayout);
            _boardLayoutResetButton.gameObject.SetActive(false);
        }

        private void BuildBalancePatchEditor(RectTransform parent)
        {
            _balanceEditorToggle = CreateButton(
                "BalancePatchEditorToggle",
                parent,
                Vector2.zero,
                Vector2.zero,
                new Vector2(24f + 148f + 12f + GetHeaderToolButtonWidth(parent) * 0.5f, 42f),
                new Vector2(GetHeaderToolButtonWidth(parent), 36f),
                null,
                new Color(0.33f, 0.22f, 0.12f, 0.98f),
                "밸런스 패치",
                13,
                inkColor);
            _balanceEditorToggle.onClick.AddListener(() => SetBalancePatchEditorOpen(!_balanceEditorOpen));

            _balanceEditorPanel = CreatePanel(
                "BalancePatchEditorPanel",
                parent,
                Vector2.zero,
                Vector2.zero,
                new Vector2(24f, 68f),
                new Vector2(940f, 820f),
                panelSprite,
                new Color(0.018f, 0.032f, 0.065f, 0.99f),
                Vector2.zero);
            Outline border = _balanceEditorPanel.GetComponent<Outline>();
            if (border != null)
            {
                border.effectColor = new Color(0.88f, 0.66f, 0.30f, 0.95f);
                border.effectDistance = new Vector2(2f, -2f);
            }
            CreateText(CreateRect("BalancePatchTitle", _balanceEditorPanel,
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(20f, -14f), new Vector2(-70f, 38f), new Vector2(0f, 1f)),
                "전투 밸런스 패치", 22, TextAnchor.MiddleLeft,
                new Color(1f, 0.82f, 0.47f, 1f), true);
            Button close = CreateButton(
                "BalancePatchClose", _balanceEditorPanel,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -14f), new Vector2(34f, 34f), null,
                new Color(0.18f, 0.22f, 0.29f, 1f), "×", 18, inkColor);
            close.onClick.AddListener(() => SetBalancePatchEditorOpen(false));

            CreateText(CreateRect("BalanceTargetListTitle", _balanceEditorPanel,
                    new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(20f, -70f), new Vector2(268f, 34f), new Vector2(0f, 1f)),
                "편집할 항목", 16, TextAnchor.MiddleLeft,
                new Color(1f, 0.82f, 0.47f, 1f), true);
            _balanceTargetListViewport = CreateImageRect(
                "BalanceTargetListViewport", _balanceEditorPanel,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -110f), new Vector2(268f, 586f),
                null, new Color(0.025f, 0.05f, 0.085f, 0.95f), new Vector2(0f, 1f));
            _balanceTargetListViewport.GetComponent<Image>().raycastTarget = true;
            _balanceTargetListViewport.gameObject.AddComponent<RectMask2D>();
            _balanceTargetListContent = CreateRect(
                "BalanceTargetListContent", _balanceTargetListViewport,
                new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero,
                new Vector2(0f, 0f), new Vector2(0f, 1f));
            _balanceTargetListScroll = _balanceTargetListViewport.gameObject.AddComponent<ScrollRect>();
            _balanceTargetListScroll.viewport = _balanceTargetListViewport;
            _balanceTargetListScroll.content = _balanceTargetListContent;
            _balanceTargetListScroll.horizontal = false;
            _balanceTargetListScroll.vertical = true;
            _balanceTargetListScroll.movementType = ScrollRect.MovementType.Clamped;
            _balanceTargetListScroll.scrollSensitivity = 42f;

            _balanceEditorTargetLabel = CreateText(CreateRect(
                    "BalanceTargetName", _balanceEditorPanel,
                    new Vector2(0.34f, 1f), new Vector2(1f, 1f),
                    new Vector2(0f, -70f), new Vector2(-24f, 40f), new Vector2(0f, 1f)),
                string.Empty, 20, TextAnchor.MiddleLeft, inkColor, true);

            string[] defaultLabels =
            {
                "필드 1", "필드 2", "필드 3", "필드 4", "필드 5", "필드 6", "필드 7"
            };
            for (int i = 0; i < _balanceEditorFields.Length; i++)
            {
                float y = -160f - i * 42f;
                _balanceEditorFieldLabels[i] = CreateText(CreateRect(
                        "BalanceFieldLabel_" + i, _balanceEditorPanel,
                        new Vector2(0.34f, 1f), new Vector2(0.64f, 1f),
                        new Vector2(0f, y + 40f), new Vector2(0f, 38f), new Vector2(0f, 1f)),
                    defaultLabels[i], 14, TextAnchor.MiddleLeft, mutedInkColor, true);
                _balanceEditorFields[i] = CreateBalancePatchInput(
                    _balanceEditorPanel, "BalanceField_" + i,
                    new Vector2(0.66f, 1f), new Vector2(1f, 1f),
                    new Vector2(0f, y + 40f), new Vector2(-22f, 38f), "0", false);
            }

            _balanceEditorDescriptionRoot = CreateRect(
                "BalanceDescriptionRoot", _balanceEditorPanel,
                new Vector2(0.34f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -500f), new Vector2(-24f, 96f), new Vector2(0f, 1f));
            CreateText(CreateRect("BalanceDescriptionLabel", _balanceEditorDescriptionRoot,
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                    Vector2.zero, new Vector2(0f, 26f), new Vector2(0f, 1f)),
                "연구 설명", 13, TextAnchor.MiddleLeft, mutedInkColor, true);
            _balanceEditorDescription = CreateBalancePatchInput(
                _balanceEditorDescriptionRoot, "BalanceDescription",
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -30f),
                string.Empty, true);

            _balanceEditorStatus = CreateText(CreateRect(
                    "BalanceEditorStatus", _balanceEditorPanel,
                    new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(310f, 82f), new Vector2(590f, 26f), new Vector2(0f, 0f)),
                string.Empty, 13, TextAnchor.MiddleLeft,
                new Color(1f, 0.80f, 0.48f, 1f), true);
            Button apply = CreateButton(
                "BalancePatchApply", _balanceEditorPanel,
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(310f, 24f), new Vector2(300f, 48f), buttonFrameSprite,
                new Color(0.78f, 0.43f, 0.15f, 1f), "적용하고 저장", 16, inkColor);
            apply.onClick.AddListener(ApplyBalanceEditorChanges);

            _balanceEditorPanel.gameObject.SetActive(false);
            RefreshBalanceEditor();
        }

        private static float GetHeaderToolButtonWidth(RectTransform parent)
        {
            return parent == null
                ? 148f
                : Mathf.Clamp(GetLogicalCanvasSize(parent).x * 0.10f, 126f, 174f);
        }

        private TMP_InputField CreateBalancePatchInput(
            RectTransform parent, string objectName, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta,
            string value, bool multiline)
        {
            RectTransform rect = CreateImageRect(
                objectName, parent, anchorMin, anchorMax, anchoredPosition, sizeDelta,
                null, new Color(0.035f, 0.075f, 0.13f, 1f),
                new Vector2(anchorMin.x, anchorMax.y));
            Image background = rect.GetComponent<Image>();
            background.raycastTarget = true;
            TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.textViewport = rect;
            input.contentType = multiline
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.DecimalNumber;
            input.lineType = multiline
                ? TMP_InputField.LineType.MultiLineNewline
                : TMP_InputField.LineType.SingleLine;
            input.characterLimit = multiline ? 320 : 14;
            TextMeshProUGUI fieldText = CreateText(CreateRect(
                    "Text", rect, Vector2.zero, Vector2.one,
                    new Vector2(10f, 1f), new Vector2(-20f, -2f), new Vector2(0.5f, 0.5f)),
                value, multiline ? 13 : 15, multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft,
                inkColor, false);
            fieldText.raycastTarget = false;
            fieldText.textWrappingMode = TextWrappingModes.Normal;
            input.textComponent = fieldText;
            input.text = value ?? string.Empty;
            input.selectionColor = new Color(0.30f, 0.65f, 1f, 0.45f);
            return input;
        }

        private static readonly string[] BalanceEditorCurrencyIds =
        {
            "gem.garnet", "gem.topaz", "gem.amethyst", "gem.sapphire", "gem.diamond", "gem.dragon"
        };

        private void SetBalancePatchEditorOpen(bool open)
        {
            _balanceEditorOpen = open;
            if (_balanceEditorPanel != null) _balanceEditorPanel.gameObject.SetActive(open);
            if (open)
            {
                SetBoardLayoutEditing(false);
                SetRelicTestMode(false);
                SetTestToolsOpen(false);
                RefreshBalanceTargetList();
                if (_balanceTargetListScroll != null)
                    _balanceTargetListScroll.verticalNormalizedPosition = 1f;
                RefreshBalanceEditor();
            }
            RefreshProgressionSidebarVisibility();
        }

        private void SelectBalanceEditorTarget(BalanceEditorTab tab, int index)
        {
            _balanceEditorTab = tab;
            _balanceEditorIndex = index;
            RefreshBalanceEditor();
        }

        private void RefreshBalanceTargetList()
        {
            if (_balanceTargetListContent == null) return;
            DestroyChildren(_balanceTargetListContent);
            _balanceTargetButtons.Clear();

            float y = 14f;
            float contentWidth = 268f;
            string[] groupTitles = { "몬스터", "스킬", "연구" };
            for (int group = 0; group < groupTitles.Length; group++)
            {
                BalanceEditorTab tab = (BalanceEditorTab)group;
                int count = GetBalanceEditorTargetCount(tab);
                if (count <= 0) continue;

                CreateText(CreateRect(
                        "BalanceGroup_" + tab.ToString(), _balanceTargetListContent,
                        new Vector2(0f, 1f), new Vector2(1f, 1f),
                        new Vector2(0f, -y - 14f), new Vector2(0f, 28f),
                        new Vector2(0f, 0.5f)),
                    groupTitles[group], 15, TextAnchor.MiddleLeft,
                    new Color(1f, 0.82f, 0.47f, 1f), true);
                y += 34f;

                for (int index = 0; index < count; index++)
                {
                    string id = GetBalanceEditorTargetId(tab, index);
                    string title = ResolveBalanceEditorTitle(id);
                    BalanceEditorTab capturedTab = tab;
                    int capturedIndex = index;
                    Button button = CreateButton(
                        "BalanceTarget_" + tab.ToString() + "_" + index.ToString(CultureInfo.InvariantCulture),
                        _balanceTargetListContent,
                        new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(contentWidth * 0.5f, -y - 16f),
                        new Vector2(contentWidth - 12f, 32f),
                        null, new Color(0.08f, 0.14f, 0.22f, 0.96f),
                        title, 13, inkColor);
                    TextMeshProUGUI buttonLabel = button.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonLabel != null)
                    {
                        buttonLabel.alignment = TextAlignmentOptions.MidlineLeft;
                        buttonLabel.textWrappingMode = TextWrappingModes.NoWrap;
                        buttonLabel.overflowMode = TextOverflowModes.Ellipsis;
                        buttonLabel.margin = new Vector4(10f, 0f, 6f, 0f);
                    }
                    button.onClick.AddListener(() => SelectBalanceEditorTarget(capturedTab, capturedIndex));
                    Outline outline = button.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0.28f, 0.44f, 0.63f, 0.35f);
                    outline.effectDistance = new Vector2(1f, -1f);
                    _balanceTargetButtons.Add(new BalanceEditorTargetButton
                    {
                        Tab = tab,
                        Index = index,
                        Button = button
                    });
                    y += 36f;
                }

                y += 8f;
            }

            _balanceTargetListContent.sizeDelta = new Vector2(0f, y + 8f);
            RefreshBalanceTargetListSelection();
        }

        private int GetBalanceEditorTargetCount()
        {
            return GetBalanceEditorTargetCount(_balanceEditorTab);
        }

        private int GetBalanceEditorTargetCount(BalanceEditorTab tab)
        {
            if (_document == null) return 0;
            switch (tab)
            {
                case BalanceEditorTab.Monsters:
                    return _document.information.monsters == null ? 0 : _document.information.monsters.Length;
                case BalanceEditorTab.Skills:
                    return _document.information.skills == null ? 0 : _document.information.skills.Length;
                case BalanceEditorTab.Research:
                    return CountResearchNodes();
                default:
                    return 0;
            }
        }

        private int CountResearchNodes()
        {
            int count = 0;
            if (_document == null || _document.progression == null || _document.progression.categories == null)
                return count;
            foreach (UpgradeCategoryData category in _document.progression.categories)
                if (category != null && category.nodes != null) count += category.nodes.Length;
            return count;
        }

        private void RefreshBalanceEditor()
        {
            if (_balanceEditorTargetLabel == null || _balanceEditorFields.Length == 0) return;
            if (_document == null || GetBalanceEditorTargetCount() <= 0)
            {
                SetLocalizedText(_balanceEditorTargetLabel, "편집할 항목이 없습니다.");
                for (int i = 0; i < _balanceEditorFields.Length; i++)
                    SetBalanceEditorField(i, string.Empty, string.Empty, false);
                if (_balanceEditorDescriptionRoot != null) _balanceEditorDescriptionRoot.gameObject.SetActive(false);
                return;
            }

            _balanceEditorIndex = Mathf.Clamp(_balanceEditorIndex, 0, GetBalanceEditorTargetCount() - 1);
            _balanceEditorTargetId = GetBalanceEditorTargetId(_balanceEditorIndex);
            if (string.IsNullOrEmpty(_balanceEditorTargetId)) return;
            RefreshBalanceTargetListSelection();
            string title = ResolveBalanceEditorTitle(_balanceEditorTargetId);
            SetLocalizedText(_balanceEditorTargetLabel, title);
            for (int i = 0; i < _balanceEditorFields.Length; i++)
                SetBalanceEditorField(i, string.Empty, string.Empty, false);
            if (_balanceEditorDescriptionRoot != null) _balanceEditorDescriptionRoot.gameObject.SetActive(false);

            switch (_balanceEditorTab)
            {
                case BalanceEditorTab.Monsters:
                    RefreshMonsterBalanceFields(_balanceEditorTargetId);
                    break;
                case BalanceEditorTab.Skills:
                    RefreshSkillBalanceFields(_balanceEditorTargetId);
                    break;
                case BalanceEditorTab.Research:
                    RefreshResearchBalanceFields(_balanceEditorTargetId);
                    break;
            }
        }

        private string GetBalanceEditorTargetId(int index)
        {
            return GetBalanceEditorTargetId(_balanceEditorTab, index);
        }

        private string GetBalanceEditorTargetId(BalanceEditorTab tab, int index)
        {
            if (_document == null || index < 0 || index >= GetBalanceEditorTargetCount(tab))
                return string.Empty;
            if (tab == BalanceEditorTab.Monsters)
                return _document.information.monsters[index].id;
            if (tab == BalanceEditorTab.Skills)
                return _document.information.skills[index].id;

            int remaining = index;
            foreach (UpgradeCategoryData category in _document.progression.categories)
            {
                if (category == null || category.nodes == null) continue;
                if (remaining < category.nodes.Length) return category.nodes[remaining].id;
                remaining -= category.nodes.Length;
            }
            return string.Empty;
        }

        private void RefreshBalanceTargetListSelection()
        {
            for (int i = 0; i < _balanceTargetButtons.Count; i++)
            {
                BalanceEditorTargetButton entry = _balanceTargetButtons[i];
                if (entry == null || entry.Button == null) continue;
                bool selected = entry.Tab == _balanceEditorTab && entry.Index == _balanceEditorIndex;
                ColorBlock colors = entry.Button.colors;
                colors.normalColor = selected
                    ? new Color(0.48f, 0.31f, 0.13f, 1f)
                    : new Color(0.08f, 0.14f, 0.22f, 0.96f);
                colors.highlightedColor = selected
                    ? new Color(0.62f, 0.42f, 0.19f, 1f)
                    : new Color(0.15f, 0.25f, 0.36f, 1f);
                colors.pressedColor = new Color(0.22f, 0.30f, 0.40f, 1f);
                colors.selectedColor = colors.highlightedColor;
                entry.Button.colors = colors;
                Outline outline = entry.Button.GetComponent<Outline>();
                if (outline != null)
                    outline.effectColor = selected
                        ? new Color(1f, 0.79f, 0.34f, 0.96f)
                        : new Color(0.28f, 0.44f, 0.63f, 0.35f);
            }
        }

        private string ResolveBalanceEditorTitle(string id)
        {
            TraitNodeDefinition node = FindNode(id);
            if (node != null) return GetLocalizedNodeTitle(node);
            EntityTextDefinition entity = _document.FindEntity(id);
            return entity == null ? id : entity.displayName;
        }

        private void SetBalanceEditorField(int index, string label, string value, bool visible)
        {
            if (index < 0 || index >= _balanceEditorFields.Length) return;
            if (_balanceEditorFieldLabels[index] != null)
            {
                _balanceEditorFieldLabels[index].gameObject.SetActive(visible);
                if (visible) SetLocalizedText(_balanceEditorFieldLabels[index], label);
            }
            if (_balanceEditorFields[index] != null)
            {
                _balanceEditorFields[index].gameObject.SetActive(visible);
                _balanceEditorFields[index].interactable = visible;
                if (visible) _balanceEditorFields[index].text = value ?? string.Empty;
            }
        }

        private void RefreshMonsterBalanceFields(string monsterId)
        {
            MonsterInformation monster = FindMonsterInformation(monsterId);
            if (monster == null) return;
            SetBalanceEditorField(0, "체력", monster.hitPoints.ToString(CultureInfo.InvariantCulture), true);
            SetBalanceEditorField(1, "한 번에 생성", monster.productionCount.ToString(CultureInfo.InvariantCulture), true);
            SetBalanceEditorField(2, "생성 간격 (초)", FormatEditorNumber(monster.spawnIntervalSeconds), true);
            SetBalanceEditorField(3, "가넷 보상", monster.garnetReward.ToString(CultureInfo.InvariantCulture), true);
            SetBalanceEditorField(4, "보너스 젬 수량", monster.gemstoneAmount.ToString(CultureInfo.InvariantCulture), true);
            SetBalanceEditorField(5, "보너스 젬 확률 (%)", FormatEditorNumber(monster.gemstoneChancePercent), true);
        }

        private void RefreshSkillBalanceFields(string skillId)
        {
            SkillInformation skill = FindSkillInformation(skillId);
            if (skill == null) return;
            string[] stats = { "damage", "radius", "count", "cooldownSeconds", "durationSeconds", "simultaneousHits" };
            string[] labels = { "추가 피해", "범위 보정", "개체 수", "쿨타임 보정 (초)", "지속시간 보정 (초)", "동시 타격 수" };
            for (int i = 0; i < stats.Length; i++)
            {
                bool allowed = SkillStatUpgrade.IsAllowed(skillId, stats[i]);
                float value = GetSkillUpgradeValue(skill, stats[i]);
                SetBalanceEditorField(i, labels[i], FormatEditorNumber(value), allowed);
            }
        }

        private void RefreshResearchBalanceFields(string nodeId)
        {
            UpgradeNodeData node = FindUpgradeNode(nodeId, out _);
            if (node == null) return;
            SetBalanceEditorField(0, "효과값", FormatEditorNumber(node.value), true);
            for (int i = 0; i < BalanceEditorCurrencyIds.Length; i++)
                SetBalanceEditorField(i + 1, GemstoneDisplayName(BalanceEditorCurrencyIds[i]),
                    GetNodeCostAmount(node, BalanceEditorCurrencyIds[i]).ToString(CultureInfo.InvariantCulture), true);
            if (_balanceEditorDescriptionRoot != null) _balanceEditorDescriptionRoot.gameObject.SetActive(true);
            if (_balanceEditorDescription != null) _balanceEditorDescription.text = node.description ?? string.Empty;
        }

        private static string FormatEditorNumber(double value) =>
            value.ToString("0.###", CultureInfo.InvariantCulture);

        private string GemstoneDisplayName(string id)
        {
            int index = GetGemstoneIndexById(id);
            if (index < 0 || index >= _document.information.gemstones.Length) return id;
            EntityTextDefinition entity = _document.FindEntity(id);
            return entity == null ? id : entity.displayName;
        }

        private MonsterInformation FindMonsterInformation(string id)
        {
            if (_document == null || _document.information.monsters == null) return null;
            foreach (MonsterInformation monster in _document.information.monsters)
                if (monster != null && monster.id == id) return monster;
            return null;
        }

        private SkillInformation FindSkillInformation(string id)
        {
            if (_document == null || _document.information.skills == null) return null;
            foreach (SkillInformation skill in _document.information.skills)
                if (skill != null && skill.id == id) return skill;
            return null;
        }

        private UpgradeNodeData FindUpgradeNode(string id, out UpgradeCategoryData foundCategory)
        {
            foundCategory = null;
            if (_document == null || _document.progression == null || _document.progression.categories == null)
                return null;
            foreach (UpgradeCategoryData category in _document.progression.categories)
            {
                if (category == null || category.nodes == null) continue;
                foreach (UpgradeNodeData node in category.nodes)
                    if (node != null && node.id == id)
                    {
                        foundCategory = category;
                        return node;
                    }
            }
            return null;
        }

        private static float GetSkillUpgradeValue(SkillInformation skill, string statId)
        {
            if (skill == null || skill.upgrades == null) return 0f;
            foreach (SkillStatUpgradeInformation upgrade in skill.upgrades)
                if (upgrade != null && upgrade.statId == statId) return upgrade.delta;
            return 0f;
        }

        private static int GetNodeCostAmount(UpgradeNodeData node, string currencyId)
        {
            if (node == null) return 0;
            if (node.costs == null || node.costs.Length == 0)
                return node.currencyId == currencyId ? node.cost : 0;
            foreach (UpgradeNodeCostData cost in node.costs)
                if (cost != null && cost.currencyId == currencyId) return cost.amount;
            return 0;
        }

        private bool TryReadBalanceEditorNumber(int index, out double value)
        {
            value = 0d;
            if (index < 0 || index >= _balanceEditorFields.Length || _balanceEditorFields[index] == null ||
                !_balanceEditorFields[index].gameObject.activeSelf)
                return false;
            return double.TryParse(_balanceEditorFields[index].text,
                       NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                   !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private void ApplyBalanceEditorChanges()
        {
            if (_document == null || string.IsNullOrEmpty(_balanceEditorTargetId)) return;
            string originalInformation = JsonUtility.ToJson(_document.information);
            string originalProgression = JsonUtility.ToJson(_document.progression);
            string error = string.Empty;
            bool applied = _balanceEditorTab == BalanceEditorTab.Monsters
                ? ApplyMonsterBalanceEdit(_balanceEditorTargetId, out error)
                : _balanceEditorTab == BalanceEditorTab.Skills
                    ? ApplySkillBalanceEdit(_balanceEditorTargetId, out error)
                    : ApplyResearchBalanceEdit(_balanceEditorTargetId, out error);

            if (!applied || !_document.TryValidate(out error))
            {
                _document.information = JsonUtility.FromJson<GameInformation>(originalInformation);
                _document.progression = JsonUtility.FromJson<ProgressionDefinition>(originalProgression);
                if (_balanceEditorStatus != null)
                    SetLocalizedText(_balanceEditorStatus, "적용하지 않았습니다: " + error);
                RefreshBalanceEditor();
                return;
            }

            if (_balanceEditorTab == BalanceEditorTab.Research)
                RefreshBalanceDescriptionOverrides();
            PlayerPrefs.SetString(BalanceInfoPreferenceKey, GameInformationJson.Serialize(_document.information));
            PlayerPrefs.SetString(BalanceProgressionPreferenceKey, JsonUtility.ToJson(_document.progression));
            PlayerPrefs.Save();
            RebuildRuntimeCatalogAfterBalanceEdit();
            if (_balanceEditorStatus != null)
                SetLocalizedText(_balanceEditorStatus, "현재 전투 설정과 연구 데이터에 저장했습니다.");
            RefreshBalanceEditor();
        }

        private bool ApplyMonsterBalanceEdit(string id, out string error)
        {
            error = "몬스터 입력값을 확인하세요.";
            MonsterInformation monster = FindMonsterInformation(id);
            if (monster == null || !TryReadBalanceEditorNumber(0, out double hp) || hp < 1 || hp > 1000000000d || hp != Math.Floor(hp) ||
                !TryReadBalanceEditorNumber(1, out double production) || production < 1 || production > 16 || production != Math.Floor(production) ||
                !TryReadBalanceEditorNumber(2, out double interval) || interval < 0.1 || interval > 3600 ||
                !TryReadBalanceEditorNumber(3, out double garnet) || garnet < 0 || garnet > 1000000000d || garnet != Math.Floor(garnet) ||
                !TryReadBalanceEditorNumber(4, out double gems) || gems < 0 || gems > 1000000000d || gems != Math.Floor(gems) ||
                !TryReadBalanceEditorNumber(5, out double chance) || chance < 0 || chance > 100)
                return false;
            monster.hitPoints = (long)hp;
            monster.productionCount = (int)production;
            monster.spawnIntervalSeconds = (float)interval;
            monster.garnetReward = (long)garnet;
            monster.gemstoneAmount = (long)gems;
            monster.gemstoneChancePercent = (float)chance;
            error = string.Empty;
            return true;
        }

        private bool ApplySkillBalanceEdit(string id, out string error)
        {
            error = "스킬 입력값을 확인하세요.";
            SkillInformation skill = FindSkillInformation(id);
            string[] stats = { "damage", "radius", "count", "cooldownSeconds", "durationSeconds", "simultaneousHits" };
            if (skill == null) return false;
            var upgrades = skill.upgrades == null
                ? new List<SkillStatUpgradeInformation>()
                : new List<SkillStatUpgradeInformation>(skill.upgrades);
            for (int i = 0; i < stats.Length; i++)
            {
                if (!SkillStatUpgrade.IsAllowed(id, stats[i])) continue;
                if (!TryReadBalanceEditorNumber(i, out double value) || value < -1000000d || value > 1000000d ||
                    ((stats[i] == "count" || stats[i] == "simultaneousHits") && value != Math.Floor(value)))
                    return false;
                int found = -1;
                for (int j = 0; j < upgrades.Count; j++)
                    if (upgrades[j] != null && upgrades[j].statId == stats[i]) { found = j; break; }
                if (Math.Abs(value) < 0.000001d)
                {
                    if (found >= 0) upgrades.RemoveAt(found);
                    continue;
                }
                SkillStatUpgradeInformation item = new SkillStatUpgradeInformation
                {
                    statId = stats[i],
                    delta = (float)value
                };
                if (found >= 0) upgrades[found] = item;
                else upgrades.Add(item);
            }
            skill.upgrades = upgrades.ToArray();
            error = string.Empty;
            return true;
        }

        private bool ApplyResearchBalanceEdit(string id, out string error)
        {
            error = "연구 효과값 또는 비용 조합이 올바르지 않습니다.";
            UpgradeNodeData node = FindUpgradeNode(id, out _);
            if (node == null || !TryReadBalanceEditorNumber(0, out double value) ||
                value < -1000000000000d || value > 1000000000000d)
                return false;
            var costs = new List<UpgradeNodeCostData>();
            long total = 0L;
            int maxCurrencyIndex = GetGemstoneIndexById(node.currencyId);
            bool rainbowFinal = IsRainbowFinalUpgradeNode(FindNode(id));
            for (int i = 0; i < BalanceEditorCurrencyIds.Length; i++)
            {
                if (!TryReadBalanceEditorNumber(i + 1, out double amount) || amount < 0 ||
                    amount > int.MaxValue || amount != Math.Floor(amount))
                    return false;
                if (amount <= 0) continue;
                if (!rainbowFinal && i > maxCurrencyIndex)
                    return false;
                total += (long)amount;
                costs.Add(new UpgradeNodeCostData
                {
                    currencyId = BalanceEditorCurrencyIds[i],
                    amount = (int)amount
                });
            }
            if (costs.Count > 5 || total > int.MaxValue) return false;
            if (rainbowFinal)
            {
                for (int i = 1; i < BalanceEditorCurrencyIds.Length; i++)
                    if (GetNodeCostAmountFromList(costs, BalanceEditorCurrencyIds[i]) <= 0)
                        return false;
                if (GetNodeCostAmountFromList(costs, BalanceEditorCurrencyIds[0]) > 0)
                    return false;
            }

            if (_balanceEditorDescription != null)
                node.description = _balanceEditorDescription.text ?? string.Empty;
            node.value = value;
            node.valueLabel = (node.operation == "add" && value > 0d ? "+" : string.Empty) + FormatEditorNumber(value);
            node.cost = (int)total;
            node.costs = costs.ToArray();
            error = string.Empty;
            return true;
        }

        private static int GetNodeCostAmountFromList(List<UpgradeNodeCostData> costs, string currencyId)
        {
            foreach (UpgradeNodeCostData cost in costs)
                if (cost.currencyId == currencyId) return cost.amount;
            return 0;
        }

        private void RebuildRuntimeCatalogAfterBalanceEdit()
        {
            TraitCatalog previous = _runtimeCatalog;
            _runtimeCatalog = TraitCatalog.CreateFromDocument(_document);
            _activeCatalog = _runtimeCatalog;
            _ownsRuntimeCatalog = true;
            if (previous != null && previous != _runtimeCatalog) Destroy(previous);
            BuildNodes(false);
            RefreshAll();
        }

        private void LoadBalancePatchOverrides()
        {
            if (_document == null) return;
            GameInformation originalInformation = _document.information;
            ProgressionDefinition originalProgression = _document.progression;
            bool loadedOverride = false;
            if (PlayerPrefs.HasKey(BalanceInfoPreferenceKey))
            {
                string json = PlayerPrefs.GetString(BalanceInfoPreferenceKey, string.Empty);
                if (GameInformationJson.TryDeserialize(json, out GameInformation information, out string error))
                {
                    _document.information = information;
                    loadedOverride = true;
                }
                else Debug.LogWarning("Ignoring saved balance information: " + error, this);
            }
            if (PlayerPrefs.HasKey(BalanceProgressionPreferenceKey))
            {
                try
                {
                    ProgressionDefinition progression = JsonUtility.FromJson<ProgressionDefinition>(
                        PlayerPrefs.GetString(BalanceProgressionPreferenceKey, string.Empty));
                    if (progression != null)
                    {
                        _document.progression = progression;
                        loadedOverride = true;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Ignoring saved progression balance: " + exception.Message, this);
                }
            }
            if (loadedOverride && !_document.TryValidate(out string validationError))
            {
                _document.information = originalInformation;
                _document.progression = originalProgression;
                Debug.LogWarning("Ignoring invalid saved balance patch: " + validationError, this);
            }
        }

        private void RefreshBalanceDescriptionOverrides()
        {
            _balanceEditedDescriptions.Clear();
            GameDataDocument defaults = GameDataDocument.Load();
            var originals = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (UpgradeCategoryData category in defaults.progression.categories)
                foreach (UpgradeNodeData node in category.nodes)
                    originals[node.id] = node.description;
            foreach (UpgradeCategoryData category in _document.progression.categories)
                foreach (UpgradeNodeData node in category.nodes)
                    if (originals.TryGetValue(node.id, out string description) && description != node.description)
                        _balanceEditedDescriptions[node.id] = node.description ?? string.Empty;
        }

        private void ToggleBoardLayoutEditor()
        {
            SetBoardLayoutEditing(!_boardLayoutEditing);
        }

        private void SetBoardLayoutEditing(bool enabled)
        {
            _boardLayoutEditing = enabled;
            if (_boardLayoutEditButton != null)
            {
                TextMeshProUGUI label = _boardLayoutEditButton.GetComponentInChildren<TextMeshProUGUI>();
                SetButtonLabel(label, enabled ? "배치 편집: 켬" : "배치 편집: 끔");
                Image image = _boardLayoutEditButton.GetComponent<Image>();
                if (image != null) image.color = enabled
                    ? new Color(0.20f, 0.48f, 0.34f, 1f)
                    : new Color(0.12f, 0.22f, 0.31f, 0.98f);
            }
            if (_boardLayoutResetButton != null)
                _boardLayoutResetButton.gameObject.SetActive(enabled);
            if (enabled) SetBalancePatchEditorOpen(false);
            if (!_showRelicCollection && !_showTalentCollection && !_showFieldCollection) BuildNodes(false);
            if (_statusMessage != null)
                SetLocalizedText(_statusMessage, enabled
                    ? "노드를 끌어 배치하세요. 빈 격자 위치에 놓으면 자동 저장됩니다."
                    : string.Empty);
        }

        private void RestoreBoardLayout()
        {
            _boardCellOverrides.Clear();
            PlayerPrefs.DeleteKey(BoardLayoutPreferenceKey);
            PlayerPrefs.Save();
            BuildNodes(false);
            CenterUnifiedGraph();
        }

        internal void BeginBoardNodeDrag(string nodeId, RectTransform nodeRect, PointerEventData eventData)
        {
            if (!_boardLayoutEditing || nodeRect == null || eventData == null) return;
            _draggingBoardNodeId = nodeId;
            HideNodeTooltip();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _nodeContent, eventData.position, GetUiCamera(), out Vector2 pointer))
                _boardNodeDragOffset = nodeRect.anchoredPosition - pointer;
        }

        internal void DragBoardNode(string nodeId, RectTransform nodeRect, PointerEventData eventData)
        {
            if (!_boardLayoutEditing || _draggingBoardNodeId != nodeId || nodeRect == null || eventData == null)
                return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _nodeContent, eventData.position, GetUiCamera(), out Vector2 pointer))
                nodeRect.anchoredPosition = pointer + _boardNodeDragOffset;
        }

        internal void EndBoardNodeDrag(string nodeId, RectTransform nodeRect)
        {
            if (!_boardLayoutEditing || _draggingBoardNodeId != nodeId || nodeRect == null) return;
            _draggingBoardNodeId = null;
            Vector2Int target = new Vector2Int(
                Mathf.RoundToInt(nodeRect.anchoredPosition.x / BoardNodePitchX),
                Mathf.RoundToInt(nodeRect.anchoredPosition.y / BoardNodePitchY));
            if (Mathf.Abs(target.x) > 40 || Mathf.Abs(target.y) > 40 || IsBoardCellOccupied(target, nodeId))
            {
                BuildNodes(false);
                SetTestStatus("이미 다른 노드가 있는 위치입니다.");
                return;
            }
            _boardCellOverrides[nodeId] = target;
            SaveBoardCellOverrides();
            BuildNodes(false);
        }

        private bool IsBoardCellOccupied(Vector2Int cell, string exceptNodeId)
        {
            foreach (KeyValuePair<string, Vector2> position in _boardPositionsByNodeId)
            {
                if (position.Key == exceptNodeId) continue;
                var other = new Vector2Int(
                    Mathf.RoundToInt(position.Value.x / BoardNodePitchX),
                    Mathf.RoundToInt(position.Value.y / BoardNodePitchY));
                if (other == cell) return true;
            }
            return false;
        }

        private void LoadBoardCellOverrides()
        {
            if (_boardCellOverridesLoaded) return;
            _boardCellOverridesLoaded = true;
            _boardCellOverrides.Clear();
            if (!PlayerPrefs.HasKey(BoardLayoutPreferenceKey)) return;
            try
            {
                BoardLayoutSave save = JsonUtility.FromJson<BoardLayoutSave>(
                    PlayerPrefs.GetString(BoardLayoutPreferenceKey, string.Empty));
                if (save == null || save.entries == null || save.entries.Count > 1000) return;
                var occupied = new HashSet<Vector2Int>();
                foreach (BoardLayoutEntry entry in save.entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.nodeId) ||
                        Mathf.Abs(entry.x) > 40 || Mathf.Abs(entry.y) > 40) continue;
                    var cell = new Vector2Int(entry.x, entry.y);
                    if (!occupied.Add(cell)) continue;
                    _boardCellOverrides[entry.nodeId] = cell;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Research layout override could not be read: " + exception.Message, this);
            }
        }

        private void SaveBoardCellOverrides()
        {
            var save = new BoardLayoutSave();
            foreach (KeyValuePair<string, Vector2Int> pair in _boardCellOverrides)
                save.entries.Add(new BoardLayoutEntry
                {
                    nodeId = pair.Key,
                    x = pair.Value.x,
                    y = pair.Value.y
                });
            PlayerPrefs.SetString(BoardLayoutPreferenceKey, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        [Serializable]
        private sealed class BoardLayoutSave
        {
            public List<BoardLayoutEntry> entries = new List<BoardLayoutEntry>();
        }

        [Serializable]
        private sealed class BoardLayoutEntry
        {
            public string nodeId;
            public int x;
            public int y;
        }

        private void SetRelicTestMode(bool enabled)
        {
            _relicTestMode = enabled;
            if (_relicTestModeLabel != null)
            {
                SetLocalizedText(
                    _relicTestModeLabel,
                    enabled ? "유물 테스트 종료" : "유물 개별 테스트");
            }
            if (_relicTestModeButton != null)
            {
                Image image = _relicTestModeButton.GetComponent<Image>();
                if (image != null)
                {
                    image.color = enabled
                        ? new Color(0.50f, 0.28f, 0.12f, 1f)
                        : new Color(0.28f, 0.20f, 0.10f, 0.98f);
                }
            }

            if (_showRelicCollection)
            {
                BuildNodes(false);
            }
        }

        private static Color GetRankFrameColor(int stage)
        {
            switch (Mathf.Clamp(stage, 1, 5))
            {
                case 1: return new Color(0.94f, 0.96f, 1f, 1f);
                case 2: return new Color(1f, 0.79f, 0.19f, 1f);
                case 3: return new Color(0.25f, 0.63f, 1f, 1f);
                case 4: return new Color(0.72f, 0.43f, 1f, 1f);
                default: return new Color(1f, 0.30f, 0.36f, 1f);
            }
        }

        private void BuildTestTools(RectTransform parent)
        {
            Button toggle = CreateButton(
                "TestToolsToggle",
                parent,
                Vector2.zero,
                Vector2.zero,
                new Vector2(98f, 42f),
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
                Vector2.zero,
                Vector2.zero,
                new Vector2(24f, 68f),
                new Vector2(360f, 756f),
                panelSprite,
                new Color(0.018f, 0.043f, 0.105f, 0.98f),
                Vector2.zero);
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
                "진행도 테스트",
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
                "젬스톤을 지급해 연구 비용과 전투 적용을 확인하세요.",
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
            amountRect.gameObject.AddComponent<TraitTooltipDismissRelay>().Bind(this);

            Button grantAll = CreateButton(
                "GrantAllGemstones",
                _testToolsPanel,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-119f, -78f),
                new Vector2(202f, 40f),
                null,
                new Color(0.20f, 0.46f, 0.60f, 0.98f),
                "전체 종류 지급",
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
                "젬스톤별 지급",
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

            CreateText(
                CreateRect(
                    "TestActionsHeading",
                    _testToolsPanel,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(18f, -384f),
                    new Vector2(-36f, 22f),
                    new Vector2(0.5f, 1f)),
                "진행도 조작",
                13,
                TextAnchor.MiddleLeft,
                new Color(0.58f, 0.79f, 1f, 1f),
                true);

            Button resetGemstones = CreateButton(
                "ResetTestGemstones",
                _testToolsPanel,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-81f, -416f),
                new Vector2(154f, 38f),
                null,
                new Color(0.42f, 0.18f, 0.24f, 0.98f),
                "젬 잔액 비우기",
                12,
                inkColor);
            resetGemstones.onClick.AddListener(ResetTestGemstones);

            Button refundGemstones = CreateButton(
                "RefundAllTestGemstones",
                _testToolsPanel,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(81f, -416f),
                new Vector2(154f, 38f),
                null,
                new Color(0.12f, 0.42f, 0.36f, 0.98f),
                "젬 전체 회수",
                12,
                inkColor);
            refundGemstones.onClick.AddListener(RefundAllTestGemstones);

            _testUnlockAllButton = CreateButton(
                "TestUnlockAllConditions",
                _testToolsPanel,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-81f, -460f),
                new Vector2(154f, 38f),
                null,
                new Color(0.42f, 0.22f, 0.50f, 0.98f),
                "조건 무시: 끔",
                12,
                inkColor);
            _testUnlockAllLabel = _testUnlockAllButton.GetComponentInChildren<TextMeshProUGUI>();
            _testUnlockAllButton.onClick.AddListener(ToggleUnlockAllConditionsForTesting);
            RefreshTestUnlockAllButton();

            Button upgradeAll = CreateButton(
                "TestUpgradeAllNodes",
                _testToolsPanel,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(81f, -460f),
                new Vector2(154f, 38f),
                null,
                new Color(0.88f, 0.48f, 0.18f, 0.98f),
                "전체 연구 완료",
                12,
                inkColor);
            upgradeAll.onClick.AddListener(UpgradeAllNodesForTesting);

            CreateText(
                CreateRect(
                    "TestRelicHeading",
                    _testToolsPanel,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(18f, -506f),
                    new Vector2(-36f, 22f),
                    new Vector2(0.5f, 1f)),
                "유물 획득 테스트",
                13,
                TextAnchor.MiddleLeft,
                new Color(1f, 0.79f, 0.38f, 1f),
                true);

            RectTransform relicIndexRect = CreateImageRect(
                "TestRelicIndex",
                _testToolsPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -544f),
                new Vector2(70f, 34f),
                null,
                new Color(0.035f, 0.075f, 0.13f, 1f),
                new Vector2(0f, 1f));
            Image relicIndexBackground = relicIndexRect.GetComponent<Image>();
            relicIndexBackground.raycastTarget = true;
            _testRelicIndexInput = relicIndexRect.gameObject.AddComponent<TMP_InputField>();
            _testRelicIndexInput.targetGraphic = relicIndexBackground;
            _testRelicIndexInput.textViewport = relicIndexRect;
            _testRelicIndexInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            _testRelicIndexInput.lineType = TMP_InputField.LineType.SingleLine;
            _testRelicIndexInput.keyboardType = TouchScreenKeyboardType.NumberPad;
            _testRelicIndexInput.characterLimit = 2;
            _testRelicIndexInput.selectionColor = new Color(0.30f, 0.65f, 1f, 0.45f);
            TextMeshProUGUI relicIndexText = CreateText(
                CreateRect(
                    "InputText",
                    relicIndexRect,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(8f, 0f),
                    new Vector2(-16f, -4f),
                    new Vector2(0.5f, 0.5f)),
                "1",
                15,
                TextAnchor.MiddleCenter,
                inkColor,
                true);
            relicIndexText.raycastTarget = false;
            _testRelicIndexInput.textComponent = relicIndexText;
            _testRelicIndexInput.text = "1";
            relicIndexRect.gameObject.AddComponent<TraitTooltipDismissRelay>().Bind(this);

            Button grantRelic = CreateButton(
                "GrantTestRelic",
                _testToolsPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(98f, -544f),
                new Vector2(120f, 34f),
                null,
                new Color(0.69f, 0.43f, 0.20f, 1f),
                "획득 설정",
                12,
                inkColor);
            grantRelic.onClick.AddListener(() => SetTestRelicObtained(true));

            Button revokeRelic = CreateButton(
                "RevokeTestRelic",
                _testToolsPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(226f, -544f),
                new Vector2(116f, 34f),
                null,
                new Color(0.30f, 0.22f, 0.29f, 1f),
                "획득 취소",
                12,
                inkColor);
            revokeRelic.onClick.AddListener(() => SetTestRelicObtained(false));

            CreateText(
                CreateRect(
                    "TestRelicHint",
                    _testToolsPanel,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(18f, -583f),
                    new Vector2(-36f, 30f),
                    new Vector2(0.5f, 1f)),
                "획득한 유물은 전투 정산에 표시되고, 유물 탭에서 New 표식을 확인합니다. 번호 1~20",
                11,
                TextAnchor.MiddleLeft,
                mutedInkColor,
                false);

            CreateText(
                CreateRect(
                    "TestResetHint",
                    _testToolsPanel,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(18f, -628f),
                    new Vector2(-36f, 34f),
                    new Vector2(0.5f, 1f)),
                "젬 잔액 비우기는 연구를 유지합니다. 전체 초기화는 연구를 시작점으로 되돌립니다.",
                10,
                TextAnchor.MiddleLeft,
                mutedInkColor,
                false);

            Button resetProgression = CreateButton(
                "ResetButton",
                _testToolsPanel,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -710f),
                new Vector2(324f, 42f),
                buttonFrameSprite,
                new Color(0.43f, 0.13f, 0.17f, 0.98f),
                "전체 초기화",
                15,
                inkColor);
            resetProgression.onClick.AddListener(ResetPreview);

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

        private void SetTestToolsOpen(bool open)
        {
            if (open && _balanceEditorOpen)
            {
                SetBalancePatchEditorOpen(false);
            }
            _testToolsOpen = open;
            if (_testToolsPanel != null)
            {
                _testToolsPanel.gameObject.SetActive(open);
                if (open) _testToolsPanel.SetAsLastSibling();
            }

            RefreshProgressionSidebarVisibility();
        }

        private void SetTestRelicObtained(bool obtained)
        {
            if (!int.TryParse(
                    _testRelicIndexInput == null ? string.Empty : _testRelicIndexInput.text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int requestedIndex) ||
                requestedIndex < 1 || requestedIndex > 20)
            {
                SetTestStatus("유물 번호는 1~20 사이로 입력하세요.");
                return;
            }

            SetTestRelicObtained(requestedIndex - 1, obtained);
        }

        private void SetTestRelicObtained(int index, bool obtained)
        {
            EnsureRelicEntries();
            if (index < 0 || index >= relicEntries.Count)
            {
                return;
            }

            RelicDisplayEntry entry = relicEntries[index];
            if (!obtained && _selectedRelicIndex == index)
            {
                CloseRelicDetail();
            }
            entry.SetObtained(obtained);
            if (obtained)
            {
                if (!string.IsNullOrWhiteSpace(entry.Id)) _unseenRelicIds.Add(entry.Id);
            }
            else if (!string.IsNullOrWhiteSpace(entry.Id))
            {
                _unseenRelicIds.Remove(entry.Id);
            }

            if (_showRelicCollection)
            {
                SetTestToolsOpen(false);
                BuildNodes(false);
            }

            SaveProgressionState();

            SetTestStatus(obtained
                ? "유물 " + (index + 1).ToString("00", CultureInfo.InvariantCulture) +
                    " 획득 상태를 설정했습니다."
                : "유물 " + (index + 1).ToString("00", CultureInfo.InvariantCulture) +
                    " 획득 상태를 해제했습니다.");
        }

        private void RefreshTestUnlockAllButton()
        {
            if (_testUnlockAllButton == null)
            {
                return;
            }

            bool ignoringConditions = testModeUnlockAll;
            SetButtonLabel(
                _testUnlockAllLabel,
                ignoringConditions
                    ? "조건 무시: 켬"
                    : "조건 무시: 끔");
            Color color = ignoringConditions
                ? new Color(0.55f, 0.31f, 0.66f, 0.98f)
                : new Color(0.28f, 0.19f, 0.36f, 0.98f);
            Image image = _testUnlockAllButton.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }

            ColorBlock colors = _testUnlockAllButton.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(color, Color.black, 0.12f);
            colors.disabledColor = color;
            _testUnlockAllButton.colors = colors;
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

        private void RefundAllTestGemstones()
        {
            EnsureGemstoneBalanceCapacity();
            var recordedSpending = new HashSet<string>(_spentByNode.Keys, StringComparer.Ordinal);
            foreach (string nodeId in _spentNodeCosts.Keys) recordedSpending.Add(nodeId);
            foreach (string nodeId in recordedSpending)
            {
                RefundSpentNodeCosts(nodeId, FindNode(nodeId));
            }

            _spentByNode.Clear();
            _spentCurrencyByNode.Clear();
            _spentNodeCosts.Clear();
            _spentNodeCosts.Clear();
            if (_gemstoneBalances.Length > 0)
            {
                _garnetBalance = _gemstoneBalances[0];
            }
            RefreshAll();
            RefreshNodeTooltip(FindNode(_tooltipNodeId));
            SaveProgressionState();
            SetTestStatus("사용한 젬스톤을 모두 회수했습니다. 연구 상태는 유지됩니다.");
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

        private void ToggleUnlockAllConditionsForTesting()
        {
            testModeUnlockAll = !testModeUnlockAll;
            RefreshTestUnlockAllButton();
            RefreshAll();
            RefreshNodeTooltip(FindNode(_tooltipNodeId));
            SetTestStatus(testModeUnlockAll
                ? "테스트: 선행 연구와 젬스톤 공급 조건을 무시합니다."
                : "테스트: 선행 연구 조건을 적용합니다.");
        }

        private void UpgradeAllNodesForTesting()
        {
            testModeUnlockAll = true;
            RefreshTestUnlockAllButton();
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
                    _spentNodeCosts.Remove(node.Id);
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
            SetLocalizedText(_statusMessage, message);
        }

        private static string GetGemstoneLocalizedName(string gemstoneId, string fallback)
        {
            switch (gemstoneId)
            {
                case "gem.garnet": return LocalizationCatalog.Get("ui.gem.garnet", "가넷");
                case "gem.topaz": return LocalizationCatalog.Get("ui.gem.topaz", "토파즈");
                case "gem.amethyst": return LocalizationCatalog.Get("ui.gem.amethyst", "자수정");
                case "gem.sapphire": return LocalizationCatalog.Get("ui.gem.sapphire", "사파이어");
                case "gem.diamond": return LocalizationCatalog.Get("ui.gem.diamond", "다이아몬드");
                case "gem.dragon": return LocalizationCatalog.Get("ui.gem.dragon", "드래곤 스톤");
                default: return string.IsNullOrEmpty(fallback)
                    ? LocalizationCatalog.Get("ui.gem.generic", "젬스톤")
                    : fallback;
            }
        }

        private void BuildGemstonePanel()
        {
            RectTransform panel = CreatePanel(
                "GemstonePanel",
                _runtimeRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, -120f),
                new Vector2(376f, 270f),
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
                new Vector2(-36f, 32f),
                new Vector2(0.5f, 1f));
            CreateText(heading, "보유 젬스톤", 20, TextAnchor.MiddleLeft, inkColor, true);

            _gemstoneList = CreateRect(
                "GemstoneList",
                panel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -50f),
                new Vector2(-36f, 210f),
                new Vector2(0.5f, 1f));
            LayoutElement listElement = _gemstoneList.gameObject.AddComponent<LayoutElement>();
            listElement.preferredHeight = 210f;
            listElement.minHeight = 30f;
            VerticalLayoutGroup listLayout = _gemstoneList.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 6f;
            listLayout.childAlignment = TextAnchor.UpperLeft;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = false;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;

            RefreshGemstonePanel();
        }

        private void BuildCombatStatsPanel()
        {
            _combatStatsPanel = CreatePanel(
                "CurrentCombatStats",
                _runtimeRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                Vector2.zero,
                new Vector2(376f, 316f),
                panelSprite,
                new Color(0.018f, 0.043f, 0.105f, 0.94f),
                new Vector2(0f, 1f));

            CreateText(
                CreateRect(
                    "CombatStatsHeading",
                    _combatStatsPanel,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(18f, -10f),
                    new Vector2(-36f, 32f),
                    new Vector2(0.5f, 1f)),
                "현재 전투 능력치",
                19,
                TextAnchor.MiddleLeft,
                inkColor,
                true);

            string[] labels =
            {
                "공격력", "공격 범위", "공격 간격", "치명타 확률", "보스 피해 배수", "일반 필드 시간"
            };
            Sprite[] icons =
            {
                attackIcon, radiusIcon, attackIntervalIcon, criticalIcon, bossIcon, timerIcon
            };
            for (int i = 0; i < labels.Length; i++)
            {
                RectTransform row = CreateRect(
                    "CombatStatRow_" + i.ToString(CultureInfo.InvariantCulture),
                    _combatStatsPanel,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, -48f - i * 40f),
                    new Vector2(-24f, 36f),
                    new Vector2(0.5f, 1f));
                Image rowBackground = row.gameObject.AddComponent<Image>();
                rowBackground.color = new Color(1f, 1f, 1f, i % 2 == 0 ? 0.045f : 0.025f);
                rowBackground.raycastTarget = false;

                if (icons[i] != null)
                {
                    RectTransform icon = CreateImageRect(
                        "Icon",
                        row,
                        new Vector2(0f, 0.5f),
                        new Vector2(0f, 0.5f),
                        new Vector2(8f, 0f),
                        new Vector2(36f, 36f),
                        icons[i],
                        Color.white,
                        new Vector2(0.5f, 0.5f));
                    icon.GetComponent<Image>().preserveAspect = true;
                }

                CreateText(
                    CreateRect(
                        "Label",
                        row,
                        new Vector2(0f, 0.5f),
                        new Vector2(1f, 0.5f),
                        new Vector2(54f, 0f),
                        new Vector2(-148f, 32f),
                        new Vector2(0f, 0.5f)),
                    labels[i],
                    15,
                    TextAnchor.MiddleLeft,
                    mutedInkColor,
                    false);
                TextMeshProUGUI value = CreateText(
                    CreateRect(
                        "Value",
                        row,
                        new Vector2(1f, 0.5f),
                        new Vector2(1f, 0.5f),
                        new Vector2(-8f, 0f),
                        new Vector2(128f, 32f),
                        new Vector2(1f, 0.5f)),
                    "-",
                    16,
                    TextAnchor.MiddleRight,
                    inkColor,
                    true);
                _combatStatValues.Add(value);
            }

            PositionCombatStatsPanelBelowWallet();
            RefreshCombatStatsPanel();
        }

        private void PositionCombatStatsPanelBelowWallet()
        {
            if (_combatStatsPanel == null || _gemstonePanel == null)
            {
                return;
            }

            _combatStatsPanel.anchoredPosition = new Vector2(
                _gemstonePanel.anchoredPosition.x,
                _gemstonePanel.anchoredPosition.y - _gemstonePanel.sizeDelta.y - 12f);
        }

        private void RefreshCombatStatsPanel()
        {
            if (_combatStatValues.Count < 6 || _document == null)
            {
                return;
            }

            GameInformation information = CreateGameInformation();
            _combatStatValues[0].text = information.stats.attackPower.ToString(
                "N0", CultureInfo.InvariantCulture);
            _combatStatValues[1].text = FormatCompactNumber(
                information.stats.attackRadiusWorldUnits);
            _combatStatValues[2].text = FormatCompactNumber(
                information.stats.attackCooldownSeconds) + "초";
            _combatStatValues[3].text = FormatCompactNumber(
                information.stats.criticalChancePercent) + "%";
            _combatStatValues[4].text = FormatCompactNumber(
                information.stats.bossDamageMultiplier) + "×";
            _combatStatValues[5].text = FormatCompactNumber(
                information.stats.normalFieldDurationSeconds) + "초";
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

            if (_gemstonePanel != null)
            {
                float listHeight = Mathf.Max(42f, visibleCount * 42f + Mathf.Max(0, visibleCount - 1) * 6f);
                _gemstoneList.sizeDelta = new Vector2(_gemstoneList.sizeDelta.x, listHeight);
                Vector2 panelSize = _gemstonePanel.sizeDelta;
                panelSize.y = Mathf.Clamp(
                    88f + listHeight,
                    160f,
                    590f);
                _gemstonePanel.sizeDelta = panelSize;
                PositionCombatStatsPanelBelowWallet();
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
                new Vector2(0f, 42f),
                new Vector2(0.5f, 0.5f));
            LayoutElement rowElement = row.gameObject.AddComponent<LayoutElement>();
            rowElement.preferredHeight = 42f;
            rowElement.minHeight = 42f;

            Image rowImage = row.gameObject.AddComponent<Image>();
            Color accent = GetGemstoneColor(gemstoneIndex);
            rowImage.color = new Color(accent.r * 0.20f, accent.g * 0.20f, accent.b * 0.24f, 0.78f);
            rowImage.raycastTarget = false;
            Outline rowOutline = row.gameObject.AddComponent<Outline>();
            rowOutline.effectColor = new Color(accent.r, accent.g, accent.b, 0.55f);
            rowOutline.effectDistance = new Vector2(1f, -1f);
            Shadow rowShadow = row.gameObject.AddComponent<Shadow>();
            rowShadow.effectColor = new Color(0f, 0.015f, 0.035f, 0.58f);
            rowShadow.effectDistance = new Vector2(0f, -2f);

            Sprite iconSprite = GetGemstoneIcon(gemstoneIndex);
            if (iconSprite != null)
            {
                RectTransform icon = CreateImageRect(
                    "Icon",
                    row,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(24f, 0f),
                    new Vector2(36f, 36f),
                    iconSprite,
                    Color.white,
                    new Vector2(0.5f, 0.5f));
                Image iconImage = icon.GetComponent<Image>();
                iconImage.preserveAspect = true;
            }

            TextMeshProUGUI label = CreateText(
                CreateRect("Label", row, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(64f, 0f), new Vector2(-144f, 0f), new Vector2(0f, 0.5f)),
                GetGemstoneLocalizedName(gemstone.Id, gemstone.Title),
                16,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            label.color = Color.Lerp(inkColor, accent, 0.25f);

            long balance = GetGemstoneBalance(gemstoneIndex);
            TextMeshProUGUI value = CreateText(
                CreateRect("Value", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(118f, 34f), new Vector2(1f, 0.5f)),
                balance.ToString("N0", CultureInfo.InvariantCulture),
                17,
                TextAnchor.MiddleRight,
                inkColor,
                true);
            if (gemstoneIndex == 0)
            {
                _garnetValue = value;
            }

        }

        private Sprite GetGemstoneIcon(int index)
        {
            GemstoneIconCatalog icons = gemstoneIconCatalog != null
                ? gemstoneIconCatalog : GemstoneIconCatalog.LoadDefault();
            TraitGemstoneDefinition gemstone = GetGemstoneDefinition(index);
            Sprite sharedIcon = icons != null && gemstone != null ? icons.GetIcon(gemstone.Id) : null;
            if (sharedIcon != null) return sharedIcon;

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

            string[] labels = { "공격력", "공격 범위", "공격 간격", "치명타", "보스 피해 배수" };
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
            viewport.gameObject.AddComponent<TraitTooltipDismissRelay>().Bind(this);
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
            // Panning is clamped to the visible node bounds below. Disable
            // inertia so a short drag cannot throw the board past those bounds.
            scroll.movementType = ScrollRect.MovementType.Unrestricted;
            scroll.inertia = false;
            scroll.scrollSensitivity = 0f;
            scroll.Bind(this);

            _nodeGroup = viewport.gameObject.AddComponent<CanvasGroup>();
            Image panelImage = panel.GetComponent<Image>();
            if (panelImage != null) panelImage.color = Color.clear;
            Outline panelOutline = panel.GetComponent<Outline>();
            if (panelOutline != null) panelOutline.enabled = false;
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
                panelSprite,
                new Color(0.92f, 0.96f, 1f, 0.98f),
                new Vector2(1f, 0.5f));
            Image panelImage = panel.GetComponent<Image>();
            if (panelImage != null) panelImage.raycastTarget = true;
            Outline panelOutline = panel.GetComponent<Outline>();
            if (panelOutline != null)
            {
                panelOutline.effectColor = new Color(0.78f, 0.59f, 0.27f, 0.58f);
                panelOutline.effectDistance = new Vector2(2f, -2f);
            }

            RectTransform headerBackground = CreateImageRect(
                "DetailHeaderBackground",
                panel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -12f),
                new Vector2(-16f, 168f),
                panelSprite,
                new Color(0.50f, 0.63f, 0.80f, 0.72f),
                new Vector2(0.5f, 1f));
            _selectedHeaderBackground = headerBackground.GetComponent<Image>();
            if (_selectedHeaderBackground != null)
            {
                _selectedHeaderBackground.type = Image.Type.Sliced;
                _selectedHeaderBackground.raycastTarget = false;
            }
            _selectedHeaderAccent = CreateImageRect(
                "DetailHeaderAccent",
                panel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -12f),
                new Vector2(-16f, 4f),
                null,
                GetCategoryAccent(_activeTab),
                new Vector2(0.5f, 1f)).GetComponent<Image>();

            RectTransform heading = CreateRect(
                "DetailHeading",
                panel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -28f),
                new Vector2(-36f, 44f),
                new Vector2(0.5f, 1f));
            CreateText(
                heading,
                "선택한 항목",
                16,
                TextAnchor.MiddleLeft,
                new Color(0.96f, 0.78f, 0.42f, 1f),
                true);

            _selectedTitle = CreateText(
                CreateRect("SelectedTitle", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -88f), new Vector2(-36f, 54f), new Vector2(0.5f, 1f)),
                "Trait",
                25,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            _selectedDescription = CreateText(
                CreateRect("SelectedDescription", panel, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 42f), new Vector2(-36f, 120f), new Vector2(0.5f, 0.5f)),
                "",
                16,
                TextAnchor.UpperLeft,
                new Color(0.84f, 0.88f, 0.94f, 1f),
                false);

            _selectedDescription.textWrappingMode = TextWrappingModes.Normal;
            _selectedDescription.enableAutoSizing = true;
            _selectedDescription.fontSizeMin = 12f;
            _selectedDescription.fontSizeMax = 16f;
            _selectedDescription.overflowMode = TextOverflowModes.Ellipsis;

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
                buttonFrameSprite,
                new Color(0.86f, 0.62f, 0.25f, 1f),
                "강화/해금",
                19,
                new Color(0.18f, 0.12f, 0.06f, 1f));
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
                buttonFrameSprite,
                new Color(0.29f, 0.34f, 0.43f, 1f),
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
            _bulkUpgradeButton = CreateButton(
                "BulkAffordableUpgradeButton",
                _runtimeRoot,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-292f, 128f),
                new Vector2(536f, 44f),
                buttonFrameSprite,
                new Color(0.46f, 0.34f, 0.15f, 1f),
                "가능한 연구 모두 진행",
                17,
                inkColor);
            _bulkUpgradeButton.onClick.AddListener(BulkUpgradeAffordableNodes);

            _generalCombatButton = CreateButton(
                "EnterCombatButton",
                _runtimeRoot,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-430f, 60f),
                new Vector2(260f, 62f),
                buttonFrameSprite,
                new Color(0.80f, 0.39f, 0.15f, 1f),
                "일반 전투",
                21,
                inkColor);
            Outline combatOutline = _generalCombatButton.GetComponent<Outline>();
            if (combatOutline == null)
            {
                combatOutline = _generalCombatButton.gameObject.AddComponent<Outline>();
            }
            combatOutline.effectColor = new Color(1f, 0.78f, 0.34f, 0.94f);
            combatOutline.effectDistance = new Vector2(2f, -2f);
            combatOutline.useGraphicAlpha = true;
            _generalCombatButton.onClick.AddListener(RequestCombat);

            _bossCombatButton = CreateButton(
                "EnterBossCombatButton",
                _runtimeRoot,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-154f, 60f),
                new Vector2(260f, 62f),
                buttonFrameSprite,
                new Color(0.43f, 0.23f, 0.18f, 1f),
                "보스 전투",
                21,
                inkColor);
            _bossCombatButton.onClick.AddListener(RequestBossCombat);

        }

        private void BulkUpgradeAffordableNodes()
        {
            if (_showRelicCollection || _showTalentCollection || _showFieldCollection)
            {
                return;
            }
            if (_bulkUpgradeRoutine != null)
            {
                return;
            }

            _bulkUpgradeButton.interactable = false;
            if (_runtimeGroup != null) _runtimeGroup.interactable = false;
            SetButtonLabel(_bulkUpgradeButton.GetComponentInChildren<TextMeshProUGUI>(), "연구 진행 중 · 0");
            _bulkUpgradeRoutine = StartCoroutine(BulkUpgradeAffordableNodesRoutine());
        }

        private IEnumerator BulkUpgradeAffordableNodesRoutine()
        {
            const int maximumPurchasesPerClick = 512;
            const float secondsBetweenPurchases = 0.075f;
            Dictionary<string, NodePurchaseState> initialStates = CaptureVisibleBoardNodeStates();
            int purchasedCount = 0;
            while (purchasedCount < maximumPurchasesPerClick && isActiveAndEnabled)
            {
                TraitNodeDefinition next = FindNextAffordableBulkUpgrade();
                if (next == null || !TryUpgradeNode(next, true))
                {
                    break;
                }

                purchasedCount++;
                SetTestStatus("연구 진행 중 · " + purchasedCount.ToString(CultureInfo.InvariantCulture));
                if (_bulkUpgradeButton != null)
                    SetButtonLabel(_bulkUpgradeButton.GetComponentInChildren<TextMeshProUGUI>(),
                        "연구 진행 중 · " + purchasedCount.ToString(CultureInfo.InvariantCulture));
                yield return new WaitForSecondsRealtime(secondsBetweenPurchases);
            }

            if (purchasedCount > 0)
            {
                BuildNodes(false);
                RefreshSummary();
                SaveProgressionState();
                RefreshGemstonePanel();
                RefreshCombatStatsPanel();
                UpdateHeaderForActiveView();
                List<string> changedNodeIds = GetBulkResearchChangedNodeIds(initialStates);
                if (changedNodeIds.Count > 0)
                    yield return StartCoroutine(AnimateBulkResearchChanges(changedNodeIds));
                TraitAudioFeedback.PlayUpgrade();
            }
            SetTestStatus(purchasedCount > 0
                ? "연구 " + purchasedCount.ToString(CultureInfo.InvariantCulture) + "개를 진행했습니다."
                : "현재 진행할 수 있는 연구가 없습니다.");
            _bulkUpgradeRoutine = null;
            if (_bulkUpgradeButton != null)
            {
                _bulkUpgradeButton.interactable = true;
                SetButtonLabel(_bulkUpgradeButton.GetComponentInChildren<TextMeshProUGUI>(), "가능한 연구 모두 진행");
            }
            if (_runtimeGroup != null) _runtimeGroup.interactable = true;
        }

        private Dictionary<string, NodePurchaseState> CaptureVisibleBoardNodeStates()
        {
            var states = new Dictionary<string, NodePurchaseState>(StringComparer.Ordinal);
            List<BoardNodeEntry> visible = CollectBoardEntries(true);
            for (int i = 0; i < visible.Count; i++)
            {
                TraitNodeDefinition node = visible[i].Node;
                if (node != null) states[node.Id] = GetNodePurchaseState(node);
            }
            return states;
        }

        private List<string> GetBulkResearchChangedNodeIds(
            Dictionary<string, NodePurchaseState> initialStates)
        {
            var changed = new List<string>();
            List<BoardNodeEntry> visible = OrderBoardEntries(CollectBoardEntries(true));
            for (int i = 0; i < visible.Count; i++)
            {
                TraitNodeDefinition node = visible[i].Node;
                if (node == null || !_boardNodeRectsById.ContainsKey(node.Id)) continue;
                NodePurchaseState currentState = GetNodePurchaseState(node);
                if (!initialStates.TryGetValue(node.Id, out NodePurchaseState previousState) ||
                    previousState != currentState)
                {
                    changed.Add(node.Id);
                }
            }
            return changed;
        }

        private IEnumerator AnimateBulkResearchChanges(IReadOnlyList<string> nodeIds)
        {
            const float revealDuration = 0.12f;
            const float staggerSeconds = 0.045f;

            // Hide every newly visible or state-changed node before the frame
            // renders. Then reveal each one in the same quick order as research.
            for (int i = 0; i < nodeIds.Count; i++)
            {
                if (!_boardNodeRectsById.TryGetValue(nodeIds[i], out RectTransform rect) || rect == null)
                    continue;
                CanvasGroup group = rect.GetComponent<CanvasGroup>();
                if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
                rect.localScale = Vector3.one * 0.76f;
            }

            for (int i = 0; i < nodeIds.Count; i++)
            {
                string nodeId = nodeIds[i];
                if (!_boardNodeRectsById.TryGetValue(nodeId, out RectTransform rect) || rect == null)
                    continue;
                CanvasGroup group = rect.GetComponent<CanvasGroup>();
                if (group == null) continue;
                float elapsed = 0f;
                while (elapsed < revealDuration && rect != null)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / revealDuration);
                    float eased = 1f - Mathf.Pow(1f - t, 3f);
                    group.alpha = t;
                    rect.localScale = Vector3.LerpUnclamped(
                        Vector3.one * 0.76f,
                        Vector3.one,
                        eased + Mathf.Sin(t * Mathf.PI) * 0.08f);
                    yield return null;
                }

                if (rect != null)
                {
                    rect.localScale = Vector3.one;
                    group.alpha = 1f;
                    group.interactable = true;
                    group.blocksRaycasts = true;
                    NodePurchaseState state = GetNodePurchaseState(FindNode(nodeId));
                    PlayNodeEffect(
                        nodeId,
                        state == NodePurchaseState.Purchased ? PurchasedColor : AvailableColor,
                        0.26f);
                }

                if (i + 1 < nodeIds.Count)
                    yield return new WaitForSecondsRealtime(staggerSeconds);
            }
        }

        private void RestoreBulkResearchRevealState()
        {
            foreach (RectTransform rect in _boardNodeRectsById.Values)
            {
                if (rect == null) continue;
                CanvasGroup group = rect.GetComponent<CanvasGroup>();
                if (group == null) continue;
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
                rect.localScale = Vector3.one;
            }
        }

        private TraitNodeDefinition FindNextAffordableBulkUpgrade()
        {
            TraitNodeDefinition best = null;
            int bestTier = int.MaxValue;
            long bestCost = long.MaxValue;
            foreach (TraitNodeDefinition candidate in EnumerateAllNodes())
            {
                if (candidate == null || candidate.AcquisitionOnly ||
                    GetNodePurchaseState(candidate) != NodePurchaseState.Available)
                {
                    continue;
                }

                List<TraitGemstoneCost> requirements = GetNodeCostRequirements(candidate);
                int highestGemstoneTier = 0;
                long totalCost = 0L;
                for (int i = 0; i < requirements.Count; i++)
                {
                    TraitGemstoneCost requirement = requirements[i];
                    highestGemstoneTier = Math.Max(
                        highestGemstoneTier,
                        Math.Max(0, GetGemstoneIndexById(requirement.GemstoneId)));
                    totalCost = AddSaturating(totalCost, requirement.Amount);
                }

                if (best == null || highestGemstoneTier < bestTier ||
                    (highestGemstoneTier == bestTier && totalCost < bestCost) ||
                    (highestGemstoneTier == bestTier && totalCost == bestCost &&
                     string.CompareOrdinal(candidate.Id, best.Id) < 0))
                {
                    best = candidate;
                    bestTier = highestGemstoneTier;
                    bestCost = totalCost;
                }
            }

            return best;
        }

        private Button CreateBackButton()
        {
            if (backButtonPrefab == null)
            {
                return CreateFallbackBackButton();
            }

            GameObject instance = Instantiate(backButtonPrefab, _runtimeRoot, false);
            instance.name = "BackButton";
            RectTransform rect = instance.transform as RectTransform;
            ConfigureBackButtonTransform(rect);

            Image background = instance.GetComponent<Image>();
            if (background != null)
            {
                if (backButtonSprite != null) background.sprite = backButtonSprite;
                background.type = Image.Type.Sliced;
                background.color = Color.white;
                background.raycastTarget = true;
            }

            Transform icon = instance.transform.Find("Icon");
            if (icon != null)
            {
                Image iconImage = icon.GetComponent<Image>();
                if (iconImage != null)
                {
                    if (backIcon != null) iconImage.sprite = backIcon;
                    iconImage.preserveAspect = true;
                }
            }

            Button button = instance.GetComponent<Button>();
            if (button == null)
            {
                Destroy(instance);
                return CreateFallbackBackButton();
            }

            if (button.GetComponent<TraitUiButtonFeedback>() == null)
            {
                button.gameObject.AddComponent<TraitUiButtonFeedback>()
                    .SetScaleFeedback(1f, 1f, 0.12f);
            }
            TraitTooltipDismissRelay relay = button.GetComponent<TraitTooltipDismissRelay>();
            if (relay == null) relay = button.gameObject.AddComponent<TraitTooltipDismissRelay>();
            relay.Bind(this);
            return button;
        }

        private Button CreateFallbackBackButton()
        {
            Button button = CreateButton(
                "BackButton",
                _runtimeRoot,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 10f),
                new Vector2(169f, 112f),
                backButtonSprite,
                Color.white,
                string.Empty,
                17,
                inkColor);
            RectTransform rect = button.transform as RectTransform;
            ConfigureBackButtonTransform(rect);
            RectTransform iconRect = CreateImageRect(
                "Icon",
                rect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(1.9f, -2.2f),
                new Vector2(74f, 67f),
                backIcon,
                Color.white,
                new Vector2(0.5f, 0.5f));
            iconRect.GetComponent<Image>().preserveAspect = true;
            return button;
        }

        private static void ConfigureBackButtonTransform(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(0f, 10f);
            rect.sizeDelta = new Vector2(169f, 112f);
            rect.localScale = Vector3.one;
        }

        private void BuildNodeTooltip()
        {
            _tooltipRoot = CreatePanel(
                "NodeTooltip",
                _runtimeRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(392f, 408f),
                panelSprite,
                Color.white,
                new Vector2(0f, 1f));
            Image background = _tooltipRoot.GetComponent<Image>();
            background.type = panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            background.color = panelSprite != null
                ? Color.white
                : new Color(0.008f, 0.014f, 0.028f, 1f);
            background.raycastTarget = true;
            Outline outline = _tooltipRoot.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(1f, 0.74f, 0.31f, 0.88f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
            Shadow tooltipShadow = _tooltipRoot.gameObject.AddComponent<Shadow>();
            tooltipShadow.effectColor = new Color(0f, 0.015f, 0.035f, 0.76f);
            tooltipShadow.effectDistance = new Vector2(0f, -7f);
            tooltipShadow.useGraphicAlpha = true;

            _tooltipGroup = _tooltipRoot.gameObject.AddComponent<CanvasGroup>();
            _tooltipRoot.gameObject.AddComponent<TraitTooltipCanvasRaycastFilter>().Bind(this);
            _tooltipRoot.gameObject.AddComponent<TraitTooltipPointerRelay>().Bind(this);
            _tooltipRoot.gameObject.AddComponent<TraitTooltipDismissRelay>().Bind(this);

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

            _tooltipCloseButton = CreateButton(
                "TooltipClose",
                _tooltipRoot,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-19f, -17f),
                new Vector2(28f, 28f),
                null,
                new Color(0f, 0f, 0f, 0f),
                string.Empty,
                17,
                inkColor);
            _tooltipCloseButton.onClick.AddListener(HideNodeTooltip);

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
            _tooltipDescription = CreateText(
                CreateRect(
                    "TooltipDescription",
                    _tooltipRoot,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, -94f),
                    new Vector2(-36f, 120f),
                    new Vector2(0.5f, 1f)),
                "상세 설명",
                16,
                TextAnchor.UpperLeft,
                new Color(0.91f, 0.95f, 1f, 1f),
                false);
            _tooltipDescription.textWrappingMode = TextWrappingModes.Normal;
            _tooltipDescription.enableAutoSizing = true;
            _tooltipDescription.fontSizeMin = 12f;
            _tooltipDescription.fontSizeMax = 15f;
            _tooltipDescription.overflowMode = TextOverflowModes.Ellipsis;

            CreateImageRect(
                "TooltipSeparator",
                _tooltipRoot,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 174f),
                new Vector2(-32f, 1f),
                null,
                new Color(0.79f, 0.62f, 0.32f, 0.60f),
                new Vector2(0.5f, 0f));
            RectTransform costBand = CreateImageRect(
                "TooltipCostBand",
                _tooltipRoot,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 122f),
                new Vector2(-28f, 48f),
                panelSprite,
                new Color(0.72f, 0.48f, 0.16f, 0.52f),
                new Vector2(0.5f, 0f));
            Image costBandImage = costBand.GetComponent<Image>();
            if (costBandImage != null && panelSprite != null)
            {
                costBandImage.type = Image.Type.Sliced;
            }
            _tooltipCost = CreateText(
                CreateRect(
                    "TooltipCost",
                    _tooltipRoot,
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(10f, 130f),
                    new Vector2(-72f, 32f),
                    new Vector2(0.5f, 0f)),
                "비용",
                18,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            _tooltipCost.enableAutoSizing = true;
            _tooltipCost.fontSizeMin = 15f;
            _tooltipCost.fontSizeMax = 18f;
            for (int i = 0; i < _tooltipCostIcons.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                Vector2 cellStart = new Vector2(col / 3f, 1f);
                Vector2 cellEnd = new Vector2((col + 1) / 3f, 1f);
                RectTransform icon = CreateImageRect(
                    "CostGem_" + i, costBand, cellStart, cellStart,
                    new Vector2(8f, -3f - 22f * row), new Vector2(18f, 18f),
                    null, Color.white, new Vector2(0f, 1f));
                _tooltipCostIcons[i] = icon.GetComponent<Image>();
                _tooltipCostIcons[i].preserveAspect = true;
                _tooltipCostAmounts[i] = CreateText(CreateRect(
                    "CostAmount_" + i, costBand, cellStart, cellEnd,
                    new Vector2(30f, -3f - 22f * row), new Vector2(-33f, 18f),
                    new Vector2(0f, 1f)), string.Empty, 13, TextAnchor.MiddleLeft, inkColor, true);
                _tooltipCostAmounts[i].enableAutoSizing = true;
                _tooltipCostAmounts[i].fontSizeMin = 9;
                _tooltipCostAmounts[i].fontSizeMax = 13;
            }
            RectTransform currencyIconRect = CreateImageRect(
                "TooltipCurrencyIcon",
                _tooltipRoot,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(27f, 146f),
                new Vector2(36f, 36f),
                null,
                Color.white,
                new Vector2(0.5f, 0.5f));
            _tooltipCurrencyIcon = currencyIconRect.GetComponent<Image>();
            _tooltipCurrencyIcon.preserveAspect = true;
            _tooltipCurrencyIcon.raycastTarget = false;
            _tooltipCurrencyIcon.gameObject.SetActive(false);
            _tooltipUpgradeButton = CreateButton(
                "TooltipUpgradeButton",
                _tooltipRoot,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 36f),
                new Vector2(348f, 48f),
                buttonFrameSprite,
                new Color(0.88f, 0.65f, 0.27f, 1f),
                "강화하기",
                17,
                new Color(0.10f, 0.07f, 0.025f, 1f));
            _tooltipUpgradeLabel = _tooltipUpgradeButton.GetComponentInChildren<TextMeshProUGUI>();
            Outline actionOutline = _tooltipUpgradeButton.gameObject.AddComponent<Outline>();
            actionOutline.effectColor = new Color(1f, 0.82f, 0.43f, 0.92f);
            actionOutline.effectDistance = new Vector2(1f, -1f);
            _tooltipUpgradeButton.onClick.AddListener(() =>
                TryUpgradeNode(FindNode(_tooltipNodeId)));

            _tooltipResetButton = CreateButton(
                "TooltipResetButton",
                _tooltipRoot,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-48f, 36f),
                new Vector2(82f, 44f),
                buttonFrameSprite,
                new Color(0.39f, 0.24f, 0.30f, 1f),
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
                new Vector2(0f, 64f),
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
            AddLocalizedInfo("공격 범위", "attackRadiusWorldUnits", information.stats.attackRadiusWorldUnits);
            AddLocalizedInfo("공격 간격", "attackCooldownSeconds", information.stats.attackCooldownSeconds);
            AddLocalizedInfo("치명타 확률", "criticalChancePercent", information.stats.criticalChancePercent);
            AddLocalizedInfo("보스 피해 배율", "bossDamageMultiplier", information.stats.bossDamageMultiplier);
            AddLocalizedInfo("일반 필드 시간", "normalFieldDurationSeconds", information.stats.normalFieldDurationSeconds);

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
                AddLocalizedInfo("드롭 재화", "gemstoneId", GetGemstoneLocalizedName(monster.gemstoneId, monster.gemstoneId));
                AddLocalizedInfo("드롭 수량", "gemstoneAmount", monster.gemstoneAmount);
                AddLocalizedInfo("드롭 확률(%)", "gemstoneChancePercent", monster.gemstoneChancePercent);
                AddLocalizedInfo(
                    "실행 특성",
                    "specialAbility",
                    GetMonsterTraitDescription(monster.id));
            }

            CreateSummarySection(_summaryOverlayContent, "스킬 정보", GetTabAccent(TraitTab.Skill));
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
            }

            CreateSummarySection(_summaryOverlayContent, "유물 전투 효과와 드롭 정보",
                new Color(0.92f, 0.69f, 0.28f, 1f));
            for (int i = 0; i < information.relics.Length; i++)
            {
                RelicInformation relic = information.relics[i];
                string title = i < relicEntries.Count && relicEntries[i] != null &&
                               !string.IsNullOrWhiteSpace(relicEntries[i].Title)
                    ? relicEntries[i].Title
                    : relic.id;
                CreateSummaryCategory(_summaryOverlayContent,
                    title + " · " + relic.id, new Color(0.92f, 0.69f, 0.28f, 1f));
                AddLocalizedInfo("획득 상태", "owned", relic.owned ? "획득" : "미획득");
                EntityTextDefinition sourceMonster = _document == null
                    ? null
                    : _document.FindEntity(relic.sourceMonsterId);
                AddLocalizedInfo("드롭 몬스터", "sourceMonsterId",
                    sourceMonster == null ? relic.sourceMonsterId : sourceMonster.displayName);
                AddLocalizedInfo("개별 드롭 확률(%)", "dropChancePercent",
                    relic.dropChancePercent);
                AddLocalizedInfo("전투 효과 ID", "effectId", relic.effectId);
                AddLocalizedInfo("효과 수치", "effectValue", relic.effectValue);
                if (relic.effectValue2 > 0f)
                    AddLocalizedInfo("보조 효과 수치(%)", "effectValue2",
                        relic.effectValue2);
            }

            CreateSummarySection(_summaryOverlayContent, "통합 연구 트리 전체 진행도", AvailableColor);
            foreach (TraitNodeDefinition node in EnumerateAllNodes())
            {
                if (node == null) continue;
                NodePurchaseState state = GetNodePurchaseState(node);
                string stateLabel = GetNodeStateLabel(node, state);
                string prerequisite = string.IsNullOrEmpty(node.PrerequisiteNodeId)
                    ? "없음"
                    : GetNodeDisplayName(node.PrerequisiteNodeId);
                AddSummaryRow(
                    _summaryOverlayContent,
                    GetLocalizedNodeTitle(node) + " · " + node.Id,
                    stateLabel + "  |  " + GetNodeCostText(node) + "  |  선행 " + prerequisite +
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

        private string GetMonsterTraitDescription(string monsterId)
        {
            MonsterDefinition definition = FindMonsterDefinition(monsterId);
            MonsterDefinition baselineDefinition = FindMonsterDefinition("monster.01");
            if (definition == null || baselineDefinition == null)
                return LocalizationCatalog.Get(
                    "ui.monster.trait.unavailable",
                    "몬스터 특성 정보가 준비되지 않았습니다.");

            return definition.BuildTraitDescription(baselineDefinition.CreateSnapshot(1));
        }

        private MonsterDefinition FindMonsterDefinition(string monsterId)
        {
            if (string.IsNullOrEmpty(monsterId)) return null;
            if (!_monsterDefinitionsLoaded)
            {
                MonsterDefinition[] definitions = Resources.LoadAll<MonsterDefinition>("MonsterDefinitions");
                for (int i = 0; i < definitions.Length; i++)
                {
                    MonsterDefinition definition = definitions[i];
                    if (definition != null && !string.IsNullOrEmpty(definition.MonsterId))
                        _monsterDefinitionsById[definition.MonsterId] = definition;
                }
                _monsterDefinitionsLoaded = true;
            }

            return _monsterDefinitionsById.TryGetValue(monsterId, out MonsterDefinition result)
                ? result
                : null;
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
            valueText.overflowMode = TextOverflowModes.Ellipsis;
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
            text.overflowMode = TextOverflowModes.Ellipsis;
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
            SetBalancePatchEditorOpen(false);
            HideNodeTooltip();
            CloseRelicDetail();
            _showRelicCollection = false;
            _showTalentCollection = false;
            _showFieldCollection = false;
            SetGraphNavigationEnabled(true);
            _relicTestMode = false;
            SetRelicTestMode(false);
            _activeTab = tab;
            _selectedNodeId = null;
            BuildCategories();
            UpdateHeaderForActiveView();
            SetCollectionChromeVisible(true);
            RefreshCollectionTabButtons();
            BuildNodes(animate);
            RefreshSummary();
        }

        private void ShowRelicCollection(bool animate = true)
        {
            SetBalancePatchEditorOpen(false);
            if (_boardLayoutEditing) SetBoardLayoutEditing(false);
            CloseRelicDetail();
            _showRelicCollection = true;
            _showTalentCollection = false;
            _showFieldCollection = false;
            SetGraphNavigationEnabled(false);
            _talentDraftRevealed = false;
            _talentDraftIds.Clear();
            _selectedNodeId = null;
            _tooltipNodeId = null;
            HideNodeTooltip();
            CloseSummaryPopup();
            SetTestToolsOpen(false);
            UpdateHeaderForActiveView();
            SetCollectionChromeVisible(false);
            RefreshCollectionTabButtons();
            BuildNodes(animate);
        }

        private void ShowTalentCollection()
        {
            SetBalancePatchEditorOpen(false);
            if (_boardLayoutEditing) SetBoardLayoutEditing(false);
            CloseRelicDetail();
            _showRelicCollection = false;
            _showTalentCollection = true;
            _showFieldCollection = false;
            SetGraphNavigationEnabled(false);
            _relicTestMode = false;
            _selectedNodeId = null;
            _tooltipNodeId = null;
            HideNodeTooltip();
            CloseSummaryPopup();
            SetTestToolsOpen(false);
            UpdateHeaderForActiveView();
            SetCollectionChromeVisible(false);
            RefreshCollectionTabButtons();
            BuildNodes(false);
        }

        private void ShowFieldSelectionForCombat()
        {
            SetBalancePatchEditorOpen(false);
            if (_boardLayoutEditing) SetBoardLayoutEditing(false);
            CloseRelicDetail();
            _showRelicCollection = false;
            _showTalentCollection = false;
            _showFieldCollection = true;
            SetGraphNavigationEnabled(false);
            _selectedNodeId = null;
            _tooltipNodeId = null;
            HideNodeTooltip();
            CloseSummaryPopup();
            SetTestToolsOpen(false);
            UpdateHeaderForActiveView();
            SetCollectionChromeVisible(false);
            RefreshCollectionTabButtons();
            BuildNodes(false);
        }

        private void RefreshCollectionTabButtons()
        {
            SetCollectionTabButtonState(
                _researchCategoryButton,
                !_showRelicCollection && !_showTalentCollection && !_showFieldCollection,
                researchCategoryTabFrame);
            SetCollectionTabButtonState(
                _relicCategoryButton,
                _showRelicCollection,
                relicCategoryTabFrame);
            SetCollectionTabButtonState(
                _talentCategoryButton,
                _showTalentCollection,
                talentCategoryTabFrame);
        }

        private void SetCollectionTabButtonState(Button button, bool selected, Sprite categoryFrame)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            Color selectedColor = new Color(1f, 0.97f, 0.88f, 1f);
            Color idleColor = Color.white;
            if (image != null)
            {
                image.sprite = categoryFrame != null
                    ? categoryFrame
                    : selected
                        ? tabFocusSprite != null ? tabFocusSprite : buttonFrameSprite
                        : tabSprite != null ? tabSprite : buttonFrameSprite;
                if (image.sprite != null) image.type = Image.Type.Sliced;
                image.color = selected ? selectedColor : idleColor;
            }

            // Keep inactive categories bright and readable; emphasize only the
            // current category with a small scale lift and a crisp gold marker.
            button.transform.localScale = selected ? Vector3.one * 1.045f : Vector3.one;
            Transform accent = button.transform.Find("SelectedAccent");
            if (accent != null) accent.gameObject.SetActive(selected);

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.color = selected
                    ? new Color(0.035f, 0.065f, 0.12f, 1f)
                    : new Color(0.10f, 0.14f, 0.20f, 1f);
                label.fontSize = selected ? 26f : 24f;
                label.fontSizeMax = selected ? 26f : 24f;
            }

            button.interactable = true;
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? selectedColor : idleColor;
            colors.highlightedColor = Color.Lerp(colors.normalColor, Color.white, 0.12f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(colors.normalColor, Color.black, 0.10f);
            colors.disabledColor = selected ? selectedColor : idleColor;
            button.colors = colors;
        }

        private void SetGraphNavigationEnabled(bool enabled)
        {
            if (_nodeViewport == null) return;
            TraitGraphScrollRect scroll = _nodeViewport.GetComponent<TraitGraphScrollRect>();
            if (scroll == null) return;
            scroll.velocity = Vector2.zero;
            scroll.enabled = enabled;
        }

        private void UpdateHeaderForActiveView()
        {
            if (_screenTitle != null)
            {
                SetLocalizedText(
                    _screenTitle,
                    _showRelicCollection ? "유물 보관소" : _showTalentCollection ? "특성 카드" :
                    _showFieldCollection ? "일반 전투 · 필드 선택" : "특성");
            }

        }

        private void SetCollectionChromeVisible(bool visible)
        {
            if (_runtimeRoot == null)
            {
                return;
            }

            RefreshProgressionSidebarVisibility();
            if (_boardLayoutEditButton != null)
                _boardLayoutEditButton.gameObject.SetActive(!_showRelicCollection && !_showTalentCollection && !_showFieldCollection);
            if (_balanceEditorToggle != null)
                _balanceEditorToggle.gameObject.SetActive(!_showRelicCollection && !_showTalentCollection && !_showFieldCollection);
            if (_balanceEditorPanel != null && (_showRelicCollection || _showTalentCollection || _showFieldCollection))
                _balanceEditorPanel.gameObject.SetActive(false);
            if (_bulkUpgradeButton != null)
                _bulkUpgradeButton.gameObject.SetActive(!_showRelicCollection && !_showTalentCollection && !_showFieldCollection);
            if (_generalCombatButton != null)
                _generalCombatButton.gameObject.SetActive(!_showRelicCollection && !_showTalentCollection && !_showFieldCollection);
            if (_bossCombatButton != null)
                _bossCombatButton.gameObject.SetActive(!_showRelicCollection && !_showTalentCollection && !_showFieldCollection);

            Transform testToolsToggle = _runtimeRoot.Find("TestToolsToggle");
            if (testToolsToggle != null)
            {
                testToolsToggle.gameObject.SetActive(
                    !_showRelicCollection && !_showTalentCollection && !_showFieldCollection);
            }

            if (_relicTestModeButton != null)
            {
                _relicTestModeButton.gameObject.SetActive(_showRelicCollection);
            }

            if (_testToolsPanel != null)
            {
                _testToolsPanel.gameObject.SetActive(visible && _testToolsOpen);
            }

            if (_statusMessage != null)
            {
                _statusMessage.gameObject.SetActive(visible);
            }
        }

        private void RefreshProgressionSidebarVisibility()
        {
            // The lower-left test panel occupies the same screen region as the wallet/stat cards.
            // Hide those cards while testing tools are open, then restore them when the panel closes.
            bool showSidebar = !_showTalentCollection && !_showFieldCollection &&
                !_testToolsOpen && !_balanceEditorOpen;
            if (_gemstonePanel != null) _gemstonePanel.gameObject.SetActive(showSidebar);
            if (_combatStatsPanel != null) _combatStatsPanel.gameObject.SetActive(showSidebar);
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
            if (_showRelicCollection)
            {
                BuildRelicGrid();
                return;
            }
            if (_showTalentCollection)
            {
                BuildTalentCollection();
                return;
            }
            if (_showFieldCollection)
            {
                BuildFieldCollection();
                return;
            }

            BuildUnifiedBoard(animate);
        }

        private void BuildTalentCollection()
        {
            if (_nodeContent == null || _nodeViewport == null) return;
            HideNodeTooltip();
            _rainbowFrameSegments.Clear();
            DestroyChildren(_nodeContent);
            _boardPositionsByNodeId.Clear();
            _boardNodeRectsById.Clear();
            _nodeContent.localScale = Vector3.one;
            _nodeContent.anchoredPosition = Vector2.zero;

            float width = Mathf.Max(760f, _nodeViewport.rect.width);
            float height = Mathf.Max(560f, _nodeViewport.rect.height);
            _nodeContent.sizeDelta = new Vector2(width, height);

            float collectionWidth = Mathf.Clamp(width * 0.22f, 300f, 370f);
            float collectionHeight = Mathf.Clamp(height * 0.56f, 320f, 560f);
            RectTransform collection = CreatePanel(
                "TalentCollectionPanel", _nodeContent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-width * 0.5f + collectionWidth * 0.5f + 32f,
                    height * 0.5f - 145f - collectionHeight * 0.5f),
                new Vector2(collectionWidth, collectionHeight),
                panelSprite, new Color(0.035f, 0.065f, 0.11f, 0.96f),
                new Vector2(0.5f, 0.5f));
            Outline collectionOutline = collection.GetComponent<Outline>();
            if (collectionOutline != null)
            {
                collectionOutline.effectColor = new Color(0.36f, 0.46f, 0.59f, 0.82f);
                collectionOutline.effectDistance = new Vector2(2f, -3f);
            }
            CreateText(CreateRect("TalentCollectionTitle", collection,
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(22f, -18f), new Vector2(-150f, 42f),
                    new Vector2(0f, 1f)),
                "보유중", 24, TextAnchor.MiddleLeft, inkColor, true);
            CreateText(CreateRect("TalentPlayerLevel", collection,
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(22f, -62f), new Vector2(-154f, 28f),
                    new Vector2(0f, 1f)),
                "현재 레벨 " + PlayerLevel.ToString(CultureInfo.InvariantCulture),
                17, TextAnchor.MiddleLeft, new Color(1f, 0.82f, 0.39f, 1f), true);

            RectTransform cardBalance = CreatePanel(
                "TalentCardBalance", collection,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -18f), new Vector2(96f, 42f),
                buttonFrameSprite, new Color(0.045f, 0.075f, 0.12f, 1f),
                new Vector2(1f, 1f));
            Sprite talentCardIcon = skillIcon != null ? skillIcon : petIcon;
            if (talentCardIcon != null)
            {
                RectTransform cardIcon = CreateImageRect(
                    "Icon", cardBalance,
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(8f, 0f), new Vector2(30f, 30f),
                    talentCardIcon, Color.white, new Vector2(0f, 0.5f));
                cardIcon.GetComponent<Image>().preserveAspect = true;
            }
            CreateText(CreateRect("Count", cardBalance,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-10f, 0f), new Vector2(50f, 32f),
                    new Vector2(1f, 0.5f)),
                _availableTraitCards.ToString(CultureInfo.InvariantCulture),
                21, TextAnchor.MiddleRight, new Color(1f, 0.82f, 0.39f, 1f), true);

            var stacks = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < _ownedTalentIds.Count; i++)
            {
                string id = _ownedTalentIds[i];
                stacks.TryGetValue(id, out int count);
                stacks[id] = count + 1;
            }
            if (stacks.Count == 0)
            {
                CreateText(CreateRect("TalentCollectionEmpty", collection,
                        new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                        Vector2.zero, new Vector2(-42f, 96f), new Vector2(0.5f, 0.5f)),
                    "아직 보유한 특성이 없습니다.",
                    17, TextAnchor.MiddleCenter, mutedInkColor, false);
            }
            else
            {
                int row = 0;
                foreach (KeyValuePair<string, int> stack in stacks)
                {
                    TalentOption option = FindTalentOption(stack.Key);
                    if (option == null) continue;
                    float y = -108f - row * 60f;
                    RectTransform card = CreatePanel(
                        "TalentOwned_" + stack.Key, collection,
                        new Vector2(0f, 1f), new Vector2(1f, 1f),
                        new Vector2(0f, y), new Vector2(-30f, 50f),
                        buttonFrameSprite, new Color(0.13f, 0.18f, 0.26f, 0.98f),
                        new Vector2(0.5f, 1f));
                    if (option.icon != null)
                    {
                        RectTransform icon = CreateImageRect("Icon", card,
                            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(25f, 0f), new Vector2(34f, 34f), option.icon,
                            Color.white, new Vector2(0.5f, 0.5f));
                        icon.GetComponent<Image>().preserveAspect = true;
                    }
                    CreateText(CreateRect("Name", card,
                            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                            new Vector2(52f, 0f), new Vector2(-100f, 36f),
                            new Vector2(0f, 0.5f)),
                        option.title, 16, TextAnchor.MiddleLeft, inkColor, true);
                    CreateText(CreateRect("Stack", card,
                            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                            new Vector2(-18f, 0f), new Vector2(58f, 32f),
                            new Vector2(1f, 0.5f)),
                        "×" + stack.Value.ToString(CultureInfo.InvariantCulture),
                        16, TextAnchor.MiddleRight, new Color(1f, 0.81f, 0.38f, 1f), true);
                    if (card.GetComponent<TraitTalentTooltipPointerRelay>() == null)
                    {
                        card.gameObject.AddComponent<TraitTalentTooltipPointerRelay>()
                            .Bind(this, stack.Key);
                    }
                    row++;
                }
            }

            float groupLeft = collection.anchoredPosition.x + collectionWidth * 0.5f + 32f;
            float groupRight = width * 0.5f - 32f;
            float availableCardWidth = Mathf.Max(360f, groupRight - groupLeft);
            float cardCenterX = (groupLeft + groupRight) * 0.5f;
            float headingTopInset = Mathf.Clamp(height * 0.23f, 176f, 240f);
            CreateText(CreateRect("TalentDraftHeading", _nodeContent,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(cardCenterX, -headingTopInset),
                    new Vector2(Mathf.Min(availableCardWidth, 760f), 46f),
                    new Vector2(0.5f, 0.5f)),
                _talentDraftRevealed ? "하나의 특성을 선택하세요" : "세 장의 특성 카드",
                27, TextAnchor.MiddleCenter, inkColor, true);

            TalentOption[] options = _talentDraftRevealed ? ResolveTalentDraft() : Array.Empty<TalentOption>();
            float cardGap = Mathf.Clamp(width * 0.014f, 12f, 20f);
            float cardWidth = Mathf.Clamp((availableCardWidth - cardGap * 2f) / 3f, 120f, 260f);
            float cardHeight = Mathf.Clamp(height * 0.39f, 240f, 360f);
            float cardCenterY = height * 0.5f - headingTopInset - 54f - cardHeight * 0.5f;
            for (int i = 0; i < 3; i++)
            {
                bool revealed = _talentDraftRevealed && i < options.Length && options[i] != null;
                TalentOption option = revealed ? options[i] : null;
                RectTransform card = CreatePanel(
                    "TalentDraftCard_" + i.ToString(CultureInfo.InvariantCulture),
                    _nodeContent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(cardCenterX + (i - 1) * (cardWidth + cardGap), cardCenterY),
                    new Vector2(cardWidth, cardHeight), panelSprite,
                    revealed ? new Color(0.25f, 0.17f, 0.09f, 0.98f) : new Color(0.06f, 0.10f, 0.17f, 0.99f),
                    new Vector2(0.5f, 0.5f));
                Image cardImage = card.GetComponent<Image>();
                Outline outline = card.GetComponent<Outline>();
                if (outline == null) outline = card.gameObject.AddComponent<Outline>();
                outline.effectColor = revealed ? new Color(1f, 0.78f, 0.31f, 0.96f) : new Color(0.38f, 0.48f, 0.61f, 0.8f);
                outline.effectDistance = new Vector2(2f, -3f);
                Shadow cardShadow = card.GetComponent<Shadow>();
                if (cardShadow == null) cardShadow = card.gameObject.AddComponent<Shadow>();
                cardShadow.effectColor = new Color(0f, 0.01f, 0.03f, 0.75f);
                cardShadow.effectDistance = new Vector2(2f, -7f);
                CreateSquareNodeFrame(
                    card,
                    revealed ? new Color(0.96f, 0.70f, 0.30f, 0.96f) : new Color(0.48f, 0.59f, 0.75f, 0.85f),
                    4f);
                if (!revealed)
                {
                    RectTransform medallion = CreatePanel(
                        "CardBackMedallion", card,
                        new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f),
                        Vector2.zero, new Vector2(82f, 82f), buttonFrameSprite,
                        new Color(0.12f, 0.19f, 0.29f, 0.96f), new Vector2(0.5f, 0.5f));
                    Outline medallionOutline = medallion.GetComponent<Outline>();
                    if (medallionOutline == null) medallionOutline = medallion.gameObject.AddComponent<Outline>();
                    medallionOutline.effectColor = new Color(0.77f, 0.62f, 0.36f, 0.9f);
                    medallionOutline.effectDistance = new Vector2(1f, -1f);
                    CreateText(CreateRect("CardBackMark", medallion,
                            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                            new Vector2(0.5f, 0.5f)),
                        "?", 44, TextAnchor.MiddleCenter,
                        new Color(0.94f, 0.76f, 0.42f, 0.95f), true);
                }
                if (revealed && option.icon != null)
                {
                    float iconSize = Mathf.Min(142f, cardWidth * 0.56f);
                    RectTransform icon = CreateImageRect("Icon", card,
                        new Vector2(0.5f, 0.59f), new Vector2(0.5f, 0.59f),
                        Vector2.zero, new Vector2(iconSize, iconSize),
                        option.icon, Color.white, new Vector2(0.5f, 0.5f));
                    icon.GetComponent<Image>().preserveAspect = true;
                }
                if (revealed)
                {
                    CreateText(CreateRect("CardTitle", card,
                            new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.97f),
                            Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)),
                        option.title, 20, TextAnchor.MiddleCenter,
                        new Color(1f, 0.84f, 0.47f, 1f), true);
                    CreateText(CreateRect("CardDescription", card,
                        new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.35f),
                        Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)),
                        option.description, 14, TextAnchor.MiddleCenter, inkColor, false);
                }
                Button choose = card.gameObject.AddComponent<Button>();
                choose.targetGraphic = cardImage;
                choose.interactable = revealed;
                choose.transition = Selectable.Transition.ColorTint;
                if (revealed)
                {
                    string capturedId = option.id;
                    RectTransform capturedCard = card;
                    choose.onClick.AddListener(() => ChooseTalentCard(capturedId, capturedCard));
                    card.gameObject.AddComponent<TraitCardRevealAnimator>().Play(i * 0.11f);
                }
                if (revealed)
                {
                    card.gameObject.AddComponent<TraitTalentTooltipPointerRelay>()
                        .Bind(this, option.id);
                }
            }

            int cost = TraitCardDrawCost;
            bool canDraw = _availableTraitCards >= cost;
            bool canReroll = CanRerollTalentDraft();
            string drawLabel = !_talentDraftRevealed
                ? "특성 뽑기 · " + cost.ToString(CultureInfo.InvariantCulture) + "장"
                : canReroll
                    ? "다시 뽑기"
                    : "특성 선택 중";
            Button draw = CreateButton(
                "DrawTalentCards", _nodeContent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(cardCenterX, cardCenterY - cardHeight * 0.5f - 42f),
                new Vector2(Mathf.Min(360f, availableCardWidth * 0.48f), 58f),
                buttonFrameSprite,
                !_talentDraftRevealed && !canDraw ||
                _talentDraftRevealed && !canReroll
                    ? new Color(0.20f, 0.24f, 0.30f, 1f)
                    : new Color(0.82f, 0.45f, 0.12f, 1f),
                drawLabel,
                19, inkColor);
            draw.interactable = _talentDraftRevealed
                ? canReroll
                : canDraw;
            draw.onClick.AddListener(DrawTalentCards);
        }

        private void BuildFieldCollection()
        {
            if (_nodeContent == null || _nodeViewport == null) return;
            HideNodeTooltip();
            _rainbowFrameSegments.Clear();
            DestroyChildren(_nodeContent);
            _boardPositionsByNodeId.Clear();
            _boardNodeRectsById.Clear();
            _nodeContent.localScale = Vector3.one;
            _nodeContent.anchoredPosition = Vector2.zero;

            float width = Mathf.Max(760f, _nodeViewport.rect.width);
            float height = Mathf.Max(560f, _nodeViewport.rect.height);
            _nodeContent.sizeDelta = new Vector2(width, height);
            int unlockedTier = UnlockedFieldTier;
            int selectedTier = SelectedFieldTier;
            float headingTopInset = Mathf.Clamp(height * 0.18f, 132f, 190f);
            CreateText(CreateRect("FieldSelectionHeading", _nodeContent,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -headingTopInset),
                    new Vector2(Mathf.Min(820f, width - 48f), 46f), new Vector2(0.5f, 0.5f)),
                "필드를 선택하세요", 27, TextAnchor.MiddleCenter, inkColor, true);

            string[] names = { "초원", "속삭임 숲", "수정 협곡", "바람 고원", "별빛 유적" };
            string[] descriptions =
            {
                "모험의 시작이 되는 평온한 들판입니다.",
                "숲의 기운이 보상을 끌어당깁니다.",
                "수정맥이 경험의 흐름을 높입니다.",
                "거센 바람이 더 많은 보상을 가져옵니다.",
                "가장 위험하지만 가장 풍성한 필드입니다."
            };
            int[] gemstoneBonuses = { 0, 10, 20, 35, 50 };
            int[] experienceBonuses = { 0, 5, 10, 20, 30 };
            const float gap = 18f;
            const float rowGap = 16f;
            float usableWidth = width - 96f;
            int columns = width >= 1480f ? 5 : 3;
            int rows = Mathf.CeilToInt(5f / columns);
            float cardWidth = Mathf.Min(320f, (usableWidth - gap * (columns - 1)) / columns);
            float cardHeight = Mathf.Clamp(
                (height - headingTopInset - 88f - rowGap * (rows - 1)) / rows,
                180f, 420f);
            float gridTopY = height * 0.5f - headingTopInset - 56f;

            for (int index = 0; index < 5; index++)
            {
                int tier = index + 1;
                bool unlocked = tier <= unlockedTier;
                bool selected = tier == selectedTier;
                int rowIndex = index / columns;
                int columnIndex = index % columns;
                int countInRow = Mathf.Min(columns, 5 - rowIndex * columns);
                float rowWidth = countInRow * cardWidth + gap * (countInRow - 1);
                float rowLeft = -rowWidth * 0.5f;
                float rowCenterY = gridTopY - rowIndex * (cardHeight + rowGap) - cardHeight * 0.5f;
                Vector2 position = new Vector2(
                    rowLeft + columnIndex * (cardWidth + gap) + cardWidth * 0.5f,
                    rowCenterY);
                Color fill = selected
                    ? new Color(0.24f, 0.17f, 0.08f, 0.98f)
                    : unlocked ? new Color(0.075f, 0.11f, 0.17f, 0.97f)
                    : new Color(0.04f, 0.06f, 0.09f, 0.90f);
                RectTransform card = CreatePanel(
                    "FieldCard_" + tier.ToString(CultureInfo.InvariantCulture),
                    _nodeContent,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    position, new Vector2(cardWidth, cardHeight), panelSprite, fill,
                    new Vector2(0.5f, 0.5f));
                Image cardImage = card.GetComponent<Image>();
                Outline outline = card.GetComponent<Outline>();
                if (outline == null) outline = card.gameObject.AddComponent<Outline>();
                outline.effectColor = selected
                    ? new Color(1f, 0.79f, 0.31f, 1f)
                    : unlocked ? new Color(0.53f, 0.68f, 0.84f, 0.9f)
                    : new Color(0.25f, 0.30f, 0.38f, 0.72f);
                outline.effectDistance = new Vector2(2f, -3f);
                CreateSquareNodeFrame(card, outline.effectColor, selected ? 4f : 3f);

                string[] tierMarks = { "I", "II", "III", "IV", "V" };
                CreateText(CreateRect("FieldTier", card,
                        new Vector2(0.5f, 0.93f), new Vector2(0.5f, 0.93f),
                        Vector2.zero, new Vector2(cardWidth - 28f, 26f),
                        new Vector2(0.5f, 1f)),
                    "필드 " + tier.ToString(CultureInfo.InvariantCulture), 16,
                    TextAnchor.MiddleCenter, selected ? new Color(1f, 0.82f, 0.4f, 1f) : inkColor, true);

                RectTransform emblem = CreatePanel(
                    "FieldEmblem", card,
                    new Vector2(0.5f, 0.70f), new Vector2(0.5f, 0.70f),
                    Vector2.zero, new Vector2(46f, 46f),
                    buttonFrameSprite,
                    unlocked ? new Color(0.20f, 0.27f, 0.35f, 1f)
                        : new Color(0.12f, 0.14f, 0.18f, 1f),
                    new Vector2(0.5f, 1f));
                Outline emblemOutline = emblem.GetComponent<Outline>();
                if (emblemOutline != null)
                {
                    emblemOutline.effectColor = unlocked
                        ? new Color(0.86f, 0.68f, 0.36f, 0.92f)
                        : new Color(0.32f, 0.35f, 0.40f, 0.78f);
                    emblemOutline.effectDistance = new Vector2(2f, -2f);
                }
                CreateText(CreateRect("FieldEmblemMark", emblem,
                        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                        new Vector2(0.5f, 0.5f)),
                    tierMarks[index], 27, TextAnchor.MiddleCenter,
                    unlocked ? new Color(1f, 0.84f, 0.48f, 1f) : mutedInkColor, true);

                CreateText(CreateRect("FieldName", card,
                        new Vector2(0.08f, 0.50f), new Vector2(0.92f, 0.50f),
                        Vector2.zero, new Vector2(0f, 28f), new Vector2(0.5f, 0.5f)),
                    unlocked ? names[index] : "잠김", 17, TextAnchor.MiddleCenter,
                    unlocked ? inkColor : mutedInkColor, true);
                CreateText(CreateRect("FieldDescription", card,
                        new Vector2(0.10f, 0.29f), new Vector2(0.90f, 0.29f),
                        Vector2.zero, new Vector2(0f, Mathf.Clamp(cardHeight * 0.18f, 32f, 48f)), new Vector2(0.5f, 0.5f)),
                    unlocked ? descriptions[index]
                        : (tier * 5).ToString(CultureInfo.InvariantCulture) + "레벨에 열립니다.",
                    cardWidth < 210f ? 12 : 14, TextAnchor.MiddleCenter, mutedInkColor, false);

                CreateText(CreateRect("FieldBonuses", card,
                        new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.10f),
                        Vector2.zero, new Vector2(0f, Mathf.Clamp(cardHeight * 0.15f, 30f, 42f)), new Vector2(0.5f, 0.5f)),
                    unlocked
                        ? "젬스톤 +" + gemstoneBonuses[index].ToString(CultureInfo.InvariantCulture) + "%\n경험치 +" + experienceBonuses[index].ToString(CultureInfo.InvariantCulture) + "%"
                        : "필드 보상 잠금", 15, TextAnchor.MiddleCenter,
                    unlocked ? new Color(1f, 0.81f, 0.39f, 1f) : mutedInkColor, true);

                // Put a dedicated, transparent hit surface above the card art
                // and text. This makes the border and every interior area use
                // the same immediate field-entry action.
                cardImage.raycastTarget = false;
                RectTransform hitArea = CreateRect(
                    "SelectField_" + tier.ToString(CultureInfo.InvariantCulture),
                    card,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero,
                    new Vector2(0.5f, 0.5f));
                Image hitImage = hitArea.gameObject.AddComponent<Image>();
                hitImage.color = new Color(1f, 1f, 1f, 0f);
                hitImage.raycastTarget = true;
                Button choose = hitArea.gameObject.AddComponent<Button>();
                choose.targetGraphic = hitImage;
                choose.transition = Selectable.Transition.None;
                choose.interactable = unlocked;
                ColorBlock colors = choose.colors;
                colors.normalColor = Color.clear;
                colors.highlightedColor = Color.clear;
                colors.pressedColor = Color.clear;
                colors.disabledColor = Color.clear;
                choose.colors = colors;
                choose.onClick.AddListener(() => SelectFieldTier(tier));

            }
        }

        public void DrawTalentCards()
        {
            EnsureProgressionState();
            int cost = TraitCardDrawCost;
            if (!_showTalentCollection || _talentSelectionAnimating) return;

            var candidates = new List<TalentOption>();
            foreach (TalentOption option in TalentOptions())
                if (option != null &&
                    GetOwnedTalentStackCount(option.id) < GetTalentMaxStacks(option))
                    candidates.Add(option);
            bool rerolling = _talentDraftRevealed;
            if (rerolling)
            {
                if (!CanRerollTalentDraft()) return;
                candidates.RemoveAll(option => _talentDraftIds.Contains(option.id));
            }
            else if (_availableTraitCards < cost)
            {
                return;
            }
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int swap = UnityEngine.Random.Range(0, i + 1);
                TalentOption temp = candidates[i];
                candidates[i] = candidates[swap];
                candidates[swap] = temp;
            }
            if (candidates.Count == 0) return;
            _talentDraftIds.Clear();
            for (int i = 0; i < 3; i++)
                _talentDraftIds.Add(candidates[i % candidates.Count].id);
            if (!rerolling) _talentRerollsUsed = 0;
            else _talentRerollsUsed++;
            _talentDraftRevealed = true;
            BuildTalentCollection();
        }

        private void ChooseTalentCard(string id, RectTransform selectedCard)
        {
            if (_talentSelectionAnimating || !_talentDraftRevealed || !_talentDraftIds.Contains(id)) return;
            TalentOption selectedOption = FindTalentOption(id);
            if (selectedOption == null ||
                GetOwnedTalentStackCount(id) >= GetTalentMaxStacks(selectedOption)) return;
            int cost = TraitCardDrawCost;
            if (_availableTraitCards < cost) return;
            int previousCards = _availableTraitCards;
            int previousDrawCount = _traitCardDrawCount;
            _availableTraitCards -= cost;
            int refundChance = Mathf.Clamp(
                GetOwnedTalentStackCount("talent.archive") * 10, 0, 100);
            if (refundChance > 0 && UnityEngine.Random.Range(0, 100) < refundChance)
                _availableTraitCards = (int)Math.Min(int.MaxValue, (long)_availableTraitCards + 1L);
            _traitCardDrawCount++;
            _ownedTalentIds.Add(id);
            if (!SaveProgressionState())
            {
                _availableTraitCards = previousCards;
                _traitCardDrawCount = previousDrawCount;
                _ownedTalentIds.RemoveAt(_ownedTalentIds.Count - 1);
                SetTestStatus("특성 선택을 저장하지 못했습니다.");
                return;
            }

            _talentSelectionAnimating = true;
            StartCoroutine(AnimateTalentCollection(id, selectedCard));
        }

        private IEnumerator AnimateTalentCollection(string id, RectTransform selectedCard)
        {
            TalentOption option = FindTalentOption(id);
            Transform collection = _nodeContent == null ? null : _nodeContent.Find("TalentCollectionPanel");
            RectTransform root = _runtimeRoot;
            if (option != null && collection is RectTransform collectionRect &&
                selectedCard != null && root != null)
            {
                Vector2 startScreen = RectTransformUtility.WorldToScreenPoint(
                    GetUiCamera(), selectedCard.position);
                Vector2 endScreen = RectTransformUtility.WorldToScreenPoint(
                    GetUiCamera(), collectionRect.TransformPoint(new Vector3(0f, collectionRect.rect.height * 0.3f, 0f)));
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        root, startScreen, GetUiCamera(), out Vector2 start) &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        root, endScreen, GetUiCamera(), out Vector2 end))
                {
                    RectTransform fly = CreatePanel(
                        "TalentCollectionFly", root,
                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        start, new Vector2(166f, 210f), panelSprite,
                        new Color(0.26f, 0.18f, 0.09f, 0.98f), new Vector2(0.5f, 0.5f));
                    fly.SetAsLastSibling();
                    _talentFlyCard = fly.gameObject;
                    if (option.icon != null)
                    {
                        RectTransform icon = CreateImageRect("Icon", fly,
                            new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f),
                            Vector2.zero, new Vector2(72f, 72f), option.icon,
                            Color.white, new Vector2(0.5f, 0.5f));
                        icon.GetComponent<Image>().preserveAspect = true;
                    }
                    CreateText(CreateRect("Title", fly,
                            new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.40f),
                            Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)),
                        option.title, 16, TextAnchor.MiddleCenter,
                        new Color(1f, 0.86f, 0.54f, 1f), true);
                    CanvasGroup group = fly.gameObject.AddComponent<CanvasGroup>();
                    group.blocksRaycasts = false;
                    const float duration = 0.48f;
                    float elapsed = 0f;
                    while (elapsed < duration && fly != null)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        float t = Mathf.Clamp01(elapsed / duration);
                        float eased = 1f - Mathf.Pow(1f - t, 3f);
                        fly.anchoredPosition = Vector2.Lerp(start, end, eased);
                        fly.localScale = Vector3.one * Mathf.Lerp(1f, 0.38f, eased);
                        group.alpha = 1f - Mathf.SmoothStep(0.4f, 1f, t);
                        yield return null;
                    }
                    if (fly != null) Destroy(fly.gameObject);
                    _talentFlyCard = null;
                }
            }

            _talentDraftIds.Clear();
            _talentDraftRevealed = false;
            _talentSelectionAnimating = false;
            _talentRerollsUsed = 0;
            RefreshAll();
        }

        private TalentOption[] ResolveTalentDraft()
        {
            var result = new TalentOption[_talentDraftIds.Count];
            for (int i = 0; i < _talentDraftIds.Count; i++) result[i] = FindTalentOption(_talentDraftIds[i]);
            return result;
        }

        private TalentOption[] TalentOptions()
        {
            return new[]
            {
                new TalentOption("talent.wisdom", "지식의 잔향", "일반 전투와 보스전에서 얻는 경험치가 10% 증가합니다.", 10, skillIcon),
                new TalentOption("talent.ledger", "현자의 장부", "보스 토벌 경험치가 10% 증가합니다.", 10, timerIcon),
                new TalentOption("talent.archive", "보존의 서약", "특성을 고를 때마다 소비한 카드 1장을 돌려받을 확률이 10% 증가합니다.", 10, gemstoneIcon),
                new TalentOption("talent.chronicle", "전장의 연대기", "5레벨을 달성할 때마다 특성 카드 1장을 추가로 얻습니다.", 5, attackIcon),
                new TalentOption("talent.library", "확장 서고", "다른 특성의 최대 중첩 수가 2 증가합니다.", 5, petIcon),
                new TalentOption("talent.foresight", "예지의 기록", "특성 카드 세 장을 다시 뽑을 수 있는 횟수가 1회 증가합니다.", 5, criticalIcon)
            };
        }

        private int GetAvailableTalentRerolls()
        {
            return Mathf.Max(0,
                GetOwnedTalentStackCount("talent.foresight") - _talentRerollsUsed);
        }

        private bool CanRerollTalentDraft()
        {
            if (!_talentDraftRevealed || GetAvailableTalentRerolls() <= 0)
                return false;

            int newChoices = 0;
            foreach (TalentOption option in TalentOptions())
            {
                if (option != null &&
                    GetOwnedTalentStackCount(option.id) < GetTalentMaxStacks(option) &&
                    !_talentDraftIds.Contains(option.id))
                {
                    newChoices++;
                }
            }

            return newChoices > 0;
        }

        private int GetTalentMaxStacks(TalentOption option)
        {
            if (option == null) return 0;
            int libraryBonus = option.id == "talent.library"
                ? 0
                : GetOwnedTalentStackCount("talent.library") * 2;
            return option.maxStacks + libraryBonus;
        }

        private TalentOption FindTalentOption(string id)
        {
            foreach (TalentOption option in TalentOptions())
                if (option.id == id) return option;
            return null;
        }

        private sealed class TalentOption
        {
            public readonly string id, title, description;
            public readonly int maxStacks;
            public readonly Sprite icon;
            public TalentOption(string id, string title, string description, int maxStacks, Sprite icon)
            { this.id = id; this.title = title; this.description = description; this.maxStacks = maxStacks; this.icon = icon; }
        }

        private void BuildRelicGrid()
        {
            if (_nodeContent == null || _nodeViewport == null)
            {
                return;
            }

            EnsureRelicEntries();
            HideNodeTooltip();
            _rainbowFrameSegments.Clear();
            DestroyChildren(_nodeContent);
            _boardPositionsByNodeId.Clear();
            _boardNodeRectsById.Clear();
            Canvas.ForceUpdateCanvases();

            float viewportWidth = Mathf.Max(640f, _nodeViewport.rect.width);
            float viewportHeight = Mathf.Max(360f, _nodeViewport.rect.height);
            _nodeContent.localScale = Vector3.one;

            const int columns = 4;
            const int rows = 5;
            const float walletClearance = 430f;
            float rightMargin = Mathf.Max(36f, viewportWidth * 0.035f);
            float availableWidth = Mathf.Max(
                4f * 118f + 3f * 16f,
                viewportWidth - walletClearance - rightMargin);
            float gapX = Mathf.Clamp(viewportWidth * 0.014f, 16f, 28f);
            float gapY = Mathf.Clamp(viewportHeight * 0.015f, 8f, 18f);
            float topMargin = Mathf.Clamp(viewportHeight * 0.14f, 88f, 156f);
            float bottomMargin = Mathf.Clamp(viewportHeight * 0.08f, 50f, 84f);
            float availableHeight = Mathf.Max(5f * 84f + 4f * gapY,
                viewportHeight - topMargin - bottomMargin);
            float widthByViewport = Mathf.Clamp(
                (availableWidth - (columns - 1) * gapX) / columns,
                118f,
                380f);
            float heightByViewport =
                (availableHeight - (rows - 1) * gapY) / rows;
            float cardWidth = Mathf.Min(
                widthByViewport,
                heightByViewport * RelicGridCardAspect);
            float cardHeight = cardWidth / RelicGridCardAspect;
            float gridWidth = columns * cardWidth + (columns - 1) * gapX;
            float gridHeight = rows * cardHeight + (rows - 1) * gapY;
            float contentWidth = Mathf.Max(
                viewportWidth,
                walletClearance + gridWidth + rightMargin);
            float contentOffsetX = (contentWidth - viewportWidth) * 0.5f;
            float gridLeft = -contentWidth * 0.5f + walletClearance;
            float gridCenterY = (bottomMargin - topMargin) * 0.5f;
            _nodeContent.sizeDelta = new Vector2(contentWidth, viewportHeight);
            _nodeContent.anchoredPosition = new Vector2(contentOffsetX, 0f);

            for (int index = 0; index < 20; index++)
            {
                int row = index / columns;
                int column = index % columns;
                Vector2 position = new Vector2(
                    gridLeft + cardWidth * 0.5f +
                        column * (cardWidth + gapX),
                    gridCenterY + gridHeight * 0.5f - cardHeight * 0.5f -
                        row * (cardHeight + gapY));
                RelicDisplayEntry entry = relicEntries[index];
                CreateRelicCard(index, entry, position, new Vector2(cardWidth, cardHeight));
            }

        }

        private void CreateRelicCard(
            int index,
            RelicDisplayEntry entry,
            Vector2 position,
            Vector2 size)
        {
            bool obtained = entry != null && entry.IsObtained;
            Color cardColor = obtained
                ? new Color(0.11f, 0.16f, 0.23f, 0.97f)
                : new Color(0.15f, 0.16f, 0.19f, 0.88f);
            RectTransform card = CreatePanel(
                "RelicSlot_" + (index + 1).ToString("00", CultureInfo.InvariantCulture),
                _nodeContent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                size,
                panelSprite,
                cardColor,
                new Vector2(0.5f, 0.5f));
            Image cardImage = card.GetComponent<Image>();
            cardImage.raycastTarget = true;
            Outline cardOutline = card.GetComponent<Outline>();
            if (cardOutline != null)
            {
                cardOutline.effectColor = obtained
                    ? new Color(0.72f, 0.55f, 0.30f, 0.56f)
                    : new Color(0.59f, 0.62f, 0.67f, 0.54f);
                cardOutline.effectDistance = new Vector2(2f, -2f);
            }

            if (!obtained)
            {
                Button disabledCard = card.gameObject.AddComponent<Button>();
                disabledCard.targetGraphic = cardImage;
                disabledCard.interactable = false;
                ColorBlock cardColors = disabledCard.colors;
                cardColors.normalColor = cardColor;
                cardColors.highlightedColor = Color.Lerp(cardColor, Color.white, 0.08f);
                cardColors.pressedColor = Color.Lerp(cardColor, Color.black, 0.10f);
                cardColors.selectedColor = cardColors.highlightedColor;
                cardColors.disabledColor = new Color(0.43f, 0.45f, 0.49f, 0.76f);
                cardColors.fadeDuration = 0.08f;
                disabledCard.colors = cardColors;

                float chainAngle = Mathf.Atan2(size.y * 0.68f, size.x * 0.68f) *
                    Mathf.Rad2Deg;
                CreateRelicChain(card, size, chainAngle, "ChainA");
                CreateRelicChain(card, size, -chainAngle, "ChainB");
                RectTransform relicLock = CreateImageRect(
                    "RelicLock",
                    card,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(42f, 42f),
                    lockIcon,
                    new Color(0.74f, 0.77f, 0.81f, 0.92f),
                    new Vector2(0.5f, 0.5f));
                relicLock.GetComponent<Image>().preserveAspect = true;
                if (_relicTestMode)
                {
                    CreateRelicTestAction(card, index, false);
                }
                return;
            }

            RectTransform icon = CreateImageRect(
                "RelicIcon",
                card,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(24f, 0f),
                new Vector2(72f, 72f),
                entry.Icon,
                entry.Icon == null ? mutedInkColor : Color.white,
                new Vector2(0f, 0.5f));
            Image iconImage = icon.GetComponent<Image>();
            iconImage.preserveAspect = true;
            if (entry.Icon == null)
            {
                iconImage.color = new Color(0f, 0f, 0f, 0f);
                RectTransform fallbackLabel = CreateRect(
                    "EmptyIconLabel",
                    icon,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero,
                    new Vector2(0.5f, 0.5f));
                CreateText(
                    fallbackLabel,
                    "?",
                    30,
                    TextAnchor.MiddleCenter,
                    mutedInkColor,
                    true);
            }

            CreateText(
                CreateRect(
                "RelicTitle",
                    card,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(112f, -22f),
                    new Vector2(-132f, 34f),
                    new Vector2(0f, 1f)),
                string.IsNullOrWhiteSpace(entry.Title)
                    ? "유물 " + (index + 1).ToString("00", CultureInfo.InvariantCulture)
                    : entry.Title,
                20,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            CreateText(
                CreateRect(
                    "RelicDescription",
                    card,
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0.5f),
                    new Vector2(112f, 10f),
                    new Vector2(-132f, -8f),
                    new Vector2(0f, 0.5f)),
                string.IsNullOrWhiteSpace(entry.Description)
                    ? "효과 정보 준비 중입니다."
                    : entry.Description,
                14,
                TextAnchor.UpperLeft,
                mutedInkColor,
                false);

            if (!string.IsNullOrWhiteSpace(entry.Id) && _unseenRelicIds.Contains(entry.Id))
            {
                RectTransform newBadge = CreateRect(
                    "NewRelicBadge",
                    card,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(12f, -10f),
                    new Vector2(56f, 24f),
                    new Vector2(0f, 1f));
                Image badgeImage = newBadge.gameObject.AddComponent<Image>();
                badgeImage.sprite = buttonFrameSprite;
                badgeImage.color = new Color(0.87f, 0.45f, 0.12f, 1f);
                badgeImage.type = badgeImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                badgeImage.raycastTarget = false;
                CreateText(newBadge, "New", 12, TextAnchor.MiddleCenter,
                    new Color(1f, 0.94f, 0.77f, 1f), true);
            }

            if (_relicTestMode)
            {
                CreateRelicTestAction(card, index, true);
            }

            Button detailButton = card.gameObject.AddComponent<Button>();
            detailButton.targetGraphic = cardImage;
            detailButton.transition = Selectable.Transition.ColorTint;
            ColorBlock detailColors = detailButton.colors;
            detailColors.normalColor = cardColor;
            detailColors.highlightedColor = Color.Lerp(cardColor, Color.white, 0.10f);
            detailColors.pressedColor = Color.Lerp(cardColor, Color.black, 0.12f);
            detailColors.selectedColor = detailColors.highlightedColor;
            detailColors.fadeDuration = 0.08f;
            detailButton.colors = detailColors;
            detailButton.onClick.AddListener(() => ShowRelicDetail(index));
        }

        private void ShowRelicDetail(int index)
        {
            EnsureRelicEntries();
            if (index < 0 || index >= relicEntries.Count ||
                relicEntries[index] == null || !relicEntries[index].IsObtained ||
                _relicDetailOverlay != null || _runtimeRoot == null)
            {
                return;
            }

            RelicDisplayEntry entry = relicEntries[index];
            if (!string.IsNullOrWhiteSpace(entry.Id) && _unseenRelicIds.Remove(entry.Id))
            {
                _lastViewedRelicIndex = index;
                SaveProgressionState();
                BuildNodes(false);
            }
            RectTransform overlay = CreateRect(
                "RelicDetailOverlay",
                _runtimeRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Vector2(0.5f, 0.5f));
            Image dimmer = overlay.gameObject.AddComponent<Image>();
            dimmer.color = new Color(0.015f, 0.025f, 0.055f, 0.88f);
            dimmer.raycastTarget = true;
            Button backdropButton = overlay.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = dimmer;
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.onClick.AddListener(CloseRelicDetail);
            _relicDetailOverlay = overlay.gameObject;
            _selectedRelicIndex = index;
            overlay.SetAsLastSibling();

            float availableHeight = Mathf.Max(280f, _runtimeRoot.rect.height - 72f);
            float cardHeight = Mathf.Min(560f, availableHeight * 0.86f);
            float cardWidth = cardHeight * RelicDetailCardAspect;
            float availableWidth = Mathf.Max(220f, _runtimeRoot.rect.width - 48f);
            if (cardWidth > availableWidth)
            {
                cardWidth = availableWidth;
                cardHeight = cardWidth / RelicDetailCardAspect;
            }

            RectTransform card = CreatePanel(
                "RelicDetailCard",
                overlay,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(cardWidth, cardHeight),
                panelSprite,
                new Color(0.39f, 0.22f, 0.68f, 0.99f),
                new Vector2(0.5f, 0.5f));
            Image cardImage = card.GetComponent<Image>();
            cardImage.raycastTarget = true;
            Outline cardOutline = card.GetComponent<Outline>();
            if (cardOutline != null)
            {
                cardOutline.effectColor = new Color(0.95f, 0.78f, 1f, 0.95f);
                cardOutline.effectDistance = new Vector2(3f, -3f);
            }

            // This no-op button makes clicks on the card stop at the card
            // instead of bubbling to the dimmer's close action.
            Button cardClickBlocker = card.gameObject.AddComponent<Button>();
            cardClickBlocker.targetGraphic = cardImage;
            cardClickBlocker.transition = Selectable.Transition.None;
            cardClickBlocker.onClick.AddListener(() => { });

            CreateText(
                CreateRect(
                    "RelicDetailTitle",
                    card,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, -cardHeight * 0.12f),
                    new Vector2(-32f, 58f),
                    new Vector2(0.5f, 0.5f)),
                string.IsNullOrWhiteSpace(entry.Title)
                    ? "유물 " + (index + 1).ToString("00", CultureInfo.InvariantCulture)
                    : entry.Title,
                27,
                TextAnchor.MiddleCenter,
                inkColor,
                true);

            if (entry.Icon != null)
            {
                RectTransform icon = CreateImageRect(
                    "RelicDetailIcon",
                    card,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, cardHeight * 0.08f),
                    new Vector2(cardWidth * 0.62f, cardHeight * 0.36f),
                    entry.Icon,
                    Color.white,
                    new Vector2(0.5f, 0.5f));
                icon.GetComponent<Image>().preserveAspect = true;
            }
            else
            {
                CreateText(
                    CreateRect(
                        "RelicDetailIconPlaceholder",
                        card,
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0f, cardHeight * 0.08f),
                        new Vector2(cardWidth * 0.62f, cardHeight * 0.24f),
                        new Vector2(0.5f, 0.5f)),
                    "이미지 준비 중",
                    18,
                    TextAnchor.MiddleCenter,
                    new Color(0.92f, 0.86f, 1f, 0.9f),
                    false);
            }

            CreateText(
                CreateRect(
                    "RelicDetailDescription",
                    card,
                    new Vector2(0.08f, 0.12f),
                    new Vector2(0.92f, 0.34f),
                    Vector2.zero,
                    new Vector2(-8f, -8f),
                    new Vector2(0.5f, 0.5f)),
                string.IsNullOrWhiteSpace(entry.Description)
                    ? "효과 정보 준비 중입니다."
                    : entry.Description,
                19,
                TextAnchor.UpperCenter,
                inkColor,
                true);

            Button closeButton = CreateButton(
                "RelicDetailClose",
                card,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 30f),
                new Vector2(Mathf.Min(180f, cardWidth - 48f), 42f),
                null,
                new Color(0.13f, 0.075f, 0.22f, 0.98f),
                "닫기",
                17,
                inkColor);
            closeButton.onClick.AddListener(CloseRelicDetail);
        }

        private void CloseRelicDetail()
        {
            if (_relicDetailOverlay != null)
            {
                Destroy(_relicDetailOverlay);
                _relicDetailOverlay = null;
            }

            _selectedRelicIndex = -1;
        }

        private void CreateRelicTestAction(RectTransform card, int index, bool obtained)
        {
            Button action = CreateButton(
                obtained ? "RevokeRelicTest_" + index : "GrantRelicTest_" + index,
                card,
                obtained ? new Vector2(1f, 0f) : new Vector2(0.5f, 0f),
                obtained ? new Vector2(1f, 0f) : new Vector2(0.5f, 0f),
                obtained ? new Vector2(-10f, 8f) : new Vector2(0f, 8f),
                new Vector2(82f, 30f),
                null,
                obtained
                    ? new Color(0.34f, 0.18f, 0.21f, 0.98f)
                    : new Color(0.63f, 0.39f, 0.14f, 0.98f),
                obtained ? "회수" : "획득",
                13,
                inkColor);
            int capturedIndex = index;
            bool capturedObtained = obtained;
            action.onClick.AddListener(() =>
                SetTestRelicObtained(capturedIndex, !capturedObtained));
        }

        private void CreateRelicChain(
            RectTransform parent,
            Vector2 cardSize,
            float angleDegrees,
            string objectName)
        {
            const int linksPerChain = 9;
            float span = cardSize.x * 0.76f;
            float slope = Mathf.Tan(angleDegrees * Mathf.Deg2Rad);
            for (int linkIndex = 0; linkIndex < linksPerChain; linkIndex++)
            {
                float t = (linkIndex / (float)(linksPerChain - 1)) - 0.5f;
                float x = t * span;
                float y = x * slope;
                RectTransform link = CreateImageRect(
                    objectName + "_Link_" + linkIndex.ToString("00", CultureInfo.InvariantCulture),
                    parent,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(x, y),
                    new Vector2(18f, 9f),
                    null,
                    new Color(0.28f, 0.31f, 0.36f, 0.86f),
                    new Vector2(0.5f, 0.5f));
                link.localRotation = Quaternion.Euler(0f, 0f, angleDegrees);
                Outline outline = link.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.68f, 0.70f, 0.74f, 0.74f);
                outline.effectDistance = new Vector2(1f, -1f);
                outline.useGraphicAlpha = true;
            }
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
            LoadBoardCellOverrides();
            _rainbowFrameSegments.Clear();
            DestroyChildren(_nodeContent);
            _boardPositionsByNodeId.Clear();
            _boardNodeRectsById.Clear();

            List<BoardNodeEntry> entries = CollectBoardEntries(false);
            if (entries.Count == 0)
            {
                _nodeContent.sizeDelta = new Vector2(100f, 100f);
                _nodeContent.anchoredPosition = Vector2.zero;
                _nodeContent.localScale = Vector3.one;
                return;
            }

            List<BoardNodeEntry> ordered = OrderBoardEntries(entries);
            List<Vector2Int> cells = BuildBoardCells(ordered.Count);
            ordered = ArrangeBoardEntriesByRegion(ordered, cells);
            var reservedOverrideCells = new HashSet<Vector2Int>();
            for (int i = 0; i < ordered.Count; i++)
                if (_boardCellOverrides.TryGetValue(ordered[i].Node.Id, out Vector2Int overrideCell))
                    reservedOverrideCells.Add(overrideCell);
            var usedCells = new HashSet<Vector2Int>();
            var visible = new List<BoardNodeEntry>(ordered.Count);
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            var occupied = new Dictionary<Vector2Int, BoardNodeEntry>();
            for (int i = 0; i < ordered.Count; i++)
            {
                BoardNodeEntry entry = ordered[i];
                if (_boardCellOverrides.TryGetValue(entry.Node.Id, out Vector2Int overrideCell) &&
                    usedCells.Add(overrideCell))
                {
                    entry.Cell = overrideCell;
                }
                else
                {
                    Vector2Int candidate = cells[i];
                    if (reservedOverrideCells.Contains(candidate) || !usedCells.Add(candidate))
                    {
                        int radius = 1;
                        while (radius <= 40)
                        {
                            bool found = false;
                            for (int y = -radius; y <= radius && !found; y++)
                            {
                                for (int x = -radius; x <= radius; x++)
                                {
                                    if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;
                                    candidate = cells[i] + new Vector2Int(x, y);
                                    if (reservedOverrideCells.Contains(candidate) || !usedCells.Add(candidate)) continue;
                                    found = true;
                                    break;
                                }
                            }
                            if (found) break;
                            radius++;
                        }
                    }
                    entry.Cell = candidate;
                }
                entry.Position = new Vector2(
                    entry.Cell.x * BoardNodePitchX,
                    entry.Cell.y * BoardNodePitchY);
                _boardPositionsByNodeId[entry.Node.Id] = entry.Position;
                if (ShouldShowBoardNode(entry.Node))
                {
                    visible.Add(entry);
                    occupied[entry.Cell] = entry;
                }
                minX = Mathf.Min(minX, entry.Cell.x);
                maxX = Mathf.Max(maxX, entry.Cell.x);
                minY = Mathf.Min(minY, entry.Cell.y);
                maxY = Mathf.Max(maxY, entry.Cell.y);
            }

            _nodeContent.sizeDelta = new Vector2(
                Mathf.Max(Mathf.Abs(minX), Mathf.Abs(maxX)) * 2f * BoardNodePitchX + BoardNodeSize + 120f,
                Mathf.Max(Mathf.Abs(minY), Mathf.Abs(maxY)) * 2f * BoardNodePitchY + BoardNodeSize + 120f);

            // Every visible connection follows the square grid. Drawing each
            // east/north edge once creates a single open network around the
            // center core without diagonal shortcuts between nodes.
            Vector2Int[] edgeDirections =
            {
                Vector2Int.right,
                Vector2Int.up
            };
            for (int i = 0; i < visible.Count; i++)
            {
                BoardNodeEntry from = visible[i];
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

            for (int i = 0; i < visible.Count; i++)
            {
                BoardNodeEntry entry = visible[i];
                NodePurchaseState state = GetNodePurchaseState(entry.Node);
                Button button = CreateBoardNode(entry, state);
                RectTransform rect = button.transform as RectTransform;
                rect.anchoredPosition = entry.Position;
                _boardNodeRectsById[entry.Node.Id] = rect;
                TraitNodeDefinition captured = entry.Node;
                button.onClick.AddListener(() => SelectNode(captured));
                if (_boardLayoutEditing)
                {
                    TraitBoardNodeDragRelay dragRelay =
                        button.gameObject.AddComponent<TraitBoardNodeDragRelay>();
                    dragRelay.Bind(this, captured.Id, rect);
                }
                else
                {
                    TraitNodeRightClickRelay rightClick =
                        button.gameObject.AddComponent<TraitNodeRightClickRelay>();
                    rightClick.Bind(this, captured.Id);
                }
                TraitUiButtonFeedback feedback = button.GetComponent<TraitUiButtonFeedback>();
                if (feedback != null && !_boardLayoutEditing)
                {
                    feedback.SetScaleFeedback(1.14f, 0.94f, 0.14f);
                    feedback.PointerEntered += data => HandleNodePointerEnter(captured, data);
                    feedback.PointerExited += data => HandleNodePointerExit(
                        captured,
                        button.transform as RectTransform);
                }
            }

            if (!string.IsNullOrEmpty(_tooltipNodeId) &&
                !_boardNodeRectsById.ContainsKey(_tooltipNodeId))
            {
                HideNodeTooltip();
            }
            if (!string.IsNullOrEmpty(_tooltipNodeId))
            {
                _selectedNodeId = _tooltipNodeId;
            }
            if (string.IsNullOrEmpty(_selectedNodeId) ||
                !_boardNodeRectsById.ContainsKey(_selectedNodeId))
            {
                _selectedNodeId = _boardNodeRectsById.ContainsKey(ResearchCoreNodeId)
                    ? ResearchCoreNodeId
                    : visible.Count > 0 ? visible[0].Node.Id : string.Empty;
            }
            UpdateNodeSelectionFrame();
            Canvas.ForceUpdateCanvases();
            if (preserveView)
            {
                _nodeContent.localScale = Vector3.one * previousZoom;
                // Revealing newly available nodes after a purchase must not
                // pull the board away from the player's current view.
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
                TraitNodeDefinition selectedNode = FindNode(_tooltipNodeId);
                RefreshNodeTooltip(selectedNode);
                PositionNodeTooltip(selectedNode);
            }

        }

        private List<BoardNodeEntry> CollectBoardEntries(bool visibleOnly = true)
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
                        if (visibleOnly && !ShouldShowBoardNode(node))
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

        private bool ShouldShowBoardNode(TraitNodeDefinition node)
        {
            if (node == null) return false;
            if (_boardLayoutEditing) return true;
            if (IsPurchased(node) ||
                string.Equals(node.Id, ResearchCoreNodeId, StringComparison.Ordinal))
            {
                return true;
            }

            if (node.AcquisitionOnly)
            {
                return false;
            }

            if (testModeUnlockAll)
            {
                return true;
            }

            if (!testModeFreeUpgrades && HasNodeCosts(node))
            {
                if (!AreNodeCostCurrenciesAvailable(node))
                {
                    return false;
                }
            }

            return _purchased.Contains(ResearchCoreNodeId) &&
                IsPrerequisiteMet(node);
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

            for (int i = 0; i < remaining.Count; i++)
            {
                remaining[i].DifficultyTier = GetBoardDifficultyTier(remaining[i], byId);
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

        private static List<BoardNodeEntry> ArrangeBoardEntriesByRegion(
            List<BoardNodeEntry> ordered,
            List<Vector2Int> cells)
        {
            if (ordered == null || cells == null || ordered.Count != cells.Count ||
                ordered.Count <= 5)
            {
                return ordered;
            }

            // Keep the research core and the four first stat choices on the
            // cardinal spokes. The remaining nodes occupy stable category
            // sectors through each diamond ring: stats, skills, then monsters.
            var arranged = new BoardNodeEntry[ordered.Count];
            int fixedCount = Mathf.Min(5, ordered.Count);
            for (int i = 0; i < fixedCount; i++) arranged[i] = ordered[i];

            var queues = new List<BoardNodeEntry>[3]
            {
                new List<BoardNodeEntry>(),
                new List<BoardNodeEntry>(),
                new List<BoardNodeEntry>()
            };
            for (int i = fixedCount; i < ordered.Count; i++)
            {
                BoardNodeEntry entry = ordered[i];
                int region = entry.Tab == TraitTab.Stat ? 0 :
                    entry.Tab == TraitTab.Skill ? 1 : 2;
                queues[region].Add(entry);
            }

            int[] remainingByRegion =
            {
                queues[0].Count, queues[1].Count, queues[2].Count
            };
            int[] queueIndexes = { 0, 0, 0 };
            int slotsRemaining = ordered.Count - fixedCount;
            int cellIndex = fixedCount;

            while (cellIndex < cells.Count)
            {
                int ring = Mathf.Abs(cells[cellIndex].x) + Mathf.Abs(cells[cellIndex].y);
                int ringEnd = cellIndex + 1;
                while (ringEnd < cells.Count &&
                       Mathf.Abs(cells[ringEnd].x) + Mathf.Abs(cells[ringEnd].y) == ring)
                {
                    ringEnd++;
                }

                int ringSlots = ringEnd - cellIndex;
                int[] regionSlots = AllocateBoardRingSlots(
                    ringSlots, slotsRemaining, remainingByRegion);
                var ringIndexes = new List<int>(ringSlots);
                for (int i = cellIndex; i < ringEnd; i++) ringIndexes.Add(i);
                ringIndexes.Sort((left, right) => CompareBoardCellBearing(
                    cells[left], cells[right]));

                int ringCellOffset = 0;
                for (int region = 0; region < queues.Length; region++)
                {
                    for (int i = 0; i < regionSlots[region]; i++)
                    {
                        if (queueIndexes[region] >= queues[region].Count ||
                            ringCellOffset >= ringIndexes.Count)
                        {
                            break;
                        }

                        arranged[ringIndexes[ringCellOffset++]] =
                            queues[region][queueIndexes[region]++];
                    }
                    remainingByRegion[region] -= regionSlots[region];
                }

                slotsRemaining -= ringSlots;
                cellIndex = ringEnd;
            }

            var result = new List<BoardNodeEntry>(arranged.Length);
            for (int i = 0; i < arranged.Length; i++)
                if (arranged[i] != null) result.Add(arranged[i]);
            return result.Count == ordered.Count ? result : ordered;
        }

        private static int[] AllocateBoardRingSlots(
            int ringSlots,
            int slotsRemaining,
            int[] remainingByRegion)
        {
            var allocation = new int[remainingByRegion.Length];
            if (ringSlots <= 0 || slotsRemaining <= 0) return allocation;

            var remainders = new float[remainingByRegion.Length];
            int allocated = 0;
            for (int i = 0; i < remainingByRegion.Length; i++)
            {
                float exact = remainingByRegion[i] * (float)ringSlots / slotsRemaining;
                allocation[i] = Mathf.Min(remainingByRegion[i], Mathf.FloorToInt(exact));
                remainders[i] = exact - allocation[i];
                allocated += allocation[i];
            }

            while (allocated < ringSlots)
            {
                int best = -1;
                for (int i = 0; i < remainingByRegion.Length; i++)
                {
                    if (allocation[i] >= remainingByRegion[i]) continue;
                    if (best < 0 || remainders[i] > remainders[best]) best = i;
                }
                if (best < 0) break;
                allocation[best]++;
                remainders[best] = -1f;
                allocated++;
            }

            return allocation;
        }

        private static int CompareBoardCellBearing(Vector2Int left, Vector2Int right)
        {
            // Start at the north point and proceed clockwise so every ring
            // keeps the same contiguous category sectors.
            float leftBearing = Mathf.Repeat(Mathf.Atan2(left.x, left.y), Mathf.PI * 2f);
            float rightBearing = Mathf.Repeat(Mathf.Atan2(right.x, right.y), Mathf.PI * 2f);
            return leftBearing.CompareTo(rightBearing);
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
            // Only the final Stat and Skill researches are rainbow/endgame
            // anchors. Monster production levels stay in their early currency
            // tier, so Stump's Garnet branch remains close to the core.
            int finalOrder = IsRainbowFinalUpgradeNode(left.Node).CompareTo(
                IsRainbowFinalUpgradeNode(right.Node));
            if (finalOrder != 0)
            {
                return finalOrder;
            }

            int starterMonsterOrder = GetStarterMonsterOrder(left.Node).CompareTo(
                GetStarterMonsterOrder(right.Node));
            if (starterMonsterOrder != 0)
            {
                return starterMonsterOrder;
            }

            int tierOrder = left.DifficultyTier.CompareTo(right.DifficultyTier);
            if (tierOrder != 0)
            {
                return tierOrder;
            }

            int leftCurrency = Mathf.Max(0, GetGemstoneIndexById(left.Node.CostGemstoneId));
            int rightCurrency = Mathf.Max(0, GetGemstoneIndexById(right.Node.CostGemstoneId));
            long leftScore = left.Node.StartsUnlocked ? 0L :
                leftCurrency * 100000L +
                (left.Node.AcquisitionOnly ? 50000L : 0L) +
                left.Node.Cost;
            long rightScore = right.Node.StartsUnlocked ? 0L :
                rightCurrency * 100000L +
                (right.Node.AcquisitionOnly ? 50000L : 0L) +
                right.Node.Cost;
            int scoreOrder = leftScore.CompareTo(rightScore);
            if (scoreOrder != 0)
            {
                return scoreOrder;
            }

            return string.Compare(left.Node.Id, right.Node.Id, StringComparison.Ordinal);
        }

        private static int GetStarterMonsterOrder(TraitNodeDefinition node)
        {
            return node != null && node.Id.StartsWith(
                "monster.01", StringComparison.Ordinal)
                ? 0
                : 1;
        }

        private int GetBoardDifficultyTier(
            BoardNodeEntry entry,
            Dictionary<string, BoardNodeEntry> entriesById)
        {
            if (entry == null || entry.Node == null)
            {
                return 0;
            }

            int difficultyTier = 0;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            string currentId = entry.Node.Id;
            while (!string.IsNullOrEmpty(currentId) && visited.Add(currentId) &&
                entriesById.TryGetValue(currentId, out BoardNodeEntry current))
            {
                int gemstoneTier = Mathf.Max(
                    0,
                    GetGemstoneIndexById(current.Node.CostGemstoneId));
                difficultyTier = Mathf.Max(difficultyTier, gemstoneTier);
                currentId = current.Node.PrerequisiteNodeId;
            }

            return difficultyTier;
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

            // Fill complete Manhattan rings around the center. With the current
            // 113-node board this produces a radius-7 diamond whose rows are
            // 1, 3, 5 ... 15 ... 5, 3, 1 nodes wide.
            int radius = 1;
            while (1 + 2 * radius * (radius + 1) < count)
            {
                radius++;
            }

            for (int ring = 2; ring <= radius && cells.Count < count; ring++)
            {
                var ringCells = new List<Vector2Int>(ring * 4);
                for (int y = -ring; y <= ring; y++)
                {
                    int remainingX = ring - Mathf.Abs(y);
                    if (remainingX == 0)
                    {
                        ringCells.Add(new Vector2Int(0, y));
                    }
                    else
                    {
                        ringCells.Add(new Vector2Int(-remainingX, y));
                        ringCells.Add(new Vector2Int(remainingX, y));
                    }
                }

                ringCells.Sort((a, b) =>
                {
                    int rowOrder = b.y.CompareTo(a.y);
                    return rowOrder != 0 ? rowOrder : a.x.CompareTo(b.x);
                });
                for (int i = 0; i < ringCells.Count && cells.Count < count; i++)
                {
                    cells.Add(ringCells[i]);
                }
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
                (!testModeUnlockAll && !IsPrerequisiteMet(node)))
            {
                return NodePurchaseState.Locked;
            }

            bool currencyUnlocked = testModeUnlockAll || testModeFreeUpgrades ||
                AreNodeCostCurrenciesAvailable(node);
            bool hasCurrency = currencyUnlocked &&
                (testModeFreeUpgrades || CanAffordNodeCosts(node));
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

        private Color GetBoardNodeRankColor(BoardNodeEntry entry)
        {
            if (entry == null || entry.Node == null)
            {
                return GetRankFrameColor(1);
            }
            int ringTier = GetBoardRingTier(entry.Cell);
            return ringTier >= 5
                ? new Color(1f, 0.82f, 0.42f, 1f)
                : GetRankFrameColor(ringTier + 1);
        }

        private static int GetBoardRingTier(Vector2Int cell)
        {
            int distance = Mathf.Abs(cell.x) + Mathf.Abs(cell.y);
            if (distance <= 2) return 0;  // 13 central nodes
            if (distance == 3) return 1;  // 12-node first ring
            if (distance == 4) return 2;
            if (distance == 5) return 3;
            if (distance == 6) return 4;
            return 5;
        }

        private Button CreateBoardNode(BoardNodeEntry entry, NodePurchaseState state)
        {
            Color rankColor = GetBoardNodeRankColor(entry);
            bool purchased = state == NodePurchaseState.Purchased;
            bool available = state == NodePurchaseState.Available;
            bool finalUpgrade = IsFinalUpgradeNode(entry.Node);
            bool rainbowRing = GetBoardRingTier(entry.Cell) == 5;
            bool isCore = string.Equals(
                entry.Node.Id,
                "stat.attack.01",
                StringComparison.Ordinal);
            Color rankBase = purchased
                ? new Color(0.14f, 0.16f, 0.20f, 0.98f)
                : new Color(0.018f, 0.024f, 0.038f, 0.99f);
            Color fill = Color.Lerp(rankBase, rankColor, purchased ? 0.28f : 0.22f);
            fill.a = purchased ? 0.98f : 0.99f;

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
                new Color(
                    rankColor.r,
                    rankColor.g,
                    rankColor.b,
                    rainbowRing ? 0.38f : 0.22f),
                isCore ? 11f : rainbowRing || finalUpgrade ? 13f : 9f,
                rainbowRing);
            CreateSquareNodeFrame(
                buttonRect,
                rankColor,
                isCore ? 5f : rainbowRing || finalUpgrade ? 5.5f : 4.5f,
                rainbowRing);
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
                    purchased ? Color.white : new Color(0.30f, 0.34f, 0.41f, 1f),
                    new Vector2(0.5f, 0.5f));
                iconRect.GetComponent<Image>().preserveAspect = true;
            }

            if (available)
            {
                CreateAffordableNodeBadge(buttonRect, compactTreeNode: false);
            }

            string marker = string.Empty;
            if (!isCore)
            {
                if (entry.Tab == TraitTab.Monster)
                {
                    marker = "M";
                }
                else if (entry.Category != null &&
                         (entry.Category.Id == "skill.cursorAura" ||
                          entry.Category.Id == "skill.pet_baby_dragon"))
                {
                    marker = "PET";
                }
                else if (entry.Tab == TraitTab.Skill)
                {
                    marker = "SK";
                }
                else
                {
                    marker = GetNodeRankLabel(entry.Node);
                }
            }
            if (!string.IsNullOrEmpty(marker))
            {
                CreateText(
                    CreateRect(
                        "NodeMarker",
                        buttonRect,
                        new Vector2(0f, 0f),
                        new Vector2(0f, 0f),
                        new Vector2(7f, 5f),
                        new Vector2(31f, 15f),
                        new Vector2(0f, 0f)),
                    marker,
                    isCore ? 8 : 8,
                    TextAnchor.LowerLeft,
                    rankColor,
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

            Sprite stateIcon = purchased ? checkIcon : null;
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
            int unlockGemstoneIndex = entry.Tab == TraitTab.Monster
                ? GetMonsterUnlockGemstoneIndex(entry.Node.Id)
                : -1;
            Sprite unlockGemstoneIcon = unlockGemstoneIndex >= 0
                ? GetGemstoneIcon(unlockGemstoneIndex)
                : null;
            if (unlockGemstoneIcon != null)
            {
                RectTransform gemBadge = CreateRect(
                    "NewGemstoneBadge",
                    buttonRect,
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(9f, 9f),
                    new Vector2(22f, 22f),
                    new Vector2(0.5f, 0.5f));
                Image gemBadgeBack = gemBadge.gameObject.AddComponent<Image>();
                gemBadgeBack.color = new Color(0.025f, 0.035f, 0.065f, 0.98f);
                gemBadgeBack.raycastTarget = false;
                CreateSquareNodeFrame(
                    gemBadge,
                    new Color(1f, 0.82f, 0.44f, 0.95f),
                    1.5f);

                RectTransform gemIcon = CreateImageRect(
                    "GemstoneIcon",
                    gemBadge,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(16f, 16f),
                    unlockGemstoneIcon,
                    Color.white,
                    new Vector2(0.5f, 0.5f));
                gemIcon.GetComponent<Image>().preserveAspect = true;
                gemIcon.GetComponent<Image>().raycastTarget = false;
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
            _rainbowFrameSegments.Clear();
            DestroyChildren(_nodeContent);

            IReadOnlyList<TraitCategoryDefinition> categories = GetCategories(TraitTab.Stat);
            if (categories == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            SetLocalizedText(_nodeHeading, LocalizationCatalog.Get(
                "ui.graph.stats",
                "스탯 연구 트리"));
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

                    NodePurchaseState purchaseState = GetNodePurchaseState(node);
                    bool purchased = purchaseState == NodePurchaseState.Purchased;
                    bool locked = purchaseState == NodePurchaseState.Locked;
                    bool available = purchaseState == NodePurchaseState.Available;
                    Button button = CreateNodeButton(
                        _nodeContent,
                        category,
                        node,
                        categoryIndex,
                        nodeIndex,
                        purchased,
                        locked,
                        available);
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
            _rainbowFrameSegments.Clear();
            DestroyChildren(_nodeContent);
            var categories = GetCategories(_activeTab);
            if (categories == null) return;
            SetLocalizedText(
                _nodeHeading,
                GetTabTitle(_activeTab) + " · " +
                LocalizationCatalog.Get("ui.graph.drag.move", "드래그로 이동"));
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
                    NodePurchaseState purchaseState = GetNodePurchaseState(node);
                    bool purchased = purchaseState == NodePurchaseState.Purchased;
                    bool locked = purchaseState == NodePurchaseState.Locked;
                    bool available = purchaseState == NodePurchaseState.Available;
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
                        purchased, locked, available);
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

            bool locked = !testModeUnlockAll && !IsPrerequisiteMet(node);
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
                ? Mathf.Clamp(requestedZoom, MinimumGraphZoom, MaximumGraphZoom)
                : Mathf.Clamp(fitZoom, MinimumGraphZoom, 1.05f);
            _nodeContent.localScale = Vector3.one * zoom;
            _nodeContent.anchoredPosition = ClampGraphPosition(Vector2.zero);
            PositionNodeTooltip(FindNode(_tooltipNodeId));
        }

        internal Vector2 ClampGraphPosition(Vector2 requestedPosition)
        {
            if (_nodeContent == null || _nodeViewport == null || _showRelicCollection || _showTalentCollection || _showFieldCollection)
            {
                return requestedPosition;
            }

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            foreach (RectTransform nodeRect in _boardNodeRectsById.Values)
            {
                if (nodeRect == null || !nodeRect.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector2 halfSize = nodeRect.rect.size * 0.5f;
                Vector2 center = nodeRect.anchoredPosition;
                minX = Mathf.Min(minX, center.x - halfSize.x);
                maxX = Mathf.Max(maxX, center.x + halfSize.x);
                minY = Mathf.Min(minY, center.y - halfSize.y);
                maxY = Mathf.Max(maxY, center.y + halfSize.y);
            }

            if (float.IsInfinity(minX) || float.IsInfinity(minY))
            {
                return Vector2.zero;
            }

            // Keep the corner gemstone/affordability badges inside the mask too.
            const float edgePadding = 24f;
            minX -= edgePadding;
            maxX += edgePadding;
            minY -= edgePadding;
            maxY += edgePadding;

            float zoom = Mathf.Max(0.01f, Mathf.Abs(_nodeContent.localScale.x));
            float halfViewportWidth = _nodeViewport.rect.width * 0.5f;
            float halfViewportHeight = _nodeViewport.rect.height * 0.5f;
            float edgeMinX = halfViewportWidth - maxX * zoom;
            float edgeMaxX = -halfViewportWidth - minX * zoom;
            float edgeMinY = halfViewportHeight - maxY * zoom;
            float edgeMaxY = -halfViewportHeight - minY * zoom;
            // At high zoom, let the outermost node travel all the way to the
            // viewport center. The old edge-only clamp stopped the diamond's
            // corner nodes at the mask edge, making them hard to inspect.
            float centerPan = Mathf.InverseLerp(0.65f, 1.1f, zoom);
            float centerMinX = -maxX * zoom;
            float centerMaxX = -minX * zoom;
            float centerMinY = -maxY * zoom;
            float centerMaxY = -minY * zoom;
            float minPositionX = Mathf.Lerp(edgeMinX, centerMinX, centerPan);
            float maxPositionX = Mathf.Lerp(edgeMaxX, centerMaxX, centerPan);
            float minPositionY = Mathf.Lerp(edgeMinY, centerMinY, centerPan);
            float maxPositionY = Mathf.Lerp(edgeMaxY, centerMaxY, centerPan);

            // If the visible nodes fit on an axis, keep their group centered
            // there instead of letting a drag expose empty board space.
            float x = minPositionX > maxPositionX
                ? -(minX + maxX) * 0.5f * zoom
                : Mathf.Clamp(requestedPosition.x, minPositionX, maxPositionX);
            float y = minPositionY > maxPositionY
                ? -(minY + maxY) * 0.5f * zoom
                : Mathf.Clamp(requestedPosition.y, minPositionY, maxPositionY);
            return new Vector2(x, y);
        }

        private IEnumerator FitGraphAfterActivation()
        {
            // The canvas can still report its previous or zero size in Awake.
            // Fit after layout has been resolved so a saved zoom from another
            // visit cannot leave most of the unified board outside the view.
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (_showRelicCollection)
            {
                _nodeContent.localScale = Vector3.one;
            }
            else
            {
                CenterUnifiedGraph();
            }
            _fitGraphRoutine = null;
        }

        private void SetGraphZoom(float zoom)
        {
            if (_showRelicCollection || _showTalentCollection || _showFieldCollection || _nodeContent == null || _nodeViewport == null)
            {
                return;
            }

            Vector2 viewportCenter = Vector2.zero;
            float oldZoom = Mathf.Max(0.01f, _nodeContent.localScale.x);
            Vector2 contentPointAtCenter = (viewportCenter - _nodeContent.anchoredPosition) / oldZoom;
            float safeZoom = Mathf.Clamp(zoom, MinimumGraphZoom, MaximumGraphZoom);
            _nodeContent.localScale = Vector3.one * safeZoom;
            _nodeContent.anchoredPosition = ClampGraphPosition(
                viewportCenter - contentPointAtCenter * safeZoom);
            PositionNodeTooltip(FindNode(_tooltipNodeId));
        }

        internal void ZoomGraphAt(Vector2 screenPosition, float wheelDelta)
        {
            if (_showRelicCollection || _showTalentCollection || _showFieldCollection || _nodeContent == null || _nodeViewport == null ||
                Mathf.Approximately(wheelDelta, 0f))
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
                previousZoom * Mathf.Pow(1.025f, wheelDelta),
                MinimumGraphZoom,
                MaximumGraphZoom);
            _nodeContent.localScale = Vector3.one * nextZoom;
            _nodeContent.anchoredPosition = ClampGraphPosition(
                viewportPoint - contentPoint * nextZoom);
            PositionNodeTooltip(FindNode(_tooltipNodeId));
        }

        internal void SetGraphDragCursor(bool dragging)
        {
            // Keep the platform cursor unchanged during board panning. The
            // ScrollRect still tracks dragging; the operating system retains
            // its normal pointer image and size.
            _graphDragCursorActive = dragging;
        }

        private void RestoreDragCursor()
        {
            _graphDragCursorActive = false;
        }

        private string GetCategoryTitle(TraitCategoryDefinition category)
        {
            if (category == null)
            {
                return string.Empty;
            }

            switch (category.Id)
            {
                case "stat.attack": return LocalizationCatalog.Get("ui.attack", "공격력");
                case "stat.radius": return LocalizationCatalog.Get("ui.radius", "공격 범위");
                case "stat.cooldown": return LocalizationCatalog.Get("ui.cooldown", "공격 간격");
                case "stat.critical": return LocalizationCatalog.Get("ui.critical", "치명타");
                case "stat.boss": return LocalizationCatalog.Get("ui.boss", "보스 피해 배수");
                case "stat.fieldDuration": return LocalizationCatalog.Get("ui.category.fieldDuration", "일반 필드 시간");
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
                GetNodeFrameColor(node, _activeTab),
                3.5f,
                IsRainbowFinalUpgradeNode(node));

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
                    GetLocalizedNodeTitle(node),
                    11,
                    TextAnchor.MiddleCenter,
                    locked ? new Color(0.63f, 0.72f, 0.84f, 1f) : inkColor,
                    true);
                CreateText(
                    CreateRect("Value", buttonRect, Vector2.zero, Vector2.one, new Vector2(0f, -29f), new Vector2(-12f, -77f), new Vector2(0.5f, 0.5f)),
                    GetLocalizedNodeValueLabel(node),
                    11,
                    TextAnchor.MiddleCenter,
                    locked ? new Color(0.50f, 0.62f, 0.76f, 1f) : category.Accent,
                    true);
            }

            if (available)
            {
                CreateAffordableNodeBadge(buttonRect, compactTreeNode);
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

        private void CreateAffordableNodeBadge(
            RectTransform nodeRect,
            bool compactTreeNode)
        {
            if (nodeRect == null)
            {
                return;
            }

            RectTransform badge = CreateImageRect(
                "AffordableBadge",
                nodeRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(-5f, 5f),
                new Vector2(compactTreeNode ? 22f : 25f, compactTreeNode ? 22f : 25f),
                null,
                new Color(0.06f, 0.58f, 0.31f, 1f),
                new Vector2(0f, 1f));
            Image badgeImage = badge.GetComponent<Image>();
            badgeImage.raycastTarget = false;
            Outline outline = badge.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.015f, 0.06f, 0.035f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);
            Shadow shadow = badge.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1f, -2f);
            TextMeshProUGUI mark = CreateText(
                CreateRect("Mark", badge, Vector2.zero, Vector2.one,
                    Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)),
                "+",
                compactTreeNode ? 15 : 18,
                TextAnchor.MiddleCenter,
                Color.white,
                true);
            mark.raycastTarget = false;
        }

        private void CreateSquareNodeFrame(
            RectTransform parent,
            Color color,
            float thickness,
            bool rainbow = false)
        {
            if (parent == null)
            {
                return;
            }

            // Keep authored borders on whole logical pixels. Half-pixel bars
            // softened when the CanvasScaler or Game view used fractional scaling.
            float safeThickness = Mathf.Max(1f, Mathf.Round(thickness));
            if (rainbow)
            {
                CreateRainbowNodeFrame(parent, safeThickness, color.a);
                return;
            }

            Color highlight = Color.Lerp(color, Color.white, 0.38f);
            highlight.a = color.a;
            Color shadowColor = Color.Lerp(color, new Color(0.025f, 0.02f, 0.035f, color.a), 0.48f);
            shadowColor.a = color.a;
            CreateImageRect(
                "FrameTop",
                parent,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -safeThickness * 0.5f),
                new Vector2(0f, safeThickness),
                null,
                highlight,
                new Vector2(0.5f, 0.5f));
            CreateImageRect(
                "FrameBottom",
                parent,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, safeThickness * 0.5f),
                new Vector2(0f, safeThickness),
                null,
                shadowColor,
                new Vector2(0.5f, 0.5f));
            CreateImageRect(
                "FrameLeft",
                parent,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(safeThickness * 0.5f, 0f),
                new Vector2(safeThickness, 0f),
                null,
                highlight,
                new Vector2(0.5f, 0.5f));
            CreateImageRect(
                "FrameRight",
                parent,
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(-safeThickness * 0.5f, 0f),
                new Vector2(safeThickness, 0f),
                null,
                shadowColor,
                new Vector2(0.5f, 0.5f));
        }

        private void CreateRainbowNodeFrame(
            RectTransform parent,
            float thickness,
            float alpha)
        {
            const int segmentsPerSide = 8;
            float segmentCount = segmentsPerSide * 4f;
            float segmentAlpha = Mathf.Clamp01(alpha);

            // A continuous hue path around the frame makes the final tier read
            // as a prismatic reward instead of four unrelated colored sides.
            for (int index = 0; index < segmentsPerSide; index++)
            {
                float start = index / (float)segmentsPerSide;
                float end = (index + 1f) / segmentsPerSide;
                float topHue = (index + 0.5f) / segmentCount;
                CreateRainbowFrameSegment(
                    "FrameRainbowTop", parent,
                    new Vector2(start, 1f), new Vector2(end, 1f),
                    new Vector2(0f, -thickness * 0.5f), new Vector2(0f, thickness),
                    topHue, segmentAlpha);

                float rightBottom = 1f - end;
                float rightTop = 1f - start;
                float rightHue = (segmentsPerSide + index + 0.5f) / segmentCount;
                CreateRainbowFrameSegment(
                    "FrameRainbowRight", parent,
                    new Vector2(1f, rightBottom), new Vector2(1f, rightTop),
                    new Vector2(-thickness * 0.5f, 0f), new Vector2(thickness, 0f),
                    rightHue, segmentAlpha);

                float bottomHue = (segmentsPerSide * 2f + index + 0.5f) / segmentCount;
                CreateRainbowFrameSegment(
                    "FrameRainbowBottom", parent,
                    new Vector2(1f - end, 0f), new Vector2(1f - start, 0f),
                    new Vector2(0f, thickness * 0.5f), new Vector2(0f, thickness),
                    bottomHue, segmentAlpha);

                float leftHue = (segmentsPerSide * 3f + index + 0.5f) / segmentCount;
                CreateRainbowFrameSegment(
                    "FrameRainbowLeft", parent,
                    new Vector2(0f, start), new Vector2(0f, end),
                    new Vector2(thickness * 0.5f, 0f), new Vector2(thickness, 0f),
                    leftHue, segmentAlpha);
            }
        }

        private void CreateRainbowFrameSegment(
            string objectName,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            float hue,
            float alpha)
        {
            RectTransform segmentRect = CreateImageRect(
                objectName,
                parent,
                anchorMin,
                anchorMax,
                anchoredPosition,
                size,
                null,
                Color.white,
                new Vector2(0.5f, 0.5f));
            Image image = segmentRect.GetComponent<Image>();
            _rainbowFrameSegments.Add(new RainbowFrameSegment
            {
                Image = image,
                Hue = hue,
                Alpha = alpha
            });
            UpdateRainbowFrameSegment(_rainbowFrameSegments[_rainbowFrameSegments.Count - 1]);
        }

        private void UpdateRainbowNodeFrames()
        {
            if (_rainbowFrameSegments.Count == 0)
            {
                return;
            }

            _rainbowFramePhase = Mathf.Repeat(
                _rainbowFramePhase + Time.unscaledDeltaTime * 0.075f,
                1f);
            for (int index = 0; index < _rainbowFrameSegments.Count; index++)
            {
                UpdateRainbowFrameSegment(_rainbowFrameSegments[index]);
            }
        }

        private void UpdateRainbowFrameSegment(RainbowFrameSegment segment)
        {
            if (segment == null || segment.Image == null)
            {
                return;
            }

            Color color = Color.HSVToRGB(
                Mathf.Repeat(segment.Hue + _rainbowFramePhase, 1f),
                0.92f,
                1f);
            color.a = segment.Alpha;
            segment.Image.color = color;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private bool IsUpgradeResearchNode(TraitNodeDefinition node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id)) return false;
            string id = node.Id;
            if (id.StartsWith("stat.", StringComparison.Ordinal))
                return GetTrailingRank(id) > 1;
            return id.IndexOf(".damage.", StringComparison.Ordinal) >= 0 ||
                id.IndexOf(".radius.", StringComparison.Ordinal) >= 0 ||
                id.IndexOf(".simultaneousHits.", StringComparison.Ordinal) >= 0 ||
                id.IndexOf(".cooldown.", StringComparison.Ordinal) >= 0 ||
                id.IndexOf(".production.", StringComparison.Ordinal) >= 0;
        }

        private int GetUpgradeSeriesMaxRank(TraitNodeDefinition node)
        {
            if (!IsUpgradeResearchNode(node)) return 0;
            int separator = node.Id.LastIndexOf('.');
            if (separator <= 0) return 0;
            string seriesPrefix = node.Id.Substring(0, separator + 1);
            int maxRank = 0;
            foreach (TraitNodeDefinition candidate in EnumerateAllNodes())
            {
                if (candidate == null || string.IsNullOrEmpty(candidate.Id) ||
                    !candidate.Id.StartsWith(seriesPrefix, StringComparison.Ordinal))
                    continue;
                maxRank = Mathf.Max(maxRank, GetTrailingRank(candidate.Id));
            }
            return maxRank;
        }

        private bool IsFinalUpgradeNode(TraitNodeDefinition node)
        {
            if (!IsUpgradeResearchNode(node)) return false;
            int rank = GetTrailingRank(node.Id);
            return rank > 0 && rank == GetUpgradeSeriesMaxRank(node);
        }

        private bool IsRainbowFinalUpgradeNode(TraitNodeDefinition node)
        {
            if (node == null || node.Id.StartsWith("monster.", StringComparison.Ordinal)) return false;
            if (node.Id.StartsWith("skill.", StringComparison.Ordinal))
            {
                UpgradeNodeData authored = FindUpgradeNode(node.Id, out UpgradeCategoryData category);
                return authored != null && category.nodes.Length > 1 &&
                    category.nodes[category.nodes.Length - 1] == authored;
            }
            return IsFinalUpgradeNode(node);
        }

        private static int GetMonsterProgressionRank(string monsterId)
        {
            const string monsterPrefix = "monster.";
            if (string.IsNullOrEmpty(monsterId) ||
                !monsterId.StartsWith(monsterPrefix, StringComparison.Ordinal))
            {
                return 1;
            }

            int end = monsterId.IndexOf('.', monsterPrefix.Length);
            string number = end < 0
                ? monsterId.Substring(monsterPrefix.Length)
                : monsterId.Substring(monsterPrefix.Length, end - monsterPrefix.Length);
            if (!int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int monsterIndex))
            {
                return 1;
            }

            // Keep monster rarity readable as the roster advances: Stump is
            // white, early monsters are yellow, then blue, purple and red.
            // Monster nodes never use the animated rainbow treatment.
            if (monsterIndex <= 1) return 1;
            if (monsterIndex <= 4) return 2;
            if (monsterIndex <= 6) return 3;
            if (monsterIndex <= 8) return 4;
            return 5;
        }

        private Color GetUpgradeRankFrameColor(TraitNodeDefinition node)
        {
            int rank = Mathf.Max(1, GetTrailingRank(node.Id));
            int maxRank = Mathf.Max(rank, GetUpgradeSeriesMaxRank(node));
            if (rank <= 5) return GetRankFrameColor(rank);
            if (rank >= maxRank - 1)
                return new Color(1f, 0.30f, 0.36f, 1f);

            switch ((rank - 6) % 6)
            {
                case 0: return new Color(0.22f, 0.88f, 0.84f, 1f);
                case 1: return new Color(0.38f, 0.86f, 0.34f, 1f);
                case 2: return new Color(0.22f, 0.67f, 0.84f, 1f);
                case 3: return new Color(1f, 0.61f, 0.22f, 1f);
                case 4: return new Color(0.96f, 0.42f, 0.72f, 1f);
                default: return new Color(0.65f, 0.48f, 1f, 1f);
            }
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

        private Color GetNodeFrameColor(TraitNodeDefinition node, TraitTab tab)
        {
            if (node == null) return GetCategoryAccent(tab);
            if (tab == TraitTab.Monster)
            {
                return GetRankFrameColor(GetMonsterProgressionRank(node.Id));
            }
            return IsUpgradeResearchNode(node)
                ? GetUpgradeRankFrameColor(node)
                : GetRankFrameColor(Mathf.Clamp(
                    GetGemstoneIndexById(node.CostGemstoneId) + 1, 1, 5));
        }

        private void SelectNode(TraitNodeDefinition node)
        {
            if (node == null || _boardLayoutEditing)
            {
                return;
            }

            CancelTooltipDismiss();
            _hoveredNodeId = node.Id;
            _selectedNodeId = node.Id;
            UpdateNodeSelectionFrame();
            ShowNodeTooltip(node);
        }

        internal void HandleNodeRightClick(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            TraitNodeDefinition node = FindNode(nodeId);
            if (node == null)
            {
                return;
            }

            _selectedNodeId = node.Id;
            HideNodeTooltip();
            UpdateNodeSelectionFrame();
            TryUpgradeNode(node);
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

            CancelTooltipDismiss();
            _hoveredNodeId = node.Id;
            _selectedNodeId = node.Id;
            UpdateNodeSelectionFrame();
            ShowNodeTooltip(node);
        }

        private void HandleNodePointerExit(
            TraitNodeDefinition node,
            RectTransform sourceRect)
        {
            if (node == null || !string.Equals(
                    _hoveredNodeId,
                    node.Id,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (sourceRect != null && !sourceRect.gameObject.activeInHierarchy &&
                _boardNodeRectsById.TryGetValue(node.Id, out RectTransform currentRect) &&
                currentRect != null && currentRect != sourceRect &&
                currentRect.gameObject.activeInHierarchy)
            {
                return;
            }

            _hoveredNodeId = null;
            ScheduleTooltipDismiss();
        }

        internal void HandleTooltipPointerEnter()
        {
            _pointerOverTooltip = true;
            CancelTooltipDismiss();
        }

        internal bool ShouldTooltipBlockRaycast(
            Vector2 screenPoint,
            Camera eventCamera)
        {
            if (_tooltipRoot == null || !_tooltipRoot.gameObject.activeInHierarchy)
            {
                return false;
            }

            // Keep the action controls usable even when a node sits behind
            // them. The rest of the panel yields pointer hits to those nodes.
            if (IsScreenPointInside(_tooltipUpgradeButton, screenPoint, eventCamera) ||
                IsScreenPointInside(_tooltipResetButton, screenPoint, eventCamera))
            {
                return true;
            }

            if (IsScreenPointInside(_tooltipCloseButton, screenPoint, eventCamera))
            {
                return true;
            }

            return !IsPointerOverBoardNode(screenPoint, eventCamera);
        }

        internal bool IsPointerOverBoardNode(Vector2 screenPoint, Camera eventCamera)
        {
            foreach (RectTransform nodeRect in _boardNodeRectsById.Values)
            {
                if (nodeRect != null && nodeRect.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(
                        nodeRect,
                        screenPoint,
                        eventCamera))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsScreenPointInside(
            Button button,
            Vector2 screenPoint,
            Camera eventCamera)
        {
            RectTransform rect = button == null
                ? null
                : button.transform as RectTransform;
            return rect != null && button.gameObject.activeInHierarchy &&
                   RectTransformUtility.RectangleContainsScreenPoint(
                       rect,
                       screenPoint,
                       eventCamera);
        }

        internal void HandleTooltipPointerExit()
        {
            _pointerOverTooltip = false;
            ScheduleTooltipDismiss();
        }

        internal void HandleTooltipOutsideClick(GameObject target)
        {
            if (target == null || _tooltipRoot == null)
            {
                return;
            }

            Transform clicked = target.transform;
            if (clicked == _tooltipRoot || clicked.IsChildOf(_tooltipRoot))
            {
                return;
            }

            Transform ancestor = clicked;
            while (ancestor != null && ancestor != _nodeContent)
            {
                if (ancestor.name.StartsWith("Node_", StringComparison.Ordinal) ||
                    ancestor.name.StartsWith("TalentDraftCard_", StringComparison.Ordinal) ||
                    ancestor.name.StartsWith("TalentOwned_", StringComparison.Ordinal))
                {
                    return;
                }
                ancestor = ancestor.parent;
            }

            HideNodeTooltip();
        }

        private void ScheduleTooltipDismiss()
        {
            CancelTooltipDismiss();
            if (_tooltipRoot == null || !_tooltipRoot.gameObject.activeSelf)
            {
                return;
            }

            _tooltipDismissRoutine = StartCoroutine(DismissTooltipAfterPointerLeaves());
        }

        private IEnumerator DismissTooltipAfterPointerLeaves()
        {
            yield return new WaitForSecondsRealtime(0.30f);
            _tooltipDismissRoutine = null;
            if (string.IsNullOrEmpty(_hoveredNodeId) && !_pointerOverTooltip)
            {
                HideNodeTooltip();
            }
        }

        private void CancelTooltipDismiss()
        {
            if (_tooltipDismissRoutine != null)
            {
                StopCoroutine(_tooltipDismissRoutine);
                _tooltipDismissRoutine = null;
            }
        }

        private void ShowNodeTooltip(TraitNodeDefinition node)
        {
            if (node == null || _tooltipRoot == null)
            {
                return;
            }

            CancelTooltipDismiss();
            bool animate = !_tooltipRoot.gameObject.activeSelf || _tooltipNodeId != node.Id;
            _tooltipNodeId = node.Id;
            _pointerOverTooltip = false;
            _tooltipRoot.gameObject.SetActive(true);
            RefreshNodeTooltip(node);
            PositionNodeTooltip(node);
            if (animate)
            {
                if (_tooltipAnimation != null)
                {
                    StopCoroutine(_tooltipAnimation);
                }
                _tooltipAnimation = StartCoroutine(AnimateTooltipIn());
            }
        }

        internal void ShowTalentTooltip(string talentId, RectTransform anchor)
        {
            TalentOption option = FindTalentOption(talentId);
            if (option == null || anchor == null || _tooltipRoot == null)
            {
                return;
            }

            CancelTooltipDismiss();
            bool animate = !_tooltipRoot.gameObject.activeSelf ||
                !string.IsNullOrEmpty(_tooltipNodeId);
            _tooltipNodeId = null;
            _hoveredNodeId = null;
            _pointerOverTooltip = false;
            _tooltipRoot.sizeDelta = new Vector2(392f, 272f);
            _tooltipRoot.gameObject.SetActive(true);
            SetLocalizedText(_tooltipType, "특성");
            _tooltipType.color = new Color(0.80f, 0.65f, 1f, 1f);
            SetLocalizedText(_tooltipTitle, option.title);
            _tooltipTitle.color = new Color(1f, 0.83f, 0.46f, 1f);
            SetLocalizedText(_tooltipDescription, option.description);
            _tooltipDescription.color = new Color(0.91f, 0.95f, 1f, 1f);
            if (_tooltipCost != null) _tooltipCost.gameObject.SetActive(false);
            if (_tooltipCurrencyIcon != null) _tooltipCurrencyIcon.gameObject.SetActive(false);
            for (int i = 0; i < _tooltipCostIcons.Length; i++)
            {
                if (_tooltipCostIcons[i] != null) _tooltipCostIcons[i].gameObject.SetActive(false);
                if (_tooltipCostAmounts[i] != null) _tooltipCostAmounts[i].gameObject.SetActive(false);
            }
            if (_tooltipUpgradeButton != null) _tooltipUpgradeButton.gameObject.SetActive(false);
            if (_tooltipResetButton != null) _tooltipResetButton.gameObject.SetActive(false);
            Transform separator = _tooltipRoot.Find("TooltipSeparator");
            if (separator != null) separator.gameObject.SetActive(false);
            Transform costBand = _tooltipRoot.Find("TooltipCostBand");
            if (costBand != null) costBand.gameObject.SetActive(false);

            PositionTooltipNearRect(anchor);
            if (animate)
            {
                if (_tooltipAnimation != null) StopCoroutine(_tooltipAnimation);
                _tooltipAnimation = StartCoroutine(AnimateTooltipIn());
            }
        }

        private void PositionNodeTooltip(TraitNodeDefinition node)
        {
            if (node == null || _tooltipRoot == null || _runtimeRoot == null ||
                !_boardNodeRectsById.TryGetValue(node.Id, out RectTransform nodeRect) ||
                nodeRect == null)
            {
                return;
            }

            PositionTooltipNearRect(nodeRect);
        }

        private void PositionTooltipNearRect(RectTransform nodeRect)
        {
            if (nodeRect == null || _tooltipRoot == null || _runtimeRoot == null) return;

            Vector3[] corners = new Vector3[4];
            nodeRect.GetWorldCorners(corners);
            Camera camera = GetUiCamera();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _runtimeRoot,
                    RectTransformUtility.WorldToScreenPoint(camera, corners[2]),
                    camera,
                    out Vector2 topRight) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _runtimeRoot,
                    RectTransformUtility.WorldToScreenPoint(camera, corners[1]),
                    camera,
                    out Vector2 topLeft))
            {
                return;
            }

            const float gap = 16f;
            const float margin = 18f;
            Rect bounds = _runtimeRoot.rect;
            Vector2 panelSize = _tooltipRoot.sizeDelta;
            float left = topRight.x + gap;
            if (left + panelSize.x > bounds.xMax - margin &&
                topLeft.x - panelSize.x - gap >= bounds.xMin + margin)
            {
                left = topLeft.x - panelSize.x - gap;
            }

            float top = topRight.y;
            if (top - panelSize.y < bounds.yMin + margin &&
                topRight.y - gap - panelSize.y >= bounds.yMin + margin)
            {
                top = topRight.y - gap;
            }

            float minLeft = bounds.xMin + margin;
            float maxLeft = bounds.xMax - panelSize.x - margin;
            float minTop = bounds.yMin + panelSize.y + margin;
            float maxTop = bounds.yMax - margin;
            if (maxLeft < minLeft)
            {
                left = (bounds.xMin + bounds.xMax - panelSize.x) * 0.5f;
            }
            else
            {
                left = Mathf.Clamp(left, minLeft, maxLeft);
            }

            if (maxTop < minTop)
            {
                top = (bounds.yMin + bounds.yMax + panelSize.y) * 0.5f;
            }
            else
            {
                top = Mathf.Clamp(top, minTop, maxTop);
            }

            _tooltipRoot.anchoredPosition = new Vector2(left, top);
        }

        internal void RefreshNodeTooltipPosition()
        {
            if (_tooltipRoot != null && _tooltipRoot.gameObject.activeInHierarchy)
            {
                PositionNodeTooltip(FindNode(_tooltipNodeId));
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

        private void RefreshNodeTooltip(TraitNodeDefinition node)
        {
            if (_tooltipType == null)
            {
                return;
            }
            if (node == null)
            {
                HideNodeTooltip();
                return;
            }

            _tooltipRoot.sizeDelta = new Vector2(392f, 408f);
            Transform separator = _tooltipRoot.Find("TooltipSeparator");
            if (separator != null) separator.gameObject.SetActive(true);
            Transform costBand = _tooltipRoot.Find("TooltipCostBand");
            if (costBand != null) costBand.gameObject.SetActive(true);

            NodePurchaseState state = GetNodePurchaseState(node);
            TraitTab tab = GetNodeTab(node);
            bool purchased = state == NodePurchaseState.Purchased;
            bool concealed = false;
            bool finalUpgrade = IsFinalUpgradeNode(node);
            string actionKind = GetTooltipActionKind(node);
            if (concealed)
            {
                SetLocalizedText(_tooltipType, "잠긴 항목");
            }
            else if (finalUpgrade)
            {
                SetLocalizedFormat(
                    _tooltipType,
                    "ui.tooltip.type.final",
                    "최종 단계 · {0}",
                    actionKind);
            }
            else
            {
                SetLocalizedText(_tooltipType, actionKind);
            }
            _tooltipType.color = concealed
                ? new Color(0.88f, 0.89f, 0.94f, 1f)
                : finalUpgrade
                    ? new Color(1f, 0.78f, 0.28f, 1f)
                    : GetCategoryAccent(tab);
            SetLocalizedText(_tooltipTitle, concealed ? "???" : GetLocalizedNodeTitle(node));
            _tooltipTitle.color = concealed
                ? new Color(0.88f, 0.89f, 0.94f, 1f)
                : GetNodeFrameColor(node, tab);
            SetLocalizedText(_tooltipDescription, concealed
                ? "잠긴 연구입니다.\n해금 조건을 충족하면 상세 정보가 공개됩니다."
                : BuildNodeDescription(node));

            List<TraitGemstoneCost> requirements = GetNodeCostRequirements(node);
            bool showCostGrid = !concealed && !node.AcquisitionOnly && requirements.Count > 1 && !testModeFreeUpgrades;
            _tooltipCost.gameObject.SetActive(!showCostGrid);
            for (int i = 0; i < _tooltipCostIcons.Length; i++)
            {
                bool show = showCostGrid && i < requirements.Count;
                _tooltipCostIcons[i].gameObject.SetActive(show);
                _tooltipCostAmounts[i].gameObject.SetActive(show);
                if (!show) continue;
                TraitGemstoneCost requirement = requirements[i];
                int gemIndex = GetGemstoneIndexById(requirement.GemstoneId);
                long owned = GetGemstoneBalance(gemIndex);
                _tooltipCostIcons[i].sprite = GetGemstoneIcon(gemIndex);
                _tooltipCostAmounts[i].SetText(
                    requirement.Amount.ToString("N0", CultureInfo.InvariantCulture) + " / " +
                    owned.ToString("N0", CultureInfo.InvariantCulture));
                _tooltipCostAmounts[i].color = owned >= requirement.Amount ? inkColor : new Color(1f, 0.56f, 0.50f);
            }
            Sprite currencySprite = null;
            if (!concealed && !node.AcquisitionOnly && requirements.Count == 1)
                currencySprite = GetGemstoneIcon(
                    GetGemstoneIndexById(requirements[0].GemstoneId));
            if (_tooltipCurrencyIcon != null)
            {
                _tooltipCurrencyIcon.sprite = currencySprite;
                _tooltipCurrencyIcon.gameObject.SetActive(currencySprite != null);
            }
            if (_tooltipCost != null)
            {
                RectTransform costRect = _tooltipCost.rectTransform;
                Vector2 costOffsetMin = costRect.offsetMin;
                costOffsetMin.x = currencySprite == null ? 16f : 72f;
                costRect.offsetMin = costOffsetMin;
            }

            if (concealed)
            {
                SetLocalizedText(_tooltipCost, "비용 · 해금 후 공개");
            }
            else
            {
                SetLocalizedText(_tooltipCost, GetNodeCostText(node));
            }

            bool canBuy = state == NodePurchaseState.Available && !node.AcquisitionOnly;
            bool showAction = canBuy || state == NodePurchaseState.Unaffordable;
            bool canReset = purchased && !node.StartsUnlocked && !HasPurchasedDependents(node);
            _tooltipUpgradeButton.gameObject.SetActive(showAction);
            _tooltipUpgradeButton.interactable = canBuy;
            if (showAction)
            {
                SetButtonLabel(_tooltipUpgradeLabel, GetTooltipActionLabel(node, state));
                _tooltipUpgradeLabel.color = canBuy
                    ? new Color(0.06f, 0.07f, 0.09f, 1f)
                    : new Color(1f, 0.94f, 0.93f, 1f);
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

            if (state == NodePurchaseState.Locked)
            {
                return "선행 조건 필요";
            }

            if (IsRootEntityNode(node.Id, "skill."))
            {
                return "배우기";
            }

            if (IsRootEntityNode(node.Id, "monster."))
            {
                return "해금하기";
            }

            if (node.Id.StartsWith("stat.", StringComparison.Ordinal))
            {
                return node.Cost <= 0 ? "배우기" : "강화하기";
            }

            if (state == NodePurchaseState.Unaffordable)
            {
                return node.Cost <= 0 ? "배우기" : "강화하기";
            }

            return string.IsNullOrEmpty(node.PrerequisiteNodeId) || node.Cost <= 0
                ? "해금하기"
                : "강화하기";
        }

        private static bool IsRootEntityNode(string nodeId, string prefix)
        {
            return !string.IsNullOrEmpty(nodeId) &&
                nodeId.StartsWith(prefix, StringComparison.Ordinal) &&
                nodeId.IndexOf('.', prefix.Length) < 0;
        }

        private static string GetNodeStateLabel(
            TraitNodeDefinition node,
            NodePurchaseState state)
        {
            if (node != null && string.Equals(node.Id, ResearchCoreNodeId, StringComparison.Ordinal) &&
                state == NodePurchaseState.Purchased)
            {
                return "시작 완료";
            }

            if (state == NodePurchaseState.Purchased)
            {
                if (node != null && IsRootEntityNode(node.Id, "skill.")) return "배움 완료";
                if (node != null && IsRootEntityNode(node.Id, "monster.")) return "해금 완료";
                if (node != null && node.StartsUnlocked) return "기본 적용";
                return "강화 완료";
            }

            if (state == NodePurchaseState.Available)
            {
                if (node != null && IsRootEntityNode(node.Id, "skill.")) return "배우기 가능";
                if (node != null && IsRootEntityNode(node.Id, "monster.")) return "해금 가능";
                return "강화 가능";
            }

            return state == NodePurchaseState.Unaffordable ? "재화 부족" : "잠김";
        }

        private static string GetTooltipActionKind(TraitNodeDefinition node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id)) return "연구";
            if (string.Equals(node.Id, ResearchCoreNodeId, StringComparison.Ordinal))
                return "연구 시작";
            if (node.AcquisitionOnly)
                return "전투 획득";
            if (IsRootEntityNode(node.Id, "monster."))
                return "몬스터 해금";
            if (IsRootEntityNode(node.Id, "skill."))
                return "스킬 배우기";
            if (node.Id.StartsWith("monster.", StringComparison.Ordinal))
                return "몬스터 업그레이드";
            if (node.Id.StartsWith("skill.", StringComparison.Ordinal))
                return "스킬 업그레이드";
            return "스탯 업그레이드";
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
            CancelTooltipDismiss();
            if (_tooltipAnimation != null)
            {
                StopCoroutine(_tooltipAnimation);
                _tooltipAnimation = null;
            }
            _tooltipNodeId = null;
            _hoveredNodeId = null;
            _pointerOverTooltip = false;
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

            List<TraitGemstoneCost> requirements = GetNodeCostRequirements(node);
            if (requirements.Count == 0)
            {
                return "비용 없음";
            }

            if (testModeFreeUpgrades)
            {
                return "테스트 무료 · 원래 비용 " + FormatNodeCostRequirements(node, false);
            }

            return "필요 " + FormatNodeCostRequirements(node, true);
        }

        private bool HasNodeCosts(TraitNodeDefinition node) =>
            node != null && GetNodeCostRequirements(node).Count > 0;

        private List<TraitGemstoneCost> GetNodeCostRequirements(TraitNodeDefinition node)
        {
            var requirements = new List<TraitGemstoneCost>();
            if (node == null) return requirements;
            bool applyTierCostMultiplier = ShouldDoubleResearchCost(node);
            if (node.CostRequirements != null && node.CostRequirements.Count > 0)
            {
                for (int i = 0; i < node.CostRequirements.Count; i++)
                {
                    TraitGemstoneCost requirement = node.CostRequirements[i];
                    if (requirement != null && requirement.Amount > 0 &&
                        !string.IsNullOrWhiteSpace(requirement.GemstoneId))
                        requirements.Add(new TraitGemstoneCost(
                            requirement.GemstoneId,
                            applyTierCostMultiplier ? DoubleSaturating(requirement.Amount) : requirement.Amount));
                }
                return requirements;
            }

            if (node.Cost > 0)
                requirements.Add(new TraitGemstoneCost(
                    node.CostGemstoneId,
                    applyTierCostMultiplier ? DoubleSaturating(node.Cost) : node.Cost));
            return requirements;
        }

        private bool ShouldDoubleResearchCost(TraitNodeDefinition node)
        {
            if (node == null) return false;
            if (node.Id.StartsWith("monster.", StringComparison.Ordinal))
            {
                string suffix = node.Id.Substring("monster.".Length);
                int separator = suffix.IndexOf('.');
                if (separator >= 0) suffix = suffix.Substring(0, separator);
                return int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int monsterIndex) && monsterIndex >= 4;
            }

            int rarityRank = IsUpgradeResearchNode(node)
                ? Mathf.Max(1, GetTrailingRank(node.Id))
                : Mathf.Clamp(GetGemstoneIndexById(node.CostGemstoneId) + 1, 1, 5);
            return rarityRank >= 4;
        }

        private static int DoubleSaturating(int value)
        {
            return value > int.MaxValue / 2 ? int.MaxValue : value * 2;
        }

        private bool AreNodeCostCurrenciesAvailable(TraitNodeDefinition node)
        {
            List<TraitGemstoneCost> requirements = GetNodeCostRequirements(node);
            for (int i = 0; i < requirements.Count; i++)
            {
                TraitGemstoneCost requirement = requirements[i];
                TraitGemstoneDefinition gemstone = GetGemstoneDefinitionById(requirement.GemstoneId);
                if (!IsGemstoneAvailable(gemstone) &&
                    GetGemstoneBalance(GetGemstoneIndexById(requirement.GemstoneId)) <= 0L)
                    return false;
            }
            return true;
        }

        private bool CanAffordNodeCosts(TraitNodeDefinition node)
        {
            List<TraitGemstoneCost> requirements = GetNodeCostRequirements(node);
            for (int i = 0; i < requirements.Count; i++)
            {
                TraitGemstoneCost requirement = requirements[i];
                if (GetGemstoneBalance(GetGemstoneIndexById(requirement.GemstoneId)) < requirement.Amount)
                    return false;
            }
            return true;
        }

        private string FormatNodeCostRequirements(TraitNodeDefinition node, bool includeBalance)
        {
            List<TraitGemstoneCost> requirements = GetNodeCostRequirements(node);
            var parts = new List<string>(requirements.Count);
            for (int i = 0; i < requirements.Count; i++)
            {
                TraitGemstoneCost requirement = requirements[i];
                TraitGemstoneDefinition gemstone = GetGemstoneDefinitionById(requirement.GemstoneId);
                string title = gemstone == null
                    ? GetGemstoneLocalizedName(requirement.GemstoneId, requirement.GemstoneId)
                    : GetGemstoneLocalizedName(gemstone.Id, gemstone.Title);
                string part = requirement.Amount.ToString("N0", CultureInfo.InvariantCulture) + " " + title;
                if (includeBalance)
                    part += " (보유 " + GetGemstoneBalance(
                        GetGemstoneIndexById(requirement.GemstoneId)).ToString("N0", CultureInfo.InvariantCulture) + ")";
                parts.Add(part);
            }
            return string.Join(" · ", parts);
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
            if (testModeFreeUpgrades || node == null)
            {
                return;
            }

            List<TraitGemstoneCost> requirements = GetNodeCostRequirements(node);
            if (requirements.Count == 0) return;
            if (!CanAffordNodeCosts(node))
            {
                return;
            }

            EnsureGemstoneBalanceCapacity();
            var receipts = new List<TraitProgressionStore.SpentCostEntry>(requirements.Count);
            for (int i = 0; i < requirements.Count; i++)
            {
                TraitGemstoneCost requirement = requirements[i];
                int index = GetGemstoneIndexById(requirement.GemstoneId);
                if (index < 0 || index >= _gemstoneBalances.Length) continue;
                _gemstoneBalances[index] = Math.Max(0L,
                    _gemstoneBalances[index] - requirement.Amount);
                receipts.Add(new TraitProgressionStore.SpentCostEntry
                {
                    nodeId = node.Id,
                    currencyId = requirement.GemstoneId,
                    amount = requirement.Amount
                });
            }
            if (receipts.Count > 0) _spentNodeCosts[node.Id] = receipts;
            _garnetBalance = _gemstoneBalances.Length > 0 ? _gemstoneBalances[0] : 0L;
        }

        private void RefundSpentNodeCosts(string nodeId, TraitNodeDefinition node)
        {
            if (string.IsNullOrWhiteSpace(nodeId)) return;
            if (_spentNodeCosts.TryGetValue(nodeId, out var receipts) && receipts != null)
            {
                for (int i = 0; i < receipts.Count; i++)
                {
                    TraitProgressionStore.SpentCostEntry receipt = receipts[i];
                    if (receipt != null)
                        RefundNodeCost(node, receipt.amount, receipt.currencyId);
                }
                return;
            }

            if (_spentByNode.TryGetValue(nodeId, out int amount))
            {
                string currencyId = _spentCurrencyByNode.TryGetValue(nodeId, out string savedCurrency)
                    ? savedCurrency
                    : node == null ? string.Empty : node.CostGemstoneId;
                RefundNodeCost(node, amount, currencyId);
            }
        }

        private void RefundNodeCost(
            TraitNodeDefinition node,
            int amount,
            string currencyId)
        {
            if (testModeFreeUpgrades || amount <= 0 ||
                (node == null && string.IsNullOrEmpty(currencyId)))
            {
                return;
            }

            string effectiveCurrencyId = currencyId;
            if (string.IsNullOrEmpty(effectiveCurrencyId))
            {
                effectiveCurrencyId = node == null ? string.Empty : node.CostGemstoneId;
            }

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
                SetLocalizedText(_selectedTitle, "선택한 항목 없음");
                SetLocalizedText(_selectedDescription, "표시할 항목이 없습니다.");
                SetLocalizedText(_selectedCost, string.Empty);
                SetLocalizedText(_selectedRequirement, string.Empty);
                _upgradeButton.interactable = false;
                _upgradeButton.gameObject.SetActive(false);
                if (_resetNodeButton != null)
                {
                    _resetNodeButton.interactable = false;
                    _resetNodeButton.gameObject.SetActive(false);
                }
                return;
            }

            TraitTab selectedTab = GetNodeTab(node);
            Color categoryAccent = GetCategoryAccent(selectedTab);
            if (_selectedHeaderAccent != null)
            {
                _selectedHeaderAccent.color = categoryAccent;
            }
            if (_selectedHeaderBackground != null)
            {
                Color categoryTint = selectedTab == TraitTab.Stat
                    ? new Color(0.045f, 0.13f, 0.20f, 0.97f)
                    : selectedTab == TraitTab.Skill
                        ? new Color(0.19f, 0.105f, 0.055f, 0.97f)
                        : new Color(0.13f, 0.085f, 0.20f, 0.97f);
                _selectedHeaderBackground.color = categoryTint;
            }
            bool concealed = false;
            SetLocalizedText(_selectedTitle, concealed ? "???" : GetLocalizedNodeTitle(node));
            _selectedTitle.color = concealed
                ? new Color(0.88f, 0.89f, 0.94f, 1f)
                : GetNodeFrameColor(node, selectedTab);
            SetLocalizedText(_selectedDescription, concealed
                ? "잠긴 연구입니다. 해금 후 효과를 확인할 수 있습니다."
                : BuildNodeDescription(node));
            SetLocalizedText(
                _selectedCost,
                concealed ? "해금 조건을 먼저 충족하세요" : GetNodeCostText(node));
            SetLocalizedText(_selectedRequirement, GetNodeRequirementText(node));

            bool purchased = IsPurchased(node);
            bool lockedByPrerequisite = !testModeUnlockAll &&
                !IsPrerequisiteMet(node);
            bool currencyUnlocked = testModeUnlockAll || testModeFreeUpgrades ||
                AreNodeCostCurrenciesAvailable(node);
            bool hasCurrency = testModeFreeUpgrades ||
                (currencyUnlocked && CanAffordNodeCosts(node));
            bool canBuy = !purchased && !lockedByPrerequisite &&
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
                SetLocalizedText(_statusMessage, canResetNode
                    ? "적용됨 · 이 노드를 초기화할 수 있습니다"
                    : HasPurchasedDependents(node)
                        ? "적용됨 · 다음 강화가 연결되어 먼저 초기화해야 합니다"
                        : "효과 적용 중");
            }
            else if (lockedByPrerequisite)
            {
                SetLocalizedText(_statusMessage, "선행 특성을 먼저 해금하세요");
            }
            else if (node.AcquisitionOnly)
            {
                SetLocalizedText(_statusMessage, "전투 중 확률로 획득하는 항목입니다");
            }
            else if (!hasCurrency)
            {
                SetLocalizedText(_statusMessage, "젬스톤이 부족합니다");
            }
            else
            {
                SetLocalizedText(_statusMessage, "강화할 수 있습니다");
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

        private bool TryUpgradeNode(TraitNodeDefinition node, bool bulkPurchase = false)
        {
            if (node == null || GetNodePurchaseState(node) != NodePurchaseState.Available ||
                node.AcquisitionOnly)
            {
                if (!bulkPurchase) ShowNodeTooltip(node);
                return false;
            }

            HashSet<string> lockedBeforePurchase = null;
            if (!bulkPurchase)
            {
                lockedBeforePurchase = new HashSet<string>(StringComparer.Ordinal);
                foreach (TraitNodeDefinition candidate in EnumerateAllNodes())
                {
                    if (candidate != null && GetNodePurchaseState(candidate) == NodePurchaseState.Locked)
                    {
                        lockedBeforePurchase.Add(candidate.Id);
                    }
                }
            }

            SpendNodeCost(node);
            _purchased.Add(node.Id);
            List<TraitGemstoneCost> paidRequirements = GetNodeCostRequirements(node);
            int totalCost = 0;
            for (int i = 0; i < paidRequirements.Count; i++)
                totalCost = (int)Math.Min(int.MaxValue, (long)totalCost + paidRequirements[i].Amount);
            _spentByNode[node.Id] = testModeFreeUpgrades ? 0 : totalCost;
            _spentCurrencyByNode[node.Id] = paidRequirements.Count > 0
                ? paidRequirements[0].GemstoneId
                : node.CostGemstoneId;
            if (testModeFreeUpgrades) _spentNodeCosts.Remove(node.Id);
            if (bulkPurchase)
            {
                _selectedNodeId = node.Id;
                PlayNodePurchaseEffect(node.Id, 0.32f);
                return true;
            }

            TraitNodeDefinition tooltipNode = node;
            _selectedNodeId = node.Id;
            BuildNodes(false);
            RefreshSummary();
            RefreshGemstonePanel();
            UpdateHeaderForActiveView();
            RefreshNodeTooltip(tooltipNode);
            if (!bulkPurchase) SaveProgressionState();
            PlayNodePurchaseEffect(node.Id, bulkPurchase ? 0.16f : 0.48f);
            if (!bulkPurchase) TraitAudioFeedback.PlayUpgrade();

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
                SetTestStatus(GetNodeCompletionMessage(node));
            }
            return true;
        }

        private string GetNodeCompletionMessage(TraitNodeDefinition node)
        {
            if (node == null)
            {
                return "연구를 완료했습니다.";
            }
            if (IsRootEntityNode(node.Id, "skill."))
            {
                return "배움 완료: " + GetLocalizedNodeTitle(node);
            }
            if (IsRootEntityNode(node.Id, "monster."))
            {
                return "몬스터 해금 완료: " + GetLocalizedNodeTitle(node);
            }
            return GetLocalizedNodeTitle(node) + " 강화 완료";
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
                SetTestStatus("연결된 다음 연구를 먼저 초기화해야 합니다.");
                return;
            }

            RefundSpentNodeCosts(node.Id, node);

            _spentByNode.Remove(node.Id);
            _spentCurrencyByNode.Remove(node.Id);
            _spentNodeCosts.Remove(node.Id);
            _purchased.Remove(node.Id);
            _selectedNodeId = node.Id;
            BuildNodes(false);
            RefreshSummary();
            RefreshGemstonePanel();
            RefreshNodeTooltip(node);
            SaveProgressionState();
        }

        private void PlayNodePurchaseEffect(string nodeId, float durationSeconds = 0.48f)
        {
            PlayNodeEffect(nodeId, PurchasedColor, durationSeconds);
        }

        private void PlayNodeAvailableEffect(string nodeId)
        {
            PlayNodeEffect(nodeId, AvailableColor, 0.48f);
        }

        private void PlayNodeEffect(string nodeId, Color effectColor, float durationSeconds)
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
            burst.Play(effectColor, durationSeconds);
        }

        private void ResetPreview()
        {
            testModeUnlockAll = false;
            testModeFreeUpgrades = false;
            unlockedBossTier = 0;
            _highestBossStageCleared = 0;
            _settledBossRunIds.Clear();
            var spentNodeIds = new HashSet<string>(_spentByNode.Keys, StringComparer.Ordinal);
            foreach (string nodeId in _spentNodeCosts.Keys) spentNodeIds.Add(nodeId);
            foreach (string nodeId in spentNodeIds)
            {
                RefundSpentNodeCosts(nodeId, FindNode(nodeId));
            }

            InitializePurchasedState();
            if (_statusMessage != null)
            {
                SetLocalizedText(_statusMessage, "시작점만 남기고 연구를 초기화했습니다");
            }
            BuildCategories();
            BuildNodes(true);
            CenterUnifiedGraph();
            RefreshSummary();
            RefreshGemstonePanel();
            UpdateHeaderForActiveView();
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
            UpdateHeaderForActiveView();
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
            RefreshCombatStatsPanel();
            if (_document == null) return;
            var info = CreateGameInformation();
            if (_summaryValues.Count >= 5)
            {
                _summaryValues[0].text = info.stats.attackPower.ToString();
                _summaryValues[1].text = info.stats.attackRadiusWorldUnits.ToString("0.##");
                _summaryValues[2].text = info.stats.attackCooldownSeconds.ToString("0.##") + "s";
                _summaryValues[3].text = info.stats.criticalChancePercent.ToString("0.#") + "%";
                _summaryValues[4].text = info.stats.bossDamageMultiplier.ToString("0.##") + "×";
            }
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
            SetLocalizedText(label, value);
        }

        private static void SetLocalizedFormat(
            TextMeshProUGUI label,
            string key,
            string fallbackFormat,
            params object[] arguments)
        {
            if (label == null) return;
            LocalizedTmpLabel localized = label.GetComponent<LocalizedTmpLabel>();
            if (localized == null) localized = label.gameObject.AddComponent<LocalizedTmpLabel>();
            localized.BindFormat(key, fallbackFormat, arguments);
        }

        private static void SetLocalizedText(TextMeshProUGUI label, string value)
        {
            if (label == null) return;
            label.text = value ?? string.Empty;
            LocalizationCatalog.BindKnown(label, label.text);
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
                return LocalizationCatalog.Format(
                    "ui.requirement.prerequisite",
                    "먼저 {0} 해금 필요",
                    title);
            }

            if (node.AcquisitionOnly)
            {
                return LocalizationCatalog.Get(
                    "ui.requirement.acquisition.available",
                    "전투 중 확률로 획득");
            }

            return string.Empty;
        }

        private string BuildNodeDescription(TraitNodeDefinition node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id)) return string.Empty;
            UpgradeNodeData authored = FindUpgradeNode(node.Id, out _);
            string description = BuildDefaultNodeDescription(node);
            if (authored != null && _balanceEditedDescriptions.TryGetValue(node.Id, out string edited))
                description = edited;
            if (authored != null && authored.bonusCastCount > 0)
                description += "\n동시 발동 +" + authored.bonusCastCount;
            return description;
        }

        private string BuildDefaultNodeDescription(TraitNodeDefinition node)
        {
            string id = node.Id;

            if (string.Equals(id, ResearchCoreNodeId, StringComparison.Ordinal))
            {
                return LocalizationCatalog.Get(
                    "ui.node.description.research.core",
                    "가운데 시작점을 배우면 네 방향의 첫 연구가 나타납니다. 다음 연구는 선행 연구와 재화로 선택할 수 있습니다.");
            }

            if (id.StartsWith("stat.", StringComparison.Ordinal))
            {
                if (id.StartsWith("stat.fieldDuration.", StringComparison.Ordinal) &&
                    id.EndsWith(".01", StringComparison.Ordinal))
                {
                    return LocalizationCatalog.Get("ui.node.description.stat.field.duration", "일반 전투 제한 시간입니다.");
                }

                if (id.StartsWith("stat.attack.", StringComparison.Ordinal))
                    return LocalizationCatalog.Get("ui.node.description.stat.attack", "기본 공격 피해가 증가합니다.");
                if (id.StartsWith("stat.radius.", StringComparison.Ordinal))
                    return LocalizationCatalog.Get("ui.node.description.stat.radius", "기본 공격이 닿는 범위가 넓어집니다.");
                if (id.StartsWith("stat.cooldown.", StringComparison.Ordinal))
                    return LocalizationCatalog.Get("ui.node.description.stat.cooldown", "같은 대상을 다시 공격하는 간격이 짧아집니다.");
                if (id.StartsWith("stat.critical.", StringComparison.Ordinal))
                    return LocalizationCatalog.Get("ui.node.description.stat.critical", "치명타가 발동할 확률이 높아집니다.");
                if (id.StartsWith("stat.boss.", StringComparison.Ordinal))
                    return LocalizationCatalog.Get("ui.node.description.stat.boss", "보스에게 주는 피해가 증가합니다.");
                if (id.StartsWith("stat.fieldDuration.", StringComparison.Ordinal))
                    return LocalizationCatalog.Get("ui.node.description.stat.field.duration", "일반 필드 전투 시간이 늘어납니다.");
            }

            if (id.StartsWith("skill.", StringComparison.Ordinal))
            {
                string skillId = GetEntityRootId(id, "skill.");
                if (string.Equals(id, skillId, StringComparison.Ordinal))
                {
                    switch (skillId)
                    {
                        case "skill.tornado": return LocalizationCatalog.Get("ui.node.description.skill.tornado", "토네이도가 범위 안의 몬스터를 여러 번 공격합니다.");
                        case "skill.headshot": return LocalizationCatalog.Get("ui.node.description.skill.headshot", "강한 탄환을 발사해 대상과 주변 몬스터를 공격합니다.");
                        case "skill.thunder": return LocalizationCatalog.Get("ui.node.description.skill.thunder", "무작위 몬스터에게 번개를 떨어뜨립니다.");
                        case "skill.flame_exhaust": return LocalizationCatalog.Get("ui.node.description.skill.flame", "불꽃 투사체를 발사해 몬스터를 공격합니다.");
                        case "skill.magic_ball": return LocalizationCatalog.Get("ui.node.description.skill.magic.ball", "마력 구체가 필드를 이동하며 닿은 몬스터를 공격합니다.");
                        case "skill.laser": return LocalizationCatalog.Get("ui.node.description.skill.laser", "레이저 빔에 닿은 몬스터를 공격합니다.");
                        case "skill.pet_baby_dragon": return LocalizationCatalog.Get("ui.node.description.skill.pet", "아기용 동료가 플레이어를 따라다니며 가까운 몬스터를 자동 공격합니다.");
                        default: return LocalizationCatalog.Get("ui.node.description.skill.generic", "배우면 다음 전투부터 자동으로 사용합니다.");
                    }
                }
                if (id.IndexOf(".damage.", StringComparison.Ordinal) >= 0 ||
                    id.EndsWith(".damage", StringComparison.Ordinal))
                {
                    return string.Equals(skillId, "skill.pet_baby_dragon",
                            StringComparison.Ordinal)
                        ? LocalizationCatalog.Get("ui.node.description.skill.pet.damage", "동료의 공격 피해를 높입니다. 플레이어 공격력과 별도로 적용됩니다.")
                        : LocalizationCatalog.Get("ui.node.description.skill.damage", "이 스킬의 피해를 높입니다. 플레이어 공격력과 별도로 적용됩니다.");
                }
                if (id.IndexOf(".radius.", StringComparison.Ordinal) >= 0 ||
                    id.EndsWith(".radius", StringComparison.Ordinal))
                    return LocalizationCatalog.Get("ui.node.description.skill.radius", "이 스킬의 공격 범위를 넓힙니다. 기본 공격 범위와 별도로 적용됩니다.");
                if (id.IndexOf(".simultaneousHits.", StringComparison.Ordinal) >= 0)
                    return LocalizationCatalog.Get("ui.node.description.skill.simultaneous", "동료가 한 번의 공격으로 동시에 타격하는 몬스터 수를 늘립니다.");
                if (id.EndsWith(".cooldown", StringComparison.Ordinal))
                    return LocalizationCatalog.Get("ui.node.description.skill.cooldown", "이 스킬을 다시 사용하는 데 필요한 대기 시간을 줄입니다.");
            }

            if (id.StartsWith("monster.", StringComparison.Ordinal))
            {
                string monsterId = GetEntityRootId(id, "monster.");
                bool isProduction = id.IndexOf(".production.", StringComparison.Ordinal) >= 0;
                if (string.Equals(id, monsterId, StringComparison.Ordinal) || isProduction)
                {
                    string behavior = GetMonsterTraitDescription(monsterId);
                    string reward = BuildMonsterRewardSummary(monsterId);
                    string details = behavior;
                    if (isProduction)
                    {
                        int productionIncrease = GetMonsterProductionCount(node);
                        if (productionIncrease > 0)
                        {
                            details += "\n\n" + LocalizationCatalog.Format(
                                "ui.value.spawn.count",
                                "생산량: 회당 {0}마리",
                                productionIncrease.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                    if (!string.IsNullOrEmpty(reward)) details += "\n\n" + reward;
                    return details;
                }
            }

            return LocalizationCatalog.Get("ui.node.description.generic", "연구 효과를 적용합니다.");
        }

        private string BuildMonsterRewardSummary(string monsterId)
        {
            MonsterInformation definition = FindMonsterInformation(monsterId);
            if (definition == null)
                return string.Empty;

            string reward = LocalizationCatalog.Format(
                "ui.monster.reward.garnet",
                "처치 보상: 가넷 {0}개",
                definition.garnetReward.ToString("N0", CultureInfo.InvariantCulture));
            if (definition.gemstoneAmount > 0 &&
                definition.gemstoneChancePercent > 0f)
            {
                reward += LocalizationCatalog.Format(
                    "ui.monster.reward.bonus",
                    " · {0} {1}개",
                    GetGemstoneLocalizedName(
                        definition.gemstoneId,
                        definition.gemstoneId),
                    definition.gemstoneAmount.ToString("N0", CultureInfo.InvariantCulture));
                if (definition.gemstoneChancePercent < 100f)
                {
                    reward += LocalizationCatalog.Format(
                        "ui.monster.reward.chance",
                        " ({0}% 확률)",
                        definition.gemstoneChancePercent.ToString(
                            "0.#", CultureInfo.InvariantCulture));
                }
            }

            return reward;
        }

        private string GetLocalizedNodeTitle(TraitNodeDefinition node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id)) return string.Empty;
            string id = node.Id;
            if (string.Equals(id, ResearchCoreNodeId, StringComparison.Ordinal))
            {
                return LocalizationCatalog.Get("ui.node.title.research.core", "연구 시작점");
            }
            if (id.StartsWith("stat.", StringComparison.Ordinal))
            {
                string group = GetStatNodeGroupName(id);
                int rank = GetTrailingRank(id);
                return rank <= 1
                    ? LocalizationCatalog.Format(
                        "ui.node.title.stat.base",
                        "{0} 기본값",
                        group)
                    : LocalizationCatalog.Format(
                        "ui.node.title.stat.upgrade",
                        "{0} 강화 {1}",
                        group,
                        rank);
            }

            if (id.StartsWith("skill.", StringComparison.Ordinal))
            {
                string skillId = GetEntityRootId(id, "skill.");
                string skillName = GetLocalizedEntityName(skillId, node.Title);
                if (string.Equals(id, skillId, StringComparison.Ordinal)) return skillName;
                int rank = GetTrailingRank(id);
                if (id.IndexOf(".damage.", StringComparison.Ordinal) >= 0 ||
                    id.EndsWith(".damage", StringComparison.Ordinal))
                    return LocalizationCatalog.Format(
                        "ui.node.title.skill.damage",
                        "{0} 피해 강화 {1}",
                        skillName,
                        rank);
                if (id.IndexOf(".radius.", StringComparison.Ordinal) >= 0 ||
                    id.EndsWith(".radius", StringComparison.Ordinal))
                    return LocalizationCatalog.Format(
                        "ui.node.title.skill.radius",
                        "{0} 범위 강화 {1}",
                        skillName,
                        rank);
                if (id.IndexOf(".simultaneousHits.", StringComparison.Ordinal) >= 0)
                    return LocalizationCatalog.Format(
                        "ui.node.title.skill.simultaneous",
                        "{0} 동시 타격 강화 {1}",
                        skillName,
                        rank);
                if (id.EndsWith(".cooldown", StringComparison.Ordinal))
                {
                    return LocalizationCatalog.Format(
                        "ui.node.title.skill.cooldown",
                        "{0} · 재사용 시간",
                        skillName);
                }
            }

            if (id.StartsWith("monster.", StringComparison.Ordinal))
            {
                string monsterId = GetEntityRootId(id, "monster.");
                string monsterName = GetLocalizedEntityName(monsterId, node.Title);
                if (string.Equals(id, monsterId, StringComparison.Ordinal)) return monsterName;
                return LocalizationCatalog.Format(
                    "ui.node.title.monster.production",
                    "{0} 생산량 증가",
                    monsterName);
            }

            return node.Title ?? node.Id;
        }

        private static string GetStatNodeGroupName(string nodeId)
        {
            if (nodeId.StartsWith("stat.attack.", StringComparison.Ordinal))
                return LocalizationCatalog.Get("ui.stat.attack", "공격력");
            if (nodeId.StartsWith("stat.radius.", StringComparison.Ordinal))
                return LocalizationCatalog.Get("ui.stat.radius", "공격 범위");
            if (nodeId.StartsWith("stat.cooldown.", StringComparison.Ordinal))
                return LocalizationCatalog.Get("ui.stat.cooldown", "공격 간격");
            if (nodeId.StartsWith("stat.critical.", StringComparison.Ordinal))
                return LocalizationCatalog.Get("ui.stat.critical.chance", "치명타 확률");
            if (nodeId.StartsWith("stat.boss.", StringComparison.Ordinal))
                return LocalizationCatalog.Get("ui.stat.boss.multiplier", "보스 피해 배수");
            if (nodeId.StartsWith("stat.fieldDuration.", StringComparison.Ordinal))
                return LocalizationCatalog.Get("ui.stat.field.duration", "일반 필드 시간");
            return LocalizationCatalog.Get("ui.stat.generic", "능력치");
        }

        private string GetLocalizedNodeValueLabel(TraitNodeDefinition node)
        {
            if (node == null) return string.Empty;
            if (string.Equals(node.Id, ResearchCoreNodeId, StringComparison.Ordinal))
            {
                return LocalizationCatalog.Get("ui.node.value.research.first", "동서남북 첫 연구 연결");
            }
            string value = node.ValueLabel ?? string.Empty;
            if (string.Equals(node.Id, "skill.pet_baby_dragon", StringComparison.Ordinal))
            {
                return LocalizationCatalog.Get("ui.node.value.companion.learn", "동료 배우기");
            }
            if (value == "Component baseline" || value == "JSON baseline")
                return LocalizationCatalog.Get("ui.node.value.baseline", "기본값");
            if (value == "Unlock skill")
                return LocalizationCatalog.Get("ui.node.value.skill.unlock", "스킬 해금");
            if (value == "Enable monster")
                return LocalizationCatalog.Get("ui.node.value.monster.unlock", "몬스터 해금");
            if (node.Id != null && node.Id.StartsWith("skill.", StringComparison.Ordinal))
            {
                UpgradeNodeData data = FindUpgradeNodeData(node.Id);
                if (data != null)
                {
                    if (string.Equals(data.statId, "damage", StringComparison.Ordinal))
                        return LocalizationCatalog.Format("ui.node.value.skill.damage", "피해 +{0}", FormatCompactNumber(data.value));
                    if (string.Equals(data.statId, "radius", StringComparison.Ordinal))
                        return LocalizationCatalog.Format("ui.node.value.skill.radius", "범위 +{0}%", FormatCompactNumber(data.value * 100d));
                    if (string.Equals(data.statId, "simultaneousHits", StringComparison.Ordinal))
                        return LocalizationCatalog.Format("ui.node.value.skill.simultaneous", "동시 타격 +{0}마리", FormatCompactNumber(data.value));
                }
            }
            if (node.Id != null && node.Id.IndexOf(".production.", StringComparison.Ordinal) >= 0)
            {
                int count = GetMonsterProductionCount(node);
                return count > 0
                    ? LocalizationCatalog.Format("ui.value.spawn.count", "회당 {0}마리", count)
                    : LocalizationCatalog.Get("ui.node.value.monster.production", "생산량 증가");
            }
            return FormatCompactValueLabel(value);
        }

        private int GetMonsterProductionCount(TraitNodeDefinition node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id)) return 0;
            UpgradeNodeData data = FindUpgradeNodeData(node.Id);
            if (data != null && data.value >= 1d && data.value <= int.MaxValue &&
                Math.Abs(data.value - Math.Round(data.value)) < 0.0001d)
            {
                return (int)Math.Round(data.value);
            }

            string label = node.ValueLabel ?? string.Empty;
            for (int index = 0; index < label.Length; index++)
            {
                if (!char.IsDigit(label[index])) continue;
                int end = index;
                while (end < label.Length && char.IsDigit(label[end])) end++;
                return int.TryParse(label.Substring(index, end - index), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int count) ? count : 0;
            }

            return 0;
        }

        private UpgradeNodeData FindUpgradeNodeData(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || _document == null ||
                _document.progression == null || _document.progression.categories == null)
            {
                return null;
            }

            foreach (UpgradeCategoryData category in _document.progression.categories)
            {
                if (category == null || category.nodes == null) continue;
                foreach (UpgradeNodeData node in category.nodes)
                {
                    if (node != null && string.Equals(node.id, nodeId, StringComparison.Ordinal))
                    {
                        return node;
                    }
                }
            }

            return null;
        }

        private static string FormatCompactValueLabel(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (string.Equals(value, "Learn Baby Dragon", StringComparison.Ordinal))
                return LocalizationCatalog.Get("ui.node.value.companion.learn", "동료 배우기");
            if (value.StartsWith("Spawn ", StringComparison.OrdinalIgnoreCase))
            {
                const string spawnPrefix = "Spawn ";
                int countStart = spawnPrefix.Length;
                int countEnd = countStart;
                while (countEnd < value.Length && char.IsDigit(value[countEnd])) countEnd++;
                if (countEnd > countStart && int.TryParse(
                        value.Substring(countStart, countEnd - countStart),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int count))
                {
                    return LocalizationCatalog.Format("ui.value.spawn.count", "한 번에 {0}마리", count);
                }
            }
            int numberStart = value[0] == '+' || value[0] == '-' ? 1 : 0;
            int numberEnd = numberStart;
            while (numberEnd < value.Length &&
                   (char.IsDigit(value[numberEnd]) || value[numberEnd] == '.'))
            {
                numberEnd++;
            }

            if (numberEnd == numberStart ||
                !double.TryParse(
                    value.Substring(numberStart, numberEnd - numberStart),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double number))
            {
                return value;
            }

            string prefix = numberStart == 1 ? value.Substring(0, 1) : string.Empty;
            string suffix = value.Substring(numberEnd);
            if (string.Equals(suffix, "u", StringComparison.OrdinalIgnoreCase))
            {
                suffix = LocalizationCatalog.Get("ui.unit.range.suffix", " 공격 범위");
            }
            else if (string.Equals(suffix, "s", StringComparison.OrdinalIgnoreCase))
            {
                suffix = LocalizationCatalog.Get("ui.unit.seconds.suffix", "초");
            }
            else if (string.Equals(suffix, "x", StringComparison.OrdinalIgnoreCase))
            {
                suffix = LocalizationCatalog.Get("ui.unit.multiplier.suffix", "배");
            }
            return prefix + FormatCompactNumber(number) + suffix;
        }

        private static string FormatCompactNumber(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
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

        private string GetLocalizedEntityName(string id, string fallback)
        {
            if (id != null && id.StartsWith("monster.", StringComparison.Ordinal))
            {
                MonsterDefinition definition = FindMonsterDefinition(id);
                if (definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName))
                {
                    string monsterKey = id switch
                    {
                        "monster.01" => "ui.entity.monster.stump",
                        "monster.02" => "ui.entity.monster.queen.bee",
                        "monster.03" => "ui.entity.monster.poison.mushroom",
                        "monster.04" => "ui.entity.monster.mask.ghost",
                        "monster.05" => "ui.entity.monster.armored.soldier",
                        "monster.06" => "ui.entity.monster.snow.fairy",
                        "monster.07" => "ui.entity.monster.hive",
                        "monster.08" => "ui.entity.monster.golem",
                        "monster.09" => "ui.entity.monster.bud",
                        "monster.10" => "ui.entity.monster.pandora",
                        _ => string.Empty
                    };
                    return LocalizationCatalog.Get(monsterKey, definition.DisplayName);
                }
            }

            switch (id)
            {
                case "skill.tornado": return LocalizationCatalog.Get("ui.entity.skill.tornado", "토네이도");
                case "skill.headshot": return LocalizationCatalog.Get("ui.entity.skill.headshot", "헤드샷");
                case "skill.thunder": return LocalizationCatalog.Get("ui.entity.skill.thunder", "벼락");
                case "skill.flame_exhaust": return LocalizationCatalog.Get("ui.entity.skill.flame", "화염 배기");
                case "skill.magic_ball": return LocalizationCatalog.Get("ui.entity.skill.magic.ball", "마력 구체");
                case "skill.laser": return LocalizationCatalog.Get("ui.entity.skill.laser", "레이저");
                case "skill.cursorAura": return LocalizationCatalog.Get("ui.entity.skill.pet", "펫 동료");
                case "skill.pet_baby_dragon": return LocalizationCatalog.Get("ui.entity.skill.baby.dragon", "베이비 드래곤");
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

            if (string.Equals(categoryId, "skill.cursorAura", StringComparison.Ordinal) ||
                string.Equals(categoryId, "skill.pet_baby_dragon", StringComparison.Ordinal))
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
                return attackIntervalIcon != null ? attackIntervalIcon : timerIcon;
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
                    return LocalizationCatalog.Get("ui.graph.skills", "스킬 연구");
                case TraitTab.Monster:
                    return LocalizationCatalog.Get("ui.graph.monsters", "몬스터 연구");
                default:
                    return LocalizationCatalog.Get("ui.graph.stats", "스탯 연구 트리");
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
                return new Vector2(27f, 27f);
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

            if (category != null &&
                (category.Id == "skill.cursorAura" ||
                 category.Id == "skill.pet_baby_dragon"))
            {
                return GetCategoryIcon(category.Id);
            }

            int skillIconIndex = category == null ? -1 : GetSkillIconIndex(category.Id);
            if (tab == TraitTab.Skill && skillIcons != null &&
                skillIconIndex >= 0 && skillIconIndex < skillIcons.Length &&
                skillIcons[skillIconIndex] != null)
            {
                return skillIcons[skillIconIndex];
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

        private static int GetSkillIconIndex(string skillId)
        {
            switch (skillId)
            {
                case "skill.tornado": return 0;
                case "skill.headshot": return 1;
                case "skill.thunder": return 2;
                case "skill.flame_exhaust": return 3;
                case "skill.magic_ball": return 4;
                case "skill.laser": return 5;
                default: return -1;
            }
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
                    return timerIcon != null ? timerIcon : attackIntervalIcon;
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

        private int GetMonsterUnlockGemstoneIndex(string monsterNodeId)
        {
            if (_document == null || _document.information == null ||
                _document.information.monsters == null)
            {
                return -1;
            }

            foreach (MonsterInformation monster in _document.information.monsters)
            {
                if (monster == null || !string.Equals(
                        monster.id,
                        monsterNodeId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                // Stump yields garnet. Later monsters display the exact gem
                // type configured in GameInformation, including duplicates.
                return GetGemstoneIndexById(monster.gemstoneId);
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
            TextMeshProUGUI text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                RectTransform textRect = rect;
                if (rect.GetComponent<Graphic>() != null)
                {
                    textRect = CreateRect(
                        "Text",
                        rect,
                        Vector2.zero,
                        Vector2.one,
                        Vector2.zero,
                        Vector2.zero,
                        new Vector2(0.5f, 0.5f));
                }

                text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            text.text = content ?? string.Empty;
            // Match PrototypeRunHud: resolve the scene's authored font first so
            // stat labels use the same localized SDF typography as combat.
            TMP_FontAsset sceneFont = LocalizedTypography.GetFont(uiFont);
            TMP_FontAsset localeFont = LocalizedTypography.GetFont(null);
            text.font = sceneFont != null && sceneFont != TMP_Settings.defaultFontAsset
                ? sceneFont
                : localeFont;
            text.fontSize = Mathf.Max(
                14,
                Mathf.RoundToInt(fontSize * Mathf.Clamp(textScale, 1f, 1.5f)));
            text.alignment = LocalizedTypography.Alignment(alignment);
            text.color = color;
            text.raycastTarget = false;

            text.overflowMode = TextOverflowModes.Ellipsis;
            // Runtime-created labels have fixed rectangles. Wrap and shrink
            // text before ellipsizing so it cannot paint over neighboring UI.
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;
            text.extraPadding = true;
            text.fontSizeMin = Mathf.Max(11f, text.fontSize * 0.72f);
            text.fontSizeMax = text.fontSize;
            text.richText = true;
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
            Sprite visualSprite = sprite != null
                ? sprite
                : color.a > 0.01f ? buttonFrameSprite : null;
            image.sprite = visualSprite;
            image.color = color;
            image.raycastTarget = true;
            if (visualSprite != null)
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
            button.gameObject.AddComponent<TraitTooltipDismissRelay>().Bind(this);

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
                TextMeshProUGUI labelText = CreateText(
                    labelRect, label, fontSize, TextAnchor.MiddleCenter, textColor, true);
                // Button labels should stay on one line; wrapping in a fixed
                // height button caused clipped two-line labels and collisions.
                labelText.textWrappingMode = TextWrappingModes.NoWrap;
                labelText.overflowMode = TextOverflowModes.Ellipsis;
            }

            return button;
        }
    }

    /// <summary>Uses the mouse wheel for zoom and preserves ScrollRect drag panning.</summary>
    public sealed class TraitGraphScrollRect : ScrollRect
    {
        private TraitScreenController _owner;
        private bool _manualDragActive;
        private Vector2 _lastDragPosition;

        public void Bind(TraitScreenController owner)
        {
            _owner = owner;
        }

        public override void OnScroll(PointerEventData eventData)
        {
            if (_owner != null && !_owner.CanNavigateResearchGraph)
            {
                eventData?.Use();
                return;
            }
            if (_owner != null && eventData != null)
            {
                _owner.ZoomGraphAt(eventData.position, eventData.scrollDelta.y);
                eventData.Use();
            }
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (_owner != null && !_owner.CanNavigateResearchGraph)
            {
                eventData?.Use();
                return;
            }
            base.OnBeginDrag(eventData);
            _manualDragActive = false;
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left)
            {
                RectTransform dragViewport = viewport != null
                    ? viewport
                    : transform as RectTransform;
                if (content != null && dragViewport != null &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        dragViewport,
                        eventData.position,
                        eventData.pressEventCamera,
                        out _lastDragPosition))
                {
                    _manualDragActive = true;
                    velocity = Vector2.zero;
                }
            }

            if (_owner != null) _owner.SetGraphDragCursor(true);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (_owner != null && !_owner.CanNavigateResearchGraph)
            {
                eventData?.Use();
                return;
            }
            RectTransform dragViewport = viewport != null
                ? viewport
                : transform as RectTransform;
            if (_manualDragActive && content != null && dragViewport != null &&
                eventData != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragViewport,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 currentPosition))
            {
                Vector2 delta = currentPosition - _lastDragPosition;
                Vector2 nextPosition = content.anchoredPosition + delta;
                content.anchoredPosition = _owner == null
                    ? nextPosition
                    : _owner.ClampGraphPosition(nextPosition);
                _lastDragPosition = currentPosition;
                velocity = Vector2.zero;
                eventData.Use();
            }
            else if (eventData != null)
            {
                // Only left-button drags pan this board. In particular, do
                // not let ScrollRect's unrestricted fallback bypass bounds.
                eventData.Use();
            }

            if (_owner != null) _owner.RefreshNodeTooltipPosition();
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            base.OnEndDrag(eventData);
            velocity = Vector2.zero;
            _manualDragActive = false;
            if (_owner != null) _owner.SetGraphDragCursor(false);
        }
    }

    public sealed class TraitTooltipDismissRelay : MonoBehaviour, IPointerClickHandler
    {
        private TraitScreenController _owner;

        public void Bind(TraitScreenController owner)
        {
            _owner = owner;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_owner != null)
            {
                _owner.HandleTooltipOutsideClick(gameObject);
            }
        }
    }

    public sealed class TraitNodeRightClickRelay : MonoBehaviour, IPointerClickHandler
    {
        private TraitScreenController _owner;
        private string _nodeId;

        public void Bind(TraitScreenController owner, string nodeId)
        {
            _owner = owner;
            _nodeId = nodeId;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
            {
                _owner?.HandleNodeRightClick(_nodeId);
            }
        }
    }

    /// <summary>
    /// Lets the tooltip yield to research nodes underneath its body while
    /// keeping its own action buttons and empty panel area interactive.
    /// </summary>
    public sealed class TraitTooltipCanvasRaycastFilter : MonoBehaviour,
        ICanvasRaycastFilter
    {
        private TraitScreenController _owner;

        public void Bind(TraitScreenController owner)
        {
            _owner = owner;
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            return _owner == null ||
                   _owner.ShouldTooltipBlockRaycast(screenPoint, eventCamera);
        }
    }

    public sealed class TraitTooltipPointerRelay : MonoBehaviour,
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

    public sealed class TraitTalentTooltipPointerRelay : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private TraitScreenController _owner;
        private string _talentId;

        public void Bind(TraitScreenController owner, string talentId)
        {
            _owner = owner;
            _talentId = talentId;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _owner?.ShowTalentTooltip(_talentId, transform as RectTransform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _owner?.HandleTooltipPointerExit();
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
        private float _duration = 0.48f;

        public void Play(Color color, float durationSeconds = 0.48f)
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                Destroy(this);
                return;
            }

            _duration = Mathf.Clamp(durationSeconds, 0.08f, 0.8f);
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
            float elapsed = 0f;
            while (elapsed < _duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _duration);
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
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left)
            {
                TraitAudioFeedback.PlayClick();
            }
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

    public sealed class TraitCardRevealAnimator : MonoBehaviour
    {
        private RectTransform _rect;
        private CanvasGroup _group;

        private void Awake()
        {
            _rect = transform as RectTransform;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        public void Play(float delay)
        {
            if (_rect == null) _rect = transform as RectTransform;
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _rect.localScale = Vector3.one * 0.72f;
            _group.alpha = 0f;
            StartCoroutine(Animate(delay));
        }

        private IEnumerator Animate(float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            const float duration = 0.32f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.055f;
                _rect.localScale = Vector3.one * Mathf.Lerp(0.72f, 1f, eased) * overshoot;
                _group.alpha = eased;
                yield return null;
            }

            _rect.localScale = Vector3.one;
            _group.alpha = 1f;
        }
    }

    /// <summary>Drag relay used only while the in-game research layout editor is enabled.</summary>
    public sealed class TraitBoardNodeDragRelay : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        private TraitScreenController _controller;
        private string _nodeId;
        private RectTransform _nodeRect;

        public void Bind(TraitScreenController controller, string nodeId, RectTransform nodeRect)
        {
            _controller = controller;
            _nodeId = nodeId;
            _nodeRect = nodeRect;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_controller != null)
                _controller.BeginBoardNodeDrag(_nodeId, _nodeRect, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_controller != null)
                _controller.DragBoardNode(_nodeId, _nodeRect, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_controller != null)
                _controller.EndBoardNodeDrag(_nodeId, _nodeRect);
        }
    }
}
