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
            public TMP_Text name;
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
        [SerializeField] private TMP_FontAsset relicDiscoveryFont;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private TMP_Text rewardHeading;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private TMP_Text emptyRewards;
        [SerializeField] private RectTransform rewardRows;
        [SerializeField] private RewardRow[] rewards = Array.Empty<RewardRow>();
        [SerializeField] private RectTransform experienceIcon;
        [SerializeField] private TMP_Text experienceLabel;
        [SerializeField] private TMP_Text experienceAmount;
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
        [SerializeField, Min(0.05f)] private float resultPopupSeconds = 0.24f;
        [SerializeField, Min(0.05f)] private float countSeconds = 0.3f;
        [SerializeField, Min(0.05f)] private float buttonPopupSeconds = 0.22f;
        [SerializeField] private bool reduceMotion;
        [SerializeField, Range(0f, 1f)] private float countTickVolume = 0.14f;

        public bool IsVisible => overlay != null && overlay.activeSelf;
        public bool IsReady => _phase == Phase.Ready;
        private bool IsBossPresentationRunning =>
            collection != null && collection.IsFinishingCollection || huntManager != null && huntManager.IsBossDeathPlaying;
        private enum Phase { Hidden, BossLoot, TimeOut, Dismiss, Rewards, RelicDiscovery, Buttons, Ready }
        private Phase _phase;
        private float _clock;
        private RunId _runId;
        private bool _navigating;
        private int _visibleRewardCount;
        private long _displayedExperience = -1;
        private Sequence _buttonSequence;
        private Sequence _relicDiscoverySequence;
        private Sequence _relicDiscoveryIntroSequence;
        private GameObject _relicDiscoveryOverlay;
        private AudioSource _countTickSource;
        private AudioClip _countTickClip;
        private RunResult _currentResult;
        private bool _hasCurrentResult;
        private bool _navigationFailed;

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
            hint.gameObject.SetActive(true);
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
            _displayedExperience = -1;
            SetExperienceAmount(0L);
            ResizeResult();
            FitLayout();
            RenderTimeOut(0f);
            if (result.BossDefeated && IsBossPresentationRunning)
            {
                _phase = Phase.BossLoot;
                overlay.SetActive(false); // Keep both the death animation and the loot burst visible.
            }
        }

        public void Hide()
        {
            StopCountTickAudio();
            _phase = Phase.Hidden;
            _buttonSequence?.Kill(false);
            _buttonSequence = null;
            ClearRelicDiscovery();
            _clock = 0f;
            _runId = default;
            _hasCurrentResult = false;
            _navigating = false;
            _navigationFailed = false;
            if (overlay != null) overlay.SetActive(false);
            if (timeOutPanel != null) timeOutPanel.SetActive(false);
            if (resultUI != null)
            {
                resultUI.gameObject.SetActive(false);
                resultUI.alpha = 1f;
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
            else if (boss)
                SetLocalizedFormat(title,
                    result.BossDefeated ? "ui.result.boss.clear.title" : "ui.result.boss.failed.title",
                    result.BossDefeated ? "보스 {0}단계 · 토벌 성공" : "보스 {0}단계 · 토벌 실패",
                    result.BossStage);
            else
                SetLocalizedText(
                    title,
                    retired ? "ui.result.retired.title" : "ui.result.title",
                    retired ? "사냥 중도 포기" : "전투 종료");
            subtitle.gameObject.SetActive(_navigationFailed || result.SettlementPolicy == RunSettlementPolicy.Discard);
            SetLocalizedText(
                subtitle,
                _navigationFailed ? "ui.result.action.retry.failed" : retired && boss ? "ui.result.boss.retired.subtitle" :
                    boss && !result.BossDefeated ? "ui.result.boss.failed.subtitle" : "ui.result.test.no.reward",
                _navigationFailed ? "진행을 완료하지 못했습니다. 다시 눌러 재시도하세요." : retired && boss ? "보스 처치 전에 포기하여 획득한 보상이 없습니다." : boss && !result.BossDefeated
                    ? "시간 안에 토벌하지 못했습니다. 강화 후 다시 도전하세요."
                    : "테스트 전투 · 보상 미지급");

            TMP_Text upgradeLabel = upgradeButton.GetComponentInChildren<TMP_Text>(true);
            if (upgradeLabel != null)
            {
                bool connected = huntManager != null && huntManager.IsTraitScreenConnectedToCombat;
                SetLocalizedText(
                    upgradeLabel,
                    connected ? "ui.result.action.upgrade" : "ui.result.action.lobby",
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
            SetLocalizedText(rewardHeading, "ui.result.rewards.heading", "획득 보상");
            SetLocalizedText(experienceLabel, "ui.result.experience", "획득 경험치");
            foreach (RewardRow row in rewards)
            {
                if (row.name == null) continue;
                SetLocalizedText(row.name, "ui.result." + row.currencyId, GemFallback(row.currencyId));
            }
            SetLocalizedText(hint, "ui.result.hint", "클릭하면 보상을 바로 볼 수 있어요.");
            SetLocalizedText(emptyRewards, "ui.result.empty.gems", "획득한 젬스톤이 없습니다.");
        }

        private static void SetLocalizedText(TMP_Text label, string key, string fallback)
        {
            if (label == null) return;
            label.GetComponent<LocalizedTmpLabel>()?.Unbind();
            ResultUiLocalizedLabel localized = label.GetComponent<ResultUiLocalizedLabel>();
            if (localized == null) localized = label.gameObject.AddComponent<ResultUiLocalizedLabel>();
            localized.Bind(key, fallback);
        }

        private static void SetLocalizedFormat(
            TMP_Text label,
            string key,
            string fallbackFormat,
            params object[] arguments)
        {
            if (label == null) return;
            label.GetComponent<LocalizedTmpLabel>()?.Unbind();
            ResultUiLocalizedLabel localized = label.GetComponent<ResultUiLocalizedLabel>();
            if (localized == null) localized = label.gameObject.AddComponent<ResultUiLocalizedLabel>();
            localized.Bind(key, fallbackFormat, arguments);
        }

        private static string GemFallback(string currencyId) => currencyId switch
        {
            "gem.garnet" => "가넷",
            "gem.topaz" => "토파즈",
            "gem.amethyst" => "자수정",
            "gem.sapphire" => "사파이어",
            "gem.diamond" => "다이아몬드",
            "gem.dragon" => "드래곤 젬",
            _ => currencyId
        };

        private void Update()
        {
            if (_phase == Phase.Hidden) return;
            FitLayout();
            if (_phase == Phase.Ready || _phase == Phase.Buttons || _phase == Phase.RelicDiscovery) return;
            _clock += Time.unscaledDeltaTime;
            switch (_phase)
            {
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
                        if (HasRelicDiscovery()) BeginRelicDiscovery();
                    }
                    break;
                case Phase.Rewards:
                    RenderRewards(_clock);
                    if (_clock >= RevealDuration) CompleteReveal();
                    break;
            }
        }

        private bool HasCountableReward => _visibleRewardCount > 0 || _currentResult.ExperienceEarned > 0L;
        private float RevealDuration => reduceMotion ? 0f :
            Mathf.Max(Mathf.Max(0.05f, resultPopupSeconds), HasCountableReward ? countSeconds : 0f);
        private void Next(Phase phase) { _phase = phase; _clock = 0f; }

        private void RenderTimeOut(float progress)
        {
            float t = Mathf.Clamp01(progress);
            // Keep the full-screen skip target without darkening the battlefield.
            backdrop.alpha = 0f;
            timeOutGroup.alpha = 1f;
            // A single continuous expansion; the previous 82%/100% step looked like two pops.
            float scale = reduceMotion ? 1f : Mathf.Lerp(0.82f, 1f, 1f - Mathf.Pow(1f - t, 3f));
            timeOutPopup.localScale = Vector3.one * scale;
        }

        private void RenderRewards(float elapsed)
        {
            resultUI.alpha = 1f;
            float popupProgress = reduceMotion ? 1f : Mathf.Clamp01(elapsed / Mathf.Max(0.05f, resultPopupSeconds));
            float popupEase = 1f - Mathf.Pow(1f - popupProgress, 3f);
            resultUI.transform.localScale = Vector3.one * Mathf.Lerp(0.82f, 1f, popupEase);
            float progress = reduceMotion ? 1f : Mathf.Clamp01(elapsed / Mathf.Max(0.05f, countSeconds));
            float easedProgress = progress * progress * (3f - 2f * progress);
            if (HasCountableReward && progress < 1f && countTickVolume > 0f)
                StartCountTickAudio();
            else
                StopCountTickAudio();
            foreach (RewardRow row in rewards)
            {
                if (row.earned <= 0) continue;
                SetAmount(row, InterpolateAmount(row.earned, easedProgress));
            }
            SetExperienceAmount(InterpolateAmount(_currentResult.ExperienceEarned, easedProgress));
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
            if (_relicDiscoveryOverlay != null)
            {
                // The result continues revealing behind the modal, but its actions
                // must remain locked until the player confirms the discovery.
                _phase = Phase.RelicDiscovery;
                return;
            }
            RevealResultActions();
        }

        private bool HasRelicDiscovery() => _currentResult.SettlementPolicy == RunSettlementPolicy.Eligible &&
            _currentResult.RelicAwards != null && _currentResult.RelicAwards.Count > 0;

        private void BeginRelicDiscovery()
        {
            if (_phase != Phase.Rewards || !HasRelicDiscovery()) return;
            CreateRelicDiscoveryOverlay();
            if (_relicDiscoveryOverlay != null) hint.gameObject.SetActive(false);
        }

        private void DismissRelicDiscovery()
        {
            if (_relicDiscoveryOverlay == null ||
                (_phase != Phase.Rewards && _phase != Phase.RelicDiscovery)) return;
            ClearRelicDiscovery();
            if (_phase == Phase.Rewards) RenderRewards(RevealDuration);
            RevealResultActions();
        }

        private void ClearRelicDiscovery()
        {
            _relicDiscoverySequence?.Kill(false);
            _relicDiscoverySequence = null;
            _relicDiscoveryIntroSequence?.Kill(false);
            _relicDiscoveryIntroSequence = null;
            if (_relicDiscoveryOverlay != null)
            {
                _relicDiscoveryOverlay.SetActive(false);
                Destroy(_relicDiscoveryOverlay);
            }
            _relicDiscoveryOverlay = null;
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
            Image inputBlocker = root.GetComponent<Image>();
            inputBlocker.color = new Color(0f, 0f, 0f, 0.72f);
            inputBlocker.raycastTarget = true;
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;

            float panelWidth = Mathf.Min(1000f, Mathf.Max(1f, parent.rect.width - 56f));
            float panelHeight = Mathf.Min(720f, Mathf.Max(1f, parent.rect.height - 48f));
            RectTransform panel = CreateCelebrationRect("RelicDiscoveryPanel", rootRect,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(panelWidth, panelHeight), new Vector2(0.5f, 0.5f));

            float raySize = Mathf.Min(panelWidth * 1.2f, panelHeight * 1.8f);
            RectTransform rays = CreateCelebrationRect("RelicDiscoveryRays", panel,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, panelHeight * 0.08f), new Vector2(raySize, raySize),
                new Vector2(0.5f, 0.5f));
            RelicDiscoveryRays rayGraphic = rays.gameObject.AddComponent<RelicDiscoveryRays>();
            rayGraphic.color = new Color(1f, 0.75f, 0.28f, 0.18f);
            rayGraphic.raycastTarget = false;

            Sprite glowSprite = null;
            Sprite sparkleSprite = null;
            if (progression != null)
                progression.GetRelicDiscoveryEffectSprites(out glowSprite, out sparkleSprite, out _);

            _relicDiscoverySequence?.Kill(false);
            _relicDiscoverySequence = null;
            if (!reduceMotion)
            {
                _relicDiscoverySequence = DOTween.Sequence().SetUpdate(true)
                    .Join(rays.DOLocalRotate(new Vector3(0f, 0f, 8f), 3.2f).SetEase(Ease.InOutSine))
                    .SetLoops(-1, LoopType.Yoyo);
            }
            if (sparkleSprite != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    float angle = (i * 45f + 15f) * Mathf.Deg2Rad;
                    Vector2 position = new Vector2(Mathf.Cos(angle) * panelWidth * 0.36f,
                        Mathf.Sin(angle) * panelHeight * 0.34f + panelHeight * 0.08f);
                    float size = i % 2 == 0 ? 30f : 20f;
                    RectTransform sparkle = CreateCelebrationImage("Sparkle_" + i, panel, sparkleSprite,
                        new Color(1f, 0.88f, 0.52f, 0.4f), position, new Vector2(size, size));
                    if (_relicDiscoverySequence != null)
                    {
                        Image sparkleImage = sparkle.GetComponent<Image>();
                        _relicDiscoverySequence.Join(DOTween.To(() => sparkleImage.color.a, alpha =>
                        {
                            Color tint = sparkleImage.color;
                            tint.a = alpha;
                            sparkleImage.color = tint;
                        }, 0.9f, 2.4f).SetTarget(sparkleImage));
                    }
                }
            }

            float viewportHeight = panelHeight * 0.66f;
            RectTransform viewport = CreateCelebrationRect("RelicAwardViewport", panel,
                new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.5f),
                new Vector2(0f, panelHeight * 0.04f), new Vector2(0f, viewportHeight), new Vector2(0.5f, 0.5f));
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateCelebrationRect("RelicAwardContent", viewport,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
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
            float visibleItems = count > 3 ? 3.35f : Mathf.Max(1, count);
            float itemWidth = Mathf.Clamp(panelWidth * 0.86f / visibleItems, 200f, 760f);
            for (int i = 0; i < count; i++)
                CreateRelicAwardCard(content, awards[i], itemWidth, viewportHeight, glowSprite, count);
            Canvas.ForceUpdateCanvases();
            scroller.horizontalNormalizedPosition = 0f;

            TMP_Text heading = CreateCelebrationText("RelicDiscoveryTitle", panel, "유물 발견!", 112f,
                Color.white, new Vector2(0f, panelHeight * 0.16f),
                new Vector2(panelWidth * 0.9f, panelHeight * 0.21f), true);
            if (relicDiscoveryFont != null) heading.font = relicDiscoveryFont;
            heading.enableAutoSizing = true;
            heading.fontSizeMin = 30f;
            heading.fontSizeMax = 112f;
            SetLocalizedText(heading, "ui.result.relic.discovery", "유물 발견!");
            LocalizedTypography.ApplyOutline(heading);

            if (count > 3)
            {
                TMP_Text scrollHint = CreateCelebrationText("RelicScrollHint", panel, "↔", 34f,
                    new Color(1f, 1f, 1f, 0.8f), new Vector2(0f, -panelHeight * 0.29f),
                    new Vector2(100f, 42f), true);
                LocalizedTypography.ApplyOutline(scrollHint);
            }

            Button confirm = CreateCelebrationButton("RelicDiscoveryConfirm", panel,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -panelHeight * 0.39f),
                new Vector2(Mathf.Min(480f, panelWidth * 0.72f), Mathf.Min(88f, panelHeight * 0.14f)),
                "OK");
            confirm.onClick.AddListener(DismissRelicDiscovery);
            confirm.transform.localScale = Vector3.one;

            group.alpha = reduceMotion ? 1f : 0f;
            if (!reduceMotion)
            {
                panel.localScale = Vector3.one * 0.88f;
                rays.localScale = Vector3.one * 0.35f;
                _relicDiscoveryIntroSequence = DOTween.Sequence().SetUpdate(true)
                    .Append(DOTween.To(() => group.alpha, value => group.alpha = value, 1f, 0.20f)
                        .SetTarget(group))
                    .Join(panel.DOScale(Vector3.one, 0.34f).SetEase(Ease.OutCubic))
                    .Join(rays.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutCubic));
            }
        }

        private void CreateRelicAwardCard(
            RectTransform parent,
            string relicId,
            float itemWidth,
            float itemHeight,
            Sprite glowSprite,
            int awardCount)
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

            string textKey = string.Empty;
            Sprite icon = null;
            if (progression != null)
                progression.TryGetRelicPresentation(relicId, out textKey, out icon);

            float iconSize = Mathf.Min(itemWidth * 0.78f, itemHeight * 0.82f, 360f);
            if (glowSprite != null)
            {
                RectTransform halo = CreateCelebrationImage("RelicHalo", item, glowSprite,
                    new Color(1f, 0.66f, 0.27f, 0.42f),
                    new Vector2(0f, itemHeight * 0.12f), new Vector2(iconSize * 1.55f, iconSize * 1.55f));
                if (_relicDiscoverySequence != null)
                    _relicDiscoverySequence.Join(halo.DOScale(Vector3.one * 1.08f, 2.4f)
                        .SetEase(Ease.InOutSine));
            }
            if (icon != null)
            {
                RectTransform relicIcon = CreateCelebrationImage("Icon", item, icon,
                    Color.white, new Vector2(0f, itemHeight * 0.12f), new Vector2(iconSize, iconSize));
                relicIcon.GetComponent<Image>().preserveAspect = true;
            }
            TMP_Text name = CreateCelebrationText("Name", item,
                string.IsNullOrWhiteSpace(textKey) ? relicId : string.Empty, awardCount == 1 ? 80f : 44f,
                Color.white, new Vector2(0f, -itemHeight * 0.19f),
                new Vector2(itemWidth - 24f, itemHeight * 0.22f), true);
            if (relicDiscoveryFont != null) name.font = relicDiscoveryFont;
            name.enableAutoSizing = true;
            name.fontSizeMin = 20f;
            name.fontSizeMax = awardCount == 1 ? 80f : 44f;
            LocalizedTypography.ApplyOutline(name);
            if (!string.IsNullOrWhiteSpace(textKey))
            {
                RelicLocalizedLabel localized = name.gameObject.AddComponent<RelicLocalizedLabel>();
                localized.Bind(textKey + ".title");
            }
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
            text.font = title != null && title.font != null ? title.font : LocalizedTypography.GetFont(null);
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
            Image actionImage = upgradeButton != null ? upgradeButton.image : null;
            image.sprite = actionImage != null ? actionImage.sprite : null;
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = Color.white;
            image.pixelsPerUnitMultiplier = 2f;
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            TMP_Text buttonLabel = CreateCelebrationText("Label", rect, label, 46f, Color.white,
                Vector2.zero, size - new Vector2(22f, 12f), true);
            buttonLabel.enableAutoSizing = true;
            buttonLabel.fontSizeMin = 20f;
            buttonLabel.fontSizeMax = 46f;
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
            _navigationFailed = true;
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

        private void SetExperienceAmount(long value)
        {
            if (experienceAmount == null || _displayedExperience == value) return;
            _displayedExperience = value;
            experienceAmount.text = "+" + value.ToString("N0", CultureInfo.InvariantCulture);
        }

        private void ResizeResult()
        {
            const float spacing = 6f;
            int count = Mathf.Max(1, _visibleRewardCount);
            float availableHeight = rewardRows.rect.height;
            float rowHeight = Mathf.Min(150f, (availableHeight - spacing * (count - 1)) / count);
            float textSize = Mathf.Lerp(64f, 30f, Mathf.Clamp01((_visibleRewardCount - 1) / 5f));
            float iconSize = Mathf.Clamp(rowHeight * 0.8f, 38f, 100f);
            foreach (RewardRow row in rewards)
            {
                if (row.group == null) continue;
                RectTransform rect = row.group.transform as RectTransform;
                if (rect != null) rect.sizeDelta = new Vector2(rewardRows.rect.width, rowHeight);
                if (row.icon != null) row.icon.sizeDelta = Vector2.one * iconSize;
                if (row.name != null)
                {
                    RectTransform nameRect = row.name.rectTransform;
                    nameRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeight);
                    nameRect.anchoredPosition = new Vector2(140f, nameRect.anchoredPosition.y);
                }
                if (row.amount != null) row.amount.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeight);
                SetAdaptiveTextSize(row.name, textSize);
                SetAdaptiveTextSize(row.amount, textSize);
            }
            if (experienceIcon != null)
                experienceIcon.sizeDelta = Vector2.one * Mathf.Min(56f, iconSize);
            float experienceSize = Mathf.Clamp(textSize * 0.78f, 30f, 48f);
            SetAdaptiveTextSize(experienceLabel, experienceSize);
            SetAdaptiveTextSize(experienceAmount, experienceSize);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rewardRows);
        }

        private static void SetAdaptiveTextSize(TMP_Text text, float maximum)
        {
            if (text == null) return;
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = maximum;
            text.fontSize = maximum;
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
