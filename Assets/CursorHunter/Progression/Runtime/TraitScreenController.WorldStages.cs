using System.Linq;
using System.Collections.Generic;
using CursorHunter.Data;

namespace CursorHunter.Progression
{
    public sealed partial class TraitScreenController
    {
        private WorldStageDefinition[] _worldStages;
        public IReadOnlyList<WorldStageDefinition> WorldStages => _worldStages ??= WorldStageDefinition.LoadCatalog();

        public WorldStageDefinition SelectedWorldStage
        {
            get
            {
                var cards = TraitCards;
                foreach (var stage in WorldStages)
                    if (stage.stableId == cards.SelectedStageId && IsStageUnlocked(stage)) return stage;
                return WorldStages[0];
            }
        }

        public bool IsStageUnlocked(WorldStageDefinition stage) =>
            stage != null && (stage.initiallyUnlocked || TraitCards.OwnsStage(stage.stableId));

        public bool CanUnlockStage(WorldStageDefinition stage) =>
            stage != null && !IsStageUnlocked(stage) && IsStageUnlocked(stage.prerequisite) &&
            TraitCards.Points >= stage.pointCost;

        public bool TryChooseStage(WorldStageDefinition stage, bool purchase)
        {
            if (!WorldStages.Contains(stage)) return false;
            if (purchase ? !CanUnlockStage(stage) : !IsStageUnlocked(stage)) return false;
            if (!purchase && SelectedWorldStage == stage) return true;
            return NotifyTraitCardChange(TraitCards.CommitStage(stage.stableId, purchase, stage.pointCost));
        }
    }
}
