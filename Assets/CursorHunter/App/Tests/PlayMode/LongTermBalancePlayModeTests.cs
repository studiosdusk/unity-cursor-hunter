using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Reflection;
using CursorHunter.App;
using CursorHunter.Combat;
using CursorHunter.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CursorHunter.App.Tests
{
    /// <summary>
    /// Short accelerated Main-scene samples used to ground long-term economy
    /// estimates in real spawn, cursor, pet, skill, and reward throughput.
    /// </summary>
    public sealed class LongTermBalancePlayModeTests
    {
        private const string ProfileKey = "cursor_hunter.progression.v1";
        private readonly Dictionary<string, string> _savedStrings = new Dictionary<string, string>();
        private readonly Dictionary<string, int?> _savedFlags = new Dictionary<string, int?>();
        private Scene _scene;
        private AppUiRootController _app;
        private TraitScreenController _trait;
        private float _timeScale;
        private float _listenerVolume;

        [UnitySetUp]
        public IEnumerator LoadMain()
        {
            if (Application.isBatchMode)
                Assert.Ignore("This live-rate sample requires a focused Unity Editor because HuntManager pauses unfocused runs.");
            _timeScale = Time.timeScale;
            _listenerVolume = AudioListener.volume;
            AudioListener.volume = 0f;
            foreach (string key in new[] { ProfileKey, ProfileKey + ".before-v5" })
            {
                _savedStrings[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
                PlayerPrefs.DeleteKey(key);
            }
            foreach (string key in new[]
                { ProfileKey + ".starter-tutorial-complete", ProfileKey + ".starter-tutorial-started" })
            {
                _savedFlags[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
                PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
#if UNITY_EDITOR
            _scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            yield return null;
            _app = Find<AppUiRootController>();
            _trait = Find<TraitScreenController>();
            Assert.That(_app, Is.Not.Null);
            Assert.That(_trait, Is.Not.Null);
            TraitProgressionStore.CompleteStarterTutorial();
            _app.ShowTrait();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreMain()
        {
            Time.timeScale = _timeScale;
            if (_scene.IsValid() && _scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_scene);
            foreach (var entry in _savedStrings)
            {
                if (entry.Value == null) PlayerPrefs.DeleteKey(entry.Key);
                else PlayerPrefs.SetString(entry.Key, entry.Value);
            }
            foreach (var entry in _savedFlags)
            {
                if (!entry.Value.HasValue) PlayerPrefs.DeleteKey(entry.Key);
                else PlayerPrefs.SetInt(entry.Key, entry.Value.Value);
            }
            PlayerPrefs.Save();
            AudioListener.volume = _listenerVolume;
        }

        [UnityTest]
        public IEnumerator MainSceneMeasuresTierByTierKillsAndGemIncomeUnderAnActiveCursorSweep()
        {
            const float acceleratedTimeScale = 4f;
            Time.timeScale = acceleratedTimeScale;
            var hunt = Find<HuntManager>();
            SetField(hunt, "_ignoreFocusPauseForAutomatedPlayModeTest", true);
            var combat = Find<CombatRunController>();
            var cursor = Find<MainCursorController>();
            var catalog = Get<TraitCatalog>(_trait, "_activeCatalog");
            var purchaseState = Get<HashSet<string>>(_trait, "_purchased");
            var allNodes = catalog.StatCategories.Concat(catalog.SkillCategories)
                .Concat(catalog.MonsterCategories).SelectMany(category => category.Nodes)
                .Where(node => node != null).ToArray();
            var gemstoneBalances = Enumerable.Repeat(100000000L, 6).ToArray();
            var tierLines = new List<string>();

            for (int tier = 0; tier <= 5; tier++)
            {
                _trait.SetProgressionSnapshot(100000000L, gemstoneBalances, tier);
                purchaseState.Clear();
                var eligible = new HashSet<string>(allNodes
                    .Where(node => node.RequiredBossTier <= tier)
                    .Select(node => node.Id));
                bool changed;
                do
                {
                    changed = false;
                    foreach (TraitNodeDefinition node in allNodes)
                    {
                        if (!eligible.Contains(node.Id) || purchaseState.Contains(node.Id)) continue;
                        if (!string.IsNullOrEmpty(node.PrerequisiteNodeId) &&
                            !purchaseState.Contains(node.PrerequisiteNodeId)) continue;
                        purchaseState.Add(node.Id);
                        changed = true;
                    }
                } while (changed);
                Assert.That(purchaseState.Contains("stat.attack.01"), Is.True);
                Assert.That(purchaseState.Contains("monster.01"), Is.True);
                Call(_trait, "RefreshAll");

                if (_app.CurrentScreen != UiScreenId.Trait) _app.ShowTrait();
                _app.CloseTrait();
                hunt.BeginPrototypeRun();
                Assert.That(combat.IsRunActive, Is.True, "Tier " + tier + " must start from its valid progression snapshot.");
                var coordinator = Get<RunCoordinator>(hunt, "_runCoordinator");
                var cursorTransform = cursor.CursorTransform;
                Assert.That(cursorTransform, Is.Not.Null);
                Collider2D hitBox = cursorTransform.GetComponent<Collider2D>();
                Assert.That(hitBox, Is.Not.Null);

                float sampleStart = Time.realtimeSinceStartup;
                while (combat.IsRunActive)
                {
                    if (combat.IsPaused) coordinator.Resume();
                    MonsterCombatTarget[] active = UnityEngine.Object.FindObjectsByType<MonsterCombatTarget>(
                            FindObjectsSortMode.None)
                        .Where(target => target != null && target.IsActive &&
                            target.RunId == combat.CurrentRunId && target.IsVulnerable)
                        .ToArray();
                    if (active.Length > 0)
                    {
                        MonsterCombatTarget target = active
                            .OrderBy(value => (value.transform.position - cursorTransform.position).sqrMagnitude)
                            .First();
                        Vector3 next = Vector3.MoveTowards(cursorTransform.position,
                            target.transform.position, 8f * Time.deltaTime);
                        cursorTransform.position = next;
                        Physics2D.SyncTransforms();
                        combat.TryAttack(hitBox, true);
                    }

                    if (Time.realtimeSinceStartup - sampleStart > 60f)
                        Assert.Fail("Tier " + tier + " sample exceeded its realtime watchdog.");
                    yield return null;
                }

                float duration = combat.DurationSeconds;
                int defeated = combat.DefeatedCount;
                long garnet = combat.GetResourceEarned("gem.garnet");
                long topaz = combat.GetResourceEarned("gem.topaz");
                long amethyst = combat.GetResourceEarned("gem.amethyst");
                long sapphire = combat.GetResourceEarned("gem.sapphire");
                long diamond = combat.GetResourceEarned("gem.diamond");
                long dragon = combat.GetResourceEarned("gem.dragon");
                string line = "tier=" + tier + " duration=" + duration.ToString("0") +
                    "s kills=" + defeated + " killsPerMinute=" +
                    (defeated * 60f / duration).ToString("0.0") +
                    " gems[g=" + garnet + ",t=" + topaz + ",a=" + amethyst +
                    ",s=" + sapphire + ",d=" + diamond + ",dr=" + dragon + "]";
                tierLines.Add(line);
                Debug.Log("LONG_TERM_BALANCE " + line);
                hunt.ReturnToMainMenu();
                yield return null;
            }

            TestContext.WriteLine("LONG_TERM_BALANCE samples at 4x game speed; cursor follows nearest visible target at 8 world units/s:");
            foreach (string line in tierLines) TestContext.WriteLine(line);
            Assert.That(tierLines, Has.Count.EqualTo(6));
        }

        [UnityTest]
        [Explicit("Long-running fresh-profile balance benchmark; run manually from the PlayMode Test Runner.")]
        [Timeout(1200000)]
        public IEnumerator FreshProfileMeasuresSpendableResearchAt110NonBossMinutes()
        {
            const float acceleratedTimeScale = 8f;
            const float nonBossFieldBudgetSeconds = 110f * 60f;
            const int maximumFieldRuns = 600;
            // Five measured pre-boss chapters. The starting research state is
            // boss tier 0; completing it prepares Boss 1. Tier 4 prepares Boss 5.
            float[] targetStageMinutes = { 5f, 16f, 20f, 27f, 42f };
            Time.timeScale = acceleratedTimeScale;

            var hunt = Find<HuntManager>();
            SetField(hunt, "_ignoreFocusPauseForAutomatedPlayModeTest", true);
            var combat = Find<CombatRunController>();
            var cursor = Find<MainCursorController>();
            var catalog = Get<TraitCatalog>(_trait, "_activeCatalog");
            var purchaseState = Get<HashSet<string>>(_trait, "_purchased");
            TraitNodeDefinition[] allNodes = catalog.StatCategories.Concat(catalog.SkillCategories)
                .Concat(catalog.MonsterCategories).SelectMany(category => category.Nodes)
                .Where(node => node != null && !node.AcquisitionOnly).ToArray();
            var nodesById = allNodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
            var gemstoneIds = catalog.Gemstones.Select(gemstone => gemstone.Id).ToArray();
            var totalEarned = new long[gemstoneIds.Length];
            var totalSpent = new long[gemstoneIds.Length];
            var initialBalances = Get<long[]>(_trait, "_gemstoneBalances").ToArray();
            var stageReports = new List<string>();
            var stageDurations = new List<float>();
            var cursorTransform = cursor.CursorTransform;
            Collider2D hitBox = cursorTransform.GetComponent<Collider2D>();
            MethodInfo getPurchaseState = FindMethod(_trait, "GetNodePurchaseState");
            MethodInfo tryUpgrade = FindMethod(_trait, "TryUpgradeNode");

            Assert.That(Get<bool>(_trait, "testModeFreeUpgrades"), Is.False,
                "Research must spend the actual saved currency balance.");
            Assert.That(Get<bool>(_trait, "testModeUnlockAll"), Is.False,
                "Boss and prerequisite gates must remain active.");
            Assert.That(cursorTransform, Is.Not.Null);
            Assert.That(hitBox, Is.Not.Null);
            Assert.That(purchaseState, Is.Empty, "The profile must start with no researched nodes.");

            float totalFieldSeconds = 0f;
            int totalRuns = 0;
            int totalKills = 0;
            bool reachedFieldBudget = false;

            for (int tier = 0; tier < targetStageMinutes.Length; tier++)
            {
                if (totalFieldSeconds >= nonBossFieldBudgetSeconds) break;

                if (tier > 0)
                {
                    // Use the same right-side test-tool button event the user will click.
                    SetBossTierFromTraitTestTool(_trait, tier);
                    Assert.That(Get<int>(_trait, "unlockedBossTier"), Is.EqualTo(tier));
                }

                TraitNodeDefinition[] tierNodes = allNodes
                    .Where(node => GetEffectiveBossGate(node, nodesById) <= tier)
                    .ToArray();
                int stageRunCount = 0;
                int stagePurchaseCount = 0;
                int stageKills = 0;
                float stageFieldSeconds = 0f;
                long[] stageStartBalances = Get<long[]>(_trait, "_gemstoneBalances").ToArray();
                int stalledRuns = 0;

                while (true)
                {
                    bool boughtSomething;
                    do
                    {
                        boughtSomething = false;
                        foreach (TraitNodeDefinition node in tierNodes
                            .Where(candidate => !purchaseState.Contains(candidate.Id) &&
                                HasSpendableResearchCurrencySource(candidate, gemstoneIds))
                            .OrderBy(candidate => candidate.Cost)
                            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal))
                        {
                            object state = getPurchaseState.Invoke(_trait, new object[] { node });
                            if (!string.Equals(state.ToString(), "Available", StringComparison.Ordinal))
                                continue;

                            int currencyIndex = Array.IndexOf(gemstoneIds, node.CostGemstoneId);
                            Assert.That(currencyIndex, Is.GreaterThanOrEqualTo(0),
                                "Missing currency for node " + node.Id);
                            long balanceBefore = Get<long[]>(_trait, "_gemstoneBalances")[currencyIndex];
                            tryUpgrade.Invoke(_trait, new object[] { node, true });
                            Assert.That(purchaseState.Contains(node.Id), Is.True,
                                "The actual upgrade transaction should purchase " + node.Id);
                            Assert.That(Get<long[]>(_trait, "_gemstoneBalances")[currencyIndex],
                                Is.EqualTo(balanceBefore - node.Cost),
                                "The purchase must deduct its authored cost for " + node.Id);
                            totalSpent[currencyIndex] += node.Cost;
                            stagePurchaseCount++;
                            boughtSomething = true;
                        }
                    } while (boughtSomething && tierNodes.Any(node =>
                        !purchaseState.Contains(node.Id) &&
                        HasSpendableResearchCurrencySource(node, gemstoneIds)));

                    int pendingSpendableNodes = tierNodes.Count(node =>
                        !purchaseState.Contains(node.Id) &&
                        HasSpendableResearchCurrencySource(node, gemstoneIds));
                    if (pendingSpendableNodes == 0) break;
                    if (totalFieldSeconds >= nonBossFieldBudgetSeconds)
                    {
                        reachedFieldBudget = true;
                        break;
                    }

                    if (totalRuns >= maximumFieldRuns)
                    {
                        Assert.Fail("Fresh-profile progression exceeded " + maximumFieldRuns +
                            " field runs at tier " + tier + ". Pending: " + string.Join(", ",
                                tierNodes.Where(node => !purchaseState.Contains(node.Id) &&
                                        HasSpendableResearchCurrencySource(node, gemstoneIds))
                                    .Select(node => node.Id)));
                    }

                    long[] walletBeforeRun = Get<long[]>(_trait, "_gemstoneBalances").ToArray();
                    if (_app.CurrentScreen != UiScreenId.Trait) _app.ShowTrait();
                    _app.CloseTrait();
                    Time.timeScale = acceleratedTimeScale;
                    hunt.BeginPrototypeRun();
                    Assert.That(combat.IsRunActive, Is.True,
                        "Tier " + tier + " must start a normal field run from its actual progression snapshot.");
                    var coordinator = Get<RunCoordinator>(hunt, "_runCoordinator");
                    float sampleStart = Time.realtimeSinceStartup;
                    while (combat.IsRunActive)
                    {
                        // Main's focus policy can pause a live run while the Editor
                        // loses focus. This is a controlled accelerated benchmark,
                        // so keep the run clock active while the test owns it.
                        Time.timeScale = acceleratedTimeScale;
                        if (combat.IsPaused) coordinator.Resume();
                        MonsterCombatTarget[] active = UnityEngine.Object.FindObjectsByType<MonsterCombatTarget>(
                                FindObjectsSortMode.None)
                            .Where(target => target != null && target.IsActive &&
                                target.RunId == combat.CurrentRunId && target.IsVulnerable)
                            .ToArray();
                        if (active.Length > 0)
                        {
                            MonsterCombatTarget target = active
                                .OrderBy(value => (value.transform.position - cursorTransform.position).sqrMagnitude)
                                .First();
                            cursorTransform.position = Vector3.MoveTowards(cursorTransform.position,
                                target.transform.position, 8f * Time.deltaTime);
                            Physics2D.SyncTransforms();
                            combat.TryAttack(hitBox, true);
                        }

                        if (Time.realtimeSinceStartup - sampleStart > 45f)
                            Assert.Fail("Tier " + tier + " campaign run exceeded its realtime watchdog. elapsed=" +
                                combat.ElapsedSeconds.ToString("0.0") + "/" + combat.DurationSeconds.ToString("0.0") +
                                "s paused=" + combat.IsPaused + " timeScale=" + Time.timeScale +
                                " coordinator=" + coordinator.State);
                        yield return null;
                    }

                    float duration = combat.DurationSeconds;
                    int defeated = combat.DefeatedCount;
                    for (int i = 0; i < gemstoneIds.Length; i++)
                        totalEarned[i] += combat.GetResourceEarned(gemstoneIds[i]);

                    totalFieldSeconds += duration;
                    stageFieldSeconds += duration;
                    totalKills += defeated;
                    stageKills += defeated;
                    stageRunCount++;
                    totalRuns++;

                    if (stageRunCount % 5 == 0)
                    {
                        long[] progressBalances = Get<long[]>(_trait, "_gemstoneBalances");
                        int progressSpendableNodes = tierNodes.Count(node =>
                            !purchaseState.Contains(node.Id) &&
                            HasSpendableResearchCurrencySource(node, gemstoneIds));
                        int deferredNodes = tierNodes.Count(node =>
                            !purchaseState.Contains(node.Id) &&
                            !HasSpendableResearchCurrencySource(node, gemstoneIds));
                        Debug.Log("LONG_TERM_CAMPAIGN_PROGRESS tier=" + tier +
                            " fieldRuns=" + stageRunCount + " fieldMinutes=" +
                            (stageFieldSeconds / 60f).ToString("0.0") + " spendableRemaining=" +
                            progressSpendableNodes + " sourceDeferred=" + deferredNodes +
                            " wallet[" + string.Join(",", gemstoneIds.Select((id, i) =>
                                id.Substring("gem.".Length) + "=" + progressBalances[i])) + "]");
                    }

                    long[] walletAfterRun = Get<long[]>(_trait, "_gemstoneBalances").ToArray();
                    bool balanceChanged = false;
                    for (int i = 0; i < walletAfterRun.Length; i++)
                    {
                        if (walletAfterRun[i] != walletBeforeRun[i]) balanceChanged = true;
                    }
                    stalledRuns = balanceChanged ? 0 : stalledRuns + 1;
                    Assert.That(stalledRuns, Is.LessThan(4),
                        "Three completed field runs in a row yielded no spendable resources at tier " + tier + ".");

                    hunt.ReturnToMainMenu();
                    _app.ShowTrait();
                    yield return null;
                }

                long[] stageEndBalances = Get<long[]>(_trait, "_gemstoneBalances").ToArray();
                string stageLine = "tier=" + tier + " fieldRuns=" + stageRunCount +
                    " fieldMinutes=" + (stageFieldSeconds / 60f).ToString("0.0") +
                    " targetMinutes=" + targetStageMinutes[tier].ToString("0") +
                    " kills=" + stageKills + " purchases=" + stagePurchaseCount +
                    " spendableRemaining=" + tierNodes.Count(node =>
                        !purchaseState.Contains(node.Id) &&
                        HasSpendableResearchCurrencySource(node, gemstoneIds)) +
                    " sourceDeferred=" + tierNodes.Count(node =>
                        !purchaseState.Contains(node.Id) &&
                        !HasSpendableResearchCurrencySource(node, gemstoneIds)) +
                    " pendingIds=[" + string.Join(",", tierNodes.Where(node =>
                        !purchaseState.Contains(node.Id) &&
                        HasSpendableResearchCurrencySource(node, gemstoneIds))
                        .Select(node => node.Id)) + "]" +
                    " deferredIds=[" + string.Join(",", tierNodes.Where(node =>
                        !purchaseState.Contains(node.Id) &&
                        !HasSpendableResearchCurrencySource(node, gemstoneIds))
                        .Select(node => node.Id)) + "]" +
                    " wallet[" + string.Join(",", gemstoneIds.Select((id, i) =>
                        id.Substring("gem.".Length) + "=" + stageEndBalances[i].ToString() +
                        "(" + (stageEndBalances[i] - stageStartBalances[i]).ToString("+0;-0;0") + ")")) + "]";
                stageReports.Add(stageLine);
                stageDurations.Add(stageFieldSeconds / 60f);
                Debug.Log("LONG_TERM_CAMPAIGN " + stageLine);
                int remainingSpendableNodes = tierNodes.Count(node =>
                    !purchaseState.Contains(node.Id) &&
                    HasSpendableResearchCurrencySource(node, gemstoneIds));
                int remainingSourceDeferredNodes = tierNodes.Count(node =>
                    !purchaseState.Contains(node.Id) &&
                    !HasSpendableResearchCurrencySource(node, gemstoneIds));
                if (reachedFieldBudget || totalFieldSeconds >= nonBossFieldBudgetSeconds)
                {
                    reachedFieldBudget = true;
                    break;
                }

                Assert.That(remainingSourceDeferredNodes, Is.EqualTo(0),
                    "Every node in pre-boss tier " + tier + " must have an available resource source.");
                Assert.That(remainingSpendableNodes, Is.EqualTo(0),
                    "Only source-available research must be complete before advancing the test boss tier.");
            }

            long[] finalBalances = Get<long[]>(_trait, "_gemstoneBalances").ToArray();
            int lastBossPreparationTier = targetStageMinutes.Length - 1;
            TraitNodeDefinition[] campaignNodes = allNodes.Where(node =>
                GetEffectiveBossGate(node, nodesById) <= lastBossPreparationTier).ToArray();
            int remainingResearch = campaignNodes.Count(node => !purchaseState.Contains(node.Id));
            int remainingResearchWithSource = campaignNodes.Count(node =>
                !purchaseState.Contains(node.Id) &&
                HasSpendableResearchCurrencySource(node, gemstoneIds));
            if (totalFieldSeconds >= nonBossFieldBudgetSeconds)
                reachedFieldBudget = true;
            TestContext.WriteLine("LONG_TERM_CAMPAIGN started from a fresh profile and used authored research costs and actual field settlement. Boss fights were not run; the 40-minute boss allowance is excluded. Five measured chapters use unlocked boss tiers 0-4, preparing bosses 1-5; tier advancement uses the Trait right-side test tool after current source-available research is bought. Gemstone exchange was not used. Field timer accelerated at 8x; purchases and menu time are excluded. The game cursor is test-driven toward the nearest vulnerable target at 8 world units per field second; this does not move the operating-system pointer or model human target choice.");
            foreach (string line in stageReports) TestContext.WriteLine(line);
            TestContext.WriteLine("total field minutes=" + (totalFieldSeconds / 60f).ToString("0.0") +
                " runs=" + totalRuns + " kills=" + totalKills +
                " remainingResearch=" + remainingResearch +
                " remainingWithAvailableSource=" + remainingResearchWithSource);
            TestContext.WriteLine("target chapter minutes=" + string.Join(",", targetStageMinutes.Select(value => value.ToString("0"))) +
                " measured chapter minutes=" + string.Join(",", stageDurations.Select(value => value.ToString("0.0"))));
            TestContext.WriteLine("resources earned=" + string.Join(",", gemstoneIds.Select((id, i) =>
                id.Substring("gem.".Length) + "=" + totalEarned[i])));
            TestContext.WriteLine("resources spent=" + string.Join(",", gemstoneIds.Select((id, i) =>
                id.Substring("gem.".Length) + "=" + totalSpent[i])));
            TestContext.WriteLine("final wallet=" + string.Join(",", gemstoneIds.Select((id, i) =>
                id.Substring("gem.".Length) + "=" + finalBalances[i])));

            Assert.That(totalFieldSeconds, Is.InRange(108f * 60f,
                nonBossFieldBudgetSeconds + 30f),
                "All research should finish near the 110-minute non-boss field-time target; full field runs can overshoot by at most 30 seconds.");
            Assert.That(stageDurations, Has.Count.EqualTo(targetStageMinutes.Length),
                "All five pre-boss chapters must be measured before the 110-minute budget ends.");
            Assert.That(remainingResearch, Is.Zero,
                "All research available through boss tier 4 must be completed before entering the five boss fights.");
            Assert.That(remainingResearchWithSource, Is.Zero,
                "No source-available research may remain at the 110-minute budget.");
            for (int index = 1; index < stageDurations.Count; index++)
                Assert.That(stageDurations[index], Is.GreaterThan(stageDurations[index - 1]),
                    "Each later boss preparation chapter should take longer than the previous chapter.");
            float[] chapterToleranceMinutes = { 1.5f, 2f, 3f, 4f, 5f };
            for (int index = 0; index < targetStageMinutes.Length; index++)
                Assert.That(stageDurations[index], Is.InRange(
                        targetStageMinutes[index] - chapterToleranceMinutes[index],
                        targetStageMinutes[index] + chapterToleranceMinutes[index]),
                    "Boss preparation tier " + index + " should stay near its planned " +
                    targetStageMinutes[index].ToString("0") + "-minute chapter.");
            Assert.That(initialBalances.Length, Is.EqualTo(finalBalances.Length));
        }

        private bool HasSpendableResearchCurrencySource(
            TraitNodeDefinition node,
            IReadOnlyList<string> gemstoneIds)
        {
            if (node == null || node.Cost <= 0) return true;
            int currencyIndex = -1;
            for (int i = 0; i < gemstoneIds.Count; i++)
            {
                if (string.Equals(gemstoneIds[i], node.CostGemstoneId, StringComparison.Ordinal))
                {
                    currencyIndex = i;
                    break;
                }
            }
            if (currencyIndex < 0) return false;

            long[] balances = Get<long[]>(_trait, "_gemstoneBalances");
            return balances[currencyIndex] > 0 || _trait.IsGemstoneAvailable(currencyIndex);
        }

        [UnityTest]
        public IEnumerator TierFiveMeasuresDragonIncomeAsMonsterProductionIsUpgraded()
        {
            Time.timeScale = 4f;
            var hunt = Find<HuntManager>();
            var combat = Find<CombatRunController>();
            var cursor = Find<MainCursorController>();
            var catalog = Get<TraitCatalog>(_trait, "_activeCatalog");
            var purchaseState = Get<HashSet<string>>(_trait, "_purchased");
            var allNodes = catalog.StatCategories.Concat(catalog.SkillCategories)
                .Concat(catalog.MonsterCategories).SelectMany(category => category.Nodes)
                .Where(node => node != null).ToArray();
            var gemstoneBalances = Enumerable.Repeat(100000000L, 6).ToArray();
            var tierLines = new List<string>();
            var cursorTransform = cursor.CursorTransform;
            Collider2D hitBox = cursorTransform.GetComponent<Collider2D>();

            for (int productionLevel = 0; productionLevel <= 3; productionLevel++)
            {
                _trait.SetProgressionSnapshot(100000000L, gemstoneBalances, 5);
                purchaseState.Clear();
                bool changed;
                do
                {
                    changed = false;
                    foreach (TraitNodeDefinition node in allNodes)
                    {
                        if (node.RequiredBossTier > 5 || purchaseState.Contains(node.Id) ||
                            !IsAllowedDragonProductionNode(node.Id, productionLevel)) continue;
                        if (!string.IsNullOrEmpty(node.PrerequisiteNodeId) &&
                            !purchaseState.Contains(node.PrerequisiteNodeId)) continue;
                        purchaseState.Add(node.Id);
                        changed = true;
                    }
                } while (changed);
                Assert.That(purchaseState.Contains("monster.09"), Is.True);
                Assert.That(purchaseState.Contains("monster.10"), Is.True);
                Call(_trait, "RefreshAll");

                if (_app.CurrentScreen != UiScreenId.Trait) _app.ShowTrait();
                _app.CloseTrait();
                hunt.BeginPrototypeRun();
                Assert.That(combat.IsRunActive, Is.True,
                    "Tier 5 production level " + productionLevel + " must start.");
                float sampleStart = Time.realtimeSinceStartup;
                while (combat.IsRunActive)
                {
                    MonsterCombatTarget[] active = UnityEngine.Object.FindObjectsByType<MonsterCombatTarget>(
                            FindObjectsSortMode.None)
                        .Where(target => target != null && target.IsActive &&
                            target.RunId == combat.CurrentRunId && target.IsVulnerable)
                        .ToArray();
                    if (active.Length > 0)
                    {
                        MonsterCombatTarget target = active
                            .OrderBy(value => (value.transform.position - cursorTransform.position).sqrMagnitude)
                            .First();
                        cursorTransform.position = Vector3.MoveTowards(cursorTransform.position,
                            target.transform.position, 8f * Time.deltaTime);
                        Physics2D.SyncTransforms();
                        combat.TryAttack(hitBox, true);
                    }

                    if (Time.realtimeSinceStartup - sampleStart > 20f)
                        Assert.Fail("Tier 5 production level " + productionLevel + " exceeded its realtime watchdog.");
                    yield return null;
                }

                string line = "tier=5 monsterProductionLevel=" + productionLevel +
                    " duration=" + combat.DurationSeconds.ToString("0") + "s kills=" + combat.DefeatedCount +
                    " dragon=" + combat.GetResourceEarned("gem.dragon") +
                    " diamond=" + combat.GetResourceEarned("gem.diamond");
                tierLines.Add(line);
                Debug.Log("LONG_TERM_BALANCE_PRODUCTION " + line);
                hunt.ReturnToMainMenu();
                yield return null;
            }

            TestContext.WriteLine("LONG_TERM_BALANCE_PRODUCTION samples at 4x game speed; only monster 09/10 production upgrades vary:");
            foreach (string line in tierLines) TestContext.WriteLine(line);
            Assert.That(tierLines, Has.Count.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator ResourceTiersMeasureIncomeAsTheirProducingMonstersAreUpgraded()
        {
            Time.timeScale = 4f;
            var hunt = Find<HuntManager>();
            var combat = Find<CombatRunController>();
            var cursor = Find<MainCursorController>();
            var catalog = Get<TraitCatalog>(_trait, "_activeCatalog");
            var purchaseState = Get<HashSet<string>>(_trait, "_purchased");
            var allNodes = catalog.StatCategories.Concat(catalog.SkillCategories)
                .Concat(catalog.MonsterCategories).SelectMany(category => category.Nodes)
                .Where(node => node != null).ToArray();
            var gemstoneBalances = Enumerable.Repeat(100000000L, 6).ToArray();
            var tierLines = new List<string>();
            var cursorTransform = cursor.CursorTransform;
            Collider2D hitBox = cursorTransform.GetComponent<Collider2D>();

            for (int tier = 1; tier <= 4; tier++)
            {
                for (int productionLevel = 0; productionLevel <= 3; productionLevel++)
                {
                    _trait.SetProgressionSnapshot(100000000L, gemstoneBalances, tier);
                    purchaseState.Clear();
                    bool changed;
                    do
                    {
                        changed = false;
                        foreach (TraitNodeDefinition node in allNodes)
                        {
                            if (node.RequiredBossTier > tier || purchaseState.Contains(node.Id) ||
                                !IsAllowedProductionNode(node.Id, tier, productionLevel)) continue;
                            if (!string.IsNullOrEmpty(node.PrerequisiteNodeId) &&
                                !purchaseState.Contains(node.PrerequisiteNodeId)) continue;
                            purchaseState.Add(node.Id);
                            changed = true;
                        }
                    } while (changed);

                    Assert.That(purchaseState.Contains("monster.0" + (tier * 2)), Is.True,
                        "Resource tier " + tier + " should include its first source monster.");
                    Call(_trait, "RefreshAll");
                    if (_app.CurrentScreen != UiScreenId.Trait) _app.ShowTrait();
                    _app.CloseTrait();
                    hunt.BeginPrototypeRun();
                    Assert.That(combat.IsRunActive, Is.True,
                        "Tier " + tier + " production level " + productionLevel + " must start.");
                    float sampleStart = Time.realtimeSinceStartup;
                    while (combat.IsRunActive)
                    {
                        MonsterCombatTarget[] active = UnityEngine.Object.FindObjectsByType<MonsterCombatTarget>(
                                FindObjectsSortMode.None)
                            .Where(target => target != null && target.IsActive &&
                                target.RunId == combat.CurrentRunId && target.IsVulnerable)
                            .ToArray();
                        if (active.Length > 0)
                        {
                            MonsterCombatTarget target = active
                                .OrderBy(value => (value.transform.position - cursorTransform.position).sqrMagnitude)
                                .First();
                            cursorTransform.position = Vector3.MoveTowards(cursorTransform.position,
                                target.transform.position, 8f * Time.deltaTime);
                            Physics2D.SyncTransforms();
                            combat.TryAttack(hitBox, true);
                        }

                        if (Time.realtimeSinceStartup - sampleStart > 20f)
                            Assert.Fail("Tier " + tier + " production level " + productionLevel + " exceeded its realtime watchdog.");
                        yield return null;
                    }

                    string line = "tier=" + tier + " productionLevel=" + productionLevel +
                        " duration=" + combat.DurationSeconds.ToString("0") + "s kills=" + combat.DefeatedCount +
                        " gems[g=" + combat.GetResourceEarned("gem.garnet") +
                        ",t=" + combat.GetResourceEarned("gem.topaz") +
                        ",a=" + combat.GetResourceEarned("gem.amethyst") +
                        ",s=" + combat.GetResourceEarned("gem.sapphire") +
                        ",d=" + combat.GetResourceEarned("gem.diamond") +
                        ",dr=" + combat.GetResourceEarned("gem.dragon") + "]";
                    tierLines.Add(line);
                    Debug.Log("LONG_TERM_BALANCE_RESOURCE_RAMP " + line);
                    hunt.ReturnToMainMenu();
                    yield return null;
                }
            }

            TestContext.WriteLine("LONG_TERM_BALANCE_RESOURCE_RAMP samples at 4x game speed; only the current tier's source-monster production ranks vary:");
            foreach (string line in tierLines) TestContext.WriteLine(line);
            Assert.That(tierLines, Has.Count.EqualTo(16));
        }

        private static bool IsAllowedDragonProductionNode(string nodeId, int productionLevel)
        {
            if (!nodeId.StartsWith("monster.09.production.") &&
                !nodeId.StartsWith("monster.10.production.")) return true;
            string suffix = nodeId.Substring(nodeId.Length - 2);
            return int.TryParse(suffix, out int nodeLevel) && nodeLevel <= productionLevel;
        }

        private static bool IsAllowedProductionNode(string nodeId, int tier, int productionLevel)
        {
            string[] prefixes;
            switch (tier)
            {
                case 1: prefixes = new[] { "monster.02.production.", "monster.03.production." }; break;
                case 2: prefixes = new[] { "monster.04.production.", "monster.05.production." }; break;
                case 3: prefixes = new[] { "monster.06.production.", "monster.07.production." }; break;
                case 4: prefixes = new[] { "monster.08.production." }; break;
                default: return true;
            }

            foreach (string prefix in prefixes)
            {
                if (!nodeId.StartsWith(prefix)) continue;
                string suffix = nodeId.Substring(nodeId.Length - 2);
                return int.TryParse(suffix, out int nodeLevel) && nodeLevel <= productionLevel;
            }
            return true;
        }

        private static T Find<T>() where T : UnityEngine.Object =>
            UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        private static void SetBossTierFromTraitTestTool(TraitScreenController trait, int tier)
        {
            Button[] buttons = Get<Button[]>(trait, "_testBossTierButtons");
            Assert.That(buttons, Is.Not.Null);
            Assert.That(tier, Is.InRange(0, buttons.Length - 1));
            MethodInfo setToolsOpen = FindMethod(trait, "SetTestToolsOpen", 1);
            setToolsOpen.Invoke(trait, new object[] { true });
            Assert.That(buttons[tier], Is.Not.Null,
                "The right-side progression test tool must expose the boss tier button.");
            buttons[tier].onClick.Invoke();
            setToolsOpen.Invoke(trait, new object[] { false });
        }

        private static int GetEffectiveBossGate(
            TraitNodeDefinition node,
            IReadOnlyDictionary<string, TraitNodeDefinition> nodesById)
        {
            int gate = 0;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            TraitNodeDefinition current = node;
            while (current != null && visited.Add(current.Id))
            {
                gate = Math.Max(gate, current.RequiredBossTier);
                if (string.IsNullOrEmpty(current.PrerequisiteNodeId)) return gate;
                if (!nodesById.TryGetValue(current.PrerequisiteNodeId, out current))
                    return int.MaxValue;
            }

            return int.MaxValue;
        }

        private static MethodInfo FindMethod(object target, string name, int parameterCount = -1)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                MethodInfo method = type.GetMethods(BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .FirstOrDefault(candidate => candidate.Name == name &&
                        (parameterCount < 0 || candidate.GetParameters().Length == parameterCount));
                if (method != null) return method;
            }

            throw new MissingMethodException(target.GetType().FullName, name);
        }

        private static T Get<T>(object target, string name)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return (T)field.GetValue(target);
            }
            throw new MissingFieldException(target.GetType().FullName, name);
        }

        private static void SetField(object target, string name, object value)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().FullName, name);
        }

        private static void Call(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing method " + name);
            method.Invoke(target, null);
        }
    }
}
