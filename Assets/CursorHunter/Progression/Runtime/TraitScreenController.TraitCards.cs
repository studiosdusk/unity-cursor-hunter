using System;
using System.Linq;
using CursorHunter.Contracts;
using CursorHunter.Data;
using UnityEngine;

namespace CursorHunter.Progression
{
    public sealed partial class TraitScreenController
    {
        [Header("New trait cards")]
        [SerializeField] private bool useNewTraitCardSystem;
        private TraitCardProgressData _newTraitCardData = new TraitCardProgressData();
        private TraitCardProgress _newTraitCards;
        private TraitCardDefinition[] _newTraitDefinitions;
        public event Action TraitCardStateChanged;
        public event Action TraitCardProgressReset;
        public bool UsesNewTraitCardSystem => useNewTraitCardSystem;

        public TraitCardProgress TraitCards
        {
            get
            {
                EnsureProgressionState();
                if (_newTraitCards == null)
                {
                    if (!TraitCardDefinitionResources.TryLoad(out _newTraitDefinitions, out string error))
                        throw new InvalidOperationException(error);
                    _newTraitCards = new TraitCardProgress(_newTraitDefinitions.Select(card => card.stableId),
                        _newTraitCardData, count => UnityEngine.Random.Range(0, count), SaveProgressionState);
                }
                return _newTraitCards;
            }
        }

        public TraitCardDefinition GetTraitCard(string id)
        {
            _ = TraitCards;
            return Array.Find(_newTraitDefinitions, card => card.stableId == id);
        }

        public bool PrepareTraitCards() => NotifyTraitCardChange(TraitCards.Prepare());
        public bool RevealTraitCards() => NotifyTraitCardChange(TraitCards.Reveal());
        public bool SelectTraitCard(string id) => NotifyTraitCardChange(TraitCards.Select(id));

        /// <summary>Saves fresh cards, zero trait points, and level-one XP as one profile update.</summary>
        public bool TryResetTraitCardProgress()
        {
            var cards = TraitCards;
            long previousExperience = _experiencePoints;
            _experiencePoints = 0;
            bool saved = false;
            try { saved = cards.Reset(); }
            finally
            {
                if (!saved) _experiencePoints = previousExperience;
            }
            if (!saved) return false;

            RefreshAll();
            TraitCardProgressReset?.Invoke();
            TraitCardStateChanged?.Invoke();
            ExperienceChanged?.Invoke();
            return true;
        }

        private bool NotifyTraitCardChange(bool success)
        {
            if (success) TraitCardStateChanged?.Invoke();
            return success;
        }

        /// <summary>Normal runs use the same persisted receipt mechanism as boss rewards.</summary>
        public bool GrantSettledRun(RunResult result)
        {
            EnsureProgressionState();
            if (!result.RunId.IsValid || result.SettlementPolicy != RunSettlementPolicy.Eligible) return false;
            if (_settledBossRunIds.Contains(result.RunId.Value)) return true;
            if (result.BossDefeated) return GrantBossClear(result);
            _settledBossRunIds.Add(result.RunId.Value);
            if (GrantRunRewards(result.Rewards, result.RelicAwards, result.ExperienceEarned,
                    result.Mode == RunMode.Boss)) return true;
            _settledBossRunIds.Remove(result.RunId.Value);
            return false;
        }
    }
}
