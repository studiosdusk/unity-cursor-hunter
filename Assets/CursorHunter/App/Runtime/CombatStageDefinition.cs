using System;
using System.Collections.Generic;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;
using UnityEngine;

namespace CursorHunter.App
{
    /// <summary>
    /// The selected production trait for one species in this stage. The
    /// numbered options match monster.xx.production.01/02/03 (2/3/4 spawns).
    /// </summary>
    public enum StageProductionOption
    {
        UsePlayerUpgrade = 0,
        Base = 1,
        Production2 = 2,
        Production3 = 3,
        Production4 = 4
    }

    [Serializable]
    public sealed class StageMonsterSpawnRule
    {
        [SerializeField] private MonsterDefinition monster;
        [Tooltip("Allow this species in this stage, regardless of its purchased unlock node.")]
        [SerializeField] private bool allowSpawn;
        [Tooltip("UsePlayerUpgrade keeps the purchased production count. Base and Production2/3/4 select the corresponding monster trait node for this run only.")]
        [SerializeField] private StageProductionOption production =
            StageProductionOption.UsePlayerUpgrade;

        public MonsterDefinition Monster => monster;
        public bool AllowSpawn => allowSpawn;
        public StageProductionOption Production => production;
    }

    /// <summary>
    /// Inspector-authored roster for one normal-field stage. It changes only
    /// the copied run information; purchased traits and saved data stay intact.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CombatStage",
        menuName = "Cursor Hunter/Stages/Combat Stage")]
    public sealed class CombatStageDefinition : ScriptableObject
    {
        [SerializeField] private string stageId = "stage.01";
        [Tooltip("Only rows with Allow Spawn checked can appear in this stage.")]
        [SerializeField] private StageMonsterSpawnRule[] monsters =
            Array.Empty<StageMonsterSpawnRule>();

        public string StageId => stageId ?? string.Empty;

        /// <summary>
        /// The stage is a whitelist. Allowed rows spawn even when the player
        /// has not bought the monster's unlock node. UsePlayerUpgrade reads
        /// the player's current production count (1 for a locked species).
        /// </summary>
        public bool TryApplyToInformation(
            GameInformation information,
            out string error)
        {
            error = string.Empty;
            if (information == null || !information.TryValidate(out error))
            {
                return false;
            }

            if (monsters == null || monsters.Length == 0)
            {
                error = "Stage has no monster rows.";
                return false;
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            int allowedCount = 0;
            foreach (StageMonsterSpawnRule rule in monsters)
            {
                if (rule == null || rule.Monster == null)
                {
                    error = "Every stage monster row needs a MonsterDefinition.";
                    return false;
                }

                string monsterId = rule.Monster.MonsterId;
                if (!seenIds.Add(monsterId))
                {
                    error = "Duplicate stage monster: " + monsterId;
                    return false;
                }

                if (FindMonster(information, monsterId) == null)
                {
                    error = "Stage monster is missing from game information: " + monsterId;
                    return false;
                }

                if (!Enum.IsDefined(typeof(StageProductionOption), rule.Production))
                {
                    error = "Invalid production option for " + monsterId;
                    return false;
                }

                if (!rule.AllowSpawn)
                {
                    continue;
                }

                if (rule.Monster.VisualPrefab == null)
                {
                    error = "Stage monster has no visual prefab: " + monsterId;
                    return false;
                }

                allowedCount++;
            }

            if (allowedCount == 0)
            {
                error = "Stage must allow at least one monster.";
                return false;
            }

            foreach (MonsterInformation monster in information.monsters)
            {
                monster.enabled = false;
            }

            foreach (StageMonsterSpawnRule rule in monsters)
            {
                if (!rule.AllowSpawn)
                {
                    continue;
                }

                MonsterInformation monster = FindMonster(
                    information,
                    rule.Monster.MonsterId);
                monster.enabled = true;
                if (rule.Production != StageProductionOption.UsePlayerUpgrade)
                {
                    monster.productionCount = (int)rule.Production;
                }
            }

            // Gem availability in the run summary follows the stage roster.
            foreach (GemstoneInformation gem in information.gemstones)
            {
                gem.enabled = gem.id == "gem.garnet";
                foreach (MonsterInformation monster in information.monsters)
                {
                    if (monster.enabled && monster.gemstoneId == gem.id &&
                        monster.gemstoneAmount > 0 &&
                        monster.gemstoneChancePercent > 0f)
                    {
                        gem.enabled = true;
                        break;
                    }
                }
            }

            return information.TryValidate(out error);
        }

        public bool TryCreateSpawnPlan(
            ProgressionCombatSnapshot runSnapshot,
            out SpawnPlan spawnPlan,
            out string error)
        {
            spawnPlan = null;
            error = string.Empty;
            if (runSnapshot == null || !runSnapshot.IsValid)
            {
                error = "Stage requires valid run information.";
                return false;
            }

            var entries = new List<SpawnPlanEntry>();
            if (monsters != null)
            {
                foreach (StageMonsterSpawnRule rule in monsters)
                {
                    if (rule == null || !rule.AllowSpawn || rule.Monster == null)
                    {
                        continue;
                    }

                    MonsterCombatSnapshot? resolved = null;
                    foreach (MonsterCombatSnapshot monster in runSnapshot.Monsters)
                    {
                        if (monster.MonsterId == rule.Monster.MonsterId)
                        {
                            resolved = monster;
                            break;
                        }
                    }

                    if (!resolved.HasValue || !resolved.Value.Unlocked ||
                        rule.Monster.VisualPrefab == null)
                    {
                        error = "Stage monster is not ready: " + rule.Monster.MonsterId;
                        return false;
                    }

                    SpawnSnapshot snapshot = rule.Monster.CreateSnapshotWithProductionBonus(
                        runSnapshot.PerMonsterAliveLimit,
                        resolved.Value.ProductionBonusCount);
                    if (!snapshot.IsValid)
                    {
                        error = "Stage monster has invalid final stats: " + rule.Monster.MonsterId;
                        return false;
                    }

                    entries.Add(new SpawnPlanEntry(snapshot, rule.Monster.VisualPrefab));
                }
            }

            if (entries.Count == 0)
            {
                error = "Stage has no allowed monster.";
                return false;
            }

            spawnPlan = new SpawnPlan(entries, runSnapshot.GlobalAliveLimit);
            return true;
        }

        private static MonsterInformation FindMonster(
            GameInformation information,
            string monsterId)
        {
            foreach (MonsterInformation monster in information.monsters)
            {
                if (monster != null && monster.id == monsterId)
                {
                    return monster;
                }
            }

            return null;
        }
    }
}
