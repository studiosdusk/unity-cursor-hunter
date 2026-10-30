using System;
using System.Globalization;
using CursorHunter.Contracts;
using CursorHunter.Data;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.App
{
    /// <summary>Presentation only. RunResult remains the authoritative, already settled reward snapshot.</summary>
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
        private enum Phase { Hidden, TimeOut, Dismiss, Rewards, Buttons, Ready }
        private Phase _phase;
        private float _clock;
        private RunId _runId;
        private bool _navigating;
        private int _visibleRewardCount;
        private Sequence _buttonSequence;
        private AudioSource _countTickSource;
        private AudioClip _countTickClip;

        private void Awake()
        {
            foreach (TMP_Text label in overlay.GetComponentsInChildren<TMP_Text>(true))
            {
                // Keep the downloaded Rubik face for numerals; use the project's Korean fallback for Hangul.
                if (label == timeOutTitle || label == kills || label == damage ||
                    label.name == "Amount") continue;
                label.font = LocalizedTypography.GetFont(null);
                if (label.fontSize >= 24f) label.fontStyle |= FontStyles.Bold;
            }
            upgradeButton.onClick.AddListener(OpenUpgrades);
            retryButton.onClick.AddListener(Retry);
            skipButton.onClick.AddListener(CompleteReveal);
            resultSkipButton.onClick.AddListener(CompleteReveal);
            CreateCountTickAudio();
            Hide();
        }

        private void OnEnable()
        {
            if (uiRoot != null) uiRoot.ScreenChanged += OnScreenChanged;
        }

        private void OnDisable()
        {
            if (uiRoot != null) uiRoot.ScreenChanged -= OnScreenChanged;
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
            _phase = Phase.TimeOut;
            overlay.SetActive(true);
            timeOutPanel.SetActive(true);
            timeOutTitle.text = result.EndReason == RunEndReason.TimeExpired ? "TIME OUT!" : "HUNT OVER";
            title.text = "전투 종료";
            subtitle.gameObject.SetActive(result.SettlementPolicy == RunSettlementPolicy.Discard);
            subtitle.text = "테스트 전투 / 보상 지급 없음";
            TMP_Text upgradeLabel = upgradeButton.GetComponentInChildren<TMP_Text>(true);
            if (upgradeLabel != null)
                upgradeLabel.text = huntManager != null && huntManager.IsTraitScreenConnectedToCombat
                    ? "특성 강화" : "로비로";
            hint.text = "클릭하면 바로 보기";
            hint.gameObject.SetActive(true);
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
            duration.text = result.ElapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "초";
            damage.text = result.EffectiveDamage.ToString("N0", CultureInfo.InvariantCulture);
            FitLayout();
            RenderTimeOut(0f);
        }

        public void Hide()
        {
            StopCountTickAudio();
            _phase = Phase.Hidden;
            _buttonSequence?.Kill(false);
            _buttonSequence = null;
            _clock = 0f;
            _runId = default;
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

        private void Update()
        {
            if (_phase == Phase.Hidden) return;
            FitLayout();
            if (_phase == Phase.Ready || _phase == Phase.Buttons) return;
            _clock += Time.unscaledDeltaTime;
            switch (_phase)
            {
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
            timeOutPopup.localScale = Vector3.one * (reduceMotion || t >= 0.5f ? 1f : 0.82f);
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
        }

        private void Retry()
        {
            if (!IsReady || _navigating || huntManager == null) return;
            _navigating = true;
            actions.interactable = false;
            huntManager.ReplayLastRun();
            // A rejected start keeps the result available instead of stranding the player.
            if (IsVisible) { _navigating = false; actions.interactable = true; }
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
