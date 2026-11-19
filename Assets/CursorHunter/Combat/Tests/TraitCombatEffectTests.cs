using System;
using CursorHunter.Contracts;
using NUnit.Framework;

namespace CursorHunter.Combat.Tests
{
    public sealed class TraitCombatEffectTests
    {
        [Test]
        public void RewardEffectOnlyMultipliesSixGemstonesAndRejectsOverflow()
        {
            var values = new[] {
                new ResourceRewardSnapshot("gem.garnet", 2), new ResourceRewardSnapshot("gem.topaz", 3),
                new ResourceRewardSnapshot("gem.amethyst", 4), new ResourceRewardSnapshot("gem.sapphire", 5),
                new ResourceRewardSnapshot("gem.diamond", 6), new ResourceRewardSnapshot("gem.dragon", 7),
                new ResourceRewardSnapshot("other.currency", 8) };
            Assert.That(TraitCombatEffects.TryCreate(new ITraitCombatEffectSnapshot[] {
                new BattleRewardTraitSnapshot("trait.reward", 100, 5) }, 42, out var effects), Is.True);
            Assert.That(effects.TryApplySettlementRewards(values, out var result), Is.True);
            for (int i = 0; i < 6; i++) Assert.That(result[i].Amount, Is.EqualTo(values[i].Amount * 5));
            Assert.That(result[6].Amount, Is.EqualTo(8));
            Assert.That(values[0].Amount, Is.EqualTo(2), "The original result remains unchanged.");
            Assert.That(effects.TryApplySettlementRewards(new[] { new ResourceRewardSnapshot("gem.garnet", long.MaxValue) }, out _), Is.False);
            Assert.That(TraitCombatEffects.TryCreate(new ITraitCombatEffectSnapshot[] {
                new BattleRewardTraitSnapshot("trait.reward", 0, 5) }, 42, out effects), Is.True);
            Assert.That(effects.TryApplySettlementRewards(values, out result), Is.True);
            Assert.That(result[0].Amount, Is.EqualTo(2));
        }

        [Test]
        public void RangeEffectUsesRunTimeBoundariesAndIndependentRolls()
        {
            Assert.That(TraitCombatEffects.TryCreate(new ITraitCombatEffectSnapshot[] {
                new CursorRangeTraitSnapshot("trait.range", 10, 100, 3, 50) }, 9, out var effects), Is.True);
            effects.Advance(9.99f, 30); Assert.That(effects.CursorRangeMultiplier, Is.EqualTo(1f));
            effects.Advance(10f, 30); Assert.That(effects.CursorRangeMultiplier, Is.EqualTo(1.5f));
            effects.Advance(12.99f, 30); Assert.That(effects.CursorRangeMultiplier, Is.EqualTo(1.5f));
            effects.Advance(13f, 30); Assert.That(effects.CursorRangeMultiplier, Is.EqualTo(1f));
            effects.Advance(20f, 30); Assert.That(effects.CursorRangeMultiplier, Is.EqualTo(1.5f));
            effects.Advance(30f, 30); Assert.That(effects.CursorRangeMultiplier, Is.EqualTo(1f));
            Assert.That(TraitCombatEffects.TryCreate(new ITraitCombatEffectSnapshot[] {
                new CursorRangeTraitSnapshot("trait.range", 10, 0, 3, 50) }, 9, out effects), Is.True);
            effects.Advance(25f, 30); Assert.That(effects.CursorRangeMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void ChestAndExperienceEffectsValidateSnapshotsAndDoNotChangeUnownedDefaults()
        {
            var spawn = new SpawnSnapshot("monster.chest", "chest", 10, 1, 1, 1, 100);
            Assert.That(TraitCombatEffects.TryCreate(null, 1, out var empty), Is.True);
            Assert.That(empty.Chests, Is.Empty);
            Assert.That(empty.HasNormalExperienceEffect, Is.False);
            Assert.That(TraitCombatEffects.TryCreate(new ITraitCombatEffectSnapshot[] {
                new TreasureChestTraitSnapshot("trait.chest", 100, 1, spawn),
                new NormalExperienceTraitSnapshot("trait.xp", 10) }, 1, out var owned), Is.True);
            Assert.That(owned.Chests.Count, Is.EqualTo(1));
            Assert.That(owned.Chests[0].Chest.GarnetReward, Is.EqualTo(100));
            decimal remainder = 0m;
            Assert.That(owned.TryScaleNormalExperience(100L, 1f, ref remainder, out long experience), Is.True);
            Assert.That(experience, Is.EqualTo(110));
            Assert.That(TraitCombatEffects.TryCreate(new ITraitCombatEffectSnapshot[] {
                new TreasureChestTraitSnapshot("trait.chest", 0, 1, spawn) }, 1, out owned), Is.True);
            Assert.That(owned.Chests, Is.Empty);
            Assert.That(TraitCombatEffects.TryCreate(new ITraitCombatEffectSnapshot[] {
                new NormalExperienceTraitSnapshot("trait.xp", 10), new NormalExperienceTraitSnapshot("trait.xp", 10) }, 1, out _), Is.False);
            Assert.That(TraitCombatEffects.TryCreate(new ITraitCombatEffectSnapshot[] {
                new BattleRewardTraitSnapshot("trait.bad", 101, 5) }, 1, out _), Is.False);
        }
    }
}
