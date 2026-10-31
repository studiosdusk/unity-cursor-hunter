using System;
using System.Globalization;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using TMPro;
using UnityEngine;

namespace CursorHunter.App
{
    /// <summary>Displays this run's drops using the rows already authored in the scene.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatGemstoneList : MonoBehaviour
    {
        [Serializable]
        private sealed class Entry
        {
            public GameObject root;
            public GemstoneIconView icon;
            public TMP_Text amountText;
            [NonSerialized] private long _displayedAmount = -1L;
            public void InvalidateAmount() { _displayedAmount = -1L; }

            public void SetAmount(long amount)
            {
                if (amountText == null || _displayedAmount == amount) return;
                amountText.text = amount.ToString(CultureInfo.InvariantCulture);
                _displayedAmount = amount;
            }
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [SerializeField] private GemstoneCollectionController collectionController;
        private RunId _runId;
        public bool IsFinishingCollection => collectionController != null && collectionController.IsFinishing;
        private void OnDisable()
        {
            if (collectionController != null) collectionController.CancelPresentation(false, false);
        }

        public void ShowRunning(CombatRunController combat, MonsterSpawner spawner)
        {
            if (combat == null || !combat.IsRunActive) return;
            if (_runId != combat.CurrentRunId)
            {
                Clear();
                _runId = combat.CurrentRunId;
            }
            if (collectionController != null && Application.isPlaying)
                collectionController.BeginRun(combat);

            foreach (Entry entry in entries)
            {
                if (entry == null || entry.root == null || entry.icon == null) continue;
                string id = entry.icon.GemstoneId;
                long earned = combat.GetResourceEarned(id);
                // Keep already earned drops visible if a live balance edit disables their source.
                bool visible = earned > 0L || CanDrop(combat, spawner, id);
                if (collectionController != null && collectionController.Owns(_runId)) entry.InvalidateAmount();
                else entry.SetAmount(earned);
                if (entry.root.activeSelf != visible) entry.root.SetActive(visible);
            }
        }

        public void ShowResult(RunResult result)
        {
            if (!_runId.IsValid || result.RunId != _runId) return;
            if (collectionController != null && collectionController.Owns(_runId))
            {
                collectionController.CompleteRun(result);
                return;
            }
            foreach (Entry entry in entries)
            {
                if (entry == null || entry.root == null || entry.icon == null) continue;
                long earned = 0L;
                foreach (ResourceRewardSnapshot reward in result.Rewards)
                {
                    if (reward.CurrencyId != entry.icon.GemstoneId) continue;
                    earned = reward.Amount;
                    break;
                }

                entry.SetAmount(earned);
                if (earned > 0L) entry.root.SetActive(true);
            }
        }

        public void Clear()
        {
            if (collectionController != null) collectionController.CancelPresentation();
            _runId = default;
            foreach (Entry entry in entries)
            {
                if (entry == null) continue;
                entry.SetAmount(0L);
                if (entry.root != null) entry.root.SetActive(false);
            }
        }

        private static bool CanDrop(CombatRunController combat, MonsterSpawner spawner, string currencyId)
        {
            if (combat.IsBossRun)
                foreach (ResourceRewardSnapshot reward in combat.BossClearRewards)
                    if (reward.CurrencyId == currencyId && reward.Amount > 0) return true;
            SpawnPlan plan = spawner != null ? spawner.CurrentSpawnPlan : null;
            if (plan == null) return false;
            for (int index = 0; index < plan.Entries.Count; index++)
            {
                SpawnSnapshot snapshot = plan.Entries[index].Snapshot;
                if (!spawner.IsSpeciesEnabled(snapshot.MonsterId)) continue;
                if (currencyId == "gem.garnet" && snapshot.GarnetReward > 0L) return true;
                if (snapshot.BonusDropCurrencyId == currencyId &&
                    snapshot.BonusDropAmount > 0L && snapshot.BonusDropChancePercent > 0f)
                    return true;
            }

            return false;
        }
    }
}
