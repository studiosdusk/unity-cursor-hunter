using CursorHunter.Contracts;
using UnityEngine;
using UnityEngine.UI;

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

        private Text _timerText;
        private GameObject _resultPanel;
        private Text _resultText;
        private Font _runtimeFont;

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

            if (_runtimeFont == null)
            {
                _runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

            _resultPanel.SetActive(false);
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

            if (_resultPanel != null)
            {
                _resultPanel.SetActive(false);
            }
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

            _resultText.text =
                "HUNT COMPLETE\n\n" +
                $"TIME {result.ElapsedSeconds:0.0}s\n" +
                $"KILLS {result.DefeatedCount}\n" +
                $"GARNET +{result.GarnetEarned}\n" +
                $"DAMAGE {result.EffectiveDamage}";
            _resultPanel.SetActive(true);
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

        private Text CreateText(
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
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            Text text = textObject.GetComponent<Text>();
            text.font = _runtimeFont;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
