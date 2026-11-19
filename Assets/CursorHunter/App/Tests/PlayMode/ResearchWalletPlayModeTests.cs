using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CursorHunter.Contracts;
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
        private const string BackupKey = Key + ".before-v6";
        private const string MainPath = "Assets/CursorHunter/App/Scenes/Main.unity";
        private Scene scene;
        private string previousProfile, previousBackup;

        [SetUp]
        public void IsolateProfile()
        {
            previousProfile = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
            previousBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(BackupKey);
        }

        [UnityTearDown]
        public IEnumerator RestoreProfile()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                if (SceneManager.sceneCount == 1) SceneManager.CreateScene("Research test teardown");
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            if (previousProfile == null) PlayerPrefs.DeleteKey(Key); else PlayerPrefs.SetString(Key, previousProfile);
            if (previousBackup == null) PlayerPrefs.DeleteKey(BackupKey); else PlayerPrefs.SetString(BackupKey, previousBackup);
            PlayerPrefs.Save();
        }

        private IEnumerator LoadMain()
        {
            yield return SceneManager.LoadSceneAsync(MainPath, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(MainPath);
            Assert.That(scene.isLoaded, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedLiveResearchChangesOnlyThePurchasedCombatStats()
        {
#if UNITY_EDITOR
            Assert.That(TraitProgressionStore.Save(1500, new long[] { 1500, 0, 0, 0, 0, 0 },
                new[] { "stat.attack.01", "stat.attack.02" }, null, null), Is.True);
            yield return LoadMain();
            var map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchTestMap>(true)).Single();
            map.gameObject.SetActive(true);
            yield return null;
            Assert.That(map.TryGetLayoutFingerprint(out string fingerprint), Is.True);
            var attack = map.nodes.Single(node => node.definition.stableId == "cursor.attack.increase" && node.requiredPrerequisites == 0);
            var radius = map.nodes.First(node => node.definition.stableId == "cursor.radius.increase" && node.prerequisites.Contains(attack));
            string attackId = attack.placementId, radiusId = radius.placementId;
            long attackDelta = (long)(attack.definition.ranks[0].levelDelta + attack.definition.ranks[1].levelDelta);
            float radiusDelta = radius.definition.ranks[0].levelDelta;
            Assert.That(attackDelta, Is.GreaterThan(0));
            Assert.That(radiusDelta, Is.GreaterThan(0));
            map.Purchase(attack);
            map.Purchase(attack);
            map.Purchase(radius);
            Assert.That(attack.rank, Is.EqualTo(2));
            Assert.That(radius.rank, Is.EqualTo(1));
            Assert.That(TraitProgressionStore.TryLoad(out var saved), Is.True);
            Assert.That(saved.researchLayoutFingerprint, Is.EqualTo(fingerprint));
            Assert.That(saved.purchasedNodeIds, Does.Contain("stat.attack.02"));
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return LoadMain();
            var progression = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TraitScreenController>(true)).Single();
            Assert.That(progression.GetResearchRank(attackId), Is.EqualTo(2));
            Assert.That(progression.GetResearchRank(radiusId), Is.EqualTo(1));
            var bonuses = progression.CreateCursorCombatStatBonusesSnapshot();
            Assert.That(bonuses.AttackPowerDelta, Is.EqualTo(attackDelta));
            Assert.That(bonuses.AttackRadiusWorldUnitsDelta, Is.EqualTo(radiusDelta).Within(.0001f));
            Assert.That(bonuses.AttackCooldownSecondsDelta, Is.Zero);
            Assert.That(bonuses.CriticalChancePercentDelta, Is.Zero);
            Assert.That(bonuses.CriticalDamageMultiplierDelta, Is.Zero);
            Assert.That(bonuses.BossDamageMultiplierDelta, Is.Zero);
            var combatStats = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CursorHunter.Combat.PlayerCombatStatsRuntime>(true)).Single();
            var defaults = combatStats.Defaults;
            var hunt = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HuntManager>(true)).Single();
            hunt.BeginPrototypeRun();
            yield return null;
            Assert.That(combatStats.HasRunSnapshot, Is.True);
            Assert.That(combatStats.AttackPower, Is.EqualTo(defaults.AttackPower + attackDelta));
            Assert.That(combatStats.AttackRadiusWorldUnits, Is.EqualTo(defaults.AttackRadiusWorldUnits + radiusDelta).Within(.0001f));
            Assert.That(combatStats.AttackCooldownSeconds, Is.EqualTo(defaults.AttackCooldownSeconds).Within(.0001f));
            Assert.That(combatStats.CriticalChancePercent, Is.EqualTo(defaults.CriticalChancePercent).Within(.0001f));
            Assert.That(combatStats.CriticalDamageMultiplier, Is.EqualTo(defaults.CriticalDamageMultiplier).Within(.0001f));
            Assert.That(combatStats.BossDamageMultiplier, Is.EqualTo(defaults.BossDamageMultiplier).Within(.0001f));
#else
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator ResearchCostUsesConfiguredGemstoneAndPersistsRank()
        {
#if UNITY_EDITOR
            Assert.That(TraitProgressionStore.Save(1500, new long[] { 1500, 42, 3, 4, 5, 6 },
                new string[0], new Dictionary<string, int>(), new Dictionary<string, string>()), Is.True);
            yield return LoadMain();
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
            }
#else
            yield return null;
#endif
        }
        [UnityTest]
        public IEnumerator MainWalletPurchasesPersistWithoutReplacingLegacyData()
        {
#if UNITY_EDITOR
            const string currentCardId = "trait_card.hunting_wisdom";
            Assert.That(TraitProgressionStore.Save(1500, new long[] { 1500, 42, 3, 4, 5, 6 },
                new[] { "stat.attack.01", "stat.attack.02", "skill.retired.legacy" },
                new Dictionary<string, int> { ["skill.retired.legacy"] = 125 },
                new Dictionary<string, string> { ["skill.retired.legacy"] = "gem.garnet" },
                settledBossRunIds: new[] { "preserved-run" }, ownedTraitCardIds: new[] { "talent.might" },
                newTraitCards: new TraitCardProgressData { points = 3, ownedIds = new List<string> { currentCardId } }), Is.True);
            yield return LoadMain();
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
            var first = map.nodes.Single(node => node.definition.stableId == "cursor.attack.increase" && node.requiredPrerequisites == 0);
            string attackId = first.placementId;
            long firstAttackDelta = (long)first.definition.ranks[0].levelDelta;
            Assert.That(firstAttackDelta, Is.GreaterThan(0));
            var combatStats = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CursorHunter.Combat.PlayerCombatStatsRuntime>(true)).Single();
            long baseAttack = combatStats.Defaults.AttackPower;
            Assert.That(first.rank, Is.Zero);
            Assert.That(map.progression.CreateCursorCombatStatBonusesSnapshot().AttackPowerDelta, Is.Zero,
                "Archived research and talent.might do not grant attack in the current card system.");
            Assert.That(first.TryGetCost(0, out var initialCost), Is.True);
            Assert.That(initialCost.currencyId, Is.EqualTo("gem.garnet"));
            long cost = initialCost.amount;
            map.Select(first);
            Assert.That(map.costValue.text, Is.EqualTo(cost.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)));
            map.Purchase(first);
            Assert.That(first.rank, Is.EqualTo(1));
            Assert.That(map.progression.CreateCursorCombatStatBonusesSnapshot().AttackPowerDelta, Is.EqualTo(firstAttackDelta));
            Assert.That(map.progression.CreateCombatSnapshot().Combat.AttackPower, Is.EqualTo(baseAttack + firstAttackDelta));
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
            Assert.That(saved.newTraitCards.ownedIds, Is.EqualTo(new[] { currentCardId }));
            Assert.That(saved.newTraitCards.points, Is.EqualTo(3));
            Assert.That(map.TryGetLayoutFingerprint(out string fingerprint), Is.True);
            Assert.That(saved.researchLayoutFingerprint, Is.EqualTo(fingerprint));
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
            yield return LoadMain();
            yield return null;
            map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchTestMap>(true)).Single();
            map.gameObject.SetActive(true);
            yield return null;
            Assert.That(map.nodes.Single(node => node.placementId == attackId).rank, Is.EqualTo(1));
            Assert.That(map.progression.CreateCursorCombatStatBonusesSnapshot().AttackPowerDelta, Is.EqualTo(firstAttackDelta));
            Assert.That(map.progression.TraitCards.Points, Is.EqualTo(3));
            var effect = TraitCardCombatLoadout.Capture(map.progression).Effects.OfType<NormalExperienceTraitSnapshot>().Single();
            Assert.That(effect.CardId, Is.EqualTo(currentCardId));
            Assert.That(effect.BonusPercent, Is.EqualTo(map.progression.GetTraitCard(currentCardId).gameplay.normalMonsterExperience.bonusPercent));
            combatStats = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CursorHunter.Combat.PlayerCombatStatsRuntime>(true)).Single();
            var hunt = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CursorHunter.App.HuntManager>(true)).Single();
            hunt.BeginPrototypeRun();
            yield return null;
            Assert.That(combatStats.HasRunSnapshot, Is.True);
            Assert.That(combatStats.AttackPower, Is.EqualTo(baseAttack + firstAttackDelta), "The purchased live rank changes the real combat stat exactly once.");
            Assert.That(map.gemstoneValues[0].text, Is.EqualTo((1500 - cost).ToString("N0", System.Globalization.CultureInfo.InvariantCulture)));
#else
            yield return null;
#endif
        }
    }
}
