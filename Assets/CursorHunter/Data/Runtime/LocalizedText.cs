using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CursorHunter.Data
{
    [Serializable]
    public struct LocalizedText
    {
        public string locale;
        public string text;

        // Regional names fall back to their parent language before the caller's default.
        public static string Find(LocalizedText[] entries, string language)
        {
            if (entries == null || entries.Length == 0) return null;
            foreach (string candidate in LocaleCode.Fallbacks(language))
                foreach (LocalizedText entry in entries)
                    if (LocaleCode.Matches(entry.locale, candidate) && !string.IsNullOrWhiteSpace(entry.text))
                        return entry.text;
            return null;
        }
    }

    public static class LocaleCode
    {
        private static readonly Regex Pattern = new Regex("^[a-z]{2,8}(-[a-z0-9]{1,8})*$", RegexOptions.CultureInvariant);

        public static bool TryNormalize(string value, out string normalized)
        {
            normalized = value?.Trim().Replace('_', '-').ToLowerInvariant();
            return normalized != null && normalized.Length <= 64 && Pattern.IsMatch(normalized);
        }

        public static string Normalize(string value)
        {
            if (!TryNormalize(value, out string normalized))
                throw new ArgumentException("Use a locale code such as ko, en, ja or zh-Hans.", nameof(value));
            return normalized;
        }

        public static bool Matches(string value, string normalized) =>
            TryNormalize(value, out string candidate) && candidate == normalized;

        public static IEnumerable<string> Fallbacks(string language)
        {
            if (!TryNormalize(language, out string candidate)) yield break;
            while (true)
            {
                yield return candidate;
                int separator = candidate.LastIndexOf('-');
                if (separator < 0) yield break;
                candidate = candidate.Substring(0, separator);
            }
        }
    }
}
