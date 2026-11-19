using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;
using CursorHunter.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CursorHunter.App.Tests
{
    public sealed class TraitCardCombatPlayModeTests
    {
        private const string Key = "cursor_hunter.progression.v1";
        private static readonly string[] Owned = { "trait_card.lucky_settlement", "trait_card.treasure_chest",
            "trait_card.expanding_focus", "trait_card.hunting_wisdom" };

        [UnityTest]
        public IEnumerator NormalExperienceUsesExactConfiguredPercentAndAccumulatesFractions()
        {
            var cases = new[] {
                (count: 1, amount: 100000000L, bonus: 10f, tier: 1, world: 1f, expected: 110000000L),
                (count: 10, amount: 1L, bonus: 30f, tier: 1, world: 1f, expected: 13L),
                (count: 1, amount: 100000000L, bonus: 10f, tier: 2, world: 1f, expected: 115500000L),
                (count: 10, amount: 1L, bonus: 0f, tier: 1, world: .5f, expected: 5L),
                (count: 10, amount: 1L, bonus: 10f, tier: 1, world: 2f, expected: 22L)
            };
            foreach (var item in cases)
            {
                var root = new GameObject("TraitExperienceCombat");
                var targets = new List<GameObject>();
                try
                {
                    var combat = root.AddComponent<CombatRunController>();
                    var request = new RunRequest(RunId.Create(), 1, 1, RunMode.NormalField, string.Empty, 1, 30,
                        fieldTier: item.tier, worldExperienceMultiplier: item.world);
                    Assert.That(combat.PrepareRun(request, new CombatSnapshot(10, 1, 1, 1)), Is.True);
                    if (item.bonus > 0f)
                        Assert.That(combat.ConfigureTraitEffects(new ITraitCombatEffectSnapshot[] {
                            new NormalExperienceTraitSnapshot("trait.xp", item.bonus) }), Is.True);
                    Assert.That(combat.CommitStart(), Is.True);
                    for (int i = 0; i < item.count; i++)
                    {
                        var targetObject = new GameObject("TraitExperienceVictim");
                        targets.Add(targetObject);
                        var target = targetObject.AddComponent<MonsterCombatTarget>();
                        target.Initialize(new SpawnSnapshot("monster.xp", "test", 1, 1, 1, 1, 0,
                            string.Empty, 0, 0, experienceReward: item.amount), request.RunId);
                        Assert.That(combat.TryApplySkillDamage(request.RunId, target, 100), Is.True);
                    }
                    Assert.That(combat.ExperienceEarned, Is.EqualTo(item.expected));
                }
                finally
                {
                    Object.Destroy(root);
                    foreach (var target in targets) Object.Destroy(target);
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator SettlementOverflowDiscardsTheWholeResult()
        {
            var root = new GameObject("TraitOverflowCombat");
            var targetObject = new GameObject("TraitOverflowTarget");
            try
            {
                var combat = root.AddComponent<CombatRunController>();
                var request = new RunRequest("trait-overflow-" + Guid.NewGuid(), 30);
                Assert.That(combat.PrepareRun(request, new CombatSnapshot(10, 1, 1, 1)), Is.True);
                Assert.That(combat.ConfigureTraitEffects(new ITraitCombatEffectSnapshot[] {
                    new BattleRewardTraitSnapshot("trait.reward", 100, 5) }), Is.True);
                Assert.That(combat.CommitStart(), Is.True);
                var target = targetObject.AddComponent<MonsterCombatTarget>();
                target.Initialize(new SpawnSnapshot("monster.overflow", "test", 1, 1, 1, 1, long.MaxValue), request.RunId);
                Assert.That(combat.TryApplySkillDamage(request.RunId, target, 100), Is.True);
                Assert.That(combat.TryCompleteRun(RunEndReason.ManualComplete, RunSettlementPolicy.Eligible, out var result), Is.True);
                Assert.That(result.EndReason, Is.EqualTo(RunEndReason.NumericOverflow));
                Assert.That(result.SettlementPolicy, Is.EqualTo(RunSettlementPolicy.Discard));
                Assert.That(result.Rewards, Is.Empty);
                Assert.That(result.GarnetEarned, Is.Zero);
                Assert.That(combat.TryCompleteRun(RunEndReason.ManualComplete, RunSettlementPolicy.Eligible, out _), Is.False);
            }
            finally { Object.Destroy(root); Object.Destroy(targetObject); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnownedCardsDoNotAlterCombatOrSpawnAChest()
        {
#if UNITY_EDITOR
            string[] keys = { Key, Key + ".before-v6", Key + ".before-v12" };
            var previous = keys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
            Scene scene = default; HuntManager hunt = null;
            try
            {
                foreach (string key in keys) PlayerPrefs.DeleteKey(key);
                Assert.That(TraitProgressionStore.Save(25, new long[] { 25 }, Array.Empty<string>(), null, null,
                    newTraitCards: new TraitCardProgressData()), Is.True);
                scene = LoadMain(); yield return null; yield return null;
                hunt = Find<HuntManager>(scene); SetField(hunt, "playEntranceSequence", false);
                SetField(hunt, "_ignoreFocusPauseForAutomatedPlayModeTest", true);
                hunt.BeginPrototypeRun();
                var combat = Find<CombatRunController>(scene);
                Assert.That(combat.IsRunning, Is.True);
                Assert.That(Object.FindObjectsByType<MonsterCombatTarget>(FindObjectsSortMode.None)
                    .Any(t => t.RunId == combat.CurrentRunId && t.IsTraitChest), Is.False);
                float originalRange = combat.PlayerCombatStatsRuntime.AttackRangeMultiplier;
                combat.AdvanceTime(10.1f);
                Assert.That(combat.PlayerCombatStatsRuntime.AttackRangeMultiplier, Is.EqualTo(originalRange));
                var snapshot = new SpawnSnapshot("monster.trait_test", "test", 1, 1, 1, 1, 1,
                    string.Empty, 0, 0, experienceReward: 1);
                for (int i = 0; i < 10; i++)
                {
                    var obj = new GameObject("UnownedTraitVictim");
                    var target = obj.AddComponent<MonsterCombatTarget>(); target.Initialize(snapshot, combat.CurrentRunId);
                    Assert.That(combat.TryApplySkillDamage(combat.CurrentRunId, target, 100), Is.True);
                    Object.Destroy(obj);
                }
                Assert.That(combat.ExperienceEarned, Is.EqualTo(10));
                var coordinator = (RunCoordinator)Field(hunt, "_runCoordinator");
                RunResult? result = null; coordinator.Aborted += value => result = value;
                hunt.EndPrototypeRun();
                Assert.That(result.HasValue, Is.True);
                Assert.That(result.Value.GarnetEarned, Is.EqualTo(10));
                Assert.That(result.Value.ExperienceEarned, Is.EqualTo(10));
            }
            finally
            {
                if (hunt != null) hunt.ResetPrototypeRun();
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                for (int i = 0; i < keys.Length; i++) if (previous[i] == null) PlayerPrefs.DeleteKey(keys[i]); else PlayerPrefs.SetString(keys[i], previous[i]);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator BalanceRunWithPlayerProgressionUsesTraitCardsWithoutSettlingRewards()
        {
#if UNITY_EDITOR
            string[] keys = { Key, Key + ".before-v6", Key + ".before-v12" };
            var previous = keys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
            Scene scene = default; HuntManager hunt = null; TraitCardDefinition chestCard = null, range = null;
            float oldChestChance = 0f, oldRangeChance = 0f;
            try
            {
                foreach (string key in keys) PlayerPrefs.DeleteKey(key);
                Assert.That(TraitProgressionStore.Save(25, new long[] { 25 }, Array.Empty<string>(), null, null,
                    newTraitCards: new TraitCardProgressData { ownedIds = new List<string> { Owned[1], Owned[2] } }), Is.True);
                scene = LoadMain(); yield return null; yield return null;
                var progression = Find<TraitScreenController>(scene);
                chestCard = progression.GetTraitCard(Owned[1]); range = progression.GetTraitCard(Owned[2]);
                oldChestChance = chestCard.gameplay.treasureChest.chancePercent;
                oldRangeChance = range.gameplay.cursorRange.chancePercent;
                chestCard.gameplay.treasureChest.chancePercent = 100f;
                range.gameplay.cursorRange.chancePercent = 100f;
                hunt = Find<HuntManager>(scene); SetField(hunt, "playEntranceSequence", false);
                SetField(hunt, "_ignoreFocusPauseForAutomatedPlayModeTest", true);
                CombatBalanceSettings settings = hunt.CaptureCombatBalanceSettings();
                settings.UsePlayerProgression = true;
                Assert.That(hunt.TrySetCombatBalanceSettings(settings, out string error), Is.True, error);
                long initialGarnet = progression.GetResearchGemstoneBalance("gem.garnet");
                hunt.BeginPrototypeRun();
                var combat = Find<CombatRunController>(scene);
                Assert.That(hunt.IsCombatBalanceTestActive, Is.True);
                var chest = Object.FindObjectsByType<MonsterCombatTarget>(FindObjectsSortMode.None)
                    .Single(t => t.RunId == combat.CurrentRunId && t.IsTraitChest);
                float baseRadius = combat.PlayerCombatStatsRuntime.AttackRadiusWorldUnits;
                combat.AdvanceTime(range.gameplay.cursorRange.intervalSeconds);
                Assert.That(combat.PlayerCombatStatsRuntime.AttackRadiusWorldUnits,
                    Is.EqualTo(baseRadius * (1f + range.gameplay.cursorRange.rangeIncreasePercent / 100f)).Within(.0001f));
                Assert.That(combat.TryApplySkillDamage(combat.CurrentRunId, chest, chest.CurrentHealth), Is.True);
                hunt.EndPrototypeRun();
                Assert.That(progression.GetResearchGemstoneBalance("gem.garnet"), Is.EqualTo(initialGarnet));

                chestCard.gameplay.treasureChest.chancePercent = 0f;
                hunt.BeginPrototypeRun();
                Assert.That(hunt.IsCombatBalanceTestActive, Is.True);
                Assert.That(Object.FindObjectsByType<MonsterCombatTarget>(FindObjectsSortMode.None)
                    .Any(t => t.RunId == combat.CurrentRunId && t.IsTraitChest), Is.False);
            }
            finally
            {
                if (chestCard != null) chestCard.gameplay.treasureChest.chancePercent = oldChestChance;
                if (range != null) range.gameplay.cursorRange.chancePercent = oldRangeChance;
                if (hunt != null) hunt.ResetPrototypeRun();
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                for (int i = 0; i < keys.Length; i++) if (previous[i] == null) PlayerPrefs.DeleteKey(keys[i]); else PlayerPrefs.SetString(keys[i], previous[i]);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator MainCombatUsesCapturedEffectsAndSettlesChestExperienceAndRewardOnce()
        {
#if UNITY_EDITOR
            string[] keys = { Key, Key + ".before-v6", Key + ".before-v12" };
            var previous = keys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
            Scene scene = default;
            HuntManager hunt = null;
            TraitCardDefinition luck = null, chestCard = null, range = null, xp = null;
            float oldLuckChance = 0, oldLuckMultiplier = 0, oldChestChance = 0, oldInterval = 0, oldRangeChance = 0, oldDuration = 0, oldIncrease = 0, oldXp = 0;
            int oldCount = 0;
            MonsterBaseStats chestStats = null;
            FieldInfo chestExperienceField = null, chestBonusIdField = null, chestBonusAmountField = null, chestBonusChanceField = null;
            long oldChestExperience = 0;
            string oldChestBonusId = null;
            long oldChestBonusAmount = 0;
            float oldChestBonusChance = 0;
            try
            {
                foreach (string key in keys) PlayerPrefs.DeleteKey(key);
                Assert.That(TraitProgressionStore.Save(25, new long[] { 25 }, Array.Empty<string>(), null, null,
                    newTraitCards: new TraitCardProgressData { ownedIds = new List<string>(Owned) }), Is.True);
                scene = LoadMain(); yield return null; yield return null;
                var progression = Find<TraitScreenController>(scene);
                luck = progression.GetTraitCard(Owned[0]); chestCard = progression.GetTraitCard(Owned[1]);
                range = progression.GetTraitCard(Owned[2]); xp = progression.GetTraitCard(Owned[3]);
                oldLuckChance = luck.gameplay.rewardMultiplier.chancePercent; oldLuckMultiplier = luck.gameplay.rewardMultiplier.rewardMultiplier;
                oldChestChance = chestCard.gameplay.treasureChest.chancePercent; oldCount = chestCard.gameplay.treasureChest.spawnCount;
                chestStats = chestCard.gameplay.treasureChest.monsterDefinition.BaseStats;
                chestExperienceField = typeof(MonsterBaseStats).GetField("experienceReward", BindingFlags.Instance | BindingFlags.NonPublic);
                oldChestExperience = (long)chestExperienceField.GetValue(chestStats);
                chestExperienceField.SetValue(chestStats, 7L);
                chestBonusIdField = typeof(MonsterBaseStats).GetField("bonusDropCurrencyId", BindingFlags.Instance | BindingFlags.NonPublic);
                chestBonusAmountField = typeof(MonsterBaseStats).GetField("bonusDropAmount", BindingFlags.Instance | BindingFlags.NonPublic);
                chestBonusChanceField = typeof(MonsterBaseStats).GetField("bonusDropChancePercent", BindingFlags.Instance | BindingFlags.NonPublic);
                oldChestBonusId = (string)chestBonusIdField.GetValue(chestStats);
                oldChestBonusAmount = (long)chestBonusAmountField.GetValue(chestStats);
                oldChestBonusChance = (float)chestBonusChanceField.GetValue(chestStats);
                chestBonusIdField.SetValue(chestStats, "gem.dragon");
                chestBonusAmountField.SetValue(chestStats, 3L);
                chestBonusChanceField.SetValue(chestStats, 100f);
                oldInterval = range.gameplay.cursorRange.intervalSeconds; oldRangeChance = range.gameplay.cursorRange.chancePercent;
                oldDuration = range.gameplay.cursorRange.durationSeconds; oldIncrease = range.gameplay.cursorRange.rangeIncreasePercent;
                oldXp = xp.gameplay.normalMonsterExperience.bonusPercent;
                luck.gameplay.rewardMultiplier.chancePercent = 100; luck.gameplay.rewardMultiplier.rewardMultiplier = 5;
                chestCard.gameplay.treasureChest.chancePercent = 100; chestCard.gameplay.treasureChest.spawnCount = 1;
                range.gameplay.cursorRange.intervalSeconds = 10; range.gameplay.cursorRange.chancePercent = 100;
                range.gameplay.cursorRange.durationSeconds = 3; range.gameplay.cursorRange.rangeIncreasePercent = 50;
                xp.gameplay.normalMonsterExperience.bonusPercent = 10;
                hunt = Find<HuntManager>(scene); SetField(hunt, "playEntranceSequence", false);
                SetField(hunt, "_ignoreFocusPauseForAutomatedPlayModeTest", true);
                hunt.BeginPrototypeRun();
                var combat = Find<CombatRunController>(scene);
                Assert.That(combat.IsRunning, Is.True);
                var coordinator = (RunCoordinator)Field(hunt, "_runCoordinator");
                var cursor = Find<MainCursorController>(scene);
                var stats = combat.PlayerCombatStatsRuntime;
                float baseRange = stats.AttackRangeMultiplier;
                var chest = Object.FindObjectsByType<MonsterCombatTarget>(FindObjectsSortMode.None)
                    .Single(t => t.RunId == combat.CurrentRunId && t.IsTraitChest);
                Assert.That(chest.MonsterId, Is.EqualTo(chestCard.gameplay.treasureChest.monsterDefinition.MonsterId));
                Assert.That(chest.GarnetReward, Is.EqualTo(chestCard.gameplay.treasureChest.monsterDefinition.GarnetReward));
                var spawner = Find<MonsterSpawner>(scene);
                Assert.That(spawner.CurrentSpawnPlan.Entries.Any(entry => entry.Snapshot.BonusDropCurrencyId == "gem.dragon"), Is.False);
                var gemstoneList = Find<CombatGemstoneList>(scene);
                Assert.That(gemstoneList.GetComponentsInChildren<GemstoneIconView>()
                    .Any(icon => icon.GemstoneId == "gem.dragon"), Is.True,
                    "A chest-only drop must be visible in the combat HUD before it is obtained.");

                // Changes made after starting must not change this run's copied values.
                luck.gameplay.rewardMultiplier.rewardMultiplier = 1;
                xp.gameplay.normalMonsterExperience.bonusPercent = 0;
                range.gameplay.cursorRange.chancePercent = 0;
                combat.AdvanceTime(10f);
                Assert.That(stats.AttackRangeMultiplier, Is.EqualTo(baseRange * 1.5f).Within(.0001f));
                yield return null;
                Assert.That(cursor.RangeMultiplier, Is.EqualTo(stats.AttackRangeMultiplier).Within(.0001f));
                Assert.That(coordinator.Pause(), Is.True);
                float pausedAt = combat.ElapsedSeconds;
                combat.AdvanceTime(10f);
                Assert.That(combat.ElapsedSeconds, Is.EqualTo(pausedAt));
                Assert.That(stats.AttackRangeMultiplier, Is.EqualTo(baseRange * 1.5f).Within(.0001f));
                Assert.That(coordinator.Resume(), Is.True);
                combat.AdvanceTime(3.1f);
                Assert.That(stats.AttackRangeMultiplier, Is.EqualTo(baseRange).Within(.0001f));

                var runId = combat.CurrentRunId;
                Assert.That(combat.TryApplySkillDamage(runId, chest, chest.CurrentHealth), Is.True);
                long chestGarnet = chest.GarnetReward;
                Assert.That(combat.DefeatedCount, Is.Zero, "The chest is not a monster kill.");
                Assert.That(combat.GarnetEarned, Is.EqualTo(chestGarnet));
                Assert.That(combat.GetResourceEarned("gem.dragon"), Is.EqualTo(3));
                Assert.That(combat.ExperienceEarned, Is.EqualTo(7), "The chest uses its own monster SO experience without the normal-monster bonus.");
                Assert.That(combat.TryApplySkillDamage(runId, chest, 10), Is.False);
                Assert.That(combat.GarnetEarned, Is.EqualTo(chestGarnet));

                var snapshot = new SpawnSnapshot("monster.trait_test", "test", 1, 1, 1, 1, 1,
                    string.Empty, 0, 0, experienceReward: 1);
                for (int i = 0; i < 10; i++)
                {
                    var obj = new GameObject("TraitXpVictim");
                    var target = obj.AddComponent<MonsterCombatTarget>(); target.Initialize(snapshot, runId);
                    Assert.That(combat.TryApplySkillDamage(runId, target, 100), Is.True);
                    Object.Destroy(obj);
                }
                Assert.That(combat.DefeatedCount, Is.EqualTo(10));
                Assert.That(combat.ExperienceEarned, Is.EqualTo(18), "Fractional 10% bonuses accumulate across normal kills.");
                long before = progression.GetResearchGemstoneBalance("gem.garnet");
                long dragonBefore = progression.GetResearchGemstoneBalance("gem.dragon");
                RunResult? result = null;
                coordinator.Aborted += value => result = value;
                hunt.EndPrototypeRun();
                Assert.That(result.HasValue, Is.True);
                Assert.That(result.Value.SettlementPolicy, Is.EqualTo(RunSettlementPolicy.Eligible));
                Assert.That(result.Value.GarnetEarned, Is.EqualTo((chestGarnet + 10) * 5));
                Assert.That(result.Value.Rewards.Single(value => value.CurrencyId == "gem.dragon").Amount, Is.EqualTo(15));
                Assert.That(result.Value.ExperienceEarned, Is.EqualTo(18));
                Assert.That(progression.ExperiencePoints, Is.EqualTo(18));
                Assert.That(progression.GetResearchGemstoneBalance("gem.garnet"), Is.EqualTo(before + result.Value.GarnetEarned));
                Assert.That(progression.GetResearchGemstoneBalance("gem.dragon"), Is.EqualTo(dragonBefore + 15));
                Assert.That(progression.GrantSettledRun(result.Value), Is.True);
                Assert.That(progression.GetResearchGemstoneBalance("gem.garnet"), Is.EqualTo(before + result.Value.GarnetEarned));
                Assert.That(progression.GetResearchGemstoneBalance("gem.dragon"), Is.EqualTo(dragonBefore + 15));
            }
            finally
            {
                if (luck != null) { luck.gameplay.rewardMultiplier.chancePercent = oldLuckChance; luck.gameplay.rewardMultiplier.rewardMultiplier = oldLuckMultiplier; }
                if (chestCard != null) { chestCard.gameplay.treasureChest.chancePercent = oldChestChance; chestCard.gameplay.treasureChest.spawnCount = oldCount; }
                if (range != null) { range.gameplay.cursorRange.intervalSeconds = oldInterval; range.gameplay.cursorRange.chancePercent = oldRangeChance; range.gameplay.cursorRange.durationSeconds = oldDuration; range.gameplay.cursorRange.rangeIncreasePercent = oldIncrease; }
                if (xp != null) xp.gameplay.normalMonsterExperience.bonusPercent = oldXp;
                if (chestStats != null && chestExperienceField != null) chestExperienceField.SetValue(chestStats, oldChestExperience);
                if (chestStats != null && chestBonusIdField != null) chestBonusIdField.SetValue(chestStats, oldChestBonusId);
                if (chestStats != null && chestBonusAmountField != null) chestBonusAmountField.SetValue(chestStats, oldChestBonusAmount);
                if (chestStats != null && chestBonusChanceField != null) chestBonusChanceField.SetValue(chestStats, oldChestBonusChance);
                if (hunt != null) hunt.ResetPrototypeRun();
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                for (int i = 0; i < keys.Length; i++) if (previous[i] == null) PlayerPrefs.DeleteKey(keys[i]); else PlayerPrefs.SetString(keys[i], previous[i]);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator ChestInBossRunNeverCountsAsBossAndBossExperienceIsNotBoosted()
        {
#if UNITY_EDITOR
            string[] keys = { Key, Key + ".before-v6", Key + ".before-v12" };
            var previous = keys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
            Scene scene = default; HuntManager hunt = null; TraitCardDefinition chestCard = null;
            float originalChance = 0; int originalCount = 0;
            try
            {
                foreach (string key in keys) PlayerPrefs.DeleteKey(key);
                Assert.That(TraitProgressionStore.Save(25, new long[] { 25 }, Array.Empty<string>(), null, null,
                    newTraitCards: new TraitCardProgressData { ownedIds = new List<string> { Owned[1], Owned[3] } }), Is.True);
                scene = LoadMain(); yield return null; yield return null;
                var progression = Find<TraitScreenController>(scene);
                chestCard = progression.GetTraitCard(Owned[1]); originalChance = chestCard.gameplay.treasureChest.chancePercent;
                originalCount = chestCard.gameplay.treasureChest.spawnCount;
                chestCard.gameplay.treasureChest.chancePercent = 100; chestCard.gameplay.treasureChest.spawnCount = 1;
                hunt = Find<HuntManager>(scene); SetField(hunt, "playEntranceSequence", false);
                SetField(hunt, "_ignoreFocusPauseForAutomatedPlayModeTest", true);
                hunt.BeginBossStage(1);
                var combat = Find<CombatRunController>(scene); var spawner = Find<MonsterSpawner>(scene);
                Assert.That(combat.IsRunning && combat.IsBossRun, Is.True);
                var coordinator = (RunCoordinator)Field(hunt, "_runCoordinator");
                RunResult? result = null; coordinator.Completed += value => result = value;
                var boss = spawner.BossTarget;
                var chest = Object.FindObjectsByType<MonsterCombatTarget>(FindObjectsSortMode.None)
                    .Single(t => t.RunId == combat.CurrentRunId && t.IsTraitChest);
                Assert.That(boss, Is.Not.Null); Assert.That(boss.IsTraitChest, Is.False);
                long chestReward = chest.GarnetReward;
                Assert.That(combat.TryApplySkillDamage(combat.CurrentRunId, chest, chest.CurrentHealth), Is.True);
                Assert.That(combat.IsRunning, Is.True, "Opening the chest must not finish the boss run.");
                Assert.That(combat.DefeatedCount, Is.Zero);
                Assert.That(combat.GarnetEarned, Is.EqualTo(chestReward));
                long bossExperience = ((RunRequest)Field(combat, "_runRequest")).BossExperienceReward;
                Assert.That(combat.TryApplySkillDamage(combat.CurrentRunId, boss, boss.CurrentHealth), Is.True);
                Assert.That(result.HasValue, Is.True);
                Assert.That(result.Value.BossDefeated, Is.True);
                Assert.That(result.Value.DefeatedCount, Is.EqualTo(1));
                Assert.That(result.Value.ExperienceEarned, Is.EqualTo(bossExperience));
                Assert.That(result.Value.GarnetEarned, Is.GreaterThanOrEqualTo(chestReward));
            }
            finally
            {
                if (chestCard != null) { chestCard.gameplay.treasureChest.chancePercent = originalChance; chestCard.gameplay.treasureChest.spawnCount = originalCount; }
                if (hunt != null) hunt.ResetPrototypeRun();
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                for (int i = 0; i < keys.Length; i++) if (previous[i] == null) PlayerPrefs.DeleteKey(keys[i]); else PlayerPrefs.SetString(keys[i], previous[i]);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }

#if UNITY_EDITOR
        private static Scene LoadMain() => UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private static object Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        private static void SetField(object value, string name, object data) => value.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, data);
#endif
    }
}
