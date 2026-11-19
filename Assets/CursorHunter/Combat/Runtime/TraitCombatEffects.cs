using System;
using System.Collections.Generic;
using CursorHunter.Contracts;

namespace CursorHunter.Combat
{
    /// <summary>Run-scoped effect modules built only from immutable trait snapshots.</summary>
    internal sealed class TraitCombatEffects
    {
        private readonly List<BattleRewardTraitEffect> rewards = new List<BattleRewardTraitEffect>();
        private readonly List<CursorRangeTraitEffect> ranges = new List<CursorRangeTraitEffect>();
        private readonly List<NormalExperienceTraitEffect> experience = new List<NormalExperienceTraitEffect>();
        private readonly List<TreasureChestTraitSnapshot> chests = new List<TreasureChestTraitSnapshot>();
        public IReadOnlyList<TreasureChestTraitSnapshot> Chests => chests;

        public static bool TryCreate(IReadOnlyList<ITraitCombatEffectSnapshot> snapshots, ulong seed,
            out TraitCombatEffects effects)
        {
            effects = new TraitCombatEffects();
            if (snapshots == null) return true;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var snapshot in snapshots)
            {
                if (snapshot == null || !snapshot.IsValid || !ids.Add(snapshot.CardId)) return false;
                ulong effectSeed = seed ^ StableHash(snapshot.CardId);
                switch (snapshot)
                {
                    case BattleRewardTraitSnapshot reward:
                        effects.rewards.Add(new BattleRewardTraitEffect(reward, effectSeed));
                        break;
                    case TreasureChestTraitSnapshot chest:
                        if (new TreasureChestTraitEffect(chest, effectSeed).ShouldSpawn()) effects.chests.Add(chest);
                        break;
                    case CursorRangeTraitSnapshot range:
                        effects.ranges.Add(new CursorRangeTraitEffect(range, effectSeed));
                        break;
                    case NormalExperienceTraitSnapshot xp:
                        effects.experience.Add(new NormalExperienceTraitEffect(xp));
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        public bool HasNormalExperienceEffect => experience.Count > 0;

        public bool TryScaleNormalExperience(long amount, float fieldMultiplier, ref decimal remainder, out long result)
        {
            result = 0L;
            if (amount <= 0L) return true;
            try
            {
                decimal multiplier = 1m;
                foreach (var effect in experience) multiplier += (decimal)effect.BonusPercent / 100m;
                decimal total = amount * (decimal)fieldMultiplier * multiplier + remainder;
                if (total < 0m || total > long.MaxValue) return false;
                result = (long)decimal.Floor(total);
                remainder = total - result;
                return true;
            }
            catch (OverflowException) { return false; }
        }

        public float CursorRangeMultiplier
        {
            get
            {
                double bonus = 0d;
                foreach (var effect in ranges) if (effect.IsActive) bonus += effect.IncreasePercent;
                return (float)(1d + bonus / 100d);
            }
        }

        public void Advance(float elapsed, float duration)
        {
            foreach (var effect in ranges) effect.Advance(elapsed, duration);
        }

        public bool TryApplySettlementRewards(ResourceRewardSnapshot[] original, out ResourceRewardSnapshot[] result)
        {
            result = original;
            foreach (var effect in rewards)
                if (!effect.TryApply(result, out result)) return false;
            return true;
        }

        private static ulong StableHash(string text)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (char c in text) { hash ^= c; hash *= 1099511628211UL; }
                return hash;
            }
        }
    }

    internal static class TraitEffectRoll
    {
        public static bool Pass(SeededRandom random, float chancePercent) => chancePercent >= 100f ||
            (chancePercent > 0f && random.NextFloat(0f, 100f) < chancePercent);
    }

    internal sealed class BattleRewardTraitEffect
    {
        private readonly BattleRewardTraitSnapshot snapshot;
        private readonly SeededRandom random;
        public BattleRewardTraitEffect(BattleRewardTraitSnapshot snapshot, ulong seed)
        { this.snapshot = snapshot; random = new SeededRandom(seed); }

        public bool TryApply(ResourceRewardSnapshot[] original, out ResourceRewardSnapshot[] result)
        {
            result = original;
            if (!TraitEffectRoll.Pass(random, snapshot.ChancePercent)) return true;
            var copy = new List<ResourceRewardSnapshot>(original.Length);
            for (int i = 0; i < original.Length; i++)
            {
                if (!IsGemstone(original[i].CurrencyId)) { copy.Add(original[i]); continue; }
                try
                {
                    decimal scaled = decimal.Floor(original[i].Amount * (decimal)snapshot.Multiplier);
                    if (scaled < 0m || scaled > long.MaxValue) return false;
                    if (scaled > 0m) copy.Add(new ResourceRewardSnapshot(original[i].CurrencyId, (long)scaled));
                }
                catch (OverflowException) { return false; }
            }
            result = copy.ToArray();
            return true;
        }

        private static bool IsGemstone(string id) => id == "gem.garnet" || id == "gem.topaz" ||
            id == "gem.amethyst" || id == "gem.sapphire" || id == "gem.diamond" || id == "gem.dragon";
    }

    internal sealed class TreasureChestTraitEffect
    {
        private readonly TreasureChestTraitSnapshot snapshot;
        private readonly SeededRandom random;
        public TreasureChestTraitEffect(TreasureChestTraitSnapshot snapshot, ulong seed)
        { this.snapshot = snapshot; random = new SeededRandom(seed); }
        public bool ShouldSpawn() => TraitEffectRoll.Pass(random, snapshot.ChancePercent);
    }

    internal sealed class CursorRangeTraitEffect
    {
        private readonly CursorRangeTraitSnapshot snapshot;
        private readonly SeededRandom random;
        private double nextRoll, activeUntil;
        public CursorRangeTraitEffect(CursorRangeTraitSnapshot snapshot, ulong seed)
        { this.snapshot = snapshot; random = new SeededRandom(seed); nextRoll = snapshot.IntervalSeconds; }
        public bool IsActive { get; private set; }
        public float IncreasePercent => snapshot.IncreasePercent;
        public void Advance(float elapsed, float runDuration)
        {
            if (snapshot.ChancePercent <= 0f) return;
            while (nextRoll <= elapsed && nextRoll < runDuration)
            {
                if (TraitEffectRoll.Pass(random, snapshot.ChancePercent))
                    activeUntil = Math.Max(activeUntil, nextRoll + snapshot.DurationSeconds);
                nextRoll += snapshot.IntervalSeconds;
            }
            IsActive = elapsed < activeUntil;
        }
    }

    internal sealed class NormalExperienceTraitEffect
    {
        private readonly NormalExperienceTraitSnapshot snapshot;
        public NormalExperienceTraitEffect(NormalExperienceTraitSnapshot snapshot) { this.snapshot = snapshot; }
        public float BonusPercent => snapshot.BonusPercent;
    }
}
