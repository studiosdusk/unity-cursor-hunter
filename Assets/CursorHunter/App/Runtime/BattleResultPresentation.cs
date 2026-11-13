using System;
using System.Collections.Generic;
using System.Globalization;
using CursorHunter.Contracts;
using CursorHunter.Data;
using CursorHunter.Progression;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.App
{
    /// <summary>Presentation only. RunResult is the authoritative reward snapshot; App owns saving it.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleResultPresentation : MonoBehaviour
    {
        [Serializable]
        private sealed class RewardRow
        {
            public string currencyId;
            public CanvasGroup group;
            public RectTransform icon;
            public TMP_Text amount;
            [NonSerialized] public long earned;
            [NonSerialized] public long displayed = -1;
        }

        [Header("Authored Main scene objects")]
        [SerializeField] private GameObject overlay;
        [SerializeField] private RectTransform layout;
        [SerializeField] private CanvasGroup backdrop;
        [SerializeField] private GameObject timeOutPanel;
        [SerializeField] private RectTransform timeOutPopup;
        [SerializeField] private CanvasGroup timeOutGroup;
        [SerializeField] private TMP_Text timeOutTitle;
        [SerializeField] private CanvasGroup resultUI;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private TMP_Text kills;
        [SerializeField] private TMP_Text duration;
        [SerializeField] private TMP_Text damage;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private TMP_Text emptyRewards;
        [SerializeField] private RectTransform rewardRows;
        [SerializeField] private RewardRow[] rewards = Array.Empty<RewardRow>();
        [SerializeField] private CanvasGroup actions;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button resultSkipButton;
        [SerializeField] private HuntManager huntManager;
        [SerializeField] private AppUiRootController uiRoot;
        [SerializeField] private CombatGemstoneList collection;
        [SerializeField] private TraitScreenController progression;

        [Header("Timing (unscaled seconds)")]
        [SerializeField, Min(0.05f)] private float popupSeconds = 0.15f;
        [SerializeField, Min(0f)] private float holdSeconds = 0.65f;
        [SerializeField, Min(0.05f)] private float dismissSeconds = 0.05f;
        [SerializeField, Min(0.05f)] private float countSeconds = 0.3f;
        [SerializeField, Min(0.05f)] private float buttonPopupSeconds = 0.22f;
        [SerializeField] private bool reduceMotion;
        [SerializeField, Range(0f, 1f)] private float countTickVolume = 0.14f;

        public bool IsVisible => overlay != null && overlay.activeSelf;
        public bool IsReady => _phase == Phase.Ready;
        private bool IsBossPresentationRunning =>
            collection != null && collection.IsFinishingCollection || huntManager != null && huntManager.IsBossDeathPlaying;
        private enum Phase { Hidden, Start, StartDismiss, BossLoot, TimeOut, Dismiss, Rewards, RelicDiscovery, Buttons, Ready }
        public bool IsShowingStart => _phase == Phase.Start || _phase == Phase.StartDismiss;
        private bool _startPaused;
        private Phase _phase;
        private float _clock;
        private RunId _runId;
        private bool _navigating;
        private int _visibleRewardCount;
        private Sequence _buttonSequence;
        private Sequence _relicDiscoverySequence;
        private Sequence _relicDiscoveryIntroSequence;
        private GameObject _relicDiscoveryOverlay;
        private AudioSource _countTickSource;
        private AudioClip _countTickClip;
        private RunResult _currentResult;
        private bool _hasCurrentResult;

        private void Awake()
        {
            upgradeButton.onClick.AddListener(OpenUpgrades);
            retryButton.onClick.AddListener(Retry);
            skipButton.onClick.AddListener(CompleteReveal);
            resultSkipButton.onClick.AddListener(CompleteReveal);
            if (progression == null)
                progression = FindFirstObjectByType<TraitScreenController>(FindObjectsInactive.Include);
            CreateCountTickAudio();
            Hide();
        }

        private void OnEnable()
        {
            if (uiRoot != null) uiRoot.ScreenChanged += OnScreenChanged;
            LocalizationCatalog.Changed += RefreshLocalizedPresentation;
        }

        private void OnDisable()
        {
            if (uiRoot != null) uiRoot.ScreenChanged -= OnScreenChanged;
            LocalizationCatalog.Changed -= RefreshLocalizedPresentation;
            Hide();
        }

        private void OnDestroy()
        {
            StopCountTickAudio();
            if (_countTickClip != null) Destroy(_countTickClip);
            if (upgradeButton != null) upgradeButton.onClick.RemoveListener(OpenUpgrades);
            if (retryButton != null) retryButton.onClick.RemoveListener(Retry);
            if (skipButton != null) skipButton.onClick.RemoveListener(CompleteReveal);
            if (resultSkipButton != null) resultSkipButton.onClick.RemoveListener(CompleteReveal);
        }

        private void OnScreenChanged(UiScreenId previous, UiScreenId current)
        {
            if (current != UiScreenId.Combat) Hide();
        }

        public void Show(RunResult result)
        {
            if (!result.RunId.IsValid || result.RunId == _runId) return;
            Hide();
            _runId = result.RunId;
            _currentResult = result;
            _hasCurrentResult = true;
            _phase = Phase.TimeOut;
            overlay.SetActive(true);
            timeOutPanel.SetActive(true);
            ApplyLocalizedPresentation(result);
            _visibleRewardCount = 0;
            foreach (RewardRow row in rewards)
            {
                row.earned = GetEarned(result, row.currencyId);
                bool earned = row.earned > 0;
                row.group.gameObject.SetActive(earned);
                if (earned) _visibleRewardCount++;
                row.displayed = -1;
                row.group.alpha = 1f;
                row.group.transform.localScale = Vector3.one;
                row.icon.localScale = Vector3.one;
                SetAmount(row, 0L);
            }
            emptyRewards.gameObject.SetActive(_visibleRewardCount == 0);
            ResizeResult();
            kills.text = result.DefeatedCount.ToString("N0", CultureInfo.InvariantCulture);
            damage.text = result.EffectiveDamage.ToString("N0", CultureInfo.InvariantCulture);
            FitLayout();
            RenderTimeOut(0f);
            if (result.BossDefeated && IsBossPresentationRunning)
            {
                _phase = Phase.BossLoot;
                overlay.SetActive(false); // Keep both the death animation and the loot burst visible.
            }
        }

        public void ShowStart(RunId runId, bool paused = false)
        {
            if (!runId.IsValid || IsShowingStart && runId == _runId) return;
            Hide();
            _runId = runId;
            _startPaused = paused;
            _phase = Phase.Start;
            overlay.SetActive(true);
            timeOutPanel.SetActive(true);
            SetLocalizedText(timeOutTitle, "ui.combat.start", "전투 시작!");
            FitLayout();
            RenderTimeOut(0f);
        }

        public void SetStartPaused(bool paused) => _startPaused = paused;

        public void Hide()
        {
            StopCountTickAudio();
            _phase = Phase.Hidden;
            _buttonSequence?.Kill(false);
            _buttonSequence = null;
            _relicDiscoverySequence?.Kill(false);
            _relicDiscoverySequence = null;
            _relicDiscoveryIntroSequence?.Kill(false);
            _relicDiscoveryIntroSequence = null;
            if (_relicDiscoveryOverlay != null) Destroy(_relicDiscoveryOverlay);
            _relicDiscoveryOverlay = null;
            _clock = 0f;
            _runId = default;
            _hasCurrentResult = false;
            _navigating = false;
            if (overlay != null) overlay.SetActive(false);
            if (timeOutPanel != null) timeOutPanel.SetActive(false);
            if (resultUI != null)
            {
                resultUI.gameObject.SetActive(false);
                resultUI.transform.localScale = Vector3.one;
            }
            if (timeOutPopup != null) timeOutPopup.localScale = Vector3.one;
            if (actions != null) { actions.alpha = 0f; actions.interactable = false; }
            if (upgradeButton != null) upgradeButton.transform.localScale = Vector3.zero;
            if (retryButton != null) retryButton.transform.localScale = Vector3.zero;
            if (backdrop != null) backdrop.alpha = 0f;
        }

        private void RefreshLocalizedPresentation()
        {
            if (IsShowingStart)
            {
                SetLocalizedText(timeOutTitle, "ui.combat.start", "전투 시작!");
                return;
            }
            if (!_hasCurrentResult || !IsVisible) return;
            ApplyLocalizedPresentation(_currentResult);
        }

        private void ApplyLocalizedPresentation(RunResult result)
        {
            bool boss = result.Mode == RunMode.Boss;
            bool retired = result.EndReason == RunEndReason.UserExit;
            SetLocalizedText(
                timeOutTitle,
                retired ? "ui.result.retired" : boss
                    ? result.BossDefeated ? "ui.result.boss.clear" : "ui.result.boss.failed"
                    : result.EndReason == RunEndReason.TimeExpired
                    ? "ui.result.timeout"
                    : "ui.result.hunt.over",
                retired ? "중도 포기" : boss
                    ? result.BossDefeated ? "BOSS CLEAR!" : "BOSS FAILED"
                    : result.EndReason == RunEndReason.TimeExpired ? "시간 종료" : "사냥 종료");
            if (retired && boss)
                SetLocalizedFormat(title, "ui.result.boss.retired.title", "보스 {0}단계 · 중도 포기", result.BossStage);
            else
                SetLocalizedText(
                    title,
                    retired ? "ui.result.retired.title" : boss ? "ui.result.boss.title" : "ui.result.title",
                    retired ? "사냥 중도 포기" : boss
                        ? $"보스 {result.BossStage}단계 · " + (result.BossDefeated ? "토벌 성공" : "토벌 실패")
                        : "전투 종료");
            subtitle.gameObject.SetActive(result.SettlementPolicy == RunSettlementPolicy.Discard);
            SetLocalizedText(
                subtitle,
                retired && boss ? "ui.result.boss.retired.subtitle" :
                    boss && !result.BossDefeated ? "ui.result.boss.failed.subtitle" : "ui.result.test.no.reward",
                retired && boss ? "보스 처치 전에 포기하여 획득한 보상이 없습니다." : boss && !result.BossDefeated
                    ? "시간 안에 토벌하지 못했습니다. 강화 후 다시 도전하세요."
                    : "테스트 전투 · 보상 미지급");

            TMP_Text upgradeLabel = upgradeButton.GetComponentInChildren<TMP_Text>(true);
            if (upgradeLabel != null)
            {
                bool connected = huntManager != null && huntManager.IsTraitScreenConnectedToCombat;
                SetLocalizedText(
                    upgradeLabel,
                    connected ? "ui.action.upgrade" : "ui.result.action.lobby",
                    connected ? "강화하기" : "로비로");
            }
            TMP_Text retryLabel = retryButton.GetComponentInChildren<TMP_Text>(true);
            string retryFallback = boss && result.BossDefeated
                ? result.BossStage < 20 ? "다음 단계 도전" : "일반 전투"
                : retired && boss ? "같은 단계 재도전" : "다시 도전";
            SetLocalizedText(
                retryLabel,
                boss && result.BossDefeated
                    ? result.BossStage < 20 ? "ui.result.action.boss.next" : "ui.result.action.boss.field"
                    : retired && boss ? "ui.result.action.boss.retry" : "ui.result.action.retry",
                retryFallback);
            // The skip buttons cover the backdrop/result panel, not dedicated labels.
            // Binding their first child text would overwrite the result title.
            SetLocalizedText(hint, "ui.result.hint", "클릭하면 보상을 바로 볼 수 있어요.");
            SetLocalizedText(emptyRewards, "ui.result.empty.rewards", "획득한 재화가 없습니다.");

            kills.text = result.DefeatedCount.ToString("N0", CultureInfo.InvariantCulture);
            SetLocalizedFormat(
                duration,
                "ui.result.duration",
                "전투 시간 {0}초",
                result.ElapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture));
            damage.text = result.EffectiveDamage.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static void SetLocalizedText(TMP_Text label, string key, string fallback)
        {
            if (label == null) return;
            LocalizedTmpLabel localized = label.GetComponent<LocalizedTmpLabel>();
            if (localized == null) localized = label.gameObject.AddComponent<LocalizedTmpLabel>();
            localized.Bind(key, fallback);
        }

        private static void SetLocalizedFormat(
            TMP_Text label,
            string key,
            string fallbackFormat,
            params object[] arguments)
        {
            if (label == null) return;
            LocalizedTmpLabel localized = label.GetComponent<LocalizedTmpLabel>();
            if (localized == null) localized = label.gameObject.AddComponent<LocalizedTmpLabel>();
            localized.BindFormat(key, fallbackFormat, arguments);
        }

        private void Update()
        {
            if (_phase == Phase.Hidden) return;
            FitLayout();
            if (_phase == Phase.Ready || _phase == Phase.Buttons || _phase == Phase.RelicDiscovery) return;
            if (IsShowingStart && (_startPaused || Time.timeScale <= 0f)) return;
            _clock += Time.unscaledDeltaTime;
            switch (_phase)
            {
                case Phase.Start:
                    RenderTimeOut(_clock / Mathf.Max(0.05f, popupSeconds));
                    if (_clock >= popupSeconds + holdSeconds) Next(Phase.StartDismiss);
                    break;
                case Phase.StartDismiss:
                    timeOutGroup.alpha = 1f - Mathf.Clamp01(_clock / Mathf.Max(0.05f, dismissSeconds));
                    if (_clock >= dismissSeconds) Hide();
                    break;
                case Phase.BossLoot:
                    if (!IsBossPresentationRunning)
                    {
                        overlay.SetActive(true);
                        Next(Phase.TimeOut);
                        RenderTimeOut(0f);
                    }
                    break;
                case Phase.TimeOut:
                    RenderTimeOut(_clock / Mathf.Max(0.05f, popupSeconds));
                    if (_clock >= popupSeconds + holdSeconds &&
                        (collection == null || !collection.IsFinishingCollection))
                        Next(Phase.Dismiss);
                    break;
                case Phase.Dismiss:
                    float exit = Mathf.Clamp01(_clock / Mathf.Max(0.05f, dismissSeconds));
                    timeOutGroup.alpha = 1f - exit;
                    timeOutPopup.localScale = Vector3.one;
                    if (exit >= 1f)
                    {
                        timeOutPanel.SetActive(false);
                        resultUI.gameObject.SetActive(true);
                        LayoutRebuilder.ForceRebuildLayoutImmediate(rewardRows);
                        Next(Phase.Rewards);
                        RenderRewards(0f);
                    }
                    break;
                case Phase.Rewards:
                    RenderRewards(_clock);
                    if (_clock >= RevealDuration) CompleteReveal();
                    break;
            }
        }

        private float RevealDuration => _visibleRewardCount == 0 || reduceMotion ? 0f : countSeconds;
        private void Next(Phase phase) { _phase = phase; _clock = 0f; }

        private void RenderTimeOut(float progress)
        {
            float t = Mathf.Clamp01(progress);
            backdrop.alpha = 1f;
            timeOutGroup.alpha = 1f;
            // A single continuous expansion; the previous 82%/100% step looked like two pops.
            float scale = reduceMotion ? 1f : Mathf.Lerp(0.82f, 1f, 1f - Mathf.Pow(1f - t, 3f));
            timeOutPopup.localScale = Vector3.one * scale;
        }

        private void RenderRewards(float elapsed)
        {
            resultUI.alpha = 1f;
            resultUI.transform.localScale = Vector3.one;
            float progress = reduceMotion ? 1f : Mathf.Clamp01(elapsed / Mathf.Max(0.05f, countSeconds));
            float easedProgress = progress * progress * (3f - 2f * progress);
            if (_visibleRewardCount > 0 && progress < 1f && countTickVolume > 0f)
                StartCountTickAudio();
            else
                StopCountTickAudio();
            foreach (RewardRow row in rewards)
            {
                if (row.earned <= 0) continue;
                SetAmount(row, InterpolateAmount(row.earned, easedProgress));
            }
        }

        private void CreateCountTickAudio()
        {
            GameObject audioObject = new GameObject("RewardCountTickAudio");
            audioObject.transform.SetParent(transform, false);
            _countTickSource = audioObject.AddComponent<AudioSource>();
            _countTickSource.playOnAwake = false;
            _countTickSource.loop = true;
            _countTickSource.spatialBlend = 0f;
            _countTickSource.ignoreListenerPause = false;
            _countTickSource.volume = 0f;

            const int sampleRate = 22050;
            const float duration = 0.09f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 58f) * Mathf.Clamp01(time / 0.002f);
                float tone = Mathf.Sin(2f * Mathf.PI * (920f - 210f * time) * time);
                float overtone = Mathf.Sin(2f * Mathf.PI * 1480f * time) * 0.2f;
                samples[i] = (tone * 0.42f + overtone) * envelope;
            }

            _countTickClip = AudioClip.Create("Reward_Count_Tick", sampleCount, 1, sampleRate, false);
            _countTickClip.SetData(samples, 0);
            _countTickClip.hideFlags = HideFlags.HideAndDontSave;
            _countTickSource.clip = _countTickClip;
        }

        private void StartCountTickAudio()
        {
            if (_countTickSource == null || _countTickSource.isPlaying) return;
            _countTickSource.volume = countTickVolume;
            _countTickSource.Play();
        }

        private void StopCountTickAudio()
        {
            if (_countTickSource == null) return;
            if (_countTickSource.isPlaying) _countTickSource.Stop();
            _countTickSource.volume = 0f;
        }

        public void CompleteReveal()
        {
            // The timeout must remain a distinct beat; clicks never bypass it or navigate.
            if (_phase != Phase.Rewards) return;
            RenderRewards(RevealDuration);
            if (HasRelicDiscovery())
            {
                BeginRelicDiscovery();
                return;
            }
            RevealResultActions();
        }

        private bool HasRelicDiscovery() => _currentResult.SettlementPolicy == RunSettlementPolicy.Eligible &&
            _currentResult.RelicAwards != null && _currentResult.RelicAwards.Count > 0;

        private void BeginRelicDiscovery()
        {
            if (_phase != Phase.Rewards || !HasRelicDiscovery()) return;
            _phase = Phase.RelicDiscovery;
            CreateRelicDiscoveryOverlay();
            // Never strand the result screen if the authored UI root is unavailable.
            if (_relicDiscoveryOverlay == null)
                RevealResultActions();
        }

        private void DismissRelicDiscovery()
        {
            if (_phase != Phase.RelicDiscovery) return;
            _relicDiscoverySequence?.Kill(false);
            _relicDiscoverySequence = null;
            _relicDiscoveryIntroSequence?.Kill(false);
            _relicDiscoveryIntroSequence = null;
            if (_relicDiscoveryOverlay != null) Destroy(_relicDiscoveryOverlay);
            _relicDiscoveryOverlay = null;
            RevealResultActions();
        }

        private void RevealResultActions()
        {
            Next(Phase.Buttons);
            actions.alpha = 1f;
            hint.gameObject.SetActive(false);
            if (reduceMotion)
            {
                upgradeButton.transform.localScale = Vector3.one;
                retryButton.transform.localScale = Vector3.one;
                FinishButtonReveal();
                return;
            }

            // Append keeps the retry button hidden until the upgrade popup has fully settled.
            float seconds = Mathf.Max(0.05f, buttonPopupSeconds);
            _buttonSequence = DOTween.Sequence().SetUpdate(true)
                .Append(upgradeButton.transform.DOScale(Vector3.one, seconds).SetEase(Ease.OutBack, 0.8f))
                .Append(retryButton.transform.DOScale(Vector3.one, seconds).SetEase(Ease.OutBack, 0.8f))
                .OnComplete(FinishButtonReveal);
        }

        private void CreateRelicDiscoveryOverlay()
        {
            if (overlay == null || _relicDiscoveryOverlay != null) return;
            RectTransform parent = overlay.transform as RectTransform;
            if (parent == null) return;

            GameObject root = new GameObject("RelicDiscoveryCelebration",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            _relicDiscoveryOverlay = root;
            RectTransform rootRect = root.transform as RectTransform;
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.SetAsLastSibling();
            Image dimmer = root.GetComponent<Image>();
            dimmer.color = new Color(0.005f, 0.008f, 0.02f, 0.86f);
            dimmer.raycastTarget = true;
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;

            float panelWidth = Mathf.Min(820f, Mathf.Max(460f, parent.rect.width - 56f));
            float panelHeight = Mathf.Min(680f, Mathf.Max(420f, parent.rect.height - 48f));
            Image popupImage = timeOutPopup != null ? timeOutPopup.GetComponent<Image>() : null;
            GameObject panelObject = new GameObject("RelicDiscoveryPanel",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(Shadow));
            RectTransform panel = panelObject.transform as RectTransform;
            panel.SetParent(rootRect, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(panelWidth, panelHeight);
            panel.localScale = Vector3.one * 0.90f;
            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.sprite = popupImage != null ? popupImage.sprite : null;
            panelImage.type = panelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            panelImage.color = new Color(0.035f, 0.028f, 0.04f, 0.98f);
            panelImage.raycastTarget = true;
            Outline border = panelObject.GetComponent<Outline>();
            border.effectColor = new Color(0.79f, 0.70f, 0.53f, 0.78f);
            border.effectDistance = new Vector2(1.5f, -2f);
            Shadow shadow = panelObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.76f);
            shadow.effectDistance = new Vector2(6f, -8f);

            Sprite glowSprite = null;
            if (progression != null)
                progression.GetRelicDiscoveryEffectSprites(out glowSprite, out _, out _);

            float viewportHeight = Mathf.Max(260f, panelHeight - 164f);
            RectTransform viewport = CreateCelebrationRect("RelicAwardViewport", panel,
                new Vector2(0.08f, 0.5f), new Vector2(0.92f, 0.5f),
                new Vector2(0f, 30f), new Vector2(-12f, viewportHeight), new Vector2(0.5f, 0.5f));
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateCelebrationRect("RelicAwardContent", viewport,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            _relicDiscoverySequence?.Kill(false);
            _relicDiscoverySequence = DOTween.Sequence().SetUpdate(true);
            HorizontalLayoutGroup row = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 18f;
            row.padding = new RectOffset(12, 12, 8, 8);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            ContentSizeFitter contentSizer = content.gameObject.AddComponent<ContentSizeFitter>();
            contentSizer.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizer.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroller = viewport.gameObject.AddComponent<ScrollRect>();
            scroller.viewport = viewport;
            scroller.content = content;
            scroller.horizontal = true;
            scroller.vertical = false;
            scroller.movementType = ScrollRect.MovementType.Clamped;
            scroller.scrollSensitivity = 34f;

            IReadOnlyList<string> awards = _currentResult.RelicAwards;
            int count = awards == null ? 0 : awards.Count;
            float itemWidth = Mathf.Clamp(
                (panelWidth - 96f) / Mathf.Max(1, Mathf.Min(count, 3)), 220f, 320f);
            for (int i = 0; i < count; i++)
                CreateRelicAwardCard(content, awards[i], itemWidth, viewportHeight, glowSprite);
            Canvas.ForceUpdateCanvases();
            scroller.horizontalNormalizedPosition = 0.5f;

            Button confirm = CreateCelebrationButton("RelicDiscoveryConfirm", panel,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(Mathf.Min(360f, panelWidth - 80f), 58f),
                "확인");
            confirm.onClick.AddListener(DismissRelicDiscovery);
            confirm.transform.localScale = Vector3.one;

            group.alpha = 0f;
            _relicDiscoveryIntroSequence = DOTween.Sequence().SetUpdate(true)
                .Append(DOTween.To(() => group.alpha, value => group.alpha = value, 1f, 0.20f)
                    .SetTarget(group))
                .Join(panel.DOScale(Vector3.one, 0.34f).SetEase(Ease.OutCubic));
        }

        private void CreateRelicAwardCard(
            RectTransform parent,
            string relicId,
            float itemWidth,
            float itemHeight,
            Sprite glowSprite)
        {
            GameObject itemObject = new GameObject(
                "RelicAward_" + relicId,
                typeof(RectTransform), typeof(LayoutElement));
            RectTransform item = itemObject.transform as RectTransform;
            item.SetParent(parent, false);
            LayoutElement element = itemObject.GetComponent<LayoutElement>();
            element.preferredWidth = itemWidth;
            element.preferredHeight = itemHeight;
            item.sizeDelta = new Vector2(itemWidth, itemHeight);

            string title = relicId;
            Sprite icon = null;
            if (progression != null)
                progression.TryGetRelicPresentation(relicId, out title, out icon);

            float iconSize = Mathf.Clamp(Mathf.Min(itemWidth * 0.78f, itemHeight * 0.64f), 150f, 260f);
            if (glowSprite != null)
            {
                RectTransform halo = CreateCelebrationImage("RelicHalo", item, glowSprite,
                    new Color(1f, 0.66f, 0.27f, 0.42f),
                    new Vector2(0f, 32f), new Vector2(iconSize * 1.55f, iconSize * 1.55f));
                halo.localScale = Vector3.one * 0.94f;
                Image haloImage = halo.GetComponent<Image>();
                if (_relicDiscoverySequence == null)
                    _relicDiscoverySequence = DOTween.Sequence().SetUpdate(true);
                _relicDiscoverySequence.Join(halo.DOScale(Vector3.one * 1.08f, 1.15f)
                        .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo))
                    .Join(DOTween.To(() => haloImage.color.a, alpha =>
                        {
                            Color color = haloImage.color;
                            color.a = alpha;
                            haloImage.color = color;
                        }, 0.66f, 1.15f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo));
            }
            if (icon != null)
            {
                RectTransform relicIcon = CreateCelebrationImage("Icon", item, icon,
                    Color.white, new Vector2(0f, 38f), new Vector2(iconSize, iconSize));
                relicIcon.GetComponent<Image>().preserveAspect = true;
                relicIcon.localScale = Vector3.one * 0.86f;
                _relicDiscoverySequence?.Join(relicIcon.DOScale(Vector3.one, 0.32f)
                    .SetEase(Ease.OutBack, 0.65f));
            }
            CreateCelebrationText("Name", item, title, 26,
                new Color(1f, 0.93f, 0.78f, 1f),
                new Vector2(0f, -itemHeight * 0.34f), new Vector2(itemWidth - 24f, 52f), true);
        }

        private RectTransform CreateCelebrationImage(string name, Transform parent, Sprite sprite,
            Color tint, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateCelebrationRect(name, parent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size,
                new Vector2(0.5f, 0.5f));
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = tint;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rect;
        }

        private TMP_Text CreateCelebrationText(string name, Transform parent, string value,
            float size, Color color, Vector2 position, Vector2 dimensions, bool bold)
        {
            RectTransform rect = CreateCelebrationRect(name, parent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position,
                dimensions, new Vector2(0.5f, 0.5f));
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = LocalizedTypography.GetFont(null);
            text.fontSize = size;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.text = value ?? string.Empty;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateCelebrationButton(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, string label)
        {
            RectTransform rect = CreateCelebrationRect(name, parent, anchorMin, anchorMax,
                position, size, new Vector2(0.5f, 0.5f));
            Image image = rect.gameObject.AddComponent<Image>();
            Image popupImage = timeOutPopup != null ? timeOutPopup.GetComponent<Image>() : null;
            image.sprite = popupImage != null ? popupImage.sprite : null;
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = new Color(0.13f, 0.14f, 0.14f, 1f);
            image.raycastTarget = true;
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.72f, 0.72f, 0.62f, 0.88f);
            outline.effectDistance = new Vector2(2f, -2f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            CreateCelebrationText("Label", rect, label, 22f, Color.white,
                Vector2.zero, size - new Vector2(22f, 12f), true);
            return button;
        }

        private static RectTransform CreateCelebrationRect(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform rect = child.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private void FinishButtonReveal()
        {
            _buttonSequence = null;
            if (_phase != Phase.Buttons) return;
            _phase = Phase.Ready;
            actions.interactable = true;
        }

        private void OpenUpgrades()
        {
            if (!IsReady || _navigating || huntManager == null) return;
            _navigating = true;
            actions.interactable = false;
            huntManager.ReturnToResearch();
            if (IsVisible) RestoreActionsAfterRejectedNavigation();
        }

        private void Retry()
        {
            if (!IsReady || _navigating || huntManager == null) return;
            _navigating = true;
            actions.interactable = false;
            huntManager.ContinueFromResult();
            // A rejected start keeps the result available instead of stranding the player.
            if (IsVisible) RestoreActionsAfterRejectedNavigation();
        }

        private void RestoreActionsAfterRejectedNavigation()
        {
            _navigating = false;
            actions.interactable = true;
            subtitle.gameObject.SetActive(true);
            SetLocalizedText(subtitle, "ui.result.action.retry.failed", "진행을 완료하지 못했습니다. 다시 눌러 재시도하세요.");
        }

        private void FitLayout()
        {
            if (layout == null || !(layout.parent is RectTransform parent)) return;
            float scale = Mathf.Min(1f, (parent.rect.width - 48f) / layout.sizeDelta.x,
                (parent.rect.height - 48f) / layout.sizeDelta.y);
            layout.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }

        private static void SetAmount(RewardRow row, long value)
        {
            if (row.displayed == value) return;
            row.displayed = value;
            row.amount.text = "+" + value.ToString("N0", CultureInfo.InvariantCulture);
        }

        private void ResizeResult()
        {
            float height = Mathf.Max(350f, 160f + _visibleRewardCount * 56f);
            ((RectTransform)resultUI.transform).sizeDelta = new Vector2(1040f, height);
            layout.sizeDelta = new Vector2(1040f, height + 150f);
            rewardRows.sizeDelta = new Vector2(620f, _visibleRewardCount * 56f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rewardRows);
        }

        public static long GetEarned(RunResult result, string currencyId)
        {
            long amount = 0;
            bool found = false;
            if (result.Rewards != null)
                foreach (ResourceRewardSnapshot reward in result.Rewards)
                    if (reward.CurrencyId == currencyId)
                    {
                        found = true;
                        amount = reward.Amount > long.MaxValue - amount ? long.MaxValue : amount + reward.Amount;
                    }
            return !found && currencyId == "gem.garnet" ? result.GarnetEarned : amount;
        }

        public static long InterpolateAmount(long amount, float progress)
        {
            // Decimal avoids double rounding long.MaxValue to 2^63 during the final frame.
            if (progress >= 1f) return amount;
            if (progress <= 0f) return 0;
            return (long)(amount * (decimal)progress);
        }

    }
}
