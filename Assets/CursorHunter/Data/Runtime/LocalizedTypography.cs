using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CursorHunter.Data
{
    public static class LocalizedTypography
    {
        private static readonly Dictionary<Font, TMP_FontAsset> Fonts = new Dictionary<Font, TMP_FontAsset>();

        public static TMP_FontAsset GetFont(Font source)
        {
            if (source == null)
            {
                var settings = Resources.Load<GameTypographySettings>("GameData/TypographySettings");
                if (settings != null) source = settings.sourceFont;
            }
            if (source == null) return TMP_Settings.defaultFontAsset;
            if (Fonts.TryGetValue(source, out var cached) && cached != null) return cached;
            var font = TMP_FontAsset.CreateFontAsset(source);
            font.name = source.name + " Dynamic SDF";
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.isMultiAtlasTexturesEnabled = true;
            Fonts[source] = font;
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
        [Serializable] public sealed class Entry { public string key; public string ko; public string en; }
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
            if (language != "ko" && language != "en") throw new ArgumentException("Supported locales: ko, en.");
            if (language == Language) return;
            Language = language;
            Changed?.Invoke();
        }
        public static string Get(string key, string fallback)
        {
            Load();
            foreach (var entry in _entries)
                if (entry.key == key) return Language == "en" && !string.IsNullOrEmpty(entry.en) ? entry.en : entry.ko;
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
