using CursorHunter.Contracts;
using CursorHunter.Combat;
using CursorHunter.Progression;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CursorHunter.Data;

namespace CursorHunter.App
{
    /// <summary>
    /// Lightweight runtime HUD for the prototype timer and result state.
    /// It intentionally avoids editing the existing art/UI prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeRunHud : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private TraitScreenController progressionController;
        [SerializeField] private Font uiFont;

        private CombatRunController _combatController;
        private TextMeshProUGUI _timerText;
        private TextMeshProUGUI _criticalNotice;
        private Button _progressionInfoButton;
        private GameObject _resultPanel;
        private TextMeshProUGUI _resultText;
        private TMP_FontAsset _runtimeFont;
        private float _criticalNoticeUntil;
        private bool _criticalSubscribed;

        public void Initialize()
        {
            if (canvas == null)
            {
                GameObject canvasObject = GameObject.Find("UICanvas");
                if (canvasObject != null)
                {
                    canvas = canvasObject.GetComponent<Canvas>();
                }
            }

            if (canvas == null)
            {
                canvas = FindFirstObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                Debug.LogWarning("PrototypeRunHud could not find a Canvas.", this);
                return;
            }

            if (progressionController == null)
            {
                progressionController = FindFirstObjectByType<TraitScreenController>(
                    FindObjectsInactive.Include);
            }

            if (_combatController == null)
            {
                _combatController = FindFirstObjectByType<CombatRunController>(
                    FindObjectsInactive.Include);
            }

            SubscribeCriticalFeedback();

            if (_runtimeFont == null)
            {
                _runtimeFont = LocalizedTypography.GetFont(uiFont);
            }

            if (_timerText == null)
            {
                _timerText = CreateText(
                    "PrototypeTimerText",
                    canvas.transform,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(32f, -28f),
                    new Vector2(260f, 90f),
                    26,
                    TextAnchor.UpperLeft);
            }

            if (_resultPanel == null)
            {
                _resultPanel = CreateResultPanel();
            }

            if (_criticalNotice == null)
            {
                _criticalNotice = CreateCriticalNotice();
            }

            if (_progressionInfoButton == null)
            {
                _progressionInfoButton = CreateProgressionInfoButton();
            }

            _resultPanel.SetActive(false);
            if (_criticalNotice != null)
            {
                _criticalNoticeUntil = 0f;
                _criticalNotice.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (_criticalNotice != null &&
                _criticalNotice.gameObject.activeSelf &&
                Time.unscaledTime >= _criticalNoticeUntil)
            {
                _criticalNotice.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            UnsubscribeCriticalFeedback();
        }

        private void OnDestroy()
        {
            UnsubscribeCriticalFeedback();
        }

        public void ShowRunning(float remainingSeconds, int defeatedCount, long garnetEarned)
        {
            if (_timerText == null)
            {
                Initialize();
            }

            if (_timerText == null)
            {
                return;
            }

            int displaySeconds = Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds));
            _timerText.text =
                $"TIME {displaySeconds:00}\nKILLS {defeatedCount}\nGARNET +{garnetEarned}";
            _timerText.gameObject.SetActive(true);

            if (_progressionInfoButton != null)
            {
                _progressionInfoButton.gameObject.SetActive(true);
            }

            if (_resultPanel != null)
            {
                _resultPanel.SetActive(false);
            }

        }

        private void SubscribeCriticalFeedback()
        {
            if (_criticalSubscribed || _combatController == null)
            {
                return;
            }

            _combatController.CriticalHit += HandleCriticalHit;
            _criticalSubscribed = true;
        }

        private void UnsubscribeCriticalFeedback()
        {
            if (!_criticalSubscribed || _combatController == null)
            {
                return;
            }

            _combatController.CriticalHit -= HandleCriticalHit;
            _criticalSubscribed = false;
        }

        private void HandleCriticalHit(long effectiveDamage)
        {
            if (_criticalNotice == null)
            {
                return;
            }

            _criticalNotice.text = "치명타! ×2\n피해 " + effectiveDamage;
            _criticalNoticeUntil = Time.unscaledTime + 0.45f;
            _criticalNotice.gameObject.SetActive(true);
        }

        public void ShowResult(RunResult result)
        {
            if (_resultPanel == null)
            {
                Initialize();
            }

            if (_timerText != null)
            {
                _timerText.gameObject.SetActive(false);
            }

            if (_resultPanel == null || _resultText == null)
            {
                return;
            }

            bool wasAborted = result.EndReason == RunEndReason.UserExit ||
                              result.EndReason == RunEndReason.StartFailed ||
                              result.EndReason == RunEndReason.NumericOverflow;
            string resultTitle = wasAborted
                ? "HUNT ABORTED"
                : "HUNT COMPLETE";

            StringBuilder rewardText = new StringBuilder();
            int displayedRewardCount = 0;
            if (result.Rewards != null)
            {
                for (int i = 0; i < result.Rewards.Count; i++)
                {
                    ResourceRewardSnapshot reward = result.Rewards[i];
                    if (reward.CurrencyId == "gem.garnet")
                    {
                        continue;
                    }

                    if (displayedRewardCount == 0)
                    {
                        rewardText.Append("\nREWARDS ");
                    }

                    if (displayedRewardCount > 0)
                    {
                        rewardText.Append(", ");
                    }

                    rewardText.Append(reward.CurrencyId)
                        .Append(" +")
                        .Append(reward.Amount);
                    displayedRewardCount++;
                }
            }

            _resultText.text =
                resultTitle + "\n\n" +
                $"TIME {result.ElapsedSeconds:0.0}s\n" +
                $"KILLS {result.DefeatedCount}\n" +
                $"GARNET +{result.GarnetEarned}\n" +
                $"DAMAGE {result.EffectiveDamage}" +
                rewardText;
            _resultPanel.SetActive(true);

            if (_progressionInfoButton != null)
            {
                _progressionInfoButton.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Opens the same key/value progression summary used by the trait
        /// screen. Keeping this entry point in the field HUD lets normal and
        /// boss combat inspect the applied build without leaving the run.
        /// </summary>
        public void ShowProgressionInfo()
        {
            if (progressionController == null)
            {
                progressionController = FindFirstObjectByType<TraitScreenController>(
                    FindObjectsInactive.Include);
            }

            if (progressionController == null)
            {
                Debug.LogWarning(
                    "PrototypeRunHud could not find the progression controller.",
                    this);
                return;
            }

            progressionController.ShowSummaryPopup();
        }

        /// <summary>
        /// Clears both the running timer and the result panel for a new test.
        /// </summary>
        public void Reset()
        {
            if (_timerText != null)
            {
                _timerText.text = string.Empty;
                _timerText.gameObject.SetActive(false);
            }

            if (_resultPanel != null)
            {
                _resultPanel.SetActive(false);
            }

            if (_progressionInfoButton != null)
            {
                _progressionInfoButton.gameObject.SetActive(false);
            }

            if (_criticalNotice != null)
            {
                _criticalNotice.gameObject.SetActive(false);
            }
        }

        private Button CreateProgressionInfoButton()
        {
            GameObject buttonObject = new GameObject(
                "ProgressionInfoButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(canvas.transform, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-28f, -28f);
            rect.sizeDelta = new Vector2(190f, 52f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.10f, 0.32f, 0.48f, 0.96f);
            image.raycastTarget = true;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.18f, 0.46f, 0.64f, 0.98f);
            colors.pressedColor = new Color(0.07f, 0.23f, 0.36f, 0.98f);
            colors.disabledColor = new Color(0.10f, 0.16f, 0.26f, 0.68f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(ShowProgressionInfo);

            CreateText(
                "Label",
                buttonObject.transform,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(-16f, -8f),
                18,
                TextAnchor.MiddleCenter).text = LocalizationCatalog.Get("ui.info.all", "전체 정보");
            return button;
        }

        private TextMeshProUGUI CreateCriticalNotice()
        {
            TextMeshProUGUI notice = CreateText(
                "CriticalHitNotice",
                canvas.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 170f),
                new Vector2(320f, 86f),
                28,
                TextAnchor.MiddleCenter);
            notice.color = new Color(1f, 0.82f, 0.20f, 1f);
            notice.fontStyle = FontStyles.Bold;
            notice.gameObject.SetActive(false);
            return notice;
        }

        private GameObject CreateResultPanel()
        {
            GameObject panelObject = new GameObject(
                "PrototypeResultPanel",
                typeof(RectTransform),
                typeof(Image));
            panelObject.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(560f, 320f);

            Image image = panelObject.GetComponent<Image>();
            image.color = new Color(0.06f, 0.08f, 0.12f, 0.94f);
            image.raycastTarget = true;

            _resultText = CreateText(
                "ResultText",
                panelObject.transform,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(-48f, -48f),
                28,
                TextAnchor.MiddleCenter);

            return panelObject;
        }

        private TextMeshProUGUI CreateText(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            int fontSize,
            TextAnchor alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = _runtimeFont;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = LocalizedTypography.Alignment(alignment);

            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
