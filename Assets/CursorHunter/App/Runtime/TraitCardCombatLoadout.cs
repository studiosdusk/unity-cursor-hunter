using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using CursorHunter.Data;
using CursorHunter.Progression;
using UnityEngine;

namespace CursorHunter.App
{
    /// <summary>Copies only acquired card data and binds chest visuals at the App boundary.</summary>
    public sealed class TraitCardCombatLoadout
    {
        private readonly IReadOnlyList<ITraitCombatEffectSnapshot> effects;
        private readonly Dictionary<string, GameObject> chestVisuals;
        private TraitCardCombatLoadout(List<ITraitCombatEffectSnapshot> effects, Dictionary<string, GameObject> visuals)
        { this.effects = Array.AsReadOnly(effects.ToArray()); chestVisuals = visuals; }
        public IReadOnlyList<ITraitCombatEffectSnapshot> Effects => effects;
        public bool TryGetChestVisual(string cardId, out GameObject visual) => chestVisuals.TryGetValue(cardId, out visual);

        public static TraitCardCombatLoadout Capture(TraitScreenController progression)
        {
            var effects = new List<ITraitCombatEffectSnapshot>();
            var visuals = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            if (progression == null || !progression.UsesNewTraitCardSystem)
                return new TraitCardCombatLoadout(effects, visuals);

            foreach (string id in progression.TraitCards.OwnedIds)
            {
                TraitCardDefinition card = progression.GetTraitCard(id);
                if (card == null) throw new InvalidOperationException("Missing acquired trait card: " + id);
                if (!card.TryValidate(out string error))
                    throw new InvalidOperationException("Invalid acquired trait card " + id + ": " + error);
                TraitCardGameplayDefinition gameplay = card.gameplay;
                ITraitCombatEffectSnapshot effect;
                switch (gameplay.effectKind)
                {
                    case TraitCardEffectKind.BattleRewardMultiplier:
                        effect = new BattleRewardTraitSnapshot(id, gameplay.rewardMultiplier.chancePercent,
                            gameplay.rewardMultiplier.rewardMultiplier);
                        break;
                    case TraitCardEffectKind.TreasureChestAtBattleStart:
                        var chest = gameplay.treasureChest;
                        effect = new TreasureChestTraitSnapshot(id, chest.chancePercent, chest.spawnCount,
                            chest.monsterDefinition.CreateSnapshot(chest.spawnCount, chest.spawnCount));
                        visuals.Add(id, chest.monsterDefinition.VisualPrefab);
                        break;
                    case TraitCardEffectKind.PeriodicCursorRangeBoost:
                        var range = gameplay.cursorRange;
                        effect = new CursorRangeTraitSnapshot(id, range.intervalSeconds, range.chancePercent,
                            range.durationSeconds, range.rangeIncreasePercent);
                        break;
                    case TraitCardEffectKind.NormalMonsterExperienceBonus:
                        effect = new NormalExperienceTraitSnapshot(id, gameplay.normalMonsterExperience.bonusPercent);
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported trait effect: " + gameplay.effectKind);
                }
                if (!effect.IsValid) throw new InvalidOperationException("Invalid trait combat snapshot: " + id);
                effects.Add(effect);
            }
            return new TraitCardCombatLoadout(effects, visuals);
        }
    }
}
