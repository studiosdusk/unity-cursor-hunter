using System;
using TMPro;
using UnityEngine;
namespace CursorHunter.Data
{
    public sealed class GameTypographySettings : ScriptableObject
    {
        public Font sourceFont;
        [Tooltip("Numeric face shared with the battle result UI and combat HUD.")]
        public TMP_FontAsset numericFont;
        [Serializable]
        public struct LocaleFont
        {
            public string locale;
            public Font sourceFont;
        }
        public LocaleFont[] localeFonts = Array.Empty<LocaleFont>();

        public Font GetSourceFont(string language)
        {
            if (localeFonts != null)
                foreach (string candidate in LocaleCode.Fallbacks(language))
                    foreach (LocaleFont entry in localeFonts)
                        if (entry.sourceFont != null && LocaleCode.Matches(entry.locale, candidate))
                            return entry.sourceFont;
            return sourceFont;
        }
    }
}
