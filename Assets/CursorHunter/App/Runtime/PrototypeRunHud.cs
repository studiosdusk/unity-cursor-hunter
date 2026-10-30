using CursorHunter.Contracts;
using CursorHunter.Combat;
using CursorHunter.Progression;
using System;
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
        [SerializeField] private CombatGemstoneList gemstoneList;
        [SerializeField] private BattleResultPresentation battleResult;
        [Tooltip("씬에 배치된 남은 시간 텍스트. 초:1/100초로 표시합니다. 예: 15:00")]
        [SerializeField] private TextMeshProUGUI timerText;
        [Tooltip("남은 시간이 3초 이하일 때의 텍스트 색상")]
        [SerializeField] private Color timerWarningColor = new Color32(255, 102, 77, 255);

        private TextMeshProUGUI _timerText;
        private int _displayedTimerCentiseconds = -1;
        private Color _timerNormalColor;
        private bool _hasTimerNormalColor;
        private bool _showingRun;
        private Button _progressionInfoButton;
        private GameObject _resultPanel;
        private TextMeshProUGUI _resultText;
        private TMP_FontAsset _runtimeFont;
        private bool _traitConnectionEnabled;
        private bool _waitingForCollection;
        private RunResult _pendingResult;
        private RunId _activeRunId;
        private bool _acceptResult;
#if UNITY_EDITOR
        private TextMeshProUGUI _editorPlaytimeText;
        private double _editorPlaytimeStartedAt;
        private long _editorPlaytimeLastSecond = -1;
#endif

        private void Start()
        {
#if UNITY_EDITOR
            _editorPlaytimeStartedAt = Time.realtimeSinceStartupAsDouble;
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR
            UpdateEditorPlaytimeClock();
#endif
            if (!_waitingForCollection || gemstoneList != null && gemstoneList.IsFinishingCollection) return;
            _waitingForCollection = false;
            DisplayResult(_pendingResult);
        }

#if UNITY_EDITOR
        private void UpdateEditorPlaytimeClock()
        {
            if (_editorPlaytimeText == null)
            {
                if (canvas == null)
                {
                    canvas = GameObject.Find("UICanvas")?.GetComponent<Canvas>();
                }
                if (canvas == null)
                {
                    canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
                }
                if (canvas == null)
                {
                    return;
                }

                if (_runtimeFont == null)
                {
                    _runtimeFont = LocalizedTypography.GetFont(uiFont);
                }

                GameObject clockObject = new GameObject(
                    "EditorPlaytimeClock",
                    typeof(RectTransform),
                    typeof(Image));
                clockObject.transform.SetParent(canvas.transform, false);
                RectTransform clockRect = clockObject.GetComponent<RectTransform>();
                clockRect.anchorMin = new Vector2(0f, 1f);
                clockRect.anchorMax = new Vector2(0f, 1f);
                clockRect.pivot = new Vector2(0f, 1f);
                clockRect.anchoredPosition = new Vector2(24f, -24f);
                clockRect.sizeDelta = new Vector2(250f, 42f);

                Image clockBackground = clockObject.GetComponent<Image>();
                clockBackground.color = new Color(0.025f, 0.06f, 0.11f, 0.82f);
                clockBackground.raycastTarget = false;

                Canvas foreground = clockObject.AddComponent<Canvas>();
                foreground.overrideSorting = true;
                foreground.sortingLayerID = canvas.sortingLayerID;
                foreground.sortingOrder = canvas.sortingOrder + 30;

                _editorPlaytimeText = CreateText(
                    "Label",
                    clockObject.transform,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(-20f, -8f),
                    16,
                    TextAnchor.MiddleCenter);
                _editorPlaytimeText.color = new Color(0.84f, 0.91f, 1f, 1f);
                _editorPlaytimeText.fontStyle = FontStyles.Bold;
            }

            long elapsedSeconds = Math.Max(
                0L,
                (long)(Time.realtimeSinceStartupAsDouble - _editorPlaytimeStartedAt));
            if (elapsedSeconds == _editorPlaytimeLastSecond)
            {
                return;
            }

            _editorPlaytimeLastSecond = elapsedSeconds;
            long hours = elapsedSeconds / 3600;
            int minutes = (int)(elapsedSeconds / 60 % 60);
            int seconds = (int)(elapsedSeconds % 60);
            _editorPlaytimeText.text = $"플레이 경과 {hours:00}:{minutes:00}:{seconds:00}";
        }
#endif

        public void SetTraitConnectionEnabled(bool enabled)
        {
            _traitConnectionEnabled = enabled;
            if (_progressionInfoButton != null)
            {
                bool showingResult = battleResult == null && _resultPanel != null && _resultPanel.activeSelf;
                _progressionInfoButton.gameObject.SetActive(enabled && (_showingRun || showingResult));
            }
        }

        public void Initialize()
        {
            if (!_traitConnectionEnabled && _progressionInfoButton != null)
                _progressionInfoButton.gameObject.SetActive(false);
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

            if (_traitConnectionEnabled && progressionController == null)
            {
                progressionController = FindFirstObjectByType<TraitScreenController>(
                    FindObjectsInactive.Include);
            }

            if (_runtimeFont == null)
            {
                _runtimeFont = LocalizedTypography.GetFont(uiFont);
            }

            if (gemstoneList == null)
                gemstoneList = canvas.GetComponentInChildren<CombatGemstoneList>(true);

            if (_timerText == null)
            {
                _timerText = timerText != null ? timerText : CreateText(
                    "PrototypeTimerText",
                    canvas.transform,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -28f),
                    new Vector2(260f, 90f),
                    26,
                    TextAnchor.UpperCenter);
                _displayedTimerCentiseconds = -1;
            }

            if (battleResult != null)
                battleResult.Hide();
            if (battleResult == null && _resultPanel == null)
            {
                _resultPanel = CreateResultPanel();
            }

            if (_traitConnectionEnabled && _progressionInfoButton == null)
            {
                _progressionInfoButton = CreateProgressionInfoButton();
            }

            if (_resultPanel != null) _resultPanel.SetActive(false);
        }

        public void ShowRunning(CombatRunController combat, MonsterSpawner spawner)
        {
            if (combat == null) return;
            _activeRunId = combat.CurrentRunId;
            _acceptResult = combat.IsRunActive;
            _showingRun = true;
            _waitingForCollection = false;
            if (_timerText == null || _traitConnectionEnabled && _progressionInfoButton == null)
            {
                Initialize();
            }

            if (_timerText == null)
            {
                return;
            }

            gemstoneList?.ShowRunning(combat, spawner);
            if (timerText != null)
            {
                ShowRemainingTime(combat.RemainingSeconds);
            }
            else
            {
                int displaySeconds = Mathf.CeilToInt(Mathf.Max(0f, combat.RemainingSeconds));
                _timerText.text = $"TIME {displaySeconds:00}\nKILLS {combat.DefeatedCount}";
                if (gemstoneList == null)
                    _timerText.text += $"\nGARNET +{combat.GarnetEarned}";
            }
            _timerText.gameObject.SetActive(true);

            if (_progressionInfoButton != null)
            {
                _progressionInfoButton.gameObject.SetActive(_traitConnectionEnabled);
            }

            if (_resultPanel != null)
            {
                _resultPanel.SetActive(false);
            }

        }

        public void ShowResult(RunResult result)
        {
            if (battleResult != null && (!_acceptResult || result.RunId != _activeRunId)) return;
            _acceptResult = false;
            _showingRun = false;
            ShowRemainingTime(0f);
            if ((battleResult == null && _resultPanel == null) || (_traitConnectionEnabled && _progressionInfoButton == null))
            {
                Initialize();
            }

            gemstoneList?.ShowResult(result);
            if (battleResult != null)
            {
                _waitingForCollection = false;
                if (_progressionInfoButton != null) _progressionInfoButton.gameObject.SetActive(false);
                battleResult.Show(result);
                return;
            }
            _pendingResult = result;
            _waitingForCollection = gemstoneList != null && gemstoneList.IsFinishingCollection;
            if (_waitingForCollection) return;
            DisplayResult(result);
        }

        private void DisplayResult(RunResult result)
        {

            if (_timerText != null && timerText == null)
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
                _progressionInfoButton.gameObject.SetActive(_traitConnectionEnabled);
            }
        }

        /// <summary>
        /// Opens the same key/value progression summary used by the trait
        /// screen. Keeping this entry point in the field HUD lets normal and
        /// boss combat inspect the applied build without leaving the run.
        /// </summary>
        public void ShowProgressionInfo()
        {
            if (!_traitConnectionEnabled) return;
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
            _showingRun = false;
            _acceptResult = false;
            _activeRunId = default;
            _waitingForCollection = false;
            battleResult?.Hide();
            gemstoneList?.Clear();
            ShowRemainingTime(0f, false);
            if (_timerText != null && timerText == null)
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
        }

        private void ShowRemainingTime(float seconds, bool showWarning = true)
        {
            if (timerText == null) return;
            if (!_hasTimerNormalColor)
            {
                _timerNormalColor = timerText.color;
                _hasTimerNormalColor = true;
            }
            Color color = showWarning && seconds <= 3f ? timerWarningColor : _timerNormalColor;
            if (timerText.color != color) timerText.color = color;

            // Round up so the display reaches zero only when combat time expires.
            int centiseconds = Mathf.CeilToInt(Mathf.Max(0f, seconds) * 100f);
            if (_displayedTimerCentiseconds == centiseconds) return;
            _displayedTimerCentiseconds = centiseconds;
            timerText.SetText("{0:00}:{1:00}", centiseconds / 100, centiseconds % 100);
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

            Canvas foreground = panelObject.AddComponent<Canvas>();
            foreground.overrideSorting = true;
            foreground.sortingLayerID = canvas.sortingLayerID;
            foreground.sortingOrder = canvas.sortingOrder + 20;
            panelObject.AddComponent<GraphicRaycaster>();

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
