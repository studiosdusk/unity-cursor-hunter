using System;
using System.Collections.Generic;
using UnityEngine;

namespace CursorHunter.Progression
{
    [Serializable]
    public sealed class RelicCollectionCatalogEntry
    {
        [SerializeField] private string title;
        [SerializeField] private Sprite icon;
        [SerializeField, TextArea(2, 4)] private string description;

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
    }
}
