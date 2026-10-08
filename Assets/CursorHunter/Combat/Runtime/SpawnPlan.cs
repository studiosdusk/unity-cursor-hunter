using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Runtime binding between a pure spawn snapshot and its species visual.
    /// The plan is intentionally outside Contracts because GameObject
    /// references must not cross the pure run-data boundary.
    /// </summary>
    public readonly struct SpawnPlanEntry
    {
        public SpawnPlanEntry(
            SpawnSnapshot snapshot,
            GameObject visualPrefab)
        {
            Snapshot = snapshot;
            VisualPrefab = visualPrefab;
        }

        public SpawnSnapshot Snapshot { get; }
        public GameObject VisualPrefab { get; }
        public GameObject Prefab => VisualPrefab;
        public bool HasValidSnapshot => Snapshot.IsValid;
        public bool HasVisualPrefab => VisualPrefab != null;
        public bool HasPrefab => HasVisualPrefab;
    }

    /// <summary>
    /// Extensible spawn input for one or more species sharing the same
    /// MonsterRoot prefab while supplying distinct visual prefabs.
    /// </summary>
    public sealed class SpawnPlan
    {
        private readonly IReadOnlyList<SpawnPlanEntry> _entries;

        public SpawnPlan(
            SpawnSnapshot snapshot,
            GameObject prefab)
            : this(new[] { new SpawnPlanEntry(snapshot, prefab) })
        {
        }

        public SpawnPlan(IReadOnlyList<SpawnPlanEntry> entries, int globalAliveLimit = 80)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            SpawnPlanEntry[] copy = new SpawnPlanEntry[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                copy[index] = entries[index];
            }

            _entries = Array.AsReadOnly(copy);
            GlobalAliveLimit = Math.Max(1, Math.Min(80, globalAliveLimit));
        }

        public int GlobalAliveLimit { get; }
        public IReadOnlyList<SpawnPlanEntry> Entries => _entries;
        public bool HasEntries => _entries.Count > 0;
    }
}
