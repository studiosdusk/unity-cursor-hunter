using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CursorHunter.Data
{
    public static class LocalizedTypography
    {
        private static readonly Dictionary<Font, TMP_FontAsset> Fonts = new Dictionary<Font, TMP_FontAsset>();

        public static TMP_FontAsset GetNumericFont()
        {
            var settings = Resources.Load<GameTypographySettings>("GameData/TypographySettings");
            return settings != null && settings.numericFont != null ? settings.numericFont : GetFont(null);
        }

        public static TMP_FontAsset GetFont(Font source)
        {
            Font fallbackSource = null;
            if (source == null)
            {
                var settings = Resources.Load<GameTypographySettings>("GameData/TypographySettings");
                if (settings != null)
                {
                    source = settings.GetSourceFont(LocalizationCatalog.Language);
                    if (source != settings.sourceFont) fallbackSource = settings.sourceFont;
                }
            }
            if (source == null) return TMP_Settings.defaultFontAsset;
            if (!Fonts.TryGetValue(source, out var font) || font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(source);
                font.name = source.name + " Dynamic SDF";
                font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                font.isMultiAtlasTexturesEnabled = true;
                Fonts[source] = font;
            }
            // Untranslated Korean names must still render with a locale-specific primary font.
            if (fallbackSource != null)
            {
                TMP_FontAsset fallback = GetFont(fallbackSource);
                font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
                if (!font.fallbackFontAssetTable.Contains(fallback)) font.fallbackFontAssetTable.Add(fallback);
            }
            return font;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach (var font in Fonts.Values)
            {
                if (font == null) continue;
                if (font.atlasTextures != null)
                    foreach (var atlas in font.atlasTextures) if (atlas != null) UnityEngine.Object.Destroy(atlas);
                if (font.material != null) UnityEngine.Object.Destroy(font.material);
                UnityEngine.Object.Destroy(font);
            }
            Fonts.Clear();
            LocalizationCatalog.Reset();
        }

        public static TextAlignmentOptions Alignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                default: return TextAlignmentOptions.BottomRight;
            }
        }
    }

    public static class LocalizationCatalog
    {
        [Serializable] public sealed class Entry
        {
            public string key;
            public string ko;
            public string en;
            // Existing ko/en JSON remains compatible; additional languages need only data.
            public LocalizedText[] translations;
        }
        [Serializable] private sealed class Table { public Entry[] entries; }
        private static Entry[] _entries;
        public static string Language { get; private set; } = "ko";
        public static event Action Changed;

        internal static void Reset() { _entries = null; Language = "ko"; Changed = null; }
        private static void Load()
        {
            if (_entries != null) return;
            var asset = Resources.Load<TextAsset>("GameData/ui-localization");
            _entries = asset == null ? Array.Empty<Entry>() :
                JsonUtility.FromJson<Table>(asset.text)?.entries ?? Array.Empty<Entry>();
        }
        public static void SetLanguage(string language)
        {
            language = LocaleCode.Normalize(language);
            if (language == Language) return;
            Language = language;
            Changed?.Invoke();
        }
        public static string Get(string key, string fallback)
        {
            Load();
            foreach (var entry in _entries)
                if (entry.key == key)
                {
                    string translated = LocalizedText.Find(entry.translations, Language);
                    if (translated != null) return translated;
                    foreach (string candidate in LocaleCode.Fallbacks(Language))
                        if (candidate == "en" && !string.IsNullOrWhiteSpace(entry.en)) return entry.en;
                    return string.IsNullOrWhiteSpace(entry.ko) ? fallback : entry.ko;
                }
            return fallback;
        }
        public static void BindKnown(TMP_Text text, string fallback)
        {
            Load();
            foreach (var entry in _entries)
                if (entry.ko == fallback)
                {
                    var label = text.GetComponent<LocalizedTmpLabel>();
                    if (label == null) label = text.gameObject.AddComponent<LocalizedTmpLabel>();
                    label.Bind(entry.key, fallback);
                    return;
                }
        }
    }
}
