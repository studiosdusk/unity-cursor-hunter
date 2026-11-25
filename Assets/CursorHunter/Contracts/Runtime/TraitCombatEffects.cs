using System;

namespace CursorHunter.Contracts
{
    /// <summary>Immutable combat input for one acquired trait card.</summary>
    public interface ITraitCombatEffectSnapshot
    {
        string CardId { get; }
        bool IsValid { get; }
    }

    public readonly struct BattleRewardTraitSnapshot : ITraitCombatEffectSnapshot
    {
        public BattleRewardTraitSnapshot(string cardId, float chancePercent, float multiplier)
        { CardId = cardId ?? string.Empty; ChancePercent = chancePercent; Multiplier = multiplier; }
        public string CardId { get; }
        public float ChancePercent { get; }
        public float Multiplier { get; }
        public bool IsValid => TraitSnapshotValidation.HasId(CardId) && TraitSnapshotValidation.Percent(ChancePercent) &&
                               TraitSnapshotValidation.Positive(Multiplier);
    }

    public readonly struct TreasureChestTraitSnapshot : ITraitCombatEffectSnapshot
    {
        public TreasureChestTraitSnapshot(string cardId, float chancePercent, int count, SpawnSnapshot chest)
        { CardId = cardId ?? string.Empty; ChancePercent = chancePercent; Count = count; Chest = chest; }
        public string CardId { get; }
        public float ChancePercent { get; }
        public int Count { get; }
        public SpawnSnapshot Chest { get; }
        public bool IsValid => TraitSnapshotValidation.HasId(CardId) && TraitSnapshotValidation.Percent(ChancePercent) &&
                               Count > 0 && Count <= GameInformation.MaximumAliveMonsters && Chest.IsValid;
    }

    public readonly struct CursorRangeTraitSnapshot : ITraitCombatEffectSnapshot
    {
        public CursorRangeTraitSnapshot(string cardId, float intervalSeconds, float chancePercent,
            float durationSeconds, float increasePercent)
        { CardId = cardId ?? string.Empty; IntervalSeconds = intervalSeconds; ChancePercent = chancePercent;
          DurationSeconds = durationSeconds; IncreasePercent = increasePercent; }
        public string CardId { get; }
        public float IntervalSeconds { get; }
        public float ChancePercent { get; }
        public float DurationSeconds { get; }
        public float IncreasePercent { get; }
        public bool IsValid => TraitSnapshotValidation.HasId(CardId) && TraitSnapshotValidation.Positive(IntervalSeconds) &&
                               TraitSnapshotValidation.Percent(ChancePercent) && TraitSnapshotValidation.Positive(DurationSeconds) &&
                               TraitSnapshotValidation.NonNegative(IncreasePercent);
    }

    public readonly struct NormalExperienceTraitSnapshot : ITraitCombatEffectSnapshot
    {
        public NormalExperienceTraitSnapshot(string cardId, float bonusPercent)
        { CardId = cardId ?? string.Empty; BonusPercent = bonusPercent; }
        public string CardId { get; }
        public float BonusPercent { get; }
        public bool IsValid => TraitSnapshotValidation.HasId(CardId) && TraitSnapshotValidation.NonNegative(BonusPercent);
    }

    internal static class TraitSnapshotValidation
    {
        internal static bool HasId(string id) => !string.IsNullOrWhiteSpace(id);
        internal static bool NonNegative(float value) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Positive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Percent(float value) => NonNegative(value) && value <= 100f;
    }
}
