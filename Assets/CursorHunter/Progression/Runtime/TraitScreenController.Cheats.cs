using System;
using System.Collections.Generic;

namespace CursorHunter.Progression
{
    public sealed partial class TraitScreenController
    {
        private bool _retiredProfile;

        /// <summary>Clear the profile and prevent this scene from saving again while App restarts it.</summary>
        public bool TryResetAllProgressForTesting()
        {
            if (_retiredProfile) return true;
            EnsureProgressionState();
            if (!CanPersistTestingChanges() || !TraitProgressionStore.TryClearEntireProfile()) return false;
            _retiredProfile = true;
            _pendingResearchLayoutSave = false;
            return true;
        }

        /// <summary>Advance exactly one player level through the normal XP/trait-point transaction.</summary>
        public bool TryIncreaseUserLevelForTesting()
        {
            EnsureProgressionState();
            if (!CanPersistTestingChanges() || !useNewTraitCardSystem) return false;
            int level = ProgressionLevelCurve.GetLevel(_experiencePoints);
            if (level >= ProgressionLevelCurve.MaximumLevel) return false;
            long required = ProgressionLevelCurve.TotalExperienceForLevel(level + 1) - _experiencePoints;
            return GrantRunRewards(null, null, required);
        }

        /// <summary>Remove owned and unseen relics as one saved change.</summary>
        public bool TryResetRelicsForTesting()
        {
            EnsureProgressionState();
            if (!CanPersistTestingChanges()) return false;
            EnsureRelicEntries();
            var previousOwned = new bool[relicEntries.Count];
            for (int i = 0; i < relicEntries.Count; i++)
            {
                if (relicEntries[i] == null) continue;
                previousOwned[i] = relicEntries[i].IsObtained;
                relicEntries[i].SetObtained(false);
            }
            var previousUnseen = new HashSet<string>(_unseenRelicIds, StringComparer.Ordinal);
            _unseenRelicIds.Clear();
            if (!SaveProgressionState())
            {
                for (int i = 0; i < relicEntries.Count; i++)
                    if (relicEntries[i] != null) relicEntries[i].SetObtained(previousOwned[i]);
                foreach (string id in previousUnseen) _unseenRelicIds.Add(id);
                return false;
            }
            CloseRelicDetail();
            RefreshAll();
            RelicStateChanged?.Invoke();
            return true;
        }

        /// <summary>Add to every gemstone and trait-point balance, or set every balance to zero.</summary>
        public bool TryChangeCurrenciesForTesting(long amount, bool clear)
        {
            EnsureProgressionState();
            if (!CanPersistTestingChanges() || amount < 0) return false;
            EnsureGemstoneBalanceCapacity();
            var cards = TraitCards;
            var previousCards = cards.Capture();
            long previousGarnet = _garnetBalance;
            var previousGemstones = (long[])_gemstoneBalances.Clone();
            for (int i = 0; i < _gemstoneBalances.Length; i++)
                _gemstoneBalances[i] = clear ? 0 : AddSaturating(_gemstoneBalances[i], amount);
            _garnetBalance = _gemstoneBalances[0];
            cards.SetPointsForTesting(clear ? 0 : AddSaturating(cards.Points, amount));
            if (!SaveProgressionState())
            {
                _gemstoneBalances = previousGemstones;
                _garnetBalance = previousGarnet;
                cards.Restore(previousCards);
                return false;
            }
            RefreshAll();
            researchMap?.RefreshAfterWalletChange();
            TraitCardStateChanged?.Invoke();
            return true;
        }

        private bool CanPersistTestingChanges() => !_retiredProfile && loadSavedProgress && _document != null &&
            _document.progression != null && _document.progression.savedProgressPolicy != "fileOnly";
    }
}
