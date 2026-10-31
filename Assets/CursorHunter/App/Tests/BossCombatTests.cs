using System;
using System.Collections.Generic;
using System.Linq;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;
using CursorHunter.Progression;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CursorHunter.App.Tests
{
    public sealed class BossCombatTests
    {
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
        [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
        [TestCase(16)] [TestCase(17)] [TestCase(18)] [TestCase(19)] [TestCase(20)]
        public void LaterBossesKeepDistinctDedicatedVisualsAndNoSpecialAbility(int stage)
        {
            var bosses = Resources.LoadAll<BossDefinition>("BossDefinitions");
            BossDefinition boss = bosses.Single(x => x.Stage == stage);
            GameObject Original(GameObject prefab) => PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefab) ?? prefab;
            GameObject source = Original(boss.VisualPrefab);
            // Master now uses the stage 16/20 source art for normal monsters too.
            // Preserve the authored boss roster and require distinct dedicated boss variants.
            foreach (BossDefinition other in bosses.Where(x => x.Stage >= 6 && x.Stage != stage))
                Assert.That(source, Is.Not.SameAs(Original(other.VisualPrefab)), other.name);
            Assert.That(AssetDatabase.GetAssetPath(boss.VisualPrefab), Does.StartWith("Assets/CursorHunter/Combat/Prefabs/Monsters/BossVisuals/"));
            SpawnSnapshot snapshot = boss.CreateSnapshot();
            Assert.That(snapshot.SpecialAbility, Is.EqualTo(MonsterSpecialAbility.None));
            Assert.That(snapshot.MovementMode, Is.EqualTo(MonsterMovementMode.BoundedWander));
            Assert.That(snapshot.MoveSpeed, Is.EqualTo(0.6f).Within(0.001));
            var animator = boss.VisualPrefab.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            var death = animator.runtimeAnimatorController.animationClips.Where(x => x.name.Contains("Dead")).ToArray();
            Assert.That(death, Is.Not.Empty);
            Assert.That(death.All(x => !AnimationUtility.GetAnimationClipSettings(x).loopTime), Is.True);
            foreach (Transform child in boss.VisualPrefab.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero);
        }

        [Test]
        public void CatalogHasTwentyUniqueStagesWithOneBossAndExplicitRewards()
        {
            BossDefinition[] bosses = Resources.LoadAll<BossDefinition>("BossDefinitions");
            Assert.That(bosses.Length, Is.EqualTo(20));
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 20), bosses.Select(x => x.Stage));
            Assert.That(bosses.Select(x => x.BossId).Distinct().Count(), Is.EqualTo(20));
            foreach (BossDefinition boss in bosses)
            {
                Assert.That(boss.IsValid, Is.True, boss.name);
                SpawnSnapshot spawn = boss.CreateSnapshot();
                Assert.That(spawn.IsValid, Is.True);
                Assert.That(spawn.PackSize, Is.EqualTo(1));
                Assert.That(spawn.AliveLimit, Is.EqualTo(1));
                Assert.That(spawn.BonusDropAmount, Is.Zero);
                Assert.That(spawn.GarnetReward, Is.Zero, "The boss run owns the reward list.");
                var rewards = boss.CreateClearRewards();
                Assert.That(rewards.Length, Is.EqualTo(boss.Stage >= 13 ? 6 : boss.Stage >= 9 ? 5 : boss.Stage >= 5 ? 4 : 3));
                Assert.That(rewards.Select(x => x.CurrencyId).Distinct().Count(), Is.EqualTo(rewards.Length));
                Assert.That(rewards.All(x => x.IsValid), Is.True);
                Assert.That(rewards.Single(x => x.CurrencyId == "gem.garnet").Amount, Is.GreaterThanOrEqualTo(1000));
            }
            Assert.That(HuntManager.GetRunDuration(RunMode.Boss, null), Is.EqualTo(30));
        }

        [TestCase(29.99f, true)]
        [TestCase(30f, false)]
        public void LethalHitBeforeDeadlineWinsOnceAndExactDeadlineFails(float elapsed, bool wins)
        {
            var go = new GameObject("Boss combat test");
            try
            {
                var combat = go.AddComponent<CombatRunController>();
                var request = new RunRequest(RunId.Create(), 4, 1, RunMode.Boss, "boss.01", 1, 30, 1);
                Assert.That(combat.StartRun(request, new CombatSnapshot(10, 1, .5f, 1)), Is.True);
                var target = new Target(request.RunId);
                RunResult result = default;
                int completions = 0;
                combat.CompletionRequested += reason =>
                {
                    completions++;
                    Assert.That(combat.TryCompleteRun(reason, RunSettlementPolicy.Eligible, out result), Is.True);
                };
                combat.AdvanceTime(elapsed);
                Assert.That(combat.TryApplySkillDamage(request.RunId, target, 1000), Is.EqualTo(wins));
                combat.AdvanceTime(10);
                Assert.That(completions, Is.EqualTo(1));
                Assert.That(result.BossStage, Is.EqualTo(1));
                Assert.That(result.BossDefeated, Is.EqualTo(wins));
                Assert.That(result.GarnetEarned, Is.EqualTo(wins ? 1000 : 0));
                Assert.That(result.SettlementPolicy, Is.EqualTo(wins ? RunSettlementPolicy.Eligible : RunSettlementPolicy.Discard));
                Assert.That(combat.TryApplySkillDamage(request.RunId, target, 1000), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void BossReceiptAndProgressRoundTripWithWalletWithoutChangingLegacyFields()
        {
            const string key = "cursor_hunter.progression.v1";
            bool existed = PlayerPrefs.HasKey(key);
            string previous = PlayerPrefs.GetString(key);
            try
            {
                PlayerPrefs.DeleteKey(key);
                Assert.That(TraitProgressionStore.Save(1234, new long[] { 1234, 40 },
                    new[] { "stat.attack.01" }, new Dictionary<string, int>(),
                    new Dictionary<string, string>(), unlockedBossTier: 5,
                    ownedRelicIds: new[] { "relic.red_potion" },
                    highestBossStageCleared: 7,
                    settledBossRunIds: new[] { "boss-receipt" }), Is.True);
                Assert.That(TraitProgressionStore.TryLoad(out var loaded), Is.True);
                Assert.That(loaded.highestBossStageCleared, Is.EqualTo(7));
                Assert.That(loaded.settledBossRunIds, Is.EqualTo(new[] { "boss-receipt" }));
                Assert.That(loaded.garnetBalance, Is.EqualTo(1234));
                Assert.That(loaded.unlockedBossTier, Is.EqualTo(5));
                Assert.That(loaded.ownedRelicIds, Is.EqualTo(new[] { "relic.red_potion" }));
                PlayerPrefs.SetString(key, "{\"version\":4,\"garnetBalance\":99}");
                Assert.That(TraitProgressionStore.TryLoad(out loaded), Is.True);
                Assert.That(loaded.highestBossStageCleared, Is.Zero);
                Assert.That(loaded.settledBossRunIds, Is.Empty);
            }
            finally
            {
                if (existed) PlayerPrefs.SetString(key, previous); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BossRewardListIsCapturedAndOnlyPaidOnVictory(bool wins)
        {
            var go = new GameObject("Multi gemstone boss test");
            try
            {
                string[] ids = { "garnet", "topaz", "amethyst", "sapphire", "diamond", "dragon" };
                var rewards = ids.Select((id, i) => new ResourceRewardSnapshot("gem." + id, 100 + i)).ToArray();
                var request = new RunRequest(RunId.Create(), 4, 1, RunMode.Boss, "boss.20", 1, 30, 20, rewards);
                rewards[0] = new ResourceRewardSnapshot("gem.garnet", 9999);
                Assert.That(request.BossClearRewards[0].Amount, Is.EqualTo(100), "Copy at run start.");
                var combat = go.AddComponent<CombatRunController>();
                Assert.That(combat.StartRun(request, new CombatSnapshot(10, 1, .5f, 1)), Is.True);
                var target = new Target(request.RunId);
                RunResult result = default;
                int completed = 0, rewardEvents = 0;
                combat.RewardResolved += _ => rewardEvents++;
                combat.CompletionRequested += reason =>
                {
                    completed++;
                    Assert.That(combat.TryCompleteRun(reason, RunSettlementPolicy.Eligible, out result), Is.True);
                };
                if (wins) Assert.That(combat.TryApplySkillDamage(request.RunId, target, 100), Is.True);
                else combat.AdvanceTime(30);
                Assert.That(result.BossDefeated, Is.EqualTo(wins));
                Assert.That(result.Rewards.Count, Is.EqualTo(wins ? 6 : 0));
                if (wins)
                    foreach (var reward in request.BossClearRewards)
                        Assert.That(result.Rewards.Single(x => x.CurrencyId == reward.CurrencyId).Amount, Is.EqualTo(reward.Amount));
                Assert.That(result.GarnetEarned, Is.EqualTo(wins ? 100 : 0), "Do not also pay the target's legacy drop.");
                Assert.That(combat.TryApplySkillDamage(request.RunId, target, 100), Is.False);
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(rewardEvents, Is.EqualTo(wins ? 1 : 0));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void DuplicateOrInvalidBossRewardsAreRejectedBeforeCombat()
        {
            Assert.Throws<ArgumentException>(() => new RunRequest(RunId.Create(), 4, 1, RunMode.Boss,
                "boss.01", 1, 30, 1, new[] { new ResourceRewardSnapshot("gem.topaz", 5), new ResourceRewardSnapshot("gem.topaz", 10) }));
            Assert.Throws<ArgumentException>(() => new RunRequest(RunId.Create(), 4, 1, RunMode.Boss,
                "boss.01", 1, 30, 1, new[] { new ResourceRewardSnapshot("gem.topaz", 0) }));
        }

        private sealed class Target : ICombatTarget
        {
            public Target(RunId id) { RunId = id; }
            public RunId RunId { get; }
            public bool IsRegistered => true;
            public bool IsActive { get; private set; } = true;
            public long GarnetReward => 1000;
            public string BonusDropCurrencyId => string.Empty;
            public long BonusDropAmount => 0;
            public float BonusDropChancePercent => 0;
            public bool ApplyDamage(RunId id, long amount, out long effective, out bool killed)
            {
                killed = IsActive && id == RunId && amount > 0;
                effective = killed ? 10 : 0;
                if (killed) IsActive = false;
                return killed;
            }
        }
    }
}
