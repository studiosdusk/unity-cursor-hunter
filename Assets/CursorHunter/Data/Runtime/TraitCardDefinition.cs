using System;
using System.Collections.Generic;
using UnityEngine;

namespace CursorHunter.Data
{
    public enum TraitCardEffectKind
    {
        BattleRewardMultiplier = 0,
        TreasureChestAtBattleStart = 1,
        PeriodicCursorRangeBoost = 2,
        NormalMonsterExperienceBonus = 3
    }

    [Serializable]
    public sealed class TraitCardPresentationDefinition
    {
        [Tooltip("TraitCardText.xlsx의 키 접두사. 제목과 설명은 .title과 .description을 사용합니다.")]
        public string textKey;
        public Sprite icon;
        [Min(0)] public int displayOrder;
    }

    [Serializable]
    public sealed class TraitCardRewardMultiplierEffect
    {
        [Range(0f, 100f)] public float chancePercent;
        [Min(0f)] public float rewardMultiplier = 1f;
    }

    [Serializable]
    public sealed class TraitCardTreasureChestEffect
    {
        [Range(0f, 100f), Tooltip("전투 시작 시 보물상자를 생성할 확률 (%).")]
        public float chancePercent = 100f;
        [Min(1), Tooltip("전투 시작 시 생성할 보물상자 수. 재화 종류와 양은 보물상자 설정에서 정합니다.")]
        public int spawnCount = 1;
        [Tooltip("보물상자의 체력, 외형, 보상을 관리하는 몬스터 SO입니다.")]
        public MonsterDefinition monsterDefinition;
    }

    [Serializable]
    public sealed class TraitCardCursorRangeEffect
    {
        [Min(0f)] public float intervalSeconds;
        [Range(0f, 100f)] public float chancePercent;
        [Min(0f)] public float durationSeconds;
        [Min(0f), Tooltip("커서 범위의 증가율입니다. 예: 50은 +50%입니다.")]
        public float rangeIncreasePercent;
    }

    [Serializable]
    public sealed class TraitCardExperienceEffect
    {
        [Min(0f), Tooltip("일반 몬스터 처치 경험치의 증가율입니다. 예: 10은 +10%입니다.")]
        public float bonusPercent;
    }

    [Serializable]
    public sealed class TraitCardGameplayDefinition
    {
        public TraitCardEffectKind effectKind;
        public TraitCardRewardMultiplierEffect rewardMultiplier = new TraitCardRewardMultiplierEffect();
        public TraitCardTreasureChestEffect treasureChest = new TraitCardTreasureChestEffect();
        public TraitCardCursorRangeEffect cursorRange = new TraitCardCursorRangeEffect();
        public TraitCardExperienceEffect normalMonsterExperience = new TraitCardExperienceEffect();
    }

    /// <summary>One independently authored card; visible text belongs to TraitCardText.xlsx.</summary>
    [CreateAssetMenu(menuName = "Cursor Hunter/Trait Cards/Definition")]
    public sealed class TraitCardDefinition : ScriptableObject
    {
        public string stableId;
        public TraitCardPresentationDefinition presentation = new TraitCardPresentationDefinition();
        public TraitCardGameplayDefinition gameplay = new TraitCardGameplayDefinition();

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(stableId) || !stableId.StartsWith("trait_card.", StringComparison.Ordinal))
            { error = "Trait card ID must start with trait_card."; return false; }
            if (presentation == null || !string.Equals(presentation.textKey, stableId, StringComparison.Ordinal) ||
                presentation.icon == null || presentation.displayOrder < 0)
            { error = "Trait card presentation needs a matching text key, icon, and non-negative order."; return false; }
            if (gameplay == null || !Enum.IsDefined(typeof(TraitCardEffectKind), gameplay.effectKind))
            { error = "Trait card effect kind is invalid."; return false; }

            switch (gameplay.effectKind)
            {
                case TraitCardEffectKind.BattleRewardMultiplier:
                    var reward = gameplay.rewardMultiplier;
                    if (reward == null || !IsPercent(reward.chancePercent) || !IsPositive(reward.rewardMultiplier))
                    { error = "Battle reward chance or multiplier is invalid."; return false; }
                    break;
                case TraitCardEffectKind.TreasureChestAtBattleStart:
                    if (gameplay.treasureChest == null || !IsPercent(gameplay.treasureChest.chancePercent) ||
                        gameplay.treasureChest.spawnCount < 1 ||
                        gameplay.treasureChest.spawnCount > CursorHunter.Contracts.GameInformation.MaximumAliveMonsters ||
                        gameplay.treasureChest.monsterDefinition == null ||
                        gameplay.treasureChest.monsterDefinition.VisualPrefab == null)
                    { error = "Treasure chest chance, count or monster definition is invalid."; return false; }
                    break;
                case TraitCardEffectKind.PeriodicCursorRangeBoost:
                    var range = gameplay.cursorRange;
                    if (range == null || !IsPositive(range.intervalSeconds) || !IsPercent(range.chancePercent) ||
                        !IsPositive(range.durationSeconds) || !IsNonNegative(range.rangeIncreasePercent))
                    { error = "Periodic cursor range effect is invalid."; return false; }
                    break;
                case TraitCardEffectKind.NormalMonsterExperienceBonus:
                    if (gameplay.normalMonsterExperience == null || !IsNonNegative(gameplay.normalMonsterExperience.bonusPercent))
                    { error = "Normal monster experience bonus is invalid."; return false; }
                    break;
            }
            error = null;
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsNonNegative(float value) => Finite(value) && value >= 0f;
        private static bool IsPositive(float value) => Finite(value) && value > 0f;
        private static bool IsPercent(float value) => IsNonNegative(value) && value <= 100f;
    }

    /// <summary>Loads validated definitions without imposing a fixed card count.</summary>
    public static class TraitCardDefinitionResources
    {
        public const string ResourcePath = "TraitCardDefinitions";

        public static bool TryLoad(out TraitCardDefinition[] definitions, out string error)
        {
            definitions = Resources.LoadAll<TraitCardDefinition>(ResourcePath);
            if (definitions == null || definitions.Length == 0)
            { error = "No trait card definitions found under Resources/" + ResourcePath; return false; }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var orders = new HashSet<int>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                { error = "A trait card definition is missing."; return false; }
                if (!definition.TryValidate(out error)) return false;
                if (!ids.Add(definition.stableId) || !orders.Add(definition.presentation.displayOrder))
                { error = "Trait card IDs and display orders must be unique."; return false; }
            }
            Array.Sort(definitions, (left, right) => left.presentation.displayOrder.CompareTo(right.presentation.displayOrder));
            error = null;
            return true;
        }
    }
}
