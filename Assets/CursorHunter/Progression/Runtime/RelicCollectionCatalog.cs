using System;
using System.Collections.Generic;
using UnityEngine;

namespace CursorHunter.Progression
{
    [Serializable]
    public sealed class RelicCollectionCatalogEntry
    {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField] private Sprite icon;
        [SerializeField, TextArea(2, 4)] private string description;

        public string Id => id;
        public string Title => title;
        public Sprite Icon => icon;
        public string Description => description;
    }

    /// <summary>
    /// Runtime-safe source of relic names and icons. Keeping this under
    /// Resources lets the collection recover its presentation if an open
    /// Unity scene still has older, empty serialized relic entries.
    /// </summary>
    public sealed class RelicCollectionCatalog : ScriptableObject
    {
        [SerializeField] private List<RelicCollectionCatalogEntry> entries =
            new List<RelicCollectionCatalogEntry>();

        public IReadOnlyList<RelicCollectionCatalogEntry> Entries => entries;

        public RelicCollectionCatalogEntry FindById(string relicId)
        {
            if (string.IsNullOrWhiteSpace(relicId) || entries == null)
            {
                return null;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                RelicCollectionCatalogEntry entry = entries[i];
                if (entry != null && string.Equals(entry.Id, relicId, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        public RelicCollectionCatalogEntry FindLegacyEntry(string title, Sprite icon)
        {
            if (entries == null)
            {
                return null;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                RelicCollectionCatalogEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if ((!string.IsNullOrWhiteSpace(title) &&
                     string.Equals(entry.Title, title, StringComparison.Ordinal)) ||
                    (icon != null && entry.Icon == icon))
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
