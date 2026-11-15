using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Data
{
    [Serializable]
    public sealed class RelicPresentationDefinition
    {
        [Tooltip("RelicText.xlsx의 키 접두사. 표시 문구는 .title과 .description 키를 사용합니다.")]
        public string textKey;
        public Sprite icon;
        [Min(0)] public int displayOrder;
    }

    [Serializable]
    public sealed class RelicGameplayDefinition
    {
        public string sourceMonsterId;
        [Range(0.0001f, 100f)] public float dropChancePercent;
        [Tooltip("Combat effect ID. Must match the effect handled by RelicCombatModifiers.")]
        public string effectId;
        public float effectValue;
        public float effectValue2;
    }

    /// <summary>Single authored source for one relic's UI references and gameplay effects.</summary>
    [CreateAssetMenu(menuName = "Cursor Hunter/Relics/Definition")]
    public sealed class RelicDefinition : ScriptableObject
    {
        public string stableId;
        public RelicPresentationDefinition presentation = new RelicPresentationDefinition();
        public RelicGameplayDefinition gameplay = new RelicGameplayDefinition();

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(stableId) || !stableId.StartsWith("relic.", StringComparison.Ordinal))
            {
                error = "Relic stable ID must start with relic.";
                return false;
            }

            if (presentation == null || string.IsNullOrWhiteSpace(presentation.textKey) ||
                !string.Equals(presentation.textKey, stableId, StringComparison.Ordinal) ||
                presentation.textKey.EndsWith(".title", StringComparison.Ordinal) ||
                presentation.textKey.EndsWith(".description", StringComparison.Ordinal) ||
                presentation.icon == null || presentation.displayOrder < 0)
            {
                error = "Relic presentation needs a text key, icon, and non-negative order.";
                return false;
            }

            if (gameplay == null || string.IsNullOrWhiteSpace(gameplay.sourceMonsterId) ||
                string.IsNullOrWhiteSpace(gameplay.effectId) || !Finite(gameplay.dropChancePercent) ||
                !Finite(gameplay.effectValue) || !Finite(gameplay.effectValue2))
            {
                error = "Relic gameplay data is incomplete or non-finite.";
                return false;
            }

            error = null;
            return true;
        }

        public RelicInformation ToRuntimeInformation(bool owned = false)
        {
            return new RelicInformation
            {
                id = stableId,
                owned = owned,
                sourceMonsterId = gameplay.sourceMonsterId,
                dropChancePercent = gameplay.dropChancePercent,
                effectId = gameplay.effectId,
                effectValue = gameplay.effectValue,
                effectValue2 = gameplay.effectValue2
            };
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Builds the immutable combat input from the authored relic definitions.</summary>
    public static class RelicDefinitionResources
    {
        public const string ResourcePath = "RelicDefinitions";
        public const int ExpectedCount = 20;

        public static bool TryApplyTo(GameInformation information, out string error)
        {
            error = string.Empty;
            if (information == null)
            {
                error = "Game information is missing.";
                return false;
            }

            RelicDefinition[] definitions = Resources.LoadAll<RelicDefinition>(ResourcePath);
            if (definitions == null || definitions.Length != ExpectedCount)
            {
                error = "Expected 20 RelicDefinition assets under Resources/" + ResourcePath + ".";
                return false;
            }

            Array.Sort(definitions, (left, right) =>
            {
                int leftOrder = left != null && left.presentation != null ? left.presentation.displayOrder : int.MaxValue;
                int rightOrder = right != null && right.presentation != null ? right.presentation.displayOrder : int.MaxValue;
                return leftOrder.CompareTo(rightOrder);
            });

            var previousOwned = new Dictionary<string, bool>(StringComparer.Ordinal);
            if (information.relics != null)
            {
                foreach (RelicInformation existing in information.relics)
                    if (existing != null && !string.IsNullOrWhiteSpace(existing.id))
                        previousOwned[existing.id] = existing.owned;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var textKeys = new HashSet<string>(StringComparer.Ordinal);
            var orders = new HashSet<int>();
            var runtimeDefinitions = new RelicInformation[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
            {
                RelicDefinition definition = definitions[i];
                if (definition == null || !definition.TryValidate(out error))
                {
                    error = "Invalid relic definition at display order " + i + ": " +
                        (definition == null ? "asset is missing" : error);
                    return false;
                }

                if (!ids.Add(definition.stableId) || !textKeys.Add(definition.presentation.textKey) ||
                    !orders.Add(definition.presentation.displayOrder) ||
                    definition.presentation.displayOrder != i)
                {
                    error = "Relic IDs, localization keys, and display orders must be unique and ordered 0..19.";
                    return false;
                }

                bool owned = previousOwned.TryGetValue(definition.stableId, out bool value) && value;
                runtimeDefinitions[i] = definition.ToRuntimeInformation(owned);
            }

            information.relics = runtimeDefinitions;
            return true;
        }
    }
}
