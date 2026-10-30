using CursorHunter.Contracts;
using NUnit.Framework;

namespace CursorHunter.App.Tests
{
    public sealed class BattleResultAmountTests
    {
        [Test]
        public void CountUpKeepsExactInt64EndpointAndMonotonicIntermediateValues()
        {
            long previous = 0;
            for (int i = 0; i <= 1000; i++)
            {
                long current = BattleResultPresentation.InterpolateAmount(long.MaxValue, i / 1000f);
                Assert.That(current, Is.GreaterThanOrEqualTo(previous));
                Assert.That(current, Is.LessThanOrEqualTo(long.MaxValue));
                previous = current;
            }
            Assert.That(previous, Is.EqualTo(long.MaxValue));
            Assert.That(BattleResultPresentation.InterpolateAmount(0, 0.5f), Is.Zero);
            Assert.That(BattleResultPresentation.InterpolateAmount(48, 1f), Is.EqualTo(48));
        }

        [Test]
        public void RewardsUseCurrencyIdsAndNeverDoubleCountLegacyGarnet()
        {
            var request = new RunRequest("result-unit-test", 15);
            var result = new RunResult(request, RunEndReason.TimeExpired, RunSettlementPolicy.Eligible,
                15, 2, 43, 100, new[] {
                    new ResourceRewardSnapshot("gem.dragon", 48),
                    new ResourceRewardSnapshot("gem.garnet", 43),
                    new ResourceRewardSnapshot("gem.topaz", long.MaxValue),
                    new ResourceRewardSnapshot("gem.topaz", 1) });
            Assert.That(BattleResultPresentation.GetEarned(result, "gem.garnet"), Is.EqualTo(43));
            Assert.That(BattleResultPresentation.GetEarned(result, "gem.dragon"), Is.EqualTo(48));
            Assert.That(BattleResultPresentation.GetEarned(result, "gem.sapphire"), Is.Zero);
            Assert.That(BattleResultPresentation.GetEarned(result, "gem.topaz"), Is.EqualTo(long.MaxValue));
            var legacy = new RunResult(request, RunEndReason.TimeExpired, RunSettlementPolicy.Eligible, 15, 2, 43, 100);
            Assert.That(BattleResultPresentation.GetEarned(legacy, "gem.garnet"), Is.EqualTo(43));
        }

        [Test]
        public void RunResultCopiesRelicAwardsAsImmutableRunOutput()
        {
            string[] awards = { "relic.red_potion" };
            var result = new RunResult(
                new RunRequest("relic-award-test", 15),
                RunEndReason.TimeExpired,
                RunSettlementPolicy.Eligible,
                15,
                1,
                5,
                5,
                null,
                awards);

            awards[0] = "relic.blue_potion";
            Assert.That(result.RelicAwards.Count, Is.EqualTo(1));
            Assert.That(result.RelicAwards[0], Is.EqualTo("relic.red_potion"));
        }
    }
}
