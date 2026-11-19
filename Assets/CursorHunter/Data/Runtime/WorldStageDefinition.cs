using System;
using System.Collections.Generic;
using UnityEngine;

namespace CursorHunter.Data
{
    [CreateAssetMenu(menuName = "Cursor Hunter/World Stage")]
    public sealed class WorldStageDefinition : ScriptableObject
    {
        public const string ResourceFolder = "WorldStages";
        public string stableId;
        public int displayOrder;
        public string textKey;
        public GameObject mapPrefab;
        public Sprite preview;
        public bool initiallyUnlocked;
        public WorldStageDefinition prerequisite;
        [Min(0)] public int pointCost;
        public float monsterExperienceMultiplier = 1;
        public float monsterHealthMultiplier = 1;

        public static WorldStageDefinition[] LoadCatalog()
        {
            var stages = Resources.LoadAll<WorldStageDefinition>(ResourceFolder);
            Array.Sort(stages, (a, b) => a.displayOrder.CompareTo(b.displayOrder));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var maps = new HashSet<GameObject>();
            if (stages.Length == 0) throw new InvalidOperationException("Missing world stages.");
            for (int i = 0; i < stages.Length; i++)
            {
                var s = stages[i];
                var previous = i > 0 ? stages[i - 1] : null;
                if (string.IsNullOrWhiteSpace(s.stableId) || !ids.Add(s.stableId) ||
                    string.IsNullOrWhiteSpace(s.textKey) || !keys.Add(s.textKey) ||
                    s.mapPrefab == null || !maps.Add(s.mapPrefab) || s.preview == null ||
                    s.prerequisite != previous || s.initiallyUnlocked != (i == 0) ||
                    (previous != null && s.displayOrder == previous.displayOrder) ||
                    (i == 0 ? s.pointCost != 0 : s.pointCost < 0) ||
                    !ValidMultiplier(s.monsterExperienceMultiplier) || !ValidMultiplier(s.monsterHealthMultiplier))
                    throw new InvalidOperationException("Invalid world stage configuration: " + s.name);
            }
            return stages;
        }

        private static bool ValidMultiplier(float value) =>
            value > 0 && !float.IsInfinity(value) && (double)value < (double)decimal.MaxValue;

        public long ScaleMonsterHealth(long health)
        {
            decimal scaled = decimal.Ceiling(health * (decimal)monsterHealthMultiplier);
            return Math.Max(1L, checked((long)scaled));
        }
    }
}
