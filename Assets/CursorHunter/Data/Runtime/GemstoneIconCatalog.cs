using System;
using System.Collections.Generic;
using UnityEngine;

namespace CursorHunter.Data
{
    [CreateAssetMenu(fileName = "GemstoneIconCatalog", menuName = "Cursor Hunter/Gemstone Icon Catalog")]
    public sealed class GemstoneIconCatalog : ScriptableObject
    {
        public const string ResourcePath = "GameData/GemstoneIconCatalog";

        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string gemstoneId;
            [SerializeField] private Sprite icon;
            [Tooltip("Project asset path for reference. Updated from Icon when this catalog is edited.")]
            [SerializeField] private string assetPath;

            public string GemstoneId => gemstoneId;
            public Sprite Icon => icon;
            public string AssetPath => assetPath;

#if UNITY_EDITOR
            internal void RefreshAssetPath()
            {
                assetPath = UnityEditor.AssetDatabase.GetAssetPath(icon);
            }
#endif
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        private static GemstoneIconCatalog _default;

        public IReadOnlyList<Entry> Entries => entries;

        public static GemstoneIconCatalog LoadDefault()
        {
            if (_default == null)
                _default = Resources.Load<GemstoneIconCatalog>(ResourcePath);
            return _default;
        }

        public bool TryGetEntry(string gemstoneId, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(gemstoneId) || entries == null) return false;

            foreach (Entry candidate in entries)
            {
                if (candidate != null && string.Equals(candidate.GemstoneId, gemstoneId, StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns the referenced sprite in both the Editor and player builds.</summary>
        public Sprite GetIcon(string gemstoneId)
        {
            return TryGetEntry(gemstoneId, out Entry entry) ? entry.Icon : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            _default = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entries == null) return;
            foreach (Entry entry in entries) entry?.RefreshAssetPath();
        }
#endif
    }
}
