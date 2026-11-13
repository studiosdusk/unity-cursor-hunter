using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CursorHunter.Progression;
using CursorHunter.Data;
using UnityEngine.UI;
using CursorHunter.Progression.ResearchPrototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.App.Tests
{
    public sealed class ResearchWalletPlayModeTests
    {
        private const string Key = "cursor_hunter.progression.v1";
        [UnityTest]
        public IEnumerator SavedResearchAppliesAllSixStatsToCombatAndIgnoresLegacyResearch()
        {
#if UNITY_EDITOR
            bool existed = PlayerPrefs.HasKey(Key);
            string previous = PlayerPrefs.GetString(Key, "");
            string backupKey = Key + ".before-v6";
            bool backupExisted = PlayerPrefs.HasKey(backupKey);
            string backup = PlayerPrefs.GetString(backupKey, "");
            var ranks = new Dictionary<string, int> {
                ["cursor.attack.start"] = 2, ["cursor.attack.next"] = 1,
                ["cursor.radius.start"] = 1, ["cursor.interval.start"] = 1,
                ["cursor.critical.start"] = 1, ["cursor.critical_damage.start"] = 1,
                ["cursor.boss_damage.start"] = 1 };
            Assert.That(TraitProgressionStore.Save(1500, new long[] { 1500, 0, 0, 0, 0, 0 },
                new[] { "stat.attack.01", "stat.attack.02" }, new Dictionary<string, int>(), new Dictionary<string, string>(),
                researchRanks: ranks), Is.True);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            try
            {
                yield return null;
                yield return null;
                var progression = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TraitScreenController>(true)).Single();
                var combatStats = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CursorHunter.Combat.PlayerCombatStatsRuntime>(true)).Single();
                var defaults = combatStats.Defaults;
                var bonuses = progression.CreateCursorCombatStatBonusesSnapshot();
                Assert.That(bonuses.AttackPowerDelta, Is.EqualTo(6));
                Assert.That(bonuses.AttackRadiusWorldUnitsDelta, Is.EqualTo(.1f).Within(.0001f));
                Assert.That(bonuses.AttackCooldownSecondsDelta, Is.EqualTo(-.08f).Within(.0001f));
                Assert.That(bonuses.CriticalChancePercentDelta, Is.EqualTo(5f).Within(.0001f));
                Assert.That(bonuses.CriticalDamageMultiplierDelta, Is.EqualTo(.1f).Within(.0001f));
                Assert.That(bonuses.BossDamageMultiplierDelta, Is.EqualTo(.01f).Within(.0001f));
                var hunt = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CursorHunter.App.HuntManager>(true)).Single();
                hunt.BeginPrototypeRun();
                yield return null;
                Assert.That(combatStats.HasRunSnapshot, Is.True);
                Assert.That(combatStats.AttackPower, Is.EqualTo(defaults.AttackPower + 6));
                Assert.That(combatStats.AttackRadiusWorldUnits, Is.EqualTo(defaults.AttackRadiusWorldUnits + .1f).Within(.0001f));
                Assert.That(combatStats.AttackCooldownSeconds, Is.EqualTo(defaults.AttackCooldownSeconds - .08f).Within(.0001f));
                Assert.That(combatStats.CriticalChancePercent, Is.EqualTo(defaults.CriticalChancePercent + 5f).Within(.0001f));
                Assert.That(combatStats.CriticalDamageMultiplier, Is.EqualTo(defaults.CriticalDamageMultiplier + .1f).Within(.0001f));
                Assert.That(combatStats.BossDamageMultiplier, Is.EqualTo(defaults.BossDamageMultiplier + .01f).Within(.0001f));
                Assert.That(TraitProgressionStore.TryLoad(out var saved), Is.True);
                Assert.That(saved.purchasedNodeIds, Does.Contain("stat.attack.02"));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                if (existed) PlayerPrefs.SetString(Key, previous); else PlayerPrefs.DeleteKey(Key);
                if (backupExisted) PlayerPrefs.SetString(backupKey, backup); else PlayerPrefs.DeleteKey(backupKey);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }
        [UnityTest]
        public IEnumerator ResearchCostUsesConfiguredGemstoneAndPersistsRank()
        {
#if UNITY_EDITOR
            bool existed = PlayerPrefs.HasKey(Key);
            string previous = PlayerPrefs.GetString(Key, "");
            string backupKey = Key + ".before-v6";
            bool backupExisted = PlayerPrefs.HasKey(backupKey);
            string backup = PlayerPrefs.GetString(backupKey, "");
            Assert.That(TraitProgressionStore.Save(1500, new long[] { 1500, 42, 3, 4, 5, 6 },
                new string[0], new Dictionary<string, int>(), new Dictionary<string, string>()), Is.True);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            ResearchDefinition modified = null;
            try
            {
                yield return null;
                var map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchTestMap>(true)).Single();
                map.gameObject.SetActive(true);
                yield return null;
                var node = map.nodes[0];
                modified = Object.Instantiate(node.definition);
                modified.ranks = (ResearchRank[])node.definition.ranks.Clone();
                modified.ranks[0].cost = new ResearchCost { currencyId = "gem.topaz", amount = 7 };
                modified.ranks[1].cost = new ResearchCost { currencyId = "gem.topaz", amount = 7 };
                node.definition = modified;
                node.usePlacementCosts = true;
                node.placementCosts = modified.ranks.Select(rank => rank.cost).ToArray();
                node.placementCosts[0] = new ResearchCost { currencyId = "gem.garnet", amount = 7 };
                map.Select(node);
                Assert.That(map.gemIcon.GetComponent<Image>().sprite, Is.EqualTo(GemstoneIconCatalog.LoadDefault().GetIcon("gem.garnet")));
                node.placementCosts[0] = new ResearchCost { currencyId = "gem.topaz", amount = 7 };
                map.Select(node);
                Assert.That(map.progression.TryPurchaseResearchRank(node.placementId, 0, "gem.garnet", 0), Is.False,
                    "A caller cannot override the placement's configured currency or cost.");
                Assert.That(map.gemIcon.GetComponent<Image>().sprite, Is.EqualTo(GemstoneIconCatalog.LoadDefault().GetIcon("gem.topaz")));
                map.Purchase(node);
                Assert.That(node.rank, Is.EqualTo(1));
                Assert.That(map.progression.GetResearchGemstoneBalance(0), Is.EqualTo(1500));
                Assert.That(map.progression.GetResearchGemstoneBalance(1), Is.EqualTo(35));
                Assert.That(TraitProgressionStore.TryLoad(out var saved), Is.True);
                Assert.That(saved.gemstoneBalances[1], Is.EqualTo(35));
                Assert.That(saved.researchRanks.Single(entry => entry.placementId == node.placementId).rank, Is.EqualTo(1));
                Assert.That(map.progression.TryPurchaseResearchRank("bogus", 0, "gem.fake", 0), Is.False);
                string json = PlayerPrefs.GetString(Key);
                PlayerPrefs.SetString(Key, "{invalid}");
                LogAssert.Expect(LogType.Warning, "Trait progression changed in memory but could not be saved.");
                map.Purchase(node);
                Assert.That(node.rank, Is.EqualTo(1));
                Assert.That(map.progression.GetResearchGemstoneBalance(1), Is.EqualTo(35));
                PlayerPrefs.SetString(Key, json);
            }
            finally
            {
                if (modified != null) Object.Destroy(modified);
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                if (existed) PlayerPrefs.SetString(Key, previous); else PlayerPrefs.DeleteKey(Key);
                if (backupExisted) PlayerPrefs.SetString(backupKey, backup); else PlayerPrefs.DeleteKey(backupKey);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }
        [UnityTest]
        public IEnumerator MainWalletPurchasesPersistWithoutReplacingLegacyData()
        {
#if UNITY_EDITOR
            bool existed = PlayerPrefs.HasKey(Key);
            string previous = PlayerPrefs.GetString(Key, "");
            string backupKey = Key + ".before-v6";
            bool backupExisted = PlayerPrefs.HasKey(backupKey);
            string backup = PlayerPrefs.GetString(backupKey, "");
            Assert.That(TraitProgressionStore.Save(1500, new long[] { 1500, 42, 3, 4, 5, 6 },
                new[] { "stat.attack.01", "stat.attack.02", "skill.retired.legacy" },
                new Dictionary<string, int> { ["skill.retired.legacy"] = 125 },
                new Dictionary<string, string> { ["skill.retired.legacy"] = "gem.garnet" },
                settledBossRunIds: new[] { "preserved-run" }, ownedTraitCardIds: new[] { "talent.might" }), Is.True);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            try
            {
                yield return null;
                var map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchTestMap>(true)).Single();
                map.gameObject.SetActive(true);
                yield return null;
                Assert.That(map.progression, Is.Not.Null);
                Assert.That(map.gemstoneValues.Length, Is.EqualTo(6));
                var layout = map.transform.Find("CurrenctLayout");
                Assert.That(layout.childCount, Is.EqualTo(6));
                Assert.That(map.transform.Find("GemstonePanel").gameObject.activeSelf, Is.False);
                string[] ids = { "gem.garnet", "gem.topaz", "gem.amethyst", "gem.sapphire", "gem.diamond", "gem.dragon" };
                string[] balances = { "1,500", "42", "3", "4", "5", "6" };
                for (int i = 0; i < ids.Length; i++)
                {
                    var row = layout.GetChild(i);
                    Assert.That(row.Find("Icon").GetComponent<Image>().sprite,
                        Is.EqualTo(GemstoneIconCatalog.LoadDefault().GetIcon(ids[i])));
                    Assert.That(map.gemstoneValues[i].transform.parent, Is.EqualTo(row));
                    Assert.That(map.gemstoneValues[i].text, Is.EqualTo(balances[i]));
                }
                Assert.That(map.gemstoneValues[0].text, Is.EqualTo("1,500"));
                Assert.That(map.gemstoneValues[1].text, Is.EqualTo("42"));
                var first = map.nodes[0];
                Assert.That(first.rank, Is.Zero);
                Assert.That(map.progression.CreateCursorCombatStatBonusesSnapshot().AttackPowerDelta, Is.EqualTo(1),
                    "Only the owned talent contributes before research; archived legacy upgrades do not.");
                var originalDefinition = first.definition;
                var largeCostDefinition = Object.Instantiate(originalDefinition);
                largeCostDefinition.ranks = (ResearchRank[])originalDefinition.ranks.Clone();
                largeCostDefinition.ranks[0].garnetCost = 1000;
                first.definition = largeCostDefinition;
                bool previousPlacementCosts = first.usePlacementCosts;
                var previousCosts = first.placementCosts;
                first.usePlacementCosts = true;
                first.placementCosts = previousPlacementCosts
                    ? (ResearchCost[])previousCosts.Clone()
                    : originalDefinition.ranks.Select(rank => rank.cost).ToArray();
                first.placementCosts[0] = new ResearchCost { currencyId = "gem.garnet", amount = 1000 };
                map.Select(first);
                Canvas.ForceUpdateCanvases();
                map.costValue.ForceMeshUpdate();
                var iconCorners = new Vector3[4];
                var costCorners = new Vector3[4];
                ((RectTransform)map.gemIcon.transform).GetWorldCorners(iconCorners);
                map.costValue.rectTransform.GetWorldCorners(costCorners);
                Assert.That(costCorners[0].x, Is.GreaterThan(iconCorners[2].x));
                Assert.That(map.costValue.isTextTruncated, Is.False);
                first.definition = originalDefinition;
                first.usePlacementCosts = previousPlacementCosts;
                first.placementCosts = previousCosts;
                Object.Destroy(largeCostDefinition);
                Assert.That(first.TryGetCost(0, out var initialCost), Is.True);
                long cost = initialCost.amount;
                map.Purchase(first);
                Assert.That(first.rank, Is.EqualTo(1));
                Assert.That(map.progression.CreateCursorCombatStatBonusesSnapshot().AttackPowerDelta, Is.EqualTo(3));
                Assert.That(map.progression.CreateCombatSnapshot().Combat.AttackPower, Is.EqualTo(5));
                Assert.That(map.progression.GetResearchGemstoneBalance(0), Is.EqualTo(1500 - cost));
                Assert.That(map.gemstoneValues[0].text, Is.EqualTo((1500 - cost).ToString("N0", System.Globalization.CultureInfo.InvariantCulture)));
                Assert.That(TraitProgressionStore.TryLoad(out var saved), Is.True);
                Assert.That(saved.garnetBalance, Is.EqualTo(1500 - cost));
                Assert.That(saved.researchRanks.Single(x => x.placementId == first.placementId).rank, Is.EqualTo(1));
                Assert.That(saved.purchasedNodeIds, Does.Contain("stat.attack.01"));
                Assert.That(saved.purchasedNodeIds, Does.Contain("stat.attack.02"));
                Assert.That(saved.purchasedNodeIds, Does.Contain("skill.retired.legacy"));
                int retiredIndex = saved.purchasedNodeIds.IndexOf("skill.retired.legacy");
                Assert.That(saved.spentCosts[retiredIndex], Is.EqualTo(125));
                Assert.That(saved.spentCurrencyIds[retiredIndex], Is.EqualTo("gem.garnet"));
                Assert.That(saved.settledBossRunIds, Does.Contain("preserved-run"));
                Assert.That(saved.ownedTraitCardIds, Does.Contain("talent.might"));
                Assert.That(saved.gemstoneBalances[1], Is.EqualTo(42));
                Assert.That(map.progression.TryPurchaseResearchRank(first.placementId, 0, cost), Is.False);
                Assert.That(map.progression.GetResearchGemstoneBalance(0), Is.EqualTo(1500 - cost));
                string savedJson = PlayerPrefs.GetString(Key);
                PlayerPrefs.SetString(Key, "{invalid}");
                LogAssert.Expect(LogType.Warning, "Trait progression changed in memory but could not be saved.");
                map.Purchase(first);
                Assert.That(first.rank, Is.EqualTo(1), "A failed save must not advance the rank.");
                Assert.That(map.progression.GetResearchGemstoneBalance(0), Is.EqualTo(1500 - cost));
                PlayerPrefs.SetString(Key, savedJson);
                yield return SceneManager.UnloadSceneAsync(scene);
                scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;
                map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchTestMap>(true)).Single();
                map.gameObject.SetActive(true);
                yield return null;
                Assert.That(map.nodes[0].rank, Is.EqualTo(1));
                Assert.That(map.progression.CreateCursorCombatStatBonusesSnapshot().AttackPowerDelta, Is.EqualTo(3));
                var combatStats = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CursorHunter.Combat.PlayerCombatStatsRuntime>(true)).Single();
                long baseAttack = combatStats.Defaults.AttackPower;
                var hunt = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CursorHunter.App.HuntManager>(true)).Single();
                hunt.BeginPrototypeRun();
                yield return null;
                Assert.That(combatStats.HasRunSnapshot, Is.True);
                Assert.That(combatStats.AttackPower, Is.EqualTo(baseAttack + 3), "Research and the owned talent change the real combat stat once.");
                Assert.That(map.gemstoneValues[0].text, Is.EqualTo((1500 - cost).ToString("N0", System.Globalization.CultureInfo.InvariantCulture)));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                if (existed) PlayerPrefs.SetString(Key, previous); else PlayerPrefs.DeleteKey(Key);
                if (backupExisted) PlayerPrefs.SetString(backupKey, backup); else PlayerPrefs.DeleteKey(backupKey);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }
    }
}
