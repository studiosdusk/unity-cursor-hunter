using System.Collections.Generic;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace CursorHunter.Combat.Tests
{
    public sealed class RelicDropResolutionTests
    {
        [Test]
        public void DropChanceIsSumOfFixedUnownedCandidatesForTheDefeatedMonster()
        {
            RelicCombatSnapshot[] relics =
            {
                Relic("relic.owned", true, "monster.bee", 40f),
                Relic("relic.first", false, "monster.bee", 3f),
                Relic("relic.second", false, "monster.bee", 2f),
                Relic("relic.otherSource", false, "monster.stump", 30f)
            };

            Assert.That(CombatRunController.SumEligibleRelicDropChance(
                "monster.bee", relics, new HashSet<string>()), Is.EqualTo(5f));
        }

        [Test]
        public void FindingOneRelicDoesNotIncreaseTheRemainingRelicChance()
        {
            RelicCombatSnapshot[] relics =
            {
                Relic("relic.first", false, "monster.bee", 3f),
                Relic("relic.second", false, "monster.bee", 2f)
            };
            var awarded = new HashSet<string> { "relic.first" };

            float remainingChance = CombatRunController.SumEligibleRelicDropChance(
                "monster.bee", relics, awarded);

            Assert.That(remainingChance, Is.EqualTo(2f),
                "The remaining item keeps its authored 2% chance; it is not normalized to 5%.");
            Assert.That(CombatRunController.PassesRelicDropRoll(1.999f, remainingChance), Is.True);
            Assert.That(CombatRunController.PassesRelicDropRoll(2f, remainingChance), Is.False);
            Assert.That(CombatRunController.SelectRelicDropCandidate(
                "monster.bee", relics, awarded, 1.999f), Is.EqualTo("relic.second"));
        }

        [Test]
        public void SuccessfulDropSelectsAtMostOneCandidateUsingItsFixedWeight()
        {
            RelicCombatSnapshot[] relics =
            {
                Relic("relic.first", false, "monster.bee", 3f),
                Relic("relic.second", false, "monster.bee", 2f)
            };
            var awarded = new HashSet<string>();
            float totalChance = CombatRunController.SumEligibleRelicDropChance(
                "monster.bee", relics, awarded);

            Assert.That(CombatRunController.PassesRelicDropRoll(4.999f, totalChance), Is.True);
            Assert.That(CombatRunController.PassesRelicDropRoll(5f, totalChance), Is.False);
            Assert.That(CombatRunController.SelectRelicDropCandidate(
                "monster.bee", relics, awarded, 2.999f), Is.EqualTo("relic.first"));
            Assert.That(CombatRunController.SelectRelicDropCandidate(
                "monster.bee", relics, awarded, 3f), Is.EqualTo("relic.second"));
            Assert.That(CombatRunController.SelectRelicDropCandidate(
                "monster.bee", relics, awarded, 4.999f), Is.EqualTo("relic.second"));
        }

        [Test]
        public void NormalMonsterKillCarriesOneRelicAwardIntoRunResult()
        {
            var root = new GameObject("Relic drop integration test");
            try
            {
                var combat = root.AddComponent<CombatRunController>();
                var request = new RunRequest("relic-drop-integration", 30f);
                Assert.That(combat.StartRun(request,
                    new CombatSnapshot(1L, 1f, 0f, 1)), Is.True);

                var relics = new RelicCombatSnapshot[20];
                for (int i = 0; i < relics.Length; i++)
                {
                    relics[i] = Relic("relic.test." + i, false,
                        i == 0 ? "monster.bee" : "monster.other." + i,
                        i == 0 ? 100f : 1f);
                }

                var dropRules = new RelicDropRulesCombatSnapshot(
                    1, "unownedOnly", "sumFixedChanceThenWeightedPick", true);
                Assert.That(combat.ConfigureRelics(relics, dropRules), Is.True);

                var monsterObject = new GameObject("Bee monster");
                monsterObject.transform.SetParent(root.transform);
                var monster = monsterObject.AddComponent<MonsterCombatTarget>();
                monster.Initialize(new SpawnSnapshot("monster.bee", "test.visual",
                    1L, 1f, 1, 10, 0L), request.RunId);

                Assert.That(combat.TryApplySkillDamage(request.RunId, monster, 1L), Is.True);
                Assert.That(monster.IsDead, Is.True);
                Assert.That(combat.TryCompleteRun(RunEndReason.UserExit,
                    RunSettlementPolicy.Eligible, out RunResult result), Is.True);
                Assert.That(result.DefeatedCount, Is.EqualTo(1));
                Assert.That(result.RelicAwards.Count, Is.EqualTo(1));
                Assert.That(result.RelicAwards[0], Is.EqualTo("relic.test.0"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RelicCombatSnapshot Relic(
            string id, bool owned, string sourceMonsterId, float dropChancePercent)
        {
            return new RelicCombatSnapshot(id, owned, sourceMonsterId,
                dropChancePercent, "test.effect", 0f, 0f);
        }
    }
}
