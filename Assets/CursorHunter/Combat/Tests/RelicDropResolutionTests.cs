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
        public void PreviouslyOwnedRelicDoesNotIncreaseTheRemainingRelicChance()
        {
            RelicCombatSnapshot[] relics =
            {
                Relic("relic.first", true, "monster.bee", 3f),
                Relic("relic.second", false, "monster.bee", 2f)
            };
            var awarded = new HashSet<string>();

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

        [TestCase(RunSettlementPolicy.Eligible)]
        [TestCase(RunSettlementPolicy.Discard)]
        public void ARunDropsAtMostOneRelicAndTheNextRunCanDropAnother(RunSettlementPolicy policy)
        {
            var root = new GameObject("Relic per-run limit test");
            try
            {
                var combat = root.AddComponent<CombatRunController>();
                var firstRun = new RunRequest("relic-limit-first-" + policy, 30f);
                var rules = new RelicDropRulesCombatSnapshot(
                    1, "unownedOnly", "sumFixedChanceThenWeightedPick", true);
                RelicCombatSnapshot[] relics = GuaranteedRelics(false);
                var rewards = new List<CombatRewardResolved>();
                combat.RewardResolved += rewards.Add;

                Assert.That(combat.StartRun(firstRun, new CombatSnapshot(1L, 1f, 0f, 1)), Is.True);
                Assert.That(combat.ConfigureRelics(relics, rules), Is.True);
                // Seed 0 rolls above 1% for this candidate, so a real failed roll
                // must leave the run's relic slot available for the next kill.
                Kill(combat, root.transform, firstRun.RunId, "monster.missed");
                Kill(combat, root.transform, firstRun.RunId, "monster.bee");
                Assert.That(combat.ConfigureRelics(relics, rules), Is.True,
                    "Refreshing the current relic configuration must not reset the run limit.");
                Kill(combat, root.transform, firstRun.RunId, "monster.stump");

                Assert.That(rewards.Count, Is.EqualTo(3));
                Assert.That(rewards[0].RelicAwardId, Is.Empty);
                Assert.That(rewards[1].RelicAwardId, Is.EqualTo("relic.test.0"));
                Assert.That(rewards[2].RelicAwardId, Is.Empty,
                    "A different monster's guaranteed drop is blocked after the first relic.");
                Assert.That(combat.GarnetEarned, Is.EqualTo(21L));
                Assert.That(combat.GetResourceEarned("gem.topaz"), Is.EqualTo(9L));
                Assert.That(combat.ExperienceEarned, Is.EqualTo(6L));
                Assert.That(combat.TryCompleteRun(RunEndReason.UserExit, policy, out RunResult firstResult), Is.True);
                Assert.That(firstResult.RelicAwards, Is.EqualTo(new[] { "relic.test.0" }));
                Assert.That(firstResult.DefeatedCount, Is.EqualTo(3));

                var secondRun = new RunRequest("relic-limit-second-" + policy, 30f);
                Assert.That(combat.StartRun(secondRun, new CombatSnapshot(1L, 1f, 0f, 1)), Is.True);
                Assert.That(combat.ConfigureRelics(GuaranteedRelics(true), rules), Is.True);
                Kill(combat, root.transform, secondRun.RunId, "monster.stump");
                Assert.That(combat.TryCompleteRun(RunEndReason.TimeExpired,
                    RunSettlementPolicy.Eligible, out RunResult secondResult), Is.True);
                Assert.That(secondResult.RelicAwards, Is.EqualTo(new[] { "relic.test.1" }));
                Assert.That(firstResult.RelicAwards, Is.EqualTo(new[] { "relic.test.0" }),
                    "Starting the next run cannot alter the previous result.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AReentrantKillCannotGrantASecondRelic()
        {
            var root = new GameObject("Relic nested reward test");
            try
            {
                var combat = root.AddComponent<CombatRunController>();
                var run = new RunRequest("relic-limit-reentrant", 30f);
                Assert.That(combat.StartRun(run, new CombatSnapshot(1L, 1f, 0f, 1)), Is.True);
                Assert.That(combat.ConfigureRelics(GuaranteedRelics(false), new RelicDropRulesCombatSnapshot(
                    1, "unownedOnly", "sumFixedChanceThenWeightedPick", true)), Is.True);
                var rewards = new List<CombatRewardResolved>();
                combat.RewardResolved += reward =>
                {
                    rewards.Add(reward);
                    if (!string.IsNullOrEmpty(reward.RelicAwardId))
                        Kill(combat, root.transform, run.RunId, "monster.stump");
                };

                Kill(combat, root.transform, run.RunId, "monster.bee");
                Assert.That(rewards.Count, Is.EqualTo(2));
                Assert.That(rewards[0].RelicAwardId, Is.EqualTo("relic.test.0"));
                Assert.That(rewards[1].RelicAwardId, Is.Empty);
                Assert.That(combat.TryCompleteRun(RunEndReason.TimeExpired,
                    RunSettlementPolicy.Eligible, out RunResult result), Is.True);
                Assert.That(result.RelicAwards, Is.EqualTo(new[] { "relic.test.0" }));
                Assert.That(result.GarnetEarned, Is.EqualTo(14L));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RelicCombatSnapshot[] GuaranteedRelics(bool firstOwned)
        {
            var relics = new RelicCombatSnapshot[20];
            for (int i = 0; i < relics.Length; i++)
                relics[i] = Relic("relic.test." + i, firstOwned && i == 0,
                    i == 0 ? "monster.bee" : i == 1 ? "monster.stump" :
                    i == 2 ? "monster.missed" : "monster.unused." + i,
                    i < 2 ? 100f : 1f);
            return relics;
        }

        private static void Kill(CombatRunController combat, Transform parent, RunId runId, string monsterId)
        {
            var monsterObject = new GameObject(monsterId);
            monsterObject.transform.SetParent(parent);
            var monster = monsterObject.AddComponent<MonsterCombatTarget>();
            monster.Initialize(new SpawnSnapshot(monsterId, "test.visual", 1L, 1f, 1, 10,
                7L, "gem.topaz", 3L, 100f, experienceReward: 2L), runId);
            Assert.That(combat.TryApplySkillDamage(runId, monster, 1L), Is.True);
            Assert.That(monster.IsDead, Is.True);
        }

        private static RelicCombatSnapshot Relic(
            string id, bool owned, string sourceMonsterId, float dropChancePercent)
        {
            return new RelicCombatSnapshot(id, owned, sourceMonsterId,
                dropChancePercent, "cursorAttackDamagePercent", 0f, 0f);
        }
    }
}
