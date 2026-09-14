using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Runtime binding between a pure spawn snapshot and its authored visual.
    /// The plan is intentionally outside Contracts because GameObject
    /// references must not cross the pure run-data boundary.
    /// </summary>
    public readonly struct SpawnPlanEntry
    {
        public SpawnPlanEntry(
            SpawnSnapshot snapshot,
            GameObject prefab)
        {
            Snapshot = snapshot;
            Prefab = prefab;
        }

        public SpawnSnapshot Snapshot { get; }
        public GameObject Prefab { get; }
        public bool HasValidSnapshot => Snapshot.IsValid;
        public bool HasPrefab => Prefab != null;
    }

    /// <summary>
    /// Extensible spawn input. The first implementation consumes one entry;
    /// the copied entry list leaves room for a multi-monster SpawnPlan later.
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

        public SpawnPlan(IReadOnlyList<SpawnPlanEntry> entries)
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
        }

        public IReadOnlyList<SpawnPlanEntry> Entries => _entries;
        public bool HasEntries => _entries.Count > 0;
    }
}
