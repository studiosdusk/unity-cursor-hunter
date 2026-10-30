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
        [Header("Data")]
        [SerializeField] private TraitCatalog catalog;
        [SerializeField, Min(0)] private long startingGarnet = 125;
        [SerializeField] private long[] startingGemstoneBalances =
            new long[] { 125L, 0L, 0L, 0L, 0L, 0L };
        [SerializeField, Range(0, 5)] private int unlockedBossTier;
        public int CurrentBossUnlockTier => Mathf.Clamp(unlockedBossTier, 0, 5);
        [SerializeField] private bool loadSavedProgress = true;
        [SerializeField] private bool testModeUnlockAll = false;
        [SerializeField] private bool testModeFreeUpgrades = false;
        [SerializeField] private UnityEvent backRequested = new UnityEvent();
        [SerializeField] private UnityEvent combatRequested = new UnityEvent();

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
        [SerializeField] private Sprite backButtonSprite;
        [SerializeField] private Sprite gemstoneExchangeIcon;
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
        [SerializeField] private Sprite affordableNodeBadgeSprite;
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
        private readonly Dictionary<string, RectTransform> _boardNodeRectsById =
            new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        private RectTransform _runtimeRoot;
        private RectTransform _nodeContent;
        private RectTransform _nodeViewport;
        private TextMeshProUGUI _screenTitle;
        private Button _researchCategoryButton;
        private Button _relicCategoryButton;
        private Button _relicTestModeButton;
        private TextMeshProUGUI _relicTestModeLabel;
        private RectTransform _gemstoneList;
        private RectTransform _gemstonePanel;
        private RectTransform _combatStatsPanel;
        private readonly List<TextMeshProUGUI> _combatStatValues =
            new List<TextMeshProUGUI>();
        private CanvasGroup _runtimeGroup;
        private CanvasGroup _nodeGroup;
        private TextMeshProUGUI _nodeHeading;
        private TextMeshProUGUI _garnetValue;
        private Image _selectedHeaderBackground;
        private Image _selectedHeaderAccent;
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
        private Image _tooltipCurrencyIcon;
        private TextMeshProUGUI _tooltipStatus;
        private Button _tooltipCloseButton;
        private Button _tooltipUpgradeButton;
        private TextMeshProUGUI _tooltipUpgradeLabel;
        private Button _tooltipResetButton;
        private string _tooltipNodeId;
        private string _hoveredNodeId;
        private bool _pointerOverTooltip;
        private Coroutine _tooltipAnimation;
        private Coroutine _tooltipDismissRoutine;
        private Coroutine _relicRevealRoutine;
        private GameObject _relicRevealOverlay;
        private RectTransform _relicRevealTarget;
        private Outline _relicRevealTargetOutline;
        private Vector3 _relicRevealTargetOriginalScale;
        private Color _relicRevealTargetOriginalOutlineColor;
        private int _pendingRelicRevealIndex = -1;
        private bool _relicRevealDismissRequested;
        private GameObject _relicDetailOverlay;
        private int _selectedRelicIndex = -1;
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
        private TextMeshProUGUI _testBossTierHeading;
        private Button[] _testBossTierButtons = Array.Empty<Button>();
        private TextMeshProUGUI[] _testBossTierLabels = Array.Empty<TextMeshProUGUI>();
        private Button _testUnlockAllButton;
        private TextMeshProUGUI _testUnlockAllLabel;
        private bool _testToolsOpen;
        private bool _graphDragCursorActive;
        private bool _starterTutorialActive;
        private GameObject _starterTutorialOverlay;
        private RectTransform _starterTutorialCard;
        private TextMeshProUGUI _starterTutorialStepLabel;
        private TextMeshProUGUI _starterTutorialTitle;
        private TextMeshProUGUI _starterTutorialMessage;
        private TextMeshProUGUI _starterTutorialFooter;
        private Button _starterTutorialActionButton;
        private TextMeshProUGUI _starterTutorialActionLabel;
        private Image[] _starterTutorialDimPanels = Array.Empty<Image>();
        private TraitStarterTutorialRaycastGate _starterTutorialRaycastGate;
        private Outline _starterTutorialOutline;
        private string _starterTutorialTargetId;
        private RectTransform _starterTutorialTarget;
        private readonly Vector3[] _starterTutorialWorldCorners = new Vector3[4];
        private readonly Vector2[] _starterTutorialLocalCorners = new Vector2[4];
        private readonly Dictionary<int, TextMeshProUGUI> _gemstoneBalanceLabels =
            new Dictionary<int, TextMeshProUGUI>();
        private Coroutine _gemstoneExchangeRepeat;
        private bool _gemstoneExchangePointerHeld;
        private bool _gemstoneExchangeChanged;
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
                if (TraitProgressionStore.ShouldShowStarterTutorial)
                    return HasCompletedStarterOnboarding;
                EnsureProgressionState();
                return _purchased.Contains(ResearchCoreNodeId) && _purchased.Contains("monster.01");
            }
        }

        public bool HasCompletedStarterOnboarding
        {
            get
            {
                EnsureProgressionState();
                for (int i = 0; i < StarterTutorialNodeIds.Length; i++)
                {
                    if (!IsPurchased(FindNode(StarterTutorialNodeIds[i])))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        [Serializable]
        private sealed class RelicDisplayEntry
        {
            [SerializeField] private string title;
            [SerializeField] private Sprite icon;
            [SerializeField, TextArea(2, 4)] private string description;
            [SerializeField] private bool isObtained;

            public string Title => title;
            public Sprite Icon => icon;
            public string Description => description;
            public bool IsObtained => isObtained;

            public void SetObtained(bool value)
            {
                isObtained = value;
            }

            public void ApplyPresentationDefaults(
                string defaultTitle,
                Sprite defaultIcon,
                string defaultDescription)
            {
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
            public int BossGateTier;
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

        private const float StatGraphDefaultZoom = 0.82f;
        private const float StarterTutorialZoom = 1.48f;
        private const float StatGraphWidth = 1600f;
        private const float StatGraphHeight = 1120f;
        private const float BoardNodeSize = 76f;
        // BoardNodeSize is 76 px. A 22 px gap was too tight for the number of
        // branches on the combined board, so increase the gap by 1.5x.
        private const float BoardNodePitchX = 109f;
        private const float BoardNodePitchY = 109f;
        private const float MinimumGraphZoom = 0.28f;
        private const float MaximumGraphZoom = 2.1f;
        private const float RelicGridCardAspect = 2.35f;
        private const float RelicDetailCardAspect = 0.68f;
        private const string ResearchCoreNodeId = "stat.attack.01";
        private static readonly string[] StarterTutorialNodeIds =
        {
            "stat.attack.01",
            "stat.radius.01",
            "stat.cooldown.01",
            "stat.critical.01",
            "stat.fieldDuration.01",
            "stat.attack.02",
            "stat.radius.02",
            "skill.pet_baby_dragon",
            "monster.01"
        };

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
            ApplyRelicPresentationDefaults();
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

            int count = Mathf.Min(relicEntries.Count, defaults.Entries.Count);
            for (int i = 0; i < count; i++)
            {
                RelicDisplayEntry entry = relicEntries[i];
                RelicCollectionCatalogEntry presentation = defaults.Entries[i];
                if (entry == null || presentation == null)
                {
                    continue;
                }

                entry.ApplyPresentationDefaults(
                    presentation.Title,
                    presentation.Icon,
                    presentation.Description);
            }
        }

        private void OnEnable()
        {
            _starterTutorialActive &= TraitProgressionStore.ShouldShowStarterTutorial;
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
            UpdateStarterTutorialPresentation();
        }

        private void Update()
        {
            UpdateStarterTutorialPulse();
            if (_starterTutorialOverlay != null && _starterTutorialOverlay.activeSelf)
            {
                UpdateStarterTutorialSpotlightLayout();
            }

            if (_relicRevealOverlay == null)
            {
                return;
            }

            if (Keyboard.current != null &&
                (Keyboard.current.escapeKey.wasPressedThisFrame ||
                 Keyboard.current.enterKey.wasPressedThisFrame ||
                 Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            {
                RequestRelicRevealDismiss();
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            Vector2 pointerPosition = mouse.position.ReadValue();
            Camera uiCamera = GetUiCamera();
            RectTransform focus = _relicRevealOverlay.transform.Find(
                "RelicRevealFocus") as RectTransform;
            RectTransform confirm = _relicRevealOverlay.transform.Find(
                "RelicRevealFocus/RelicRevealConfirm") as RectTransform;

            if ((confirm != null && RectTransformUtility.RectangleContainsScreenPoint(
                    confirm, pointerPosition, uiCamera)) ||
                (focus != null && !RectTransformUtility.RectangleContainsScreenPoint(
                    focus, pointerPosition, uiCamera)))
            {
                RequestRelicRevealDismiss();
            }
        }

        private void OnDisable()
        {
            FinishGemstoneExchangeHold(false);
            if (_fitGraphRoutine != null)
            {
                StopCoroutine(_fitGraphRoutine);
                _fitGraphRoutine = null;
            }
            CloseSummaryPopup();
            HideNodeTooltip();
            CancelRelicReveal();
            CloseRelicDetail();
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
            RefreshTestBossTierButtons();
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
            RefreshTestBossTierButtons();
            RefreshAll();
            SaveProgressionState();
            SetTestStatus("보스 " + unlockedBossTier + "단계 격파 · 새 연구가 열렸습니다.");
        }

        /// <summary>
        /// Opens the first incomplete step when the player tries to enter
        /// combat before starting research and unlocking Stump.
        /// </summary>
        public void GuideStarterResearch()
        {
            EnsureProgressionState();
            if (!TraitProgressionStore.ShouldShowStarterTutorial)
            {
                _starterTutorialActive = false;
                UpdateStarterTutorialPresentation();
                return;
            }

            TraitProgressionStore.BeginStarterTutorial();
            _starterTutorialActive = true;
            TraitNodeDefinition next = GetNextStarterTutorialNode();
            if (next != null)
            {
                ShowTab(GetNodeTab(next));
                _selectedNodeId = next.Id;
                HideNodeTooltip();
                UpdateNodeSelectionFrame();
            }
            else
            {
                ShowTab(TraitTab.Stat);
            }

            UpdateStarterTutorialPresentation();
            UpdateHeaderForActiveView();
        }

        private TraitNodeDefinition GetNextStarterTutorialNode()
        {
            for (int i = 0; i < StarterTutorialNodeIds.Length; i++)
            {
                TraitNodeDefinition node = FindNode(StarterTutorialNodeIds[i]);
                if (node != null && !IsPurchased(node))
                {
                    return node;
                }
            }

            return null;
        }

        private void UpdateStarterTutorialPresentation()
        {
            if (_runtimeRoot == null)
            {
                return;
            }

            bool active = _starterTutorialActive &&
                TraitProgressionStore.ShouldShowStarterTutorial;
            if (!active)
            {
                _starterTutorialActive = false;
                if (_starterTutorialOverlay != null)
                {
                    _starterTutorialOverlay.SetActive(false);
                }
                if (_starterTutorialOutline != null)
                {
                    Destroy(_starterTutorialOutline);
                    _starterTutorialOutline = null;
                }
                _starterTutorialTarget = null;
                _starterTutorialTargetId = null;
                return;
            }

            EnsureStarterTutorialOverlay();
            TraitNodeDefinition next = GetNextStarterTutorialNode();
            string targetId = next == null ? "__back__" : next.Id;
            RectTransform target = next == null
                ? _runtimeRoot.Find("BackButton") as RectTransform
                : _boardNodeRectsById.TryGetValue(next.Id, out RectTransform nodeRect)
                    ? nodeRect
                    : null;
            if (_starterTutorialOverlay == null || _starterTutorialMessage == null ||
                _starterTutorialRaycastGate == null || target == null)
            {
                if (_starterTutorialOverlay != null)
                {
                    _starterTutorialOverlay.SetActive(false);
                }
                if (_starterTutorialOutline != null)
                {
                    Destroy(_starterTutorialOutline);
                    _starterTutorialOutline = null;
                }
                _starterTutorialTarget = null;
                _starterTutorialTargetId = null;
                return;
            }

            bool changedTarget = !string.Equals(
                _starterTutorialTargetId,
                targetId,
                StringComparison.Ordinal) ||
                _starterTutorialTarget != target ||
                _starterTutorialOutline == null;
            if (changedTarget)
            {
                if (_starterTutorialOutline != null)
                {
                    Destroy(_starterTutorialOutline);
                }

                _starterTutorialTarget = target;
                _starterTutorialOutline = target.gameObject.AddComponent<Outline>();
                _starterTutorialOutline.effectColor = new Color(1f, 0.91f, 0.55f, 1f);
                _starterTutorialOutline.effectDistance = new Vector2(7f, -7f);
                _starterTutorialOutline.useGraphicAlpha = false;
                _starterTutorialTargetId = targetId;

                if (next != null && _boardPositionsByNodeId.TryGetValue(
                        next.Id,
                        out Vector2 focusPosition))
                {
                    CenterGraphOnNode(focusPosition, StarterTutorialZoom);
                }
            }

            _starterTutorialRaycastGate.Bind(this, target, GetUiCamera());
            SetStarterTutorialPrompt(next);
            _starterTutorialOverlay.SetActive(true);
            _starterTutorialOverlay.transform.SetAsLastSibling();
            UpdateStarterTutorialSpotlightLayout();
            RefreshStarterTutorialTooltipPresentation();
        }

        private void EnsureStarterTutorialOverlay()
        {
            if (_starterTutorialOverlay != null || _runtimeRoot == null)
            {
                return;
            }

            RectTransform overlay = CreateRect(
                "StarterTutorialOverlay",
                _runtimeRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Vector2(0.5f, 0.5f));
            _starterTutorialOverlay = overlay.gameObject;
            _starterTutorialOverlay.SetActive(false);
            Image blocker = overlay.gameObject.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0f);
            blocker.raycastTarget = true;
            _starterTutorialRaycastGate =
                overlay.gameObject.AddComponent<TraitStarterTutorialRaycastGate>();

            _starterTutorialDimPanels = new Image[4];
            string[] panelNames = { "DimTop", "DimBottom", "DimLeft", "DimRight" };
            for (int i = 0; i < _starterTutorialDimPanels.Length; i++)
            {
                RectTransform panel = CreateImageRect(
                    panelNames[i],
                    overlay,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    Vector2.zero,
                    null,
                    new Color(0.23f, 0.27f, 0.32f, 0.78f),
                    new Vector2(0.5f, 0.5f));
                _starterTutorialDimPanels[i] = panel.GetComponent<Image>();
                _starterTutorialDimPanels[i].raycastTarget = true;
            }

            _starterTutorialCard = CreatePanel(
                "GuideCard",
                overlay,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(480f, 224f),
                panelSprite,
                new Color(0.055f, 0.073f, 0.105f, 0.99f),
                new Vector2(0.5f, 0.5f));
            Image cardImage = _starterTutorialCard.GetComponent<Image>();
            cardImage.raycastTarget = false;
            cardImage.type = panelSprite == null ? Image.Type.Simple : Image.Type.Sliced;
            Outline cardOutline = _starterTutorialCard.GetComponent<Outline>();
            if (cardOutline != null)
            {
                cardOutline.effectColor = new Color(1f, 0.79f, 0.36f, 0.98f);
                cardOutline.effectDistance = new Vector2(2.5f, -2.5f);
            }

            CreateImageRect(
                "GuideAccent",
                _starterTutorialCard,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -4f),
                new Vector2(-8f, 5f),
                null,
                new Color(1f, 0.76f, 0.30f, 1f),
                new Vector2(0.5f, 1f));
            _starterTutorialStepLabel = CreateText(
                CreateRect(
                    "GuideStep",
                    _starterTutorialCard,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(20f, -18f),
                    new Vector2(-40f, 26f),
                    new Vector2(0f, 1f)),
                string.Empty,
                15,
                TextAnchor.MiddleLeft,
                new Color(1f, 0.82f, 0.45f, 1f),
                true);
            _starterTutorialTitle = CreateText(
                CreateRect(
                    "GuideTitle",
                    _starterTutorialCard,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(20f, -52f),
                    new Vector2(-40f, 34f),
                    new Vector2(0f, 1f)),
                string.Empty,
                24,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            _starterTutorialTitle.enableAutoSizing = true;
            _starterTutorialTitle.fontSizeMin = 20f;
            _starterTutorialTitle.fontSizeMax = 24f;
            _starterTutorialTitle.textWrappingMode = TextWrappingModes.Normal;
            _starterTutorialMessage = CreateText(
                CreateRect(
                    "GuideMessage",
                    _starterTutorialCard,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(20f, -94f),
                    new Vector2(-40f, 72f),
                    new Vector2(0f, 1f)),
                string.Empty,
                17,
                TextAnchor.MiddleLeft,
                Color.white,
                false);
            _starterTutorialMessage.textWrappingMode = TextWrappingModes.Normal;
            _starterTutorialMessage.enableAutoSizing = true;
            _starterTutorialMessage.fontSizeMin = 14f;
            _starterTutorialMessage.fontSizeMax = 17f;
            _starterTutorialFooter = CreateText(
                CreateRect(
                    "GuideFooter",
                    _starterTutorialCard,
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(20f, 23f),
                    new Vector2(220f, 22f),
                    new Vector2(0f, 0f)),
                "마우스를 올리면 설명이 나와요.",
                12,
                TextAnchor.MiddleLeft,
                mutedInkColor,
                false);

            _starterTutorialActionButton = CreateButton(
                "GuideAction",
                _starterTutorialCard,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-92f, 23f),
                new Vector2(148f, 42f),
                null,
                new Color(0.96f, 0.67f, 0.24f, 1f),
                "진행",
                16,
                new Color(0.13f, 0.09f, 0.04f, 1f));
            _starterTutorialActionLabel =
                _starterTutorialActionButton.GetComponentInChildren<TextMeshProUGUI>();
            TraitTooltipDismissRelay tooltipRelay =
                _starterTutorialActionButton.GetComponent<TraitTooltipDismissRelay>();
            if (tooltipRelay != null) Destroy(tooltipRelay);
            _starterTutorialActionButton.onClick.AddListener(ActivateStarterTutorialTarget);
        }

        private void SetStarterTutorialPrompt(TraitNodeDefinition node)
        {
            if (_starterTutorialStepLabel == null || _starterTutorialTitle == null ||
                _starterTutorialMessage == null || _starterTutorialFooter == null ||
                _starterTutorialActionLabel == null)
            {
                return;
            }

            if (node == null)
            {
                _starterTutorialStepLabel.text = "처음 안내 · 준비 완료";
                _starterTutorialTitle.text = "첫 전투를 시작해요";
                _starterTutorialMessage.text =
                    "특성 연구가 끝났어요. 뒤로 돌아가 필드를 선택하면 첫 전투를 시작할 수 있어요.";
                _starterTutorialFooter.text = "다음 단계는 로비에서 이어집니다.";
                SetButtonLabel(_starterTutorialActionLabel, "뒤로 가기");
                return;
            }

            int step = 1;
            for (int i = 0; i < StarterTutorialNodeIds.Length; i++)
            {
                if (string.Equals(StarterTutorialNodeIds[i], node.Id, StringComparison.Ordinal))
                {
                    step = i + 1;
                    break;
                }
            }

            _starterTutorialStepLabel.text = "처음 안내 · " +
                step.ToString("00", CultureInfo.InvariantCulture) + " / " +
                StarterTutorialNodeIds.Length.ToString("00", CultureInfo.InvariantCulture);
            _starterTutorialTitle.text = GetLocalizedNodeTitle(node);
            _starterTutorialMessage.text = BuildStarterTutorialPrompt(node);
            _starterTutorialFooter.text = IsStarterTutorialRightClickStep(node)
                ? "강조된 아이콘을 우클릭하세요."
                : "우클릭: 배우기 / 강화";
            SetButtonLabel(_starterTutorialActionLabel, GetStarterTutorialActionLabel(node));
        }

        private static string BuildStarterTutorialPrompt(TraitNodeDefinition node)
        {
            if (node == null)
            {
                return "특성 연구가 끝났어요. 뒤로 돌아가 필드 선택을 진행하세요.";
            }

            if (node.Id == ResearchCoreNodeId)
            {
                return "아이콘에 마우스를 올리면 설명과 비용을 볼 수 있어요.\n가운데 시작점부터 연구해 보세요.";
            }
            if (node.Id.StartsWith("stat.", StringComparison.Ordinal) &&
                node.Id.EndsWith(".01", StringComparison.Ordinal))
            {
                return "기본 능력치를 배워 다음 강화를 준비해요.\n아이콘을 우클릭해도 바로 배울 수 있어요.";
            }
            if (node.Id == "stat.attack.02")
            {
                return "느낌표(!)는 지금 가진 재화로 강화할 수 있다는 표시예요.\n아이콘의 설명을 확인한 뒤 우클릭으로 강화해 보세요.";
            }
            if (node.Id == "stat.radius.02")
            {
                return "커서 공격 범위를 넓힙니다.\n아이콘을 우클릭하면 바로 강화할 수 있어요.";
            }
            if (node.Id == "skill.pet_baby_dragon")
            {
                return "아기용 동료를 배워 전투에 데려가세요.";
            }
            return "그루터기를 해금하면 첫 전투에 나타납니다.";
        }

        private static string GetStarterTutorialActionLabel(TraitNodeDefinition node)
        {
            if (node == null) return "뒤로 가기";
            if (IsStarterTutorialRightClickStep(node)) return "설명 보기";
            if (node.Id == ResearchCoreNodeId) return "연구 시작";
            if (node.Id.StartsWith("skill.", StringComparison.Ordinal)) return "배우기";
            if (node.Id.StartsWith("monster.", StringComparison.Ordinal)) return "해금하기";
            return node.Id.EndsWith(".01", StringComparison.Ordinal)
                ? "배우기"
                : "강화하기";
        }

        private static bool IsStarterTutorialRightClickStep(TraitNodeDefinition node)
        {
            return node != null && string.Equals(
                node.Id,
                "stat.attack.02",
                StringComparison.Ordinal);
        }

        private void ActivateStarterTutorialTarget()
        {
            if (string.Equals(_starterTutorialTargetId, "__back__", StringComparison.Ordinal))
            {
                RequestBack();
                return;
            }

            TraitNodeDefinition node = FindNode(_starterTutorialTargetId);
            if (IsStarterTutorialRightClickStep(node))
            {
                ShowNodeTooltip(node);
                ScheduleTooltipDismiss();
                return;
            }

            TryUpgradeNode(node);
        }

        private void RefreshStarterTutorialTooltipPresentation()
        {
            bool tooltipVisible = _tooltipRoot != null && _tooltipRoot.gameObject.activeSelf;
            if (_starterTutorialCard != null && _starterTutorialOverlay != null &&
                _starterTutorialOverlay.activeSelf)
            {
                // The normal description replaces the guide card while hovered,
                // so the two panels never cover one another.
                _starterTutorialCard.gameObject.SetActive(!tooltipVisible);
            }

            if (tooltipVisible)
            {
                _tooltipRoot.SetAsLastSibling();
            }
        }

        private void UpdateStarterTutorialPulse()
        {
            if (!_starterTutorialActive || _starterTutorialOutline == null)
            {
                return;
            }

            float pulse = (Mathf.Sin(Time.unscaledTime * 4.2f) + 1f) * 0.5f;
            _starterTutorialOutline.effectColor = Color.Lerp(
                new Color(1f, 0.66f, 0.22f, 0.86f),
                new Color(1f, 0.95f, 0.62f, 1f),
                pulse);
            float thickness = Mathf.Lerp(5f, 10f, pulse);
            _starterTutorialOutline.effectDistance = new Vector2(thickness, -thickness);
        }

        private void UpdateStarterTutorialSpotlightLayout()
        {
            if (_starterTutorialOverlay == null || _starterTutorialTarget == null ||
                _starterTutorialCard == null || _starterTutorialDimPanels == null ||
                _starterTutorialDimPanels.Length != 4)
            {
                return;
            }

            RectTransform overlay = _starterTutorialOverlay.transform as RectTransform;
            if (overlay == null) return;
            Camera camera = GetUiCamera();
            _starterTutorialTarget.GetWorldCorners(_starterTutorialWorldCorners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < _starterTutorialWorldCorners.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                    camera,
                    _starterTutorialWorldCorners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        overlay,
                        screen,
                        camera,
                        out _starterTutorialLocalCorners[i]))
                {
                    return;
                }

                min = Vector2.Min(min, _starterTutorialLocalCorners[i]);
                max = Vector2.Max(max, _starterTutorialLocalCorners[i]);
            }

            Rect bounds = overlay.rect;
            const float spotlightPadding = 14f;
            Rect hole = Rect.MinMaxRect(
                Mathf.Max(bounds.xMin, min.x - spotlightPadding),
                Mathf.Max(bounds.yMin, min.y - spotlightPadding),
                Mathf.Min(bounds.xMax, max.x + spotlightPadding),
                Mathf.Min(bounds.yMax, max.y + spotlightPadding));
            SetStarterTutorialDimPanel(0, Rect.MinMaxRect(
                bounds.xMin, hole.yMax, bounds.xMax, bounds.yMax));
            SetStarterTutorialDimPanel(1, Rect.MinMaxRect(
                bounds.xMin, bounds.yMin, bounds.xMax, hole.yMin));
            SetStarterTutorialDimPanel(2, Rect.MinMaxRect(
                bounds.xMin, hole.yMin, hole.xMin, hole.yMax));
            SetStarterTutorialDimPanel(3, Rect.MinMaxRect(
                hole.xMax, hole.yMin, bounds.xMax, hole.yMax));

            float cardWidth = Mathf.Min(500f, Mathf.Max(260f, bounds.width - 24f));
            float cardHeight = Mathf.Min(224f, Mathf.Max(188f, bounds.height - 24f));
            _starterTutorialCard.sizeDelta = new Vector2(cardWidth, cardHeight);
            _starterTutorialCard.anchoredPosition = TutorialPopupLayout.AboveOrUpperLeft(
                bounds, hole, new Vector2(cardWidth, cardHeight));
        }

        private void SetStarterTutorialDimPanel(int index, Rect rect)
        {
            Image image = _starterTutorialDimPanels[index];
            if (image == null) return;
            bool visible = rect.width > 1f && rect.height > 1f;
            image.gameObject.SetActive(visible);
            if (!visible) return;
            RectTransform panel = image.rectTransform;
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = rect.center;
            panel.sizeDelta = rect.size;
        }

        private void CenterGraphOnNode(Vector2 position, float zoom)
        {
            if (_nodeContent == null || _nodeViewport == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            float safeZoom = Mathf.Clamp(zoom, MinimumGraphZoom, MaximumGraphZoom);
            _nodeContent.localScale = Vector3.one * safeZoom;
            _nodeContent.anchoredPosition = -position * safeZoom;
            PositionNodeTooltip(FindNode(_tooltipNodeId));
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
            return GrantRunRewards(rewards, null);
        }

        /// <summary>
        /// Applies currency and relic awards from one run before writing the
        /// profile once, so newly found relics are available on the next run.
        /// </summary>
        public bool GrantRunRewards(
            IReadOnlyList<ResourceRewardSnapshot> rewards,
            IReadOnlyList<string> relicAwards)
        {
            EnsureProgressionState();
            EnsureRelicEntries();

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
            int previousPendingRevealIndex = _pendingRelicRevealIndex;
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
                if (_pendingRelicRevealIndex < 0)
                    _pendingRelicRevealIndex = relicIndex;
            }

            RefreshAll();
            if (SaveProgressionState())
                return true;

            _garnetBalance = previousGarnet;
            _gemstoneBalances = previousGemstoneBalances;
            for (int i = 0; i < relicIndexes.Count; i++)
                relicEntries[relicIndexes[i]].SetObtained(false);
            _pendingRelicRevealIndex = previousPendingRevealIndex;
            RefreshAll();
            return false;
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
            if (GrantRunRewards(result.Rewards, result.RelicAwards))
            {
                RefreshTestBossTierButtons();
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
            return GameInformationBuilder.Build(_document, _purchased,
                id => GetGemstoneBalance(GetGemstoneIndexById(id)),
                _cursorCombatStatDefaults, GetOwnedRelicIds());
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
            backRequested?.Invoke();
        }

        public void RequestCombat()
        {
            combatRequested?.Invoke();
        }

        private void ResolveCatalog()
        {
            _highestBossStageCleared = 0;
            _settledBossRunIds.Clear();
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
            EnsureRelicEntries();
            for (int i = 0; i < relicEntries.Count; i++)
                if (relicEntries[i] != null)
                    relicEntries[i].SetObtained(false);
            _pendingRelicRevealIndex = -1;
        }

        private void LoadSavedProgress()
        {
            if (_document.progression.savedProgressPolicy == "fileOnly" || !loadSavedProgress ||
                !TraitProgressionStore.TryLoad(out TraitProgressionStore.SaveData data))
            {
                return;
            }

            RestoreOwnedRelics(data.ownedRelicIds);
            _garnetBalance = Math.Max(0L, data.garnetBalance);
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
                settledBossRunIds: _settledBossRunIds);
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
            RectTransform title = CreateRect(
                "Title",
                _runtimeRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(42f, -102f),
                new Vector2(560f, 38f),
                new Vector2(0f, 1f));
            _screenTitle = CreateText(
                title,
                "특성",
                32,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            CreateBoardLegend(_runtimeRoot);
            BuildTopCategoryTabs(_runtimeRoot);
            BuildTestTools(_runtimeRoot);
            BuildRelicTestModeToggle(_runtimeRoot);
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
            float tabsWidth = Mathf.Clamp(parent.rect.width * 0.36f, 470f, 620f);
            float buttonWidth = (tabsWidth - 32f) * 0.5f;
            RectTransform tabs = CreatePanel(
                "ResearchCategoryTabs",
                parent,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -24f),
                new Vector2(tabsWidth, 78f),
                panelSprite,
                new Color(0.025f, 0.055f, 0.105f, 0.96f),
                new Vector2(0.5f, 1f));
            Image strip = tabs.GetComponent<Image>();
            strip.raycastTarget = false;
            Outline stripOutline = tabs.GetComponent<Outline>();
            if (stripOutline != null)
            {
                stripOutline.effectColor = new Color(0.70f, 0.79f, 0.92f, 0.48f);
                stripOutline.effectDistance = new Vector2(1f, -1f);
            }

            _researchCategoryButton = CreateButton(
                "ResearchTreeTab",
                tabs,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-(buttonWidth * 0.5f + 4f), 0f),
                new Vector2(buttonWidth, 60f),
                tabFocusSprite,
                new Color(1f, 0.69f, 0.18f, 1f),
                "통합 연구",
                21,
                new Color(0.16f, 0.10f, 0.05f, 1f));
            AddButtonIcon(
                _researchCategoryButton.transform as RectTransform,
                skillIcon != null ? skillIcon : monsterIcon,
                new Vector2(18f, 0f));
            _researchCategoryButton.onClick.AddListener(() => ShowTab(TraitTab.Stat));

            _relicCategoryButton = CreateButton(
                "RelicTab",
                tabs,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(buttonWidth * 0.5f + 4f, 0f),
                new Vector2(buttonWidth, 60f),
                tabSprite,
                new Color(0.78f, 0.83f, 0.90f, 1f),
                "유물",
                21,
                new Color(0.14f, 0.18f, 0.25f, 1f));
            AddButtonIcon(
                _relicCategoryButton.transform as RectTransform,
                gemstoneIcon != null ? gemstoneIcon : gemIcon,
                new Vector2(18f, 0f));
            _relicCategoryButton.onClick.AddListener(() => ShowRelicCollection());
            RefreshCollectionTabButtons();
        }

        private void BuildRelicTestModeToggle(RectTransform parent)
        {
            _relicTestModeButton = CreateButton(
                "RelicTestModeToggle",
                parent,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-270f, -72f),
                new Vector2(174f, 36f),
                null,
                new Color(0.28f, 0.20f, 0.10f, 0.98f),
                "유물 개별 테스트",
                13,
                inkColor);
            _relicTestModeLabel = _relicTestModeButton.GetComponentInChildren<TextMeshProUGUI>();
            _relicTestModeButton.onClick.AddListener(() => SetRelicTestMode(!_relicTestMode));
            _relicTestModeButton.gameObject.SetActive(false);
        }

        private void SetRelicTestMode(bool enabled)
        {
            _relicTestMode = enabled;
            if (_relicTestModeLabel != null)
            {
                _relicTestModeLabel.text = enabled ? "유물 테스트 종료" : "유물 개별 테스트";
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

        private void CreateBoardLegend(RectTransform parent)
        {
            RectTransform legend = CreateRect(
                "BoardLegend",
                parent,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -28f),
                new Vector2(492f, 28f),
                new Vector2(1f, 1f));
            CreateLegendChip(legend, 0f, "1단계", GetRankFrameColor(1));
            CreateLegendChip(legend, 98f, "2단계", GetRankFrameColor(2));
            CreateLegendChip(legend, 196f, "3단계", GetRankFrameColor(3));
            CreateLegendChip(legend, 294f, "4단계", GetRankFrameColor(4));
            CreateLegendChip(legend, 392f, "5단계", GetRankFrameColor(5));
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
                    new Vector2(70f, 24f),
                    new Vector2(0f, 0.5f)),
                label,
                11,
                TextAnchor.MiddleLeft,
                mutedInkColor,
                true);
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
                new Vector2(360f, 756f),
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
                "수량을 정해 젬스톤을 지급하고 해금 단계를 확인하세요.",
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

            _testBossTierHeading = CreateText(
                CreateRect(
                    "TestBossTierHeading",
                    _testToolsPanel,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(18f, -384f),
                    new Vector2(-36f, 22f),
                    new Vector2(0.5f, 1f)),
                "보스 해금 단계 · 현재 0단계",
                13,
                TextAnchor.MiddleLeft,
                new Color(0.82f, 0.90f, 1f, 1f),
                true);
            BuildTestBossTierSelector();

            CreateText(
                CreateRect(
                    "TestActionsHeading",
                    _testToolsPanel,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(18f, -460f),
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
                new Vector2(-81f, -492f),
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
                new Vector2(81f, -492f),
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
                new Vector2(-81f, -536f),
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
                new Vector2(81f, -536f),
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
                    new Vector2(18f, -582f),
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
                new Vector2(18f, -620f),
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
                new Vector2(98f, -620f),
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
                new Vector2(226f, -620f),
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
                    new Vector2(18f, -659f),
                    new Vector2(-36f, 30f),
                    new Vector2(0.5f, 1f)),
                "유물 탭에서 중앙 강조 연출을 확인할 수 있습니다. 번호 1~20",
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
                    new Vector2(18f, -704f),
                    new Vector2(-36f, 34f),
                    new Vector2(0.5f, 1f)),
                "젬 회수는 연구 상태를 유지합니다. 우측 아래 전체 초기화는 연구도 되돌립니다.",
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
                _pendingRelicRevealIndex = index;
            }
            else if (_pendingRelicRevealIndex == index)
            {
                _pendingRelicRevealIndex = -1;
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

        private void SetTestBossTier(int tier)
        {
            unlockedBossTier = Mathf.Clamp(tier, 0, 5);
            // Selecting a milestone explicitly tests its authored gates, even
            // if the all-conditions override was previously turned on.
            testModeUnlockAll = false;
            RefreshTestBossTierButtons();
            RefreshAll();
            RefreshNodeTooltip(FindNode(_tooltipNodeId));
            SetTestStatus("테스트 보스 단계: " + unlockedBossTier +
                "단계까지의 해금 조건을 적용했습니다.");
        }

        private void RefreshTestBossTierButtons()
        {
            RefreshTestUnlockAllButton();
            if (_testBossTierHeading != null)
            {
                _testBossTierHeading.text = "보스 해금 단계 · 현재 " +
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
            var recordedSpending = new List<KeyValuePair<string, int>>(_spentByNode);
            for (int i = 0; i < recordedSpending.Count; i++)
            {
                KeyValuePair<string, int> spent = recordedSpending[i];
                TraitNodeDefinition node = FindNode(spent.Key);
                string currencyId = _spentCurrencyByNode.TryGetValue(
                    spent.Key,
                    out string savedCurrency)
                    ? savedCurrency
                    : node == null ? string.Empty : node.CostGemstoneId;
                RefundNodeCost(node, spent.Value, currencyId);
            }

            _spentByNode.Clear();
            _spentCurrencyByNode.Clear();
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
                ? "테스트: 보스·선행 연구·젬스톤 공급 조건을 무시합니다."
                : "테스트: 보스 단계와 선행 연구 조건을 적용합니다.");
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
                new Vector2(28f, -170f),
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
                new Vector2(300f, 246f),
                panelSprite,
                new Color(0.018f, 0.043f, 0.105f, 0.94f),
                new Vector2(0f, 1f));

            CreateText(
                CreateRect(
                    "CombatStatsHeading",
                    _combatStatsPanel,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(14f, -9f),
                    new Vector2(-28f, 28f),
                    new Vector2(0.5f, 1f)),
                "현재 전투 능력치",
                16,
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
                    new Vector2(0f, -42f - i * 32f),
                    new Vector2(-20f, 29f),
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
                        new Vector2(22f, 22f),
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
                        new Vector2(39f, 0f),
                        new Vector2(-116f, 25f),
                        new Vector2(0f, 0.5f)),
                    labels[i],
                    13,
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
                        new Vector2(104f, 25f),
                        new Vector2(1f, 0.5f)),
                    "-",
                    14,
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
            _gemstoneBalanceLabels.Clear();

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
                float listHeight = Mathf.Max(38f, visibleCount * 38f);
                _gemstoneList.sizeDelta = new Vector2(_gemstoneList.sizeDelta.x, listHeight);
                Vector2 panelSize = _gemstonePanel.sizeDelta;
                panelSize.y = Mathf.Clamp(
                    80f + listHeight,
                    148f,
                    560f);
                _gemstonePanel.sizeDelta = panelSize;
                PositionCombatStatsPanelBelowWallet();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_gemstoneList);
        }

        internal void BeginGemstoneExchangeHold(int sourceIndex, int destinationIndex)
        {
            StopGemstoneExchangeRepeat();
            _gemstoneExchangePointerHeld = true;
            _gemstoneExchangeChanged = false;
            if (!TryExchangeGemstoneOnce(sourceIndex, destinationIndex))
            {
                _gemstoneExchangePointerHeld = false;
                return;
            }

            _gemstoneExchangeRepeat = StartCoroutine(
                RepeatGemstoneExchangeAfterDelay(sourceIndex, destinationIndex));
        }

        internal void EndGemstoneExchangeHold()
        {
            FinishGemstoneExchangeHold(true);
        }

        private void FinishGemstoneExchangeHold(bool refreshUi)
        {
            _gemstoneExchangePointerHeld = false;
            StopGemstoneExchangeRepeat();
            if (!_gemstoneExchangeChanged)
            {
                return;
            }

            _gemstoneExchangeChanged = false;
            SaveProgressionState();
            if (!refreshUi)
            {
                return;
            }

            BuildNodes(false);
            RefreshSummary();
            RefreshGemstonePanel();
            RefreshCombatStatsPanel();
            UpdateHeaderForActiveView();
            SetTestStatus("상위 젬스톤으로 교환했습니다.");
        }

        private IEnumerator RepeatGemstoneExchangeAfterDelay(
            int sourceIndex,
            int destinationIndex)
        {
            yield return new WaitForSecondsRealtime(0.32f);
            while (_gemstoneExchangePointerHeld)
            {
                if (!TryExchangeGemstoneOnce(sourceIndex, destinationIndex))
                {
                    _gemstoneExchangePointerHeld = false;
                    break;
                }
                yield return new WaitForSecondsRealtime(0.09f);
            }
            _gemstoneExchangeRepeat = null;
        }

        private bool TryExchangeGemstoneOnce(int sourceIndex, int destinationIndex)
        {
            IReadOnlyList<TraitGemstoneDefinition> gemstones =
                _activeCatalog == null ? null : _activeCatalog.Gemstones;
            if (gemstones == null || sourceIndex < 0 ||
                sourceIndex + 1 >= gemstones.Count ||
                destinationIndex != sourceIndex + 1 ||
                gemstones[sourceIndex] == null || gemstones[destinationIndex] == null ||
                GetGemstoneBalance(sourceIndex) < 100L ||
                GetGemstoneBalance(destinationIndex) == long.MaxValue)
            {
                return false;
            }

            SetGemstoneBalance(sourceIndex, GetGemstoneBalance(sourceIndex) - 100L);
            SetGemstoneBalance(
                destinationIndex,
                AddSaturating(GetGemstoneBalance(destinationIndex), 1L));
            UpdateGemstoneBalanceLabel(sourceIndex);
            UpdateGemstoneBalanceLabel(destinationIndex);
            _gemstoneExchangeChanged = true;
            return true;
        }

        private void StopGemstoneExchangeRepeat()
        {
            if (_gemstoneExchangeRepeat == null)
            {
                return;
            }

            StopCoroutine(_gemstoneExchangeRepeat);
            _gemstoneExchangeRepeat = null;
        }

        private void UpdateGemstoneBalanceLabel(int index)
        {
            if (_gemstoneBalanceLabels.TryGetValue(index, out TextMeshProUGUI label) &&
                label != null)
            {
                label.text = GetGemstoneBalance(index).ToString("N0", CultureInfo.InvariantCulture);
            }
        }

        private void SetGemstoneBalance(int index, long amount)
        {
            if (index == 0)
            {
                _garnetBalance = Math.Max(0L, amount);
                EnsureGemstoneBalanceCapacity();
                if (_gemstoneBalances.Length > 0)
                {
                    _gemstoneBalances[0] = _garnetBalance;
                }
                return;
            }

            EnsureGemstoneBalanceCapacity();
            if (index >= 0 && index < _gemstoneBalances.Length)
            {
                _gemstoneBalances[index] = Math.Max(0L, amount);
            }
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
                CreateRect("Label", row, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(44f, 0f), new Vector2(-136f, 0f), new Vector2(0f, 0.5f)),
                GetGemstoneLocalizedName(gemstone.Id, gemstone.Title),
                14,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            label.color = Color.Lerp(inkColor, accent, 0.25f);

            long balance = GetGemstoneBalance(gemstoneIndex);
            TextMeshProUGUI value = CreateText(
                CreateRect("Value", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-70f, 0f), new Vector2(60f, 30f), new Vector2(1f, 0.5f)),
                balance.ToString("N0", CultureInfo.InvariantCulture),
                15,
                TextAnchor.MiddleRight,
                inkColor,
                true);
            _gemstoneBalanceLabels[gemstoneIndex] = value;
            if (gemstoneIndex == 0)
            {
                _garnetValue = value;
            }

            IReadOnlyList<TraitGemstoneDefinition> gemstones =
                _activeCatalog == null ? null : _activeCatalog.Gemstones;
            int destinationIndex = gemstoneIndex + 1;
            if (gemstones != null && destinationIndex < gemstones.Count &&
                gemstones[destinationIndex] != null)
            {
                RectTransform exchangeRect = CreateRect(
                    "ExchangeToNextTier",
                    row,
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(-18f, 0f),
                    new Vector2(30f, 30f),
                    new Vector2(0.5f, 0.5f));
                Image exchangeImage = exchangeRect.gameObject.AddComponent<Image>();
                exchangeImage.sprite = gemstoneExchangeIcon;
                exchangeImage.preserveAspect = true;
                exchangeImage.raycastTarget = true;
                exchangeImage.color = balance >= 100L
                    ? Color.white
                    : new Color(0.53f, 0.59f, 0.66f, 0.62f);

                Button exchangeButton = exchangeRect.gameObject.AddComponent<Button>();
                exchangeButton.targetGraphic = exchangeImage;
                exchangeButton.interactable = balance >= 100L;
                exchangeButton.transition = Selectable.Transition.ColorTint;
                exchangeButton.colors = new ColorBlock
                {
                    normalColor = exchangeImage.color,
                    highlightedColor = Color.white,
                    pressedColor = new Color(0.76f, 0.9f, 1f, 1f),
                    selectedColor = Color.white,
                    disabledColor = new Color(0.4f, 0.44f, 0.5f, 0.45f),
                    colorMultiplier = 1f,
                    fadeDuration = 0.08f
                };
                exchangeButton.gameObject.AddComponent<TraitUiButtonFeedback>()
                    .SetScaleFeedback(1.12f, 0.88f, 0.1f);
                exchangeButton.gameObject.AddComponent<TraitGemstoneExchangeHoldInput>()
                    .Bind(this, gemstoneIndex, destinationIndex);
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
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.decelerationRate = 0.18f;
            scroll.elasticity = 0.12f;
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

            RectTransform headerBackground = CreateImageRect(
                "DetailHeaderBackground",
                panel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -12f),
                new Vector2(-16f, 168f),
                null,
                new Color(0.055f, 0.12f, 0.19f, 0.96f),
                new Vector2(0.5f, 1f));
            _selectedHeaderBackground = headerBackground.GetComponent<Image>();
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
            CreateText(heading, "선택한 항목", 15, TextAnchor.MiddleLeft, mutedInkColor, true);

            _selectedTitle = CreateText(
                CreateRect("SelectedTitle", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -88f), new Vector2(-36f, 54f), new Vector2(0.5f, 1f)),
                "Trait",
                25,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            _selectedValue = CreateText(
                CreateRect("SelectedValue", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -132f), new Vector2(-36f, 44f), new Vector2(0.5f, 1f)),
                "",
                15,
                TextAnchor.MiddleLeft,
                new Color(0.91f, 0.95f, 1f, 1f),
                false);
            _selectedValue.richText = true;
            _selectedValue.textWrappingMode = TextWrappingModes.NoWrap;
            _selectedDescription = CreateText(
                CreateRect("SelectedDescription", panel, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 42f), new Vector2(-36f, 120f), new Vector2(0.5f, 0.5f)),
                "",
                16,
                TextAnchor.UpperLeft,
                new Color(0.79f, 0.86f, 0.94f, 1f),
                false);

            _selectedDescription.textWrappingMode = TextWrappingModes.Normal;
            _selectedDescription.enableAutoSizing = true;
            _selectedDescription.fontSizeMin = 12f;
            _selectedDescription.fontSizeMax = 16f;
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
                new Vector2(0f, 10f),
                new Vector2(169f, 112f),
                backButtonSprite,
                Color.white,
                string.Empty,
                17,
                inkColor);
            RectTransform backRect = back.transform as RectTransform;
            if (backRect != null)
            {
                backRect.pivot = Vector2.zero;
                backRect.anchoredPosition = new Vector2(0f, 10f);
                RectTransform iconRect = CreateImageRect(
                    "Icon",
                    backRect,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(1.9f, -2.2f),
                    new Vector2(74f, 67f),
                    backIcon,
                    Color.white,
                    new Vector2(0.5f, 0.5f));
                iconRect.GetComponent<Image>().preserveAspect = true;
            }
            back.onClick.AddListener(RequestBack);

            Button combat = CreateButton(
                "EnterCombatButton",
                _runtimeRoot,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 18f),
                new Vector2(184f, 48f),
                tabFocusSprite != null ? tabFocusSprite : panelSprite,
                new Color(0.88f, 0.39f, 0.08f, 1f),
                "전투 시작",
                18,
                inkColor);
            combat.onClick.AddListener(RequestCombat);

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
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(392f, 372f),
                null,
                new Color(0.008f, 0.014f, 0.028f, 1f),
                new Vector2(0f, 1f));
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
                new Color(0.12f, 0.18f, 0.29f, 0.92f),
                "×",
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
                    new Vector2(-36f, 96f),
                    new Vector2(0.5f, 1f)),
                "상세 설명",
                16,
                TextAnchor.UpperLeft,
                new Color(0.91f, 0.95f, 1f, 1f),
                false);
            _tooltipDescription.textWrappingMode = TextWrappingModes.Normal;
            _tooltipDescription.enableAutoSizing = true;
            _tooltipDescription.fontSizeMin = 13f;
            _tooltipDescription.fontSizeMax = 16f;
            _tooltipDescription.overflowMode = TextOverflowModes.Overflow;

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
            CreateImageRect(
                "TooltipCostBand",
                _tooltipRoot,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 104f),
                new Vector2(-28f, 42f),
                panelSprite,
                new Color(0.28f, 0.19f, 0.06f, 0.58f),
                new Vector2(0.5f, 0f));
            _tooltipCost = CreateText(
                CreateRect(
                    "TooltipCost",
                    _tooltipRoot,
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(10f, 111f),
                    new Vector2(-64f, 30f),
                    new Vector2(0.5f, 0f)),
                "비용",
                17,
                TextAnchor.MiddleLeft,
                inkColor,
                true);
            _tooltipCost.enableAutoSizing = true;
            _tooltipCost.fontSizeMin = 14f;
            _tooltipCost.fontSizeMax = 17f;
            RectTransform currencyIconRect = CreateImageRect(
                "TooltipCurrencyIcon",
                _tooltipRoot,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(28f, 125f),
                new Vector2(28f, 28f),
                null,
                Color.white,
                new Vector2(0.5f, 0.5f));
            _tooltipCurrencyIcon = currencyIconRect.GetComponent<Image>();
            _tooltipCurrencyIcon.preserveAspect = true;
            _tooltipCurrencyIcon.raycastTarget = false;
            _tooltipCurrencyIcon.gameObject.SetActive(false);
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
                    GetMonsterBehaviorLabel(monster.id, monster.behaviorType));
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

        private static string GetMonsterBehaviorLabel(
            string monsterId,
            MonsterBehaviorType behaviorType)
        {
            switch (monsterId)
            {
                case "monster.01": return "기본 몬스터";
                case "monster.02": return "소형 · 시각·피격 영역 0.62배";
                case "monster.03": return "고속 · 이동 속도 1.8배";
                case "monster.04": return "은신 · 4초마다 1.2초";
                case "monster.05": return "방패 · 4.5초마다 1.7초, 피해 65% 감소";
                case "monster.06": return "순간이동 · 3.5초마다";
                case "monster.07": return "고체력 · 체력 2.5배";
                case "monster.08": return "대형 · 시각 2배, 피격 영역 1.8배";
                case "monster.09": return "회복 · 3.5초마다 반경 2.75, 아군 체력 20%";
                case "monster.10": return "부활 · 4초마다 반경 3.5, 아군 체력 30%";
            }

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
            HideNodeTooltip();
            CancelRelicReveal();
            CloseRelicDetail();
            _showRelicCollection = false;
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
            CloseRelicDetail();
            _showRelicCollection = true;
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

        private void RefreshCollectionTabButtons()
        {
            SetCollectionTabButtonState(_researchCategoryButton, !_showRelicCollection);
            SetCollectionTabButtonState(_relicCategoryButton, _showRelicCollection);
        }

        private void SetCollectionTabButtonState(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            Color selectedColor = new Color(1f, 0.69f, 0.18f, 1f);
            Color idleColor = new Color(0.78f, 0.83f, 0.90f, 1f);
            if (image != null)
            {
                image.sprite = selected && tabFocusSprite != null
                    ? tabFocusSprite
                    : tabSprite;
                image.color = selected ? selectedColor : idleColor;
            }

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.color = selected
                    ? new Color(0.16f, 0.10f, 0.05f, 1f)
                    : new Color(0.14f, 0.18f, 0.25f, 1f);
            }

            button.interactable = !selected;
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? selectedColor : idleColor;
            colors.highlightedColor = Color.Lerp(colors.normalColor, Color.white, 0.12f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(colors.normalColor, Color.black, 0.10f);
            colors.disabledColor = selected ? selectedColor : idleColor;
            button.colors = colors;
        }

        private void UpdateHeaderForActiveView()
        {
            if (_screenTitle != null)
            {
                _screenTitle.text = _showRelicCollection ? "유물 보관소" : "특성";
            }

        }

        private void SetCollectionChromeVisible(bool visible)
        {
            if (_runtimeRoot == null)
            {
                return;
            }

            Transform legend = _runtimeRoot.Find("BoardLegend");
            if (legend != null)
            {
                legend.gameObject.SetActive(visible);
            }

            Transform testToolsToggle = _runtimeRoot.Find("TestToolsToggle");
            if (testToolsToggle != null)
            {
                testToolsToggle.gameObject.SetActive(true);
            }

            if (_relicTestModeButton != null)
            {
                _relicTestModeButton.gameObject.SetActive(!visible);
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

            BuildUnifiedBoard(animate);
        }

        private void BuildRelicGrid()
        {
            if (_nodeContent == null || _nodeViewport == null)
            {
                return;
            }

            EnsureRelicEntries();
            HideNodeTooltip();
            DestroyChildren(_nodeContent);
            _boardPositionsByNodeId.Clear();
            _boardNodeRectsById.Clear();
            Canvas.ForceUpdateCanvases();

            float viewportWidth = Mathf.Max(640f, _nodeViewport.rect.width);
            float viewportHeight = Mathf.Max(360f, _nodeViewport.rect.height);
            _nodeContent.localScale = Vector3.one;

            const int columns = 4;
            const int rows = 5;
            const float walletClearance = 352f;
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

            PlayPendingRelicReveal();
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

        private void PlayPendingRelicReveal()
        {
            if (_pendingRelicRevealIndex < 0 || _relicRevealRoutine != null ||
                _pendingRelicRevealIndex >= relicEntries.Count)
            {
                return;
            }

            ApplyRelicPresentationDefaults();
            RelicDisplayEntry entry = relicEntries[_pendingRelicRevealIndex];
            RectTransform card = _nodeContent.Find(
                "RelicSlot_" + (_pendingRelicRevealIndex + 1).ToString("00", CultureInfo.InvariantCulture))
                as RectTransform;
            if (entry == null || !entry.IsObtained || card == null)
            {
                return;
            }

            int index = _pendingRelicRevealIndex;
            _pendingRelicRevealIndex = -1;
            TraitAudioFeedback.PlayRelicReveal();
            _relicRevealRoutine = StartCoroutine(AnimateRelicReveal(index, card, entry));
        }

        private IEnumerator AnimateRelicReveal(
            int index,
            RectTransform card,
            RelicDisplayEntry entry)
        {
            float availableHeight = Mathf.Max(300f, _runtimeRoot.rect.height - 64f);
            float overlayHeight = Mathf.Min(720f, availableHeight * 0.84f);
            float overlayWidth = overlayHeight * RelicDetailCardAspect;
            float availableWidth = Mathf.Max(240f, _runtimeRoot.rect.width - 48f);
            if (overlayWidth > availableWidth)
            {
                overlayWidth = availableWidth;
                overlayHeight = overlayWidth / RelicDetailCardAspect;
            }

            RectTransform overlayRoot = CreateRect(
                "RelicRevealOverlay",
                _runtimeRoot,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Vector2(0.5f, 0.5f));
            Image dimmer = overlayRoot.gameObject.AddComponent<Image>();
            dimmer.color = new Color(0.008f, 0.018f, 0.042f, 0.76f);
            dimmer.raycastTarget = true;
            Button backdropButton = overlayRoot.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = dimmer;
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.onClick.AddListener(RequestRelicRevealDismiss);
            overlayRoot.gameObject
                .AddComponent<TraitRelicRevealDismissRelay>()
                .Bind(RequestRelicRevealDismiss);
            CanvasGroup group = overlayRoot.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;
            _relicRevealDismissRequested = false;

            RectTransform burst = CreateRect(
                "RelicRevealBurst",
                overlayRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(overlayWidth * 1.16f, overlayHeight * 0.92f),
                new Vector2(0.5f, 0.5f));
            CanvasGroup burstGroup = burst.gameObject.AddComponent<CanvasGroup>();
            burstGroup.alpha = 0f;
            RectTransform glow = CreateImageRect(
                "RelicRevealGlow",
                burst,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(overlayWidth * 0.58f, overlayHeight * 0.44f),
                relicRevealGlowSprite,
                new Color(1f, 0.91f, 0.70f, 0.16f),
                new Vector2(0.5f, 0.5f));
            glow.GetComponent<Image>().raycastTarget = false;

            RectTransform flash = CreateImageRect(
                "RelicRevealFlash",
                burst,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(58f, 76f),
                relicRevealSparkleSprite,
                new Color(1f, 0.96f, 0.84f, 0.46f),
                new Vector2(0.5f, 0.5f));
            flash.GetComponent<Image>().preserveAspect = true;

            const int sparkCount = 12;
            var revealSparks = new List<RectTransform>(sparkCount);
            var revealSparkTargets = new List<Vector2>(sparkCount);
            var revealSparkBaseRotations = new List<float>(sparkCount);
            for (int sparkIndex = 0; sparkIndex < sparkCount; sparkIndex++)
            {
                float angle = (360f / sparkCount) * sparkIndex +
                    (sparkIndex % 2 == 0 ? -8f : 11f);
                float radians = angle * Mathf.Deg2Rad;
                float radius = overlayHeight * (sparkIndex % 2 == 0 ? 0.32f : 0.26f);
                Vector2 target = new Vector2(
                    Mathf.Cos(radians) * radius,
                    Mathf.Sin(radians) * radius);
                Sprite sparkSprite = sparkIndex % 3 == 0
                    ? relicRevealShardSprite
                    : relicRevealSparkleSprite;
                RectTransform spark = CreateImageRect(
                    "RevealSpark_" + sparkIndex.ToString("00", CultureInfo.InvariantCulture),
                    burst,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    sparkIndex % 3 == 0 ? new Vector2(8f, 8f) : new Vector2(15f, 19f),
                    sparkSprite,
                    sparkIndex % 4 == 0
                        ? new Color(1f, 0.98f, 0.88f, 0.82f)
                        : new Color(0.80f, 0.89f, 1f, 0.62f),
                    new Vector2(0.5f, 0.5f));
                spark.GetComponent<Image>().preserveAspect = true;
                revealSparks.Add(spark);
                revealSparkTargets.Add(target);
                revealSparkBaseRotations.Add(angle);
            }

            RectTransform overlay = CreatePanel(
                "RelicRevealFocus",
                overlayRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(overlayWidth, overlayHeight),
                panelSprite,
                new Color(0.24f, 0.13f, 0.43f, 0.99f),
                new Vector2(0.5f, 0.5f));
            overlayRoot.SetAsLastSibling();
            _relicRevealOverlay = overlayRoot.gameObject;
            Image cardImage = overlay.GetComponent<Image>();
            cardImage.raycastTarget = true;
            Button cardClickBlocker = overlay.gameObject.AddComponent<Button>();
            cardClickBlocker.targetGraphic = cardImage;
            cardClickBlocker.transition = Selectable.Transition.None;
            cardClickBlocker.onClick.AddListener(() => { });
            CreateSquareNodeFrame(overlay, new Color(1f, 0.75f, 0.30f, 0.98f), 7f);
            CreateSquareNodeFrame(overlay, new Color(1f, 0.88f, 0.54f, 0.30f), 15f);
            Outline revealOutline = overlay.GetComponent<Outline>();
            if (revealOutline != null)
            {
                revealOutline.effectColor = new Color(1f, 0.84f, 0.48f, 0.98f);
                revealOutline.effectDistance = new Vector2(4f, -4f);
            }

            CreateText(
                CreateRect(
                    "RevealCaption",
                    overlay,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, -overlayHeight * 0.065f),
                    new Vector2(-40f, 40f),
                    new Vector2(0.5f, 1f)),
                "새 유물 획득",
                19,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.84f, 0.53f, 1f),
                true);

            CreateText(
                CreateRect(
                    "RevealTitle",
                    overlay,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, -overlayHeight * 0.17f),
                    new Vector2(-48f, 64f),
                    new Vector2(0.5f, 1f)),
                string.IsNullOrWhiteSpace(entry.Title)
                    ? "유물 " + (index + 1).ToString("00", CultureInfo.InvariantCulture)
                    : entry.Title,
                31,
                TextAnchor.MiddleCenter,
                inkColor,
                true);

            if (entry.Icon != null)
            {
                RectTransform icon = CreateImageRect(
                    "RevealIcon",
                    overlay,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, overlayHeight * 0.12f),
                    new Vector2(overlayWidth * 0.48f, overlayHeight * 0.30f),
                    entry.Icon,
                    Color.white,
                    new Vector2(0.5f, 0.5f));
                icon.GetComponent<Image>().preserveAspect = true;
            }
            else
            {
                CreateText(
                    CreateRect(
                        "RevealPlaceholder",
                        overlay,
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0f, overlayHeight * 0.12f),
                        new Vector2(overlayWidth * 0.48f, overlayHeight * 0.30f),
                        new Vector2(0.5f, 0.5f)),
                    "?",
                    64,
                    TextAnchor.MiddleCenter,
                    new Color(1f, 0.82f, 0.42f, 1f),
                    true);
            }

            CreateText(
                CreateRect(
                    "RevealDescription",
                    overlay,
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0.42f),
                    new Vector2(0f, overlayHeight * 0.025f),
                    new Vector2(-48f, -24f),
                    new Vector2(0.5f, 0f)),
                string.IsNullOrWhiteSpace(entry.Description)
                    ? "효과 정보 준비 중입니다."
                    : entry.Description,
                21,
                TextAnchor.UpperCenter,
                inkColor,
                false);

            Button confirmButton = CreateButton(
                "RelicRevealConfirm",
                overlay,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, overlayHeight * 0.075f),
                new Vector2(Mathf.Min(overlayWidth - 72f, 280f), 58f),
                null,
                new Color(0.43f, 0.25f, 0.08f, 1f),
                "획득",
                21,
                inkColor);
            confirmButton.onClick.AddListener(RequestRelicRevealDismiss);
            confirmButton.gameObject
                .AddComponent<TraitRelicRevealDismissRelay>()
                .Bind(RequestRelicRevealDismiss);

            const float appearDuration = 0.72f;
            float elapsed = 0f;
            while (elapsed < appearDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / appearDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                group.alpha = eased;
                float pop = t < 0.72f
                    ? Mathf.Lerp(0.48f, 1.12f, 1f - Mathf.Pow(1f - t / 0.72f, 3f))
                    : Mathf.Lerp(1.12f, 1f, (t - 0.72f) / 0.28f);
                overlay.localScale = Vector3.one * pop;
                burstGroup.alpha = Mathf.SmoothStep(0f, 0.72f, Mathf.Min(1f, t * 2f));
                float spread = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 1.5f));
                burst.localScale = Vector3.one * Mathf.Lerp(0.88f, 1.02f, spread);
                burst.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-8f, 2f, eased));
                flash.localScale = Vector3.one * Mathf.Lerp(0.48f, 0.82f, spread);
                flash.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-9f, 7f, eased));
                for (int sparkIndex = 0; sparkIndex < revealSparks.Count; sparkIndex++)
                {
                    RectTransform spark = revealSparks[sparkIndex];
                    spark.anchoredPosition = revealSparkTargets[sparkIndex] * spread;
                    float sparkScale = Mathf.Lerp(0.15f, 1f, spread);
                    spark.localScale = Vector3.one * sparkScale;
                    spark.localRotation = Quaternion.Euler(
                        0f,
                        0f,
                        revealSparkBaseRotations[sparkIndex] + t * 32f);
                }
                yield return null;
            }

            confirmButton.interactable = true;
            while (!_relicRevealDismissRequested && _relicRevealOverlay != null)
            {
                overlay.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 2.4f) * 0.012f);
                burst.localRotation *= Quaternion.Euler(0f, 0f, Time.unscaledDeltaTime * 7f);
                flash.localRotation *= Quaternion.Euler(0f, 0f, Time.unscaledDeltaTime * 3f);
                yield return null;
            }

            backdropButton.interactable = false;
            confirmButton.interactable = false;
            Vector3[] corners = new Vector3[4];
            card.GetWorldCorners(corners);
            Vector2 targetScreen = RectTransformUtility.WorldToScreenPoint(
                GetUiCamera(),
                (corners[0] + corners[2]) * 0.5f);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _runtimeRoot,
                    targetScreen,
                    GetUiCamera(),
                    out Vector2 targetLocal))
            {
                targetLocal = card.anchoredPosition;
            }

            float targetScale = Mathf.Clamp(
                Mathf.Min(card.rect.width / overlayWidth, card.rect.height / overlayHeight),
                0.18f,
                0.72f);
            Vector2 startPosition = overlay.anchoredPosition;
            const float travelDuration = 0.42f;
            elapsed = 0f;
            while (elapsed < travelDuration && card != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                overlay.anchoredPosition = Vector2.Lerp(startPosition, targetLocal, eased);
                overlay.localScale = Vector3.one * Mathf.Lerp(1f, targetScale, eased);
                burst.localScale = Vector3.one * Mathf.Lerp(1.08f, 0.28f, eased);
                burstGroup.alpha = 1f - eased;
                group.alpha = 1f - eased;
                yield return null;
            }

            if (card != null)
            {
                Outline outline = card.GetComponent<Outline>();
                Color previousOutline = outline == null ? Color.clear : outline.effectColor;
                Vector3 previousScale = card.localScale;
                _relicRevealTarget = card;
                _relicRevealTargetOutline = outline;
                _relicRevealTargetOriginalScale = previousScale;
                _relicRevealTargetOriginalOutlineColor = previousOutline;
                if (outline != null)
                {
                    outline.effectColor = new Color(1f, 0.79f, 0.32f, 1f);
                }

                const float cardPulseDuration = 0.42f;
                elapsed = 0f;
                while (elapsed < cardPulseDuration && card != null)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / cardPulseDuration);
                    float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f;
                    card.localScale = previousScale * pulse;
                    if (outline != null)
                    {
                        Color focus = new Color(1f, 0.78f, 0.31f, 1f - t * 0.4f);
                        outline.effectColor = Color.Lerp(previousOutline, focus, 1f - t * 0.2f);
                    }
                    yield return null;
                }

                if (card != null) card.localScale = previousScale;
                if (outline != null) outline.effectColor = previousOutline;
                _relicRevealTarget = null;
                _relicRevealTargetOutline = null;
            }

            if (_relicRevealOverlay != null)
            {
                Destroy(_relicRevealOverlay);
                _relicRevealOverlay = null;
            }

            _relicRevealRoutine = null;
            _relicRevealDismissRequested = false;
            PlayPendingRelicReveal();
        }

        private void RequestRelicRevealDismiss()
        {
            if (_relicRevealOverlay == null)
            {
                return;
            }

            // Hide the modal synchronously. The reveal coroutine may be paused
            // or unable to finish its return animation in a test session.
            CancelRelicReveal();
            PlayPendingRelicReveal();
        }

        private void CancelRelicReveal()
        {
            if (_relicRevealRoutine != null)
            {
                StopCoroutine(_relicRevealRoutine);
                _relicRevealRoutine = null;
            }

            if (_relicRevealTarget != null)
            {
                _relicRevealTarget.localScale = _relicRevealTargetOriginalScale;
            }
            if (_relicRevealTargetOutline != null)
            {
                _relicRevealTargetOutline.effectColor = _relicRevealTargetOriginalOutlineColor;
            }
            _relicRevealTarget = null;
            _relicRevealTargetOutline = null;

            if (_relicRevealOverlay != null)
            {
                GameObject overlay = _relicRevealOverlay;
                _relicRevealOverlay = null;
                overlay.SetActive(false);
                Destroy(overlay);
            }

            _relicRevealDismissRequested = false;
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
            var visible = new List<BoardNodeEntry>(ordered.Count);
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
                TraitNodeRightClickRelay rightClick =
                    button.gameObject.AddComponent<TraitNodeRightClickRelay>();
                rightClick.Bind(this, captured.Id);
                TraitUiButtonFeedback feedback = button.GetComponent<TraitUiButtonFeedback>();
                if (feedback != null)
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

            UpdateStarterTutorialPresentation();
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

            return _purchased.Contains(ResearchCoreNodeId) &&
                IsPrerequisiteMet(node) &&
                HasRequiredBossTierUnlockedThroughChain(node);
        }

        private bool HasRequiredBossTierUnlockedThroughChain(TraitNodeDefinition node)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            TraitNodeDefinition current = node;
            while (current != null && visited.Add(current.Id))
            {
                if (current.RequiredBossTier > unlockedBossTier) return false;
                current = string.IsNullOrEmpty(current.PrerequisiteNodeId)
                    ? null
                    : FindNode(current.PrerequisiteNodeId);
            }

            return true;
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
                GetBoardUnlockTiers(remaining[i], byId, out int difficultyTier, out int bossGateTier);
                remaining[i].DifficultyTier = difficultyTier;
                remaining[i].BossGateTier = bossGateTier;
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
            int tierOrder = left.DifficultyTier.CompareTo(right.DifficultyTier);
            if (tierOrder != 0)
            {
                return tierOrder;
            }

            // At a shared gemstone tier, place direct boss-gated unlocks first
            // so the rewards from the next boss remain close to the core.
            int bossGateOrder = right.BossGateTier.CompareTo(left.BossGateTier);
            if (bossGateOrder != 0)
            {
                return bossGateOrder;
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
            return scoreOrder != 0
                ? scoreOrder
                : string.Compare(left.Node.Id, right.Node.Id, StringComparison.Ordinal);
        }

        private void GetBoardUnlockTiers(
            BoardNodeEntry entry,
            Dictionary<string, BoardNodeEntry> entriesById,
            out int difficultyTier,
            out int bossGateTier)
        {
            difficultyTier = 0;
            bossGateTier = 0;
            if (entry == null || entry.Node == null)
            {
                return;
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            string currentId = entry.Node.Id;
            while (!string.IsNullOrEmpty(currentId) && visited.Add(currentId) &&
                entriesById.TryGetValue(currentId, out BoardNodeEntry current))
            {
                bossGateTier = Mathf.Max(bossGateTier, current.Node.RequiredBossTier);
                int gemstoneTier = Mathf.Max(
                    0,
                    GetGemstoneIndexById(current.Node.CostGemstoneId));
                difficultyTier = Mathf.Max(
                    difficultyTier,
                    Mathf.Max(current.Node.RequiredBossTier, gemstoneTier));
                currentId = current.Node.PrerequisiteNodeId;
            }

            difficultyTier = Mathf.Max(difficultyTier, bossGateTier);
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
            bool currencyUnlocked = testModeUnlockAll || testModeFreeUpgrades || node.Cost <= 0 ||
                IsGemstoneAvailable(gemstone) || GetNodeBalance(node) > 0;
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

        private Color GetBoardNodeRankColor(BoardNodeEntry entry)
        {
            if (entry == null || entry.Node == null)
            {
                return GetRankFrameColor(1);
            }

            TraitNodeDefinition node = entry.Node;
            return IsUpgradeResearchNode(node)
                ? GetUpgradeRankFrameColor(node)
                : GetRankFrameColor(node.RequiredBossTier + 1);
        }

        private Button CreateBoardNode(BoardNodeEntry entry, NodePurchaseState state)
        {
            Color rankColor = GetBoardNodeRankColor(entry);
            bool purchased = state == NodePurchaseState.Purchased;
            bool available = state == NodePurchaseState.Available;
            bool finalUpgrade = purchased && IsFinalUpgradeNode(entry.Node);
            bool isCore = string.Equals(
                entry.Node.Id,
                "stat.attack.01",
                StringComparison.Ordinal);
            Color fill = purchased
                ? new Color(0.14f, 0.16f, 0.20f, 0.98f)
                : new Color(0.018f, 0.024f, 0.038f, 0.99f);

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
                new Color(rankColor.r, rankColor.g, rankColor.b, 0.20f),
                isCore ? 11f : 9f,
                finalUpgrade);
            CreateSquareNodeFrame(buttonRect, rankColor, isCore ? 5f : 4.5f, finalUpgrade);
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
                if (finalUpgrade)
                {
                    marker = "최종";
                }
                else if (entry.Tab == TraitTab.Monster)
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
                ? Mathf.Clamp(requestedZoom, MinimumGraphZoom, MaximumGraphZoom)
                : Mathf.Clamp(fitZoom, MinimumGraphZoom, 1.05f);
            _nodeContent.localScale = Vector3.one * zoom;
            _nodeContent.anchoredPosition = Vector2.zero;
            PositionNodeTooltip(FindNode(_tooltipNodeId));
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
            else if (_starterTutorialActive)
            {
                TraitNodeDefinition next = GetNextStarterTutorialNode();
                if (next != null && _boardPositionsByNodeId.TryGetValue(
                        next.Id,
                        out Vector2 focusPosition))
                {
                    CenterGraphOnNode(focusPosition, StarterTutorialZoom);
                }
                else
                {
                    CenterUnifiedGraph(StatGraphDefaultZoom);
                }
                UpdateStarterTutorialPresentation();
            }
            else
            {
                CenterUnifiedGraph();
            }
            _fitGraphRoutine = null;
        }

        private void SetGraphZoom(float zoom)
        {
            if (_showRelicCollection || _nodeContent == null || _nodeViewport == null)
            {
                return;
            }

            Vector2 viewportCenter = Vector2.zero;
            float oldZoom = Mathf.Max(0.01f, _nodeContent.localScale.x);
            Vector2 contentPointAtCenter = (viewportCenter - _nodeContent.anchoredPosition) / oldZoom;
            float safeZoom = Mathf.Clamp(zoom, MinimumGraphZoom, MaximumGraphZoom);
            _nodeContent.localScale = Vector3.one * safeZoom;
            _nodeContent.anchoredPosition = viewportCenter - contentPointAtCenter * safeZoom;
            PositionNodeTooltip(FindNode(_tooltipNodeId));
        }

        internal void ZoomGraphAt(Vector2 screenPosition, float wheelDelta)
        {
            if (_showRelicCollection || _nodeContent == null || _nodeViewport == null ||
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
                previousZoom * Mathf.Pow(1.055f, wheelDelta),
                MinimumGraphZoom,
                MaximumGraphZoom);
            _nodeContent.localScale = Vector3.one * nextZoom;
            _nodeContent.anchoredPosition = viewportPoint - contentPoint * nextZoom;
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

        private static string GetCategoryTitle(TraitCategoryDefinition category)
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
            bool finalUpgrade = purchased && IsFinalUpgradeNode(node);
            CreateSquareNodeFrame(
                button.transform as RectTransform,
                GetNodeFrameColor(node, _activeTab),
                3.5f,
                finalUpgrade);

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
                string rank = finalUpgrade ? "최종" : GetNodeRankLabel(node);
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
                new Vector2(-3f, 3f),
                new Vector2(compactTreeNode ? 20f : 22f, compactTreeNode ? 20f : 22f),
                affordableNodeBadgeSprite,
                Color.white,
                new Vector2(0f, 1f));
            Image badgeImage = badge.GetComponent<Image>();
            badgeImage.raycastTarget = false;
            badgeImage.preserveAspect = true;
            if (affordableNodeBadgeSprite == null)
            {
                badgeImage.color = new Color(1f, 0.80f, 0.22f, 1f);
                Outline outline = badge.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.20f, 0.10f, 0.01f, 0.96f);
                outline.effectDistance = new Vector2(1.2f, -1.2f);
                TextMeshProUGUI mark = CreateText(
                    CreateRect("Mark", badge, Vector2.zero, Vector2.one,
                        Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)),
                    "!",
                    13,
                    TextAnchor.MiddleCenter,
                    new Color(0.19f, 0.10f, 0.02f, 1f),
                    true);
                mark.raycastTarget = false;
            }
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

            float safeThickness = Mathf.Max(1f, thickness);
            Color topColor = rainbow ? WithAlpha(new Color(1f, 0.25f, 0.36f, 1f), color.a) : color;
            Color rightColor = rainbow ? WithAlpha(new Color(1f, 0.75f, 0.20f, 1f), color.a) : color;
            Color bottomColor = rainbow ? WithAlpha(new Color(0.20f, 0.82f, 1f, 1f), color.a) : color;
            Color leftColor = rainbow ? WithAlpha(new Color(0.70f, 0.40f, 1f, 1f), color.a) : color;
            CreateImageRect(
                "FrameTop",
                parent,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -safeThickness * 0.5f),
                new Vector2(0f, safeThickness),
                null,
                topColor,
                new Vector2(0.5f, 0.5f));
            CreateImageRect(
                "FrameBottom",
                parent,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, safeThickness * 0.5f),
                new Vector2(0f, safeThickness),
                null,
                bottomColor,
                new Vector2(0.5f, 0.5f));
            CreateImageRect(
                "FrameLeft",
                parent,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(safeThickness * 0.5f, 0f),
                new Vector2(safeThickness, 0f),
                null,
                leftColor,
                new Vector2(0.5f, 0.5f));
            CreateImageRect(
                "FrameRight",
                parent,
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(-safeThickness * 0.5f, 0f),
                new Vector2(safeThickness, 0f),
                null,
                rightColor,
                new Vector2(0.5f, 0.5f));
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
            return IsUpgradeResearchNode(node)
                ? GetUpgradeRankFrameColor(node)
                : GetRankFrameColor(node.RequiredBossTier + 1);
        }

        private void SelectNode(TraitNodeDefinition node)
        {
            if (node == null)
            {
                return;
            }

            if (_starterTutorialActive)
            {
                TraitNodeDefinition guidedNode = GetNextStarterTutorialNode();
                if (guidedNode == null || !string.Equals(
                        guidedNode.Id,
                        node.Id,
                        StringComparison.Ordinal))
                {
                    return;
                }

                if (IsStarterTutorialRightClickStep(node))
                {
                    ShowNodeTooltip(node);
                }
                else
                {
                    TryUpgradeNode(node);
                }
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

            if (_starterTutorialActive)
            {
                TraitNodeDefinition guidedNode = GetNextStarterTutorialNode();
                if (guidedNode == null || !string.Equals(
                        guidedNode.Id,
                        node.Id,
                        StringComparison.Ordinal))
                {
                    return;
                }
            }

            _selectedNodeId = node.Id;
            HideNodeTooltip();
            UpdateNodeSelectionFrame();
            TryUpgradeNode(node, true);
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

            if (target.name.StartsWith("Node_", StringComparison.Ordinal))
            {
                return;
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
            RefreshStarterTutorialTooltipPresentation();
            if (animate)
            {
                if (_tooltipAnimation != null)
                {
                    StopCoroutine(_tooltipAnimation);
                }
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

            NodePurchaseState state = GetNodePurchaseState(node);
            TraitTab tab = GetNodeTab(node);
            bool concealed = false;
            _tooltipType.text = concealed ? "잠긴 항목" : GetNodeTypeLabel(node, tab);
            _tooltipType.color = concealed
                ? new Color(0.88f, 0.89f, 0.94f, 1f)
                : GetCategoryAccent(tab);
            _tooltipTitle.text = concealed ? "???" : GetLocalizedNodeTitle(node);
            _tooltipValue.text = concealed ? "???" : GetLocalizedNodeValueLabel(node);
            _tooltipDescription.text = concealed
                ? "잠긴 연구입니다.\n해금 조건을 충족하면 상세 정보가 공개됩니다."
                : BuildNodeDescription(node);

            TraitGemstoneDefinition gemstone = GetGemstoneDefinitionById(node.CostGemstoneId);
            int gemstoneIndex = GetGemstoneIndexById(node.CostGemstoneId);
            Sprite currencySprite = !concealed && !node.AcquisitionOnly &&
                node.Cost > 0 && !testModeFreeUpgrades
                ? GetGemstoneIcon(gemstoneIndex)
                : null;
            if (_tooltipCurrencyIcon != null)
            {
                _tooltipCurrencyIcon.sprite = currencySprite;
                _tooltipCurrencyIcon.gameObject.SetActive(currencySprite != null);
            }
            if (_tooltipCost != null)
            {
                RectTransform costRect = _tooltipCost.rectTransform;
                Vector2 costOffsetMin = costRect.offsetMin;
                costOffsetMin.x = currencySprite == null ? 16f : 62f;
                costRect.offsetMin = costOffsetMin;
            }

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
                _tooltipCost.text = "테스트 무료 · 보유 " +
                    GetNodeBalance(node).ToString("N0", CultureInfo.InvariantCulture);
            }
            else
            {
                string currencyName = gemstone == null
                    ? GetGemstoneLocalizedName(node.CostGemstoneId, node.CostGemstoneId)
                    : GetGemstoneLocalizedName(gemstone.Id, gemstone.Title);
                _tooltipCost.text = "필요 " +
                    node.Cost.ToString("N0", CultureInfo.InvariantCulture) + "  " +
                    currencyName + " · 보유 " +
                    GetNodeBalance(node).ToString("N0", CultureInfo.InvariantCulture);
            }

            bool purchased = state == NodePurchaseState.Purchased;
            bool canBuy = state == NodePurchaseState.Available && !node.AcquisitionOnly;
            bool showAction = canBuy || state == NodePurchaseState.Unaffordable;
            bool canReset = purchased && !node.StartsUnlocked && !HasPurchasedDependents(node);
            _tooltipStatus.text = GetTooltipStatus(node, state);
            _tooltipStatus.color = GetBoardStateColor(state);
            bool rightClickPractice = false;
            if (_starterTutorialActive)
            {
                TraitNodeDefinition guidedNode = GetNextStarterTutorialNode();
                bool isGuidedNode = guidedNode != null && string.Equals(
                    guidedNode.Id, node.Id, StringComparison.Ordinal);
                rightClickPractice = isGuidedNode && IsStarterTutorialRightClickStep(node) && canBuy;
                canBuy &= isGuidedNode && !rightClickPractice;
                showAction &= isGuidedNode;
                canReset = false;
                if (rightClickPractice)
                {
                    _tooltipStatus.text = "아이콘을 우클릭해 강화해 보세요.";
                    _tooltipStatus.color = AvailableColor;
                }
            }
            _tooltipUpgradeButton.gameObject.SetActive(showAction);
            _tooltipUpgradeButton.interactable = canBuy;
            if (showAction)
            {
                SetButtonLabel(_tooltipUpgradeLabel, rightClickPractice
                    ? "아이콘 우클릭으로 강화"
                    : GetTooltipActionLabel(node, state));
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

            Color actionColor = rightClickPractice
                ? new Color(0.55f, 0.38f, 0.14f, 1f)
                : canBuy
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
                return "재화 부족";
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

        private string GetTooltipStatus(TraitNodeDefinition node, NodePurchaseState state)
        {
            if (node != null && string.Equals(
                    node.Id,
                    ResearchCoreNodeId,
                    StringComparison.Ordinal))
            {
                return state == NodePurchaseState.Purchased
                    ? "시작 완료 · 네 방향 연구 개방"
                    : "중앙 시작점 · 강화하면 첫 연구 개방";
            }

            if (state == NodePurchaseState.Purchased)
            {
                if (IsRootEntityNode(node.Id, "skill."))
                {
                    return "배움 완료 · 전투 사용 중";
                }
                if (IsRootEntityNode(node.Id, "monster."))
                {
                    return "해금 완료 · 전투 출현 중";
                }
                if (IsFinalUpgradeNode(node))
                {
                    return "최종 강화 완료 · 효과 적용 중";
                }
                return node.StartsUnlocked
                    ? "기본 효과 적용 중"
                    : "강화 완료 · 효과 적용 중";
            }
            if (state == NodePurchaseState.Available)
            {
                if (IsRootEntityNode(node.Id, "skill."))
                {
                    return "배우기 가능 · 배우면 전투에서 사용";
                }
                if (IsRootEntityNode(node.Id, "monster."))
                {
                    return "해금 가능 · 해금 후 전투 출현";
                }
                return "강화 가능 · 적용 즉시 효과 반영";
            }
            if (state == NodePurchaseState.Unaffordable)
            {
                TraitGemstoneDefinition gemstone = GetGemstoneDefinitionById(node.CostGemstoneId);
                if (GetNodeBalance(node) <= 0 && !IsGemstoneAvailable(gemstone))
                {
                    return (gemstone == null
                        ? GetGemstoneLocalizedName(node.CostGemstoneId, node.CostGemstoneId)
                        : GetGemstoneLocalizedName(gemstone.Id, gemstone.Title)) +
                        " 공급 필요";
                }
                return "재화 부족 · " + node.Cost.ToString("N0", CultureInfo.InvariantCulture) +
                    " 필요 / " + GetNodeBalance(node).ToString("N0", CultureInfo.InvariantCulture) + " 보유";
            }
            if (!testModeUnlockAll && node.RequiredBossTier > unlockedBossTier)
            {
                return "잠김 · 보스 " + node.RequiredBossTier + "단계 필요";
            }
            if (!testModeUnlockAll && !IsPrerequisiteMet(node))
            {
                return "잠김 · 선행 연구 완료 필요";
            }

            return node.AcquisitionOnly
                ? "잠김 · 전투에서 획득"
                : "잠김 · 해금 조건 필요";
        }

        private static string GetNodeTypeLabel(TraitNodeDefinition node, TraitTab tab)
        {
            if (node != null && node.Id != null &&
                node.Id.StartsWith("monster.", StringComparison.Ordinal))
            {
                return node.Id.IndexOf(".production.", StringComparison.Ordinal) >= 0
                    ? "몬스터 생산량 증가"
                    : "몬스터 해금";
            }

            if (node != null && node.Id != null &&
                (node.Id.StartsWith("skill.cursorAura", StringComparison.Ordinal) ||
                 node.Id.StartsWith("skill.pet_baby_dragon", StringComparison.Ordinal)))
            {
                return node.Id == "skill.pet_baby_dragon"
                    ? "펫 동료"
                    : "펫 동료 강화";
            }
            if (node != null && node.Id != null &&
                node.Id.StartsWith("skill.", StringComparison.Ordinal) &&
                node.Id != GetEntityRootId(node.Id, "skill."))
            {
                return "스킬 강화";
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
            RefreshStarterTutorialTooltipPresentation();
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
            _selectedTitle.text = concealed ? "???" : GetLocalizedNodeTitle(node);
            _selectedValue.text = concealed
                ? "<size=12><color=#AFC5D8>해금 조건 달성 후 공개</color></size>\n<b>???</b>"
                : BuildNodeChangePreview(node);
            _selectedDescription.text = concealed
                ? "잠긴 연구입니다. 해금 후 효과를 확인할 수 있습니다."
                : BuildNodeDescription(node);
            _selectedCost.text = concealed ? "해금 조건을 먼저 충족하세요" : GetNodeCostText(node);
            _selectedRequirement.text = GetNodeRequirementText(node);

            bool purchased = IsPurchased(node);
            bool lockedByBoss = !testModeUnlockAll &&
                node.RequiredBossTier > unlockedBossTier;
            bool lockedByPrerequisite = !testModeUnlockAll &&
                !IsPrerequisiteMet(node);
            bool currencyUnlocked = testModeUnlockAll || testModeFreeUpgrades || node.Cost <= 0 ||
                IsGemstoneAvailable(GetGemstoneDefinitionById(node.CostGemstoneId)) ||
                GetNodeBalance(node) > 0;
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

        private void TryUpgradeNode(TraitNodeDefinition node, bool viaRightClick = false)
        {
            if (node == null || GetNodePurchaseState(node) != NodePurchaseState.Available ||
                node.AcquisitionOnly)
            {
                ShowNodeTooltip(node);
                return;
            }

            if (_starterTutorialActive)
            {
                TraitNodeDefinition guidedNode = GetNextStarterTutorialNode();
                if (guidedNode == null || !string.Equals(
                        guidedNode.Id,
                        node.Id,
                        StringComparison.Ordinal))
                {
                    return;
                }

                if (IsStarterTutorialRightClickStep(node) && !viaRightClick)
                {
                    ShowNodeTooltip(node);
                    return;
                }
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
            TraitNodeDefinition nextOnboardingNode = _starterTutorialActive
                ? GetNextStarterTutorialNode()
                : null;
            TraitNodeDefinition tooltipNode = nextOnboardingNode ?? node;
            _selectedNodeId = tooltipNode == null ? node.Id : tooltipNode.Id;
            BuildNodes(false);
            RefreshSummary();
            RefreshGemstonePanel();
            UpdateHeaderForActiveView();
            if (_starterTutorialActive)
            {
                HideNodeTooltip();
                UpdateStarterTutorialPresentation();
            }
            else
            {
                RefreshNodeTooltip(tooltipNode);
            }
            SaveProgressionState();
            PlayNodePurchaseEffect(node.Id);
            TraitAudioFeedback.PlayUpgrade();

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
            TraitProgressionStore.ResetStarterTutorial();
            _starterTutorialActive = true;
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
            UpdateHeaderForActiveView();
            GuideStarterResearch();
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
                return "가운데 시작점을 배우면 네 방향의 첫 연구가 나타납니다. 이후 연구는 보스 진행 단계와 선행 연구에 따라 차례로 열립니다.";
            }

            if (id.StartsWith("stat.", StringComparison.Ordinal))
            {
                if (id.StartsWith("stat.fieldDuration.", StringComparison.Ordinal) &&
                    id.EndsWith(".01", StringComparison.Ordinal))
                {
                    return "일반 전투 제한 시간입니다.";
                }

                if (id.StartsWith("stat.attack.", StringComparison.Ordinal))
                    return "기본 공격 피해가 증가합니다.";
                if (id.StartsWith("stat.radius.", StringComparison.Ordinal))
                    return "기본 공격이 닿는 범위가 넓어집니다.";
                if (id.StartsWith("stat.cooldown.", StringComparison.Ordinal))
                    return "같은 대상을 다시 공격하는 간격이 짧아집니다.";
                if (id.StartsWith("stat.critical.", StringComparison.Ordinal))
                    return "치명타가 발동할 확률이 높아집니다.";
                if (id.StartsWith("stat.boss.", StringComparison.Ordinal))
                    return "보스에게 주는 피해가 증가합니다.";
                if (id.StartsWith("stat.fieldDuration.", StringComparison.Ordinal))
                    return "일반 필드 전투 시간이 늘어납니다.";
            }

            if (id.StartsWith("skill.", StringComparison.Ordinal))
            {
                string skillId = GetEntityRootId(id, "skill.");
                if (string.Equals(id, skillId, StringComparison.Ordinal))
                {
                    switch (skillId)
                    {
                        case "skill.tornado": return "전투 중 토네이도를 소환해 범위 안의 몬스터를 여러 번 공격합니다. 배우면 전투에서 자동으로 사용합니다.";
                        case "skill.headshot": return "대상을 조준해 강한 탄환을 발사하고, 명중 지점 주변의 적에게도 피해를 줍니다. 배우면 전투에서 자동으로 사용합니다.";
                        case "skill.thunder": return "전투 중 무작위 몬스터에게 번개를 떨어뜨립니다. 배우면 전투에서 자동으로 사용합니다.";
                        case "skill.flame_exhaust": return "커서 방향으로 불꽃 투사체를 발사해 몬스터를 공격합니다. 배우면 전투에서 자동으로 사용합니다.";
                        case "skill.magic_ball": return "마력 구체가 필드를 이동하며 닿은 몬스터를 공격합니다. 배우면 전투에서 자동으로 사용합니다.";
                        case "skill.laser": return "필드에 레이저를 발사해 빔에 닿은 몬스터를 공격합니다. 배우면 전투에서 자동으로 사용합니다.";
                        case "skill.pet_baby_dragon": return "아기용 동료가 플레이어를 따라다니며 가까운 몬스터를 자동 공격합니다. 첫 번째로 배울 수 있는 동료 기술입니다.";
                        default: return "배우면 다음 전투부터 자동으로 사용합니다.";
                    }
                }
                if (id.IndexOf(".damage.", StringComparison.Ordinal) >= 0 ||
                    id.EndsWith(".damage", StringComparison.Ordinal))
                {
                    return string.Equals(skillId, "skill.pet_baby_dragon",
                            StringComparison.Ordinal)
                        ? "동료의 공격 피해를 높입니다. 플레이어 공격력과 별도로 적용됩니다."
                        : "이 스킬의 피해를 높입니다. 플레이어 공격력과 별도로 적용됩니다.";
                }
                if (id.IndexOf(".radius.", StringComparison.Ordinal) >= 0 ||
                    id.EndsWith(".radius", StringComparison.Ordinal))
                    return "이 스킬의 공격 범위를 넓힙니다. 기본 공격 범위와 별도로 적용됩니다.";
                if (id.IndexOf(".simultaneousHits.", StringComparison.Ordinal) >= 0)
                    return "동료가 한 번의 공격으로 동시에 타격하는 몬스터 수를 늘립니다.";
                if (id.EndsWith(".cooldown", StringComparison.Ordinal))
                    return "이 스킬을 다시 사용하는 데 필요한 대기 시간을 줄입니다.";
            }

            if (id.StartsWith("monster.", StringComparison.Ordinal))
            {
                string monsterId = GetEntityRootId(id, "monster.");
                bool isProduction = id.IndexOf(".production.", StringComparison.Ordinal) >= 0;
                if (string.Equals(id, monsterId, StringComparison.Ordinal) || isProduction)
                {
                    string behavior = GetMonsterTraitDescription(monsterId);
                    string reward = BuildMonsterRewardSummary(monsterId);
                    string production = string.Empty;
                    if (isProduction)
                    {
                        int productionCount = GetMonsterProductionCount(node);
                        production = productionCount > 0
                            ? "한 번에 " + productionCount.ToString(CultureInfo.InvariantCulture) + "마리 등장"
                            : "한 번에 등장하는 몬스터 수가 증가";
                    }

                    string details = behavior;
                    if (!string.IsNullOrEmpty(production)) details += "\n" + production;
                    if (!string.IsNullOrEmpty(reward)) details += "\n\n" + reward;
                    return details;
                }
            }

            return "연구 효과를 적용합니다.";
        }

        private static string GetMonsterTraitDescription(string monsterId)
        {
            switch (monsterId)
            {
                case "monster.01": return "특수 능력이 없는 기본 몬스터입니다.";
                case "monster.02": return "몸집이 작아 공격을 맞히기 어렵습니다.";
                case "monster.03": return "빠르게 이동합니다.";
                case "monster.04": return "주기적으로 모습을 감춰 공격을 피합니다.";
                case "monster.05": return "간헐적으로 방패를 들어 피해를 줄입니다.";
                case "monster.06": return "순간이동으로 위치를 바꿉니다.";
                case "monster.07": return "높은 체력을 지녔습니다.";
                case "monster.08": return "몸집이 크고 피격 범위가 넓습니다.";
                case "monster.09": return "주변 몬스터의 체력을 회복시킵니다.";
                case "monster.10": return "쓰러진 주변 몬스터를 되살립니다.";
                default: return "해금하면 전투 필드에 등장합니다.";
            }
        }

        private string BuildMonsterRewardSummary(string monsterId)
        {
            if (_document == null || _document.information == null ||
                _document.information.monsters == null)
            {
                return string.Empty;
            }

            foreach (MonsterInformation monster in _document.information.monsters)
            {
                if (monster == null || !string.Equals(
                        monster.id,
                        monsterId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                string reward = "처치 보상\n가넷 " + monster.garnetReward.ToString(
                    "N0", CultureInfo.InvariantCulture) + "개";
                if (monster.gemstoneAmount > 0 &&
                    monster.gemstoneChancePercent > 0f)
                {
                    reward += " · " + GetGemstoneLocalizedName(
                        monster.gemstoneId,
                        monster.gemstoneId) + " " +
                        monster.gemstoneAmount.ToString("N0", CultureInfo.InvariantCulture) +
                        "개";
                    if (monster.gemstoneChancePercent < 100f)
                    {
                        reward += " (" + monster.gemstoneChancePercent.ToString(
                            "0.#", CultureInfo.InvariantCulture) + "% 확률)";
                    }
                }

                return reward;
            }

            return string.Empty;
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
                    id.StartsWith("stat.radius.", StringComparison.Ordinal) ? "공격 범위" :
                    id.StartsWith("stat.cooldown.", StringComparison.Ordinal) ? "공격 간격" :
                    id.StartsWith("stat.critical.", StringComparison.Ordinal) ? "치명타 확률" :
                    id.StartsWith("stat.boss.", StringComparison.Ordinal) ? "보스 피해 배수" :
                    id.StartsWith("stat.fieldDuration.", StringComparison.Ordinal) ? "일반 필드 시간" : "능력치";
                int rank = GetTrailingRank(id);
                return rank <= 1 ? group + " 기본값" : group + " 강화 " + rank;
            }

            if (id.StartsWith("skill.", StringComparison.Ordinal))
            {
                string skillId = GetEntityRootId(id, "skill.");
                string skillName = GetLocalizedEntityName(skillId, node.Title);
                if (string.Equals(id, skillId, StringComparison.Ordinal)) return skillName;
                int rank = GetTrailingRank(id);
                if (id.IndexOf(".damage.", StringComparison.Ordinal) >= 0 ||
                    id.EndsWith(".damage", StringComparison.Ordinal))
                    return skillName + " 피해 강화 " + rank;
                if (id.IndexOf(".radius.", StringComparison.Ordinal) >= 0 ||
                    id.EndsWith(".radius", StringComparison.Ordinal))
                    return skillName + " 범위 강화 " + rank;
                if (id.IndexOf(".simultaneousHits.", StringComparison.Ordinal) >= 0)
                    return skillName + " 동시 타격 강화 " + rank;
                if (id.EndsWith(".cooldown", StringComparison.Ordinal)) return skillName + " · 재사용 시간";
            }

            if (id.StartsWith("monster.", StringComparison.Ordinal))
            {
                string monsterId = GetEntityRootId(id, "monster.");
                string monsterName = GetLocalizedEntityName(monsterId, node.Title);
                if (string.Equals(id, monsterId, StringComparison.Ordinal)) return monsterName;
                int productionCount = GetMonsterProductionCount(node);
                return productionCount > 0
                    ? monsterName + " · 생산량 " + productionCount.ToString(CultureInfo.InvariantCulture) + "마리"
                    : monsterName + " · 생산량 증가";
            }

            return node.Title ?? node.Id;
        }

        private string GetLocalizedNodeValueLabel(TraitNodeDefinition node)
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
            if (node.Id != null && node.Id.StartsWith("skill.", StringComparison.Ordinal))
            {
                UpgradeNodeData data = FindUpgradeNodeData(node.Id);
                if (data != null)
                {
                    if (string.Equals(data.statId, "damage", StringComparison.Ordinal))
                        return "피해 +" + FormatCompactNumber(data.value);
                    if (string.Equals(data.statId, "radius", StringComparison.Ordinal))
                        return "범위 +" + FormatCompactNumber(data.value * 100d) + "%";
                    if (string.Equals(data.statId, "simultaneousHits", StringComparison.Ordinal))
                        return "동시 타격 +" + FormatCompactNumber(data.value) + "마리";
                }
            }
            if (node.Id != null && node.Id.IndexOf(".production.", StringComparison.Ordinal) >= 0)
            {
                int count = GetMonsterProductionCount(node);
                return count > 0
                    ? "한 번에 " + count.ToString(CultureInfo.InvariantCulture) + "마리"
                    : "생산량 증가";
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

        private string BuildNodeChangePreview(TraitNodeDefinition node)
        {
            if (node == null || _document == null || _document.progression == null)
            {
                return string.Empty;
            }

            UpgradeNodeData nodeData = FindUpgradeNodeData(node.Id);
            bool alreadyPurchased = IsPurchased(node);
            if (GetNodePurchaseState(node) == NodePurchaseState.Locked)
            {
                return "<size=12><color=#AFC5D8>해금 조건 달성 후 공개</color></size>\n<b>???</b>";
            }

            if (string.Equals(node.Id, ResearchCoreNodeId, StringComparison.Ordinal))
            {
                return BuildPreviewRows(
                    alreadyPurchased,
                    alreadyPurchased ? "네 방향 연구 활성" : "연구 가지 없음",
                    alreadyPurchased ? "첫 연구 선택 가능" : "네 방향 첫 연구 개방");
            }

            var beforePurchased = new HashSet<string>(_purchased, StringComparer.Ordinal);
            var afterPurchased = new HashSet<string>(_purchased, StringComparer.Ordinal);
            if (alreadyPurchased)
            {
                beforePurchased.Remove(node.Id);
            }
            else
            {
                afterPurchased.Add(node.Id);
            }

            if (nodeData != null && nodeData.operation == "baseline")
            {
                string baseline = GetStatDisplayValue(node.Id, BuildInformationForNodes(_purchased));
                return "<size=12><color=#AFC5D8>전투 기준값</color></size>\n" +
                    "<b>" + baseline + "</b>";
            }

            GameInformation before = BuildInformationForNodes(beforePurchased);
            GameInformation after = BuildInformationForNodes(afterPurchased);

            string beforeValue;
            string afterValue;
            string id = node.Id ?? string.Empty;
            if (id.StartsWith("stat.", StringComparison.Ordinal))
            {
                beforeValue = GetStatDisplayValue(id, before);
                afterValue = GetStatDisplayValue(id, after);
            }
            else if (id.StartsWith("skill.", StringComparison.Ordinal))
            {
                string skillId = GetEntityRootId(id, "skill.");
                SkillInformation beforeSkill = FindSkill(before, skillId);
                SkillInformation afterSkill = FindSkill(after, skillId);
                if (beforeSkill == null || afterSkill == null) return string.Empty;

                if (string.Equals(id, skillId, StringComparison.Ordinal))
                {
                    beforeValue = beforeSkill.enabled ? "전투 사용 가능" : "전투 사용 불가";
                    afterValue = afterSkill.enabled ? "전투 사용 가능" : "전투 사용 불가";
                }
                else if (nodeData != null && nodeData.operation == "add" &&
                         !string.IsNullOrEmpty(nodeData.statId))
                {
                    float beforeDelta = GetSkillUpgradeDelta(beforeSkill, nodeData.statId);
                    float afterDelta = GetSkillUpgradeDelta(afterSkill, nodeData.statId);
                    switch (nodeData.statId)
                    {
                        case "damage":
                            beforeValue = "추가 피해 +" + FormatCompactNumber(beforeDelta);
                            afterValue = "추가 피해 +" + FormatCompactNumber(afterDelta);
                            break;
                        case "radius":
                            beforeValue = "범위 증가 +" + FormatCompactNumber(beforeDelta * 100f) + "%";
                            afterValue = "범위 증가 +" + FormatCompactNumber(afterDelta * 100f) + "%";
                            break;
                        case "simultaneousHits":
                            beforeValue = "추가 타격 " + FormatCompactNumber(beforeDelta) + "마리";
                            afterValue = "추가 타격 " + FormatCompactNumber(afterDelta) + "마리";
                            break;
                        default:
                            beforeValue = "누적 증가량 " + FormatCompactNumber(beforeDelta);
                            afterValue = "누적 증가량 " + FormatCompactNumber(afterDelta);
                            break;
                    }
                }
                else
                {
                    return string.Empty;
                }
            }
            else if (id.StartsWith("monster.", StringComparison.Ordinal))
            {
                string monsterId = GetEntityRootId(id, "monster.");
                MonsterInformation beforeMonster = FindMonster(before, monsterId);
                MonsterInformation afterMonster = FindMonster(after, monsterId);
                if (beforeMonster == null || afterMonster == null) return string.Empty;

                if (string.Equals(id, monsterId, StringComparison.Ordinal))
                {
                    beforeValue = beforeMonster.enabled ? "전투 등장 가능" : "전투 등장 불가";
                    afterValue = afterMonster.enabled ? "전투 등장 가능" : "전투 등장 불가";
                }
                else
                {
                    beforeValue = beforeMonster.productionCount.ToString("N0", CultureInfo.InvariantCulture) + "마리";
                    afterValue = afterMonster.productionCount.ToString("N0", CultureInfo.InvariantCulture) + "마리";
                }
            }
            else
            {
                return string.Empty;
            }

            return BuildPreviewRows(alreadyPurchased, beforeValue, afterValue);
        }

        private string BuildPreviewRows(bool alreadyPurchased, string beforeValue, string afterValue)
        {
            string beforeLabel = alreadyPurchased ? "구매 전" : "현재";
            string afterLabel = alreadyPurchased ? "현재" : "강화 후";
            return "<size=12><color=#AFC5D8>" + beforeLabel +
                "</color>  →  <color=#F6C46C>" + afterLabel +
                "</color></size>\n<b>" + beforeValue + "</b>  →  <b><color=#FFD38A>" +
                afterValue + "</color></b>";
        }

        private GameInformation BuildInformationForNodes(ISet<string> purchased)
        {
            return GameInformationBuilder.Build(
                _document,
                purchased,
                id => GetGemstoneBalance(GetGemstoneIndexById(id)),
                _cursorCombatStatDefaults, GetOwnedRelicIds());
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

        private static SkillInformation FindSkill(GameInformation information, string id)
        {
            if (information == null || information.skills == null) return null;
            foreach (SkillInformation skill in information.skills)
                if (skill != null && string.Equals(skill.id, id, StringComparison.Ordinal)) return skill;
            return null;
        }

        private static float GetSkillUpgradeDelta(SkillInformation skill, string statId)
        {
            if (skill == null || skill.upgrades == null) return 0f;
            foreach (SkillStatUpgradeInformation upgrade in skill.upgrades)
            {
                if (upgrade != null && string.Equals(upgrade.statId, statId,
                        StringComparison.Ordinal))
                    return upgrade.delta;
            }
            return 0f;
        }

        private static MonsterInformation FindMonster(GameInformation information, string id)
        {
            if (information == null || information.monsters == null) return null;
            foreach (MonsterInformation monster in information.monsters)
                if (monster != null && string.Equals(monster.id, id, StringComparison.Ordinal)) return monster;
            return null;
        }

        private static string GetStatDisplayValue(string nodeId, GameInformation information)
        {
            if (information == null || information.stats == null) return string.Empty;
            StatInformation stats = information.stats;
            if (nodeId.StartsWith("stat.attack.", StringComparison.Ordinal))
                return stats.attackPower.ToString("N0", CultureInfo.InvariantCulture) + " 공격력";
            if (nodeId.StartsWith("stat.radius.", StringComparison.Ordinal))
                return "공격 범위 " + FormatCompactNumber(stats.attackRadiusWorldUnits);
            if (nodeId.StartsWith("stat.cooldown.", StringComparison.Ordinal))
                return "간격 " + FormatCompactNumber(stats.attackCooldownSeconds) + "초";
            if (nodeId.StartsWith("stat.critical.", StringComparison.Ordinal))
                return "치명타 " + FormatCompactNumber(stats.criticalChancePercent) + "%";
            if (nodeId.StartsWith("stat.boss.", StringComparison.Ordinal))
                return "보스 피해 배수 " + FormatCompactNumber(stats.bossDamageMultiplier) + "배";
            if (nodeId.StartsWith("stat.fieldDuration.", StringComparison.Ordinal))
                return "일반 필드 시간 " + FormatCompactNumber(stats.normalFieldDurationSeconds) + "초";
            return string.Empty;
        }

        private static string FormatCompactValueLabel(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
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
                suffix = " 공격 범위";
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

        private static string GetLocalizedEntityName(string id, string fallback)
        {
            switch (id)
            {
                case "skill.tornado": return "토네이도";
                case "skill.headshot": return "헤드샷";
                case "skill.thunder": return "벼락";
                case "skill.flame_exhaust": return "화염 배기";
                case "skill.magic_ball": return "마력 구체";
                case "skill.laser": return "레이저";
                case "skill.cursorAura": return "펫 동료";
                case "skill.pet_baby_dragon": return "베이비 드래곤";
                case "monster.01": return "그루터기";
                case "monster.02": return "여왕벌";
                case "monster.03": return "독버섯";
                case "monster.04": return "가면유령";
                case "monster.05": return "갑옷병사";
                case "monster.06": return "눈요정";
                case "monster.07": return "벌집";
                case "monster.08": return "골렘";
                case "monster.09": return "봉오리";
                case "monster.10": return "판도라 상자";
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
                CreateText(labelRect, label, fontSize, TextAnchor.MiddleCenter, textColor, true);
            }

            return button;
        }
    }

    /// <summary>Turns a gemstone row's exchange icon into a press-and-hold action.</summary>
    public sealed class TraitGemstoneExchangeHoldInput : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        private TraitScreenController _owner;
        private int _sourceIndex;
        private int _destinationIndex;
        private bool _holding;

        public void Bind(
            TraitScreenController owner,
            int sourceIndex,
            int destinationIndex)
        {
            _owner = owner;
            _sourceIndex = sourceIndex;
            _destinationIndex = destinationIndex;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_owner == null || eventData == null ||
                eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _holding = true;
            _owner.BeginGemstoneExchangeHold(_sourceIndex, _destinationIndex);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            EndHold();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            EndHold();
        }

        private void OnDisable()
        {
            EndHold();
        }

        private void EndHold()
        {
            if (!_holding)
            {
                return;
            }

            _holding = false;
            if (_owner != null)
            {
                _owner.EndGemstoneExchangeHold();
            }
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

        public override void OnDrag(PointerEventData eventData)
        {
            base.OnDrag(eventData);
            if (_owner != null) _owner.RefreshNodeTooltipPosition();
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            base.OnEndDrag(eventData);
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

    /// <summary>Allows node hover inspection while guided actions enforce purchase order.</summary>
    public sealed class TraitStarterTutorialRaycastGate : MonoBehaviour,
        ICanvasRaycastFilter
    {
        private TraitScreenController _owner;
        private RectTransform _focus;
        private Camera _camera;

        public void Bind(TraitScreenController owner, RectTransform focus, Camera camera)
        {
            _owner = owner;
            _focus = focus;
            _camera = camera;
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            Camera camera = _camera != null ? _camera : eventCamera;
            return _focus != null &&
                !RectTransformUtility.RectangleContainsScreenPoint(
                    _focus,
                    screenPoint,
                    camera) &&
                (_owner == null || !_owner.IsPointerOverBoardNode(screenPoint, camera));
        }
    }

    /// <summary>
    /// Keeps the relic reveal dismissal responsive even when a UI Button's
    /// color transition or a sibling graphic interferes with its onClick.
    /// </summary>
    public sealed class TraitRelicRevealDismissRelay : MonoBehaviour, IPointerClickHandler
    {
        private Action _dismiss;

        public void Bind(Action dismiss)
        {
            _dismiss = dismiss;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _dismiss?.Invoke();
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
}
