using System.Collections;
using CursorHunter.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace CursorHunter.App
{
    /// <summary>Bridges Unity Localization to the game's existing localized UI and settings controls.</summary>
    public sealed class LanguageSelectorController : MonoBehaviour
    {
        private const string PreferenceKey = "CursorHunter.Language";
        private static readonly Color SelectedButtonColor = new Color32(37, 133, 193, 255);
        private static readonly Color IdleButtonColor = new Color32(47, 62, 82, 255);
        private static readonly Color PressedButtonColor = new Color32(24, 91, 140, 255);
        private static LanguageSelectorController instance;

        private GameObject selectorCard;
        private GameObject mountedSettingsRoot;
        private Button koreanButton;
        private Button englishButton;
        private TextMeshProUGUI selectorTitle;
        private TextMeshProUGUI koreanLabel;
        private TextMeshProUGUI englishLabel;

        public static LanguageSelectorController EnsureRuntime()
        {
            if (instance != null) return instance;

            LanguageSelectorController existing = FindAnyObjectByType<LanguageSelectorController>();
            if (existing != null) return instance = existing;

            var host = new GameObject("CursorHunter Language Selector");
            return instance = host.AddComponent<LanguageSelectorController>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        private IEnumerator Start()
        {
            yield return LocalizationSettings.InitializationOperation;

            string savedCode = PlayerPrefs.GetString(PreferenceKey, "ko");
            if (!TrySelectLocale(savedCode)) TrySelectLocale("ko");

            if (LocalizationSettings.SelectedLocale != null)
                ApplyLocale(LocalizationSettings.SelectedLocale);
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
            if (instance == this) instance = null;
        }

        public void AttachSettingsUi(GameObject settingsRoot)
        {
            if (settingsRoot == null) return;
            if (mountedSettingsRoot == settingsRoot && selectorCard != null)
            {
                RefreshSelector();
                return;
            }

            if (selectorCard != null) Destroy(selectorCard);
            mountedSettingsRoot = settingsRoot;

            selectorCard = new GameObject("Language Selector", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            selectorCard.transform.SetParent(settingsRoot.transform, false);
            selectorCard.transform.SetAsLastSibling();
            var cardRect = selectorCard.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 1f);
            cardRect.anchorMax = new Vector2(0.5f, 1f);
            cardRect.pivot = new Vector2(0.5f, 1f);
            cardRect.anchoredPosition = new Vector2(0f, -155f);
            cardRect.sizeDelta = new Vector2(420f, 112f);

            var background = selectorCard.GetComponent<Image>();
            background.color = new Color32(24, 39, 59, 246);
            background.raycastTarget = false;
            var outline = selectorCard.AddComponent<Outline>();
            outline.effectColor = new Color32(9, 17, 30, 255);
            outline.effectDistance = new Vector2(2f, -2f);

            selectorTitle = CreateText(selectorCard.transform, "Language / 언어", 22f);
            var titleRect = selectorTitle.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -7f);
            titleRect.sizeDelta = new Vector2(-20f, 30f);

            koreanButton = CreateButton(selectorCard.transform, "Korean", "한국어", "ko", -94f, out koreanLabel);
            englishButton = CreateButton(selectorCard.transform, "English", "English", "en", 94f, out englishLabel);
            RefreshSelector();
        }

        private Button CreateButton(Transform parent, string objectName, string label, string localeCode,
            float x, out TextMeshProUGUI text)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, -74f);
            rect.sizeDelta = new Vector2(178f, 44f);

            var image = buttonObject.GetComponent<Image>();
            image.color = Color.white;
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.pressedColor = new Color(0.82f, 0.9f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => TrySelectLocale(localeCode));

            text = CreateText(buttonObject.transform, label, 23f);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string value, float size)
        {
            var textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = LocalizedTypography.GetFont(null);
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(14f, size - 5f);
            text.fontSizeMax = size;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            LocalizedTypography.ApplyOutline(text);
            return text;
        }

        private bool TrySelectLocale(string code)
        {
            var locales = LocalizationSettings.AvailableLocales;
            Locale locale = locales == null ? null : locales.GetLocale(code);
            if (locale == null) return false;

            if (LocalizationSettings.SelectedLocale != locale)
                LocalizationSettings.SelectedLocale = locale;
            else
                ApplyLocale(locale);
            return true;
        }

        private void OnSelectedLocaleChanged(Locale locale) => ApplyLocale(locale);

        private void ApplyLocale(Locale locale)
        {
            if (locale == null) return;
            string code = locale.Identifier.Code;
            LocalizationCatalog.SetLanguage(code);
            PlayerPrefs.SetString(PreferenceKey, code);
            PlayerPrefs.Save();
            RefreshSelector();
        }

        private void RefreshSelector()
        {
            if (selectorCard == null) return;
            string code = LocalizationSettings.SelectedLocale?.Identifier.Code ?? "ko";
            SetButtonLanguageStyle(koreanButton, code == "ko");
            SetButtonLanguageStyle(englishButton, code == "en");

            var font = LocalizedTypography.GetFont(null);
            if (font == null) return;
            if (selectorTitle != null) selectorTitle.font = font;
            if (koreanLabel != null) koreanLabel.font = font;
            if (englishLabel != null) englishLabel.font = font;
        }

        private static void SetButtonLanguageStyle(Button button, bool selected)
        {
            if (button == null) return;
            Color normal = selected ? SelectedButtonColor : IdleButtonColor;
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = Color.Lerp(normal, Color.white, 0.18f);
            colors.pressedColor = PressedButtonColor;
            colors.selectedColor = normal;
            button.colors = colors;
        }
    }
}
