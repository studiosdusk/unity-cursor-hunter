using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace CursorHunter.Data
{
    public static class LocalizedTypography
    {
        private const float OutlineWidth = 0.18f;
        private static readonly Color OutlineColor = new Color32(14, 16, 21, 255);
        private static readonly Dictionary<Material, Material> OutlinedMaterials = new Dictionary<Material, Material>();
        private static readonly Dictionary<Font, TMP_FontAsset> Fonts = new Dictionary<Font, TMP_FontAsset>();

        public static TMP_FontAsset GetNumericFont()
        {
            var settings = Resources.Load<GameTypographySettings>("GameData/TypographySettings");
            return settings != null && settings.numericFont != null ? settings.numericFont : GetFont(null);
        }

        public static TMP_FontAsset GetFont(Font source)
        {
            Font fallbackSource = null;
            Font supportSource = null;
            if (source == null)
            {
                var settings = Resources.Load<GameTypographySettings>("GameData/TypographySettings");
                if (settings != null)
                {
                    source = settings.GetSourceFont(LocalizationCatalog.Language);
                    if (source != settings.sourceFont) fallbackSource = settings.sourceFont;
                    if (source != settings.fallbackFont) supportSource = settings.fallbackFont;
                }
            }
            if (source == null) return TMP_Settings.defaultFontAsset;
            if (!Fonts.TryGetValue(source, out var font) || font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(source);
                font.name = source.name + " Dynamic SDF";
                ConfigureOutline(font.material);
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
            if (supportSource != null)
            {
                TMP_FontAsset support = GetFont(supportSource);
                font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
                if (!font.fallbackFontAssetTable.Contains(support)) font.fallbackFontAssetTable.Add(support);
            }
            return font;
        }

        // Preserve authored font assets and share one outlined material per source material.
        public static void ApplyOutline(TMP_Text text)
        {
            if (text == null || text.fontSharedMaterial == null) return;
            Material source = text.fontSharedMaterial;
            if (!source.HasProperty(ShaderUtilities.ID_OutlineWidth) || !source.HasProperty(ShaderUtilities.ID_OutlineColor)) return;
            if (!OutlinedMaterials.TryGetValue(source, out var outlined) || outlined == null)
            {
                outlined = new Material(source) { name = source.name + " UI Outline" };
                ConfigureOutline(outlined);
                OutlinedMaterials[source] = outlined;
                // Reapplying the style must not generate a chain of material copies.
                OutlinedMaterials[outlined] = outlined;
            }
            text.fontSharedMaterial = outlined;
            text.extraPadding = true;
        }

        private static void ConfigureOutline(Material material)
        {
            if (material == null || !material.HasProperty(ShaderUtilities.ID_OutlineWidth) ||
                !material.HasProperty(ShaderUtilities.ID_OutlineColor)) return;
            material.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
            material.EnableKeyword("OUTLINE_ON");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach (var material in new HashSet<Material>(OutlinedMaterials.Values))
                if (material != null) UnityEngine.Object.Destroy(material);
            OutlinedMaterials.Clear();
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
        private static Dictionary<string, Entry> _entriesByKey;
        private static Dictionary<string, Entry> _entriesByText;
        public static string Language { get; private set; } = "ko";
        public static event Action Changed;

        internal static void Reset()
        {
            _entries = null;
            _entriesByKey = null;
            _entriesByText = null;
            Language = "ko";
            Changed = null;
        }

        private static void Load()
        {
            if (_entriesByKey != null) return;
            var asset = Resources.Load<TextAsset>("GameData/ui-localization");
            _entries = asset == null ? Array.Empty<Entry>() :
                JsonUtility.FromJson<Table>(asset.text)?.entries ?? Array.Empty<Entry>();
            _entriesByKey = new Dictionary<string, Entry>(StringComparer.Ordinal);
            _entriesByText = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (Entry entry in _entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
                if (!_entriesByKey.ContainsKey(entry.key)) _entriesByKey.Add(entry.key, entry);
                AddTextLookup(entry.ko, entry);
                AddTextLookup(entry.en, entry);
            }
        }

        private static void AddTextLookup(string value, Entry entry)
        {
            if (string.IsNullOrEmpty(value) || _entriesByText.ContainsKey(value)) return;
            _entriesByText.Add(value, entry);
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
            if (!string.IsNullOrEmpty(key) && _entriesByKey.TryGetValue(key, out Entry entry))
            {
                string translated = LocalizedText.Find(entry.translations, Language);
                if (translated != null) return translated;
                if (Language == "en" && !string.IsNullOrEmpty(entry.en)) return entry.en;
                if (!string.IsNullOrEmpty(entry.ko)) return entry.ko;
            }
            return fallback;
        }

        public static string Format(string key, string fallbackFormat, params object[] arguments)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                Get(key, fallbackFormat),
                arguments ?? Array.Empty<object>());
        }

        public static void BindKnown(TMP_Text text, string fallback)
        {
            if (text == null) return;
            Load();
            var label = text.GetComponent<LocalizedTmpLabel>();
            if (string.IsNullOrEmpty(fallback))
            {
                if (label != null) label.Unbind();
                return;
            }
            if (!_entriesByText.TryGetValue(fallback, out Entry entry))
            {
                if (label != null) label.Unbind();
                return;
            }
            if (label == null) label = text.gameObject.AddComponent<LocalizedTmpLabel>();
            // Keep Korean as the stable fallback even when the label was
            // created while English is active.
            label.Bind(entry.key, string.IsNullOrEmpty(entry.ko) ? fallback : entry.ko);
        }
    }
}
