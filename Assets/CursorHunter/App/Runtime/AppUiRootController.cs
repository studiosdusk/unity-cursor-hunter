using System;
using System.Collections.Generic;
using CursorHunter.Data;
using CursorHunter.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace CursorHunter.App
{
    /// <summary>
    /// Serialized binding between an application screen id and its authored
    /// scene root. The root controller is the only application service that
    /// changes screen visibility.
    /// </summary>
    [Serializable]
    public sealed class UiScreenBinding
    {
        [SerializeField] private UiScreenId screenId;
        [SerializeField] private GameObject root;

        public UiScreenId ScreenId => screenId;

        public GameObject Root => root;

        public bool IsValid => root != null;

        public void SetVisible(bool isVisible)
        {
            if (root != null && root.activeSelf != isVisible)
            {
                root.SetActive(isVisible);
            }
        }
    }

    /// <summary>
    /// Owns application-level screen navigation for the one-scene runtime.
    /// Feature screens keep their own presentation logic; this controller
    /// only selects the active root and prevents multiple base screens from
    /// remaining visible at the same time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AppUiRootController : MonoBehaviour
    {
        private const int StarterGuidePhaseTraitEntry = 3;
        [SerializeField] private UiScreenBinding[] screenBindings =
            new UiScreenBinding[0];
        [SerializeField] private UiScreenId initialScreen = UiScreenId.MainMenu;

        private readonly Dictionary<UiScreenId, UiScreenBinding> _bindings =
            new Dictionary<UiScreenId, UiScreenBinding>();
        private UiScreenId _currentScreen;
        private bool _hasCurrentScreen;
        private bool _bindingsInitialized;
        private int _starterGuidePhase;
        private readonly List<Button> _temporarilyDisabledButtons =
            new List<Button>();
        private Button _starterGuideButton;
        private Outline _starterGuideOutline;
        private GameObject _starterGuideHint;
        private RectTransform _starterGuideOverlay;
        private RectTransform _starterGuideCard;
        private Image[] _starterGuideDimPanels = Array.Empty<Image>();
        private AppStarterGuideRaycastGate _starterGuideRaycastGate;
        private Vector3 _starterGuideOriginalScale = Vector3.one;
        private readonly Vector3[] _starterGuideWorldCorners = new Vector3[4];
        private readonly Vector2[] _starterGuideLocalCorners = new Vector2[4];

        public event Action<UiScreenId, UiScreenId> ScreenChanged;

        public UiScreenId CurrentScreen => _hasCurrentScreen
            ? _currentScreen
            : initialScreen;

        private void Awake()
        {
            InitializeBindings();
            bool firstRunNeedsResearch = TraitProgressionStore.ShouldShowStarterTutorial;
            if (firstRunNeedsResearch)
            {
                _starterGuidePhase = StarterGuidePhaseTraitEntry;
                ShowScreen(UiScreenId.MainMenu);
            }
            else
            {
                ShowScreen(initialScreen);
            }
        }

        private void OnEnable()
        {
            if (_hasCurrentScreen) AppAmbientMusic.SetEnabled(_currentScreen != UiScreenId.Combat);
        }

        private void Update()
        {
            AppAmbientMusic.Tick(Time.unscaledDeltaTime);
            if (_starterGuideOverlay != null && _starterGuideOverlay.gameObject.activeSelf)
            {
                UpdateStarterGuideSpotlight();
            }

            if (_starterGuideOutline != null)
            {
                float pulse = (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.5f;
                _starterGuideOutline.effectColor = Color.Lerp(
                    new Color(1f, 0.64f, 0.20f, 0.84f),
                    new Color(1f, 0.95f, 0.62f, 1f),
                    pulse);
                float thickness = Mathf.Lerp(5f, 9f, pulse);
                _starterGuideOutline.effectDistance = new Vector2(thickness, -thickness);
            }
        }

        private void OnDisable()
        {
            AppAmbientMusic.Stop();
            RestoreStarterGuideControls();
            ClearStarterGuideHint();
        }

        public void ShowMainMenu()
        {
            ShowScreen(UiScreenId.MainMenu);
        }

        public void ShowFieldSelect()
        {
            if (TraitProgressionStore.ShouldShowStarterTutorial)
            {
                ShowScreen(UiScreenId.Trait);
                FindTraitController()?.GuideStarterResearch();
                return;
            }

            if (_starterGuidePhase == 1)
            {
                _starterGuidePhase = 2;
            }
            ShowScreen(UiScreenId.FieldSelect);
        }

        public void ShowBossSelect()
        {
            ShowScreen(UiScreenId.BossSelect);
        }

        public void ShowTrait()
        {
            bool beginFirstRunGuide = _starterGuidePhase == StarterGuidePhaseTraitEntry &&
                TraitProgressionStore.ShouldShowStarterTutorial;
            ShowScreen(UiScreenId.Trait);
            if (!beginFirstRunGuide)
            {
                return;
            }

            TraitScreenController trait = FindTraitController();
            if (trait == null)
            {
                Debug.LogWarning("Cannot start the first-run guide because the Trait screen is missing.", this);
                ShowScreen(UiScreenId.MainMenu);
                return;
            }

            _starterGuidePhase = 0;
            trait.GuideStarterResearch();
        }

        public void ShowSettings()
        {
            ShowScreen(UiScreenId.Settings);
        }

        public void EnterCombat()
        {
            CompleteStarterBattleTutorial();
            ShowScreen(UiScreenId.Combat);
        }

        public void CloseFieldSelect()
        {
            ShowMainMenu();
        }

        public void CloseBossSelect()
        {
            ShowMainMenu();
        }

        public void CloseTrait()
        {
            TraitScreenController trait = FindTraitController();
            bool shouldShowStarterTutorial = TraitProgressionStore.ShouldShowStarterTutorial;
            if (shouldShowStarterTutorial && trait == null)
            {
                Debug.LogWarning(
                    "Cannot finish the starter guide because the Trait screen is missing.",
                    this);
                return;
            }

            if (shouldShowStarterTutorial && trait != null &&
                !trait.HasCompletedStarterOnboarding)
            {
                trait.GuideStarterResearch();
                return;
            }

            if (shouldShowStarterTutorial)
            {
                TraitProgressionStore.CompleteStarterTutorial();
                _starterGuidePhase = 1;
            }
            ShowMainMenu();
        }

        public void CompleteStarterBattleTutorial()
        {
            _starterGuidePhase = 0;
            RestoreStarterGuideControls();
            ClearStarterGuideHint();
        }

        public void CloseSettings()
        {
            ShowMainMenu();
        }

        public bool ShowScreen(UiScreenId screenId)
        {
            InitializeBindings();

            if (!_bindings.TryGetValue(screenId, out UiScreenBinding binding) ||
                !binding.IsValid)
            {
                // A binding can be incomplete during a scene or domain reload
                // while serialized references are being restored. Rebuild once
                // from the authored array before reporting a navigation error.
                InitializeBindings(true);
            }

            if (!_bindings.TryGetValue(screenId, out UiScreenBinding refreshedBinding) ||
                !refreshedBinding.IsValid)
            {
                Debug.LogError(
                    $"AppUiRootController has no valid binding for screen " +
                    $"'{screenId}'.",
                    this);
                return false;
            }

            binding = refreshedBinding;
            UiScreenId previousScreen = CurrentScreen;
            foreach (UiScreenBinding screenBinding in _bindings.Values)
            {
                screenBinding.SetVisible(false);
            }

            binding.SetVisible(true);
            AppAmbientMusic.SetEnabled(screenId != UiScreenId.Combat);
            if (screenId != UiScreenId.Trait)
            {
                InstallButtonAudio(binding.Root);
            }
            bool changed = !_hasCurrentScreen || previousScreen != screenId;
            _currentScreen = screenId;
            _hasCurrentScreen = true;

            if (changed)
            {
                ScreenChanged?.Invoke(previousScreen, screenId);
            }

            RefreshStarterGuide(screenId);

            return true;
        }

        private static void InstallButtonAudio(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null &&
                    buttons[i].GetComponent<AppUiButtonClickFeedback>() == null)
                {
                    buttons[i].gameObject.AddComponent<AppUiButtonClickFeedback>();
                }
            }
        }

        private void RefreshStarterGuide(UiScreenId screenId)
        {
            RestoreStarterGuideControls();
            ClearStarterGuideHint();
            if (_starterGuidePhase == 0 || screenId == UiScreenId.Trait ||
                screenId == UiScreenId.Combat)
            {
                return;
            }

            string method = _starterGuidePhase == StarterGuidePhaseTraitEntry &&
                screenId == UiScreenId.MainMenu
                ? nameof(ShowTrait)
                : _starterGuidePhase == 1 && screenId == UiScreenId.MainMenu
                    ? nameof(ShowFieldSelect)
                    : _starterGuidePhase == 2 && screenId == UiScreenId.FieldSelect
                        ? "BeginPrototypeRun"
                        : null;
            if (string.IsNullOrEmpty(method) ||
                !_bindings.TryGetValue(screenId, out UiScreenBinding binding) ||
                binding.Root == null)
            {
                return;
            }

            Button[] buttons = binding.Root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || !button.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (HasPersistentCallback(button, method))
                {
                    _starterGuideButton = button;
                    continue;
                }

                if (button.interactable)
                {
                    button.interactable = false;
                    _temporarilyDisabledButtons.Add(button);
                }
            }

            if (_starterGuideButton == null)
            {
                RestoreStarterGuideControls();
                ClearStarterGuideHint();
                return;
            }

            _starterGuideOriginalScale = _starterGuideButton.transform.localScale;
            _starterGuideButton.transform.localScale = _starterGuideOriginalScale * 1.10f;
            Graphic targetGraphic = _starterGuideButton.targetGraphic;
            if (targetGraphic != null)
            {
                _starterGuideOutline = targetGraphic.gameObject.AddComponent<Outline>();
                _starterGuideOutline.effectColor = new Color(1f, 0.82f, 0.30f, 1f);
                _starterGuideOutline.effectDistance = new Vector2(5f, -5f);
            }

            string step;
            string title;
            string message;
            if (_starterGuidePhase == StarterGuidePhaseTraitEntry)
            {
                step = "처음 안내 · 01 / 01";
                title = "특성 연구부터 시작해요";
                message = "강조된 Trait 버튼을 누르세요.\n능력치와 스킬을 연구할 수 있어요.";
            }
            else if (_starterGuidePhase == 1)
            {
                step = "처음 안내 · 연구 완료";
                title = "전투 준비를 해요";
                message = "강조된 필드 버튼을 눌러 전투 준비 화면으로 이동하세요.";
            }
            else
            {
                step = "처음 안내 · 전투 준비";
                title = "첫 전투를 시작해요";
                message = "강조된 버튼을 눌러 첫 전투를 시작하세요.";
            }

            CreateStarterGuideHint(
                binding.Root.transform as RectTransform,
                step,
                title,
                message);
        }

        private static bool HasPersistentCallback(Button button, string methodName)
        {
            if (button == null || button.onClick == null)
            {
                return false;
            }

            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (string.Equals(button.onClick.GetPersistentMethodName(i),
                        methodName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void CreateStarterGuideHint(
            RectTransform parent,
            string step,
            string title,
            string message)
        {
            if (parent == null || _starterGuideButton == null)
            {
                return;
            }

            GameObject overlayObject = new GameObject(
                "StarterGuideSpotlight",
                typeof(RectTransform),
                typeof(Image));
            _starterGuideOverlay = overlayObject.transform as RectTransform;
            Canvas parentCanvas = parent.GetComponentInParent<Canvas>();
            // Screen bindings may be zero-sized grouping transforms. The
            // spotlight and popup must measure the full, real viewport.
            Transform overlayParent = parentCanvas != null
                ? parentCanvas.rootCanvas.transform
                : parent;
            _starterGuideOverlay.SetParent(overlayParent, false);
            _starterGuideOverlay.anchorMin = Vector2.zero;
            _starterGuideOverlay.anchorMax = Vector2.one;
            _starterGuideOverlay.pivot = new Vector2(0.5f, 0.5f);
            _starterGuideOverlay.offsetMin = Vector2.zero;
            _starterGuideOverlay.offsetMax = Vector2.zero;
            Image blocker = overlayObject.GetComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0f);
            blocker.raycastTarget = true;
            _starterGuideRaycastGate =
                overlayObject.AddComponent<AppStarterGuideRaycastGate>();
            _starterGuideRaycastGate.Bind(
                _starterGuideButton.transform as RectTransform,
                parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : parentCanvas.worldCamera);

            _starterGuideDimPanels = new Image[4];
            string[] names = { "DimTop", "DimBottom", "DimLeft", "DimRight" };
            for (int i = 0; i < _starterGuideDimPanels.Length; i++)
            {
                GameObject panelObject = new GameObject(names[i],
                    typeof(RectTransform), typeof(Image));
                RectTransform panelRect = panelObject.transform as RectTransform;
                panelRect.SetParent(_starterGuideOverlay, false);
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                Image panel = panelObject.GetComponent<Image>();
                panel.color = new Color(0.23f, 0.27f, 0.32f, 0.78f);
                panel.raycastTarget = true;
                _starterGuideDimPanels[i] = panel;
            }

            _starterGuideHint = new GameObject(
                "StarterGuideHint",
                typeof(RectTransform),
                typeof(Image));
            _starterGuideCard = _starterGuideHint.transform as RectTransform;
            _starterGuideCard.SetParent(_starterGuideOverlay, false);
            _starterGuideCard.anchorMin = new Vector2(0.5f, 0.5f);
            _starterGuideCard.anchorMax = new Vector2(0.5f, 0.5f);
            _starterGuideCard.pivot = new Vector2(0.5f, 0.5f);
            _starterGuideCard.sizeDelta = new Vector2(460f, 164f);
            Image background = _starterGuideHint.GetComponent<Image>();
            background.color = new Color(0.055f, 0.073f, 0.105f, 0.99f);
            background.raycastTarget = false;
            Outline outline = _starterGuideHint.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.79f, 0.36f, 0.98f);
            outline.effectDistance = new Vector2(2f, -2f);

            GameObject accentObject = new GameObject(
                "Accent",
                typeof(RectTransform),
                typeof(Image));
            RectTransform accentRect = accentObject.transform as RectTransform;
            accentRect.SetParent(_starterGuideCard, false);
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.anchoredPosition = new Vector2(0f, -4f);
            accentRect.sizeDelta = new Vector2(-8f, 5f);
            Image accent = accentObject.GetComponent<Image>();
            accent.color = new Color(1f, 0.76f, 0.30f, 1f);
            accent.raycastTarget = false;

            CreateStarterGuideLabel(
                _starterGuideCard,
                "Step",
                step,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(22f, -18f),
                new Vector2(-44f, 24f),
                14f,
                new Color(1f, 0.82f, 0.45f, 1f),
                FontStyles.Bold,
                TextAlignmentOptions.Left);
            CreateStarterGuideLabel(
                _starterGuideCard,
                "Title",
                title,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(22f, -48f),
                new Vector2(-44f, 34f),
                22f,
                Color.white,
                FontStyles.Bold,
                TextAlignmentOptions.Left);
            CreateStarterGuideLabel(
                _starterGuideCard,
                "Message",
                message,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(22f, -87f),
                new Vector2(-44f, 54f),
                17f,
                new Color(0.87f, 0.91f, 0.96f, 1f),
                FontStyles.Normal,
                TextAlignmentOptions.Left);

            _starterGuideOverlay.SetAsLastSibling();
            _starterGuideOverlay.gameObject.SetActive(true);
            UpdateStarterGuideSpotlight();
        }

        private static TextMeshProUGUI CreateStarterGuideLabel(
            RectTransform parent,
            string objectName,
            string content,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 size,
            float fontSize,
            Color color,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            GameObject labelObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObject.transform as RectTransform;
            labelRect.SetParent(parent, false);
            labelRect.anchorMin = anchorMin;
            labelRect.anchorMax = anchorMax;
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = position;
            labelRect.sizeDelta = size;
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = LocalizedTypography.GetFont(null);
            label.text = content ?? string.Empty;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Max(12f, fontSize - 2f);
            label.fontSizeMax = fontSize;
            label.overflowMode = TextOverflowModes.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private void UpdateStarterGuideSpotlight()
        {
            if (_starterGuideOverlay == null || _starterGuideButton == null ||
                _starterGuideDimPanels == null || _starterGuideDimPanels.Length != 4 ||
                _starterGuideCard == null)
            {
                return;
            }

            RectTransform focus = _starterGuideButton.transform as RectTransform;
            if (focus == null) return;
            Canvas canvas = _starterGuideOverlay.GetComponentInParent<Canvas>();
            Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            focus.GetWorldCorners(_starterGuideWorldCorners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < _starterGuideWorldCorners.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                    camera,
                    _starterGuideWorldCorners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _starterGuideOverlay,
                        screen,
                        camera,
                        out _starterGuideLocalCorners[i]))
                {
                    return;
                }
                min = Vector2.Min(min, _starterGuideLocalCorners[i]);
                max = Vector2.Max(max, _starterGuideLocalCorners[i]);
            }

            Rect bounds = _starterGuideOverlay.rect;
            Rect hole = Rect.MinMaxRect(
                Mathf.Max(bounds.xMin, min.x - 14f),
                Mathf.Max(bounds.yMin, min.y - 14f),
                Mathf.Min(bounds.xMax, max.x + 14f),
                Mathf.Min(bounds.yMax, max.y + 14f));
            SetStarterGuideDimPanel(0, Rect.MinMaxRect(
                bounds.xMin, hole.yMax, bounds.xMax, bounds.yMax));
            SetStarterGuideDimPanel(1, Rect.MinMaxRect(
                bounds.xMin, bounds.yMin, bounds.xMax, hole.yMin));
            SetStarterGuideDimPanel(2, Rect.MinMaxRect(
                bounds.xMin, hole.yMin, hole.xMin, hole.yMax));
            SetStarterGuideDimPanel(3, Rect.MinMaxRect(
                hole.xMax, hole.yMin, bounds.xMax, hole.yMax));

            Vector2 size = new Vector2(
                Mathf.Min(480f, Mathf.Max(260f, bounds.width - 24f)),
                Mathf.Min(176f, Mathf.Max(148f, bounds.height - 24f)));
            _starterGuideCard.sizeDelta = size;
            _starterGuideCard.anchoredPosition = TutorialPopupLayout.AboveOrUpperLeft(
                bounds, hole, size, 18f, 12f);
        }

        private void SetStarterGuideDimPanel(int index, Rect rect)
        {
            Image image = _starterGuideDimPanels[index];
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

        private void RestoreStarterGuideControls()
        {
            for (int i = 0; i < _temporarilyDisabledButtons.Count; i++)
            {
                if (_temporarilyDisabledButtons[i] != null)
                {
                    _temporarilyDisabledButtons[i].interactable = true;
                }
            }
            _temporarilyDisabledButtons.Clear();
            if (_starterGuideButton != null)
            {
                _starterGuideButton.transform.localScale = _starterGuideOriginalScale;
            }
            if (_starterGuideOutline != null)
            {
                Destroy(_starterGuideOutline);
                _starterGuideOutline = null;
            }
            _starterGuideButton = null;
        }

        private void ClearStarterGuideHint()
        {
            if (_starterGuideOverlay != null)
            {
                _starterGuideOverlay.gameObject.SetActive(false);
                Destroy(_starterGuideOverlay.gameObject);
                _starterGuideOverlay = null;
                _starterGuideCard = null;
                _starterGuideDimPanels = Array.Empty<Image>();
                _starterGuideRaycastGate = null;
            }
            _starterGuideHint = null;
        }

        private static TraitScreenController FindTraitController()
        {
            return FindFirstObjectByType<TraitScreenController>(
                FindObjectsInactive.Include);
        }

        public bool IsVisible(UiScreenId screenId)
        {
            InitializeBindings();

            return _bindings.TryGetValue(screenId, out UiScreenBinding binding) &&
                   binding.IsValid &&
                   binding.Root.activeSelf;
        }

        private void InitializeBindings(bool forceRefresh = false)
        {
            if (_bindingsInitialized && !forceRefresh)
            {
                return;
            }

            _bindings.Clear();
            if (screenBindings != null)
            {
                foreach (UiScreenBinding binding in screenBindings)
                {
                    if (binding == null || !binding.IsValid)
                    {
                        Debug.LogWarning(
                            "AppUiRootController contains an empty screen binding.",
                            this);
                        continue;
                    }

                    if (_bindings.ContainsKey(binding.ScreenId))
                    {
                        Debug.LogError(
                            $"AppUiRootController contains duplicate screen " +
                            $"binding '{binding.ScreenId}'.",
                            this);
                        continue;
                    }

                    _bindings.Add(binding.ScreenId, binding);
                }
            }

            _bindingsInitialized = true;
        }
    }

    public sealed class AppStarterGuideRaycastGate : MonoBehaviour, ICanvasRaycastFilter
    {
        private RectTransform _focus;
        private Camera _camera;

        public void Bind(RectTransform focus, Camera camera)
        {
            _focus = focus;
            _camera = camera;
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            return _focus != null &&
                !RectTransformUtility.RectangleContainsScreenPoint(
                    _focus,
                    screenPoint,
                    _camera != null ? _camera : eventCamera);
        }
    }

    public sealed class AppUiButtonClickFeedback : MonoBehaviour, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left)
            {
                TraitAudioFeedback.PlayClick();
            }
        }
    }

    /// <summary>Loops the authored peaceful theme outside of combat.</summary>
    internal static class AppAmbientMusic
    {
        private const string RootName = "CursorHunter_NonCombatMusic";
        private const string ResourcePath = "Audio/PeacefulFantasyLoop";
        private const float MusicVolume = 0.24f;
        private const float FadeSpeed = 0.32f;
        private static GameObject _root;
        private static AudioSource _source;
        private static float _targetVolume;
        private static bool _missingClipReported;

        public static void Stop()
        {
            _targetVolume = 0f;
            if (_source == null) return;
            _source.Stop();
            _source.volume = 0f;
        }

        public static void SetEnabled(bool enabled)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureInitialized();
            if (_source == null || _source.clip == null)
            {
                if (!_missingClipReported)
                {
                    Debug.LogWarning(
                        "Non-combat music is missing. Expected Resources/Audio/PeacefulFantasyLoop.wav.");
                    _missingClipReported = true;
                }
                return;
            }

            _targetVolume = enabled ? MusicVolume : 0f;
            if (enabled && !_source.isPlaying)
            {
                _source.volume = 0f;
                _source.Play();
            }
        }

        public static void Tick(float deltaTime)
        {
            if (_source == null || !_source.isPlaying)
            {
                return;
            }

            _source.volume = Mathf.MoveTowards(
                _source.volume,
                _targetVolume,
                Mathf.Max(0f, deltaTime) * FadeSpeed);
            if (_targetVolume <= 0f && _source.volume <= 0.001f)
            {
                _source.Stop();
            }
        }

        private static void EnsureInitialized()
        {
            if (_source != null)
            {
                return;
            }

            _root = GameObject.Find(RootName);
            if (_root == null)
            {
                _root = new GameObject(RootName);
                UnityEngine.Object.DontDestroyOnLoad(_root);
            }

            _source = _root.GetComponent<AudioSource>();
            if (_source == null)
            {
                _source = _root.AddComponent<AudioSource>();
            }

            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.volume = 0f;
            _source.priority = 0;
            _source.clip = Resources.Load<AudioClip>(ResourcePath);
        }
    }
}
