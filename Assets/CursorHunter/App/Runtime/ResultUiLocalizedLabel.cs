using System;
using CursorHunter.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace CursorHunter.App
{
    /// <summary>Resolves ResultUI text from the Excel-backed string table.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class ResultUiLocalizedLabel : MonoBehaviour
    {
        private TMP_Text _text;
        private TMP_FontAsset _authoredFont;
        private string _key;
        private string _fallback;
        private object[] _arguments;
        private LocalizedString _localized;
        private AsyncOperationHandle<LocalizationSettings> _initialization;
        private bool _waitingForInitialization;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            if (_text != null) _authoredFont = _text.font;
        }

        private void OnEnable()
        {
            LocalizationCatalog.Changed += OnLanguageChanged;
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
            _initialization = LocalizationSettings.InitializationOperation;
            if (!_initialization.IsDone)
            {
                _waitingForInitialization = true;
                _initialization.Completed += OnInitialized;
            }
            Subscribe();
        }

        private void OnDisable()
        {
            LocalizationCatalog.Changed -= OnLanguageChanged;
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
            if (_waitingForInitialization && _initialization.IsValid())
                _initialization.Completed -= OnInitialized;
            _waitingForInitialization = false;
            Unsubscribe();
        }

        public void Bind(string key, string fallback, params object[] arguments)
        {
            Unsubscribe();
            _key = key;
            _fallback = fallback;
            _arguments = arguments != null && arguments.Length > 0 ? arguments : null;
            ApplyFallback();
            if (isActiveAndEnabled) Subscribe();
        }

        private void Subscribe()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (_text == null || string.IsNullOrEmpty(_key) || _localized != null) return;
            _localized = new LocalizedString("ResultUI", _key)
            {
                FallbackState = FallbackBehavior.UseFallback,
                Arguments = _arguments,
                LocaleOverride = FindLocale()
            };
            _localized.StringChanged += Apply;
        }

        private void Unsubscribe()
        {
            if (_localized == null) return;
            _localized.StringChanged -= Apply;
            ((IDisposable)_localized).Dispose();
            _localized = null;
        }

        private Locale FindLocale()
        {
            var locales = LocalizationSettings.AvailableLocales;
            if (locales == null) return null;
            foreach (string code in LocaleCode.Fallbacks(LocalizationCatalog.Language))
            {
                Locale locale = locales.GetLocale(code);
                if (locale != null) return locale;
            }
            return locales.GetLocale("ko");
        }

        private void OnInitialized(AsyncOperationHandle<LocalizationSettings> operation)
        {
            _waitingForInitialization = false;
            RefreshLocale();
        }

        private void OnLanguageChanged() => RefreshLocale();
        private void OnSelectedLocaleChanged(Locale locale) => RefreshLocale();

        private void RefreshLocale()
        {
            if (_localized == null) return;
            Locale locale = FindLocale();
            if (_localized.LocaleOverride != locale) _localized.LocaleOverride = locale;
            else _localized.RefreshString();
        }

        private void ApplyFallback()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (_text == null || string.IsNullOrEmpty(_key)) return;
            _text.text = _arguments == null
                ? LocalizationCatalog.Get(_key, _fallback)
                : LocalizationCatalog.Format(_key, _fallback, _arguments);
        }

        private void Apply(string value)
        {
            if (_text == null || string.IsNullOrEmpty(value)) return;
            var typography = Resources.Load<GameTypographySettings>("GameData/TypographySettings");
            bool hasLocaleFont = typography != null &&
                typography.GetSourceFont(LocalizationCatalog.Language) != typography.sourceFont;
            TMP_FontAsset font = hasLocaleFont ? LocalizedTypography.GetFont(null) : _authoredFont;
            if (font != null && _text.font != font) _text.font = font;
            _text.text = value;
        }
    }
}
