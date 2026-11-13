using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CursorHunter.Progression;
using CursorHunter.Progression.ResearchPrototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.App.Tests
{
    public class ResearchExpandedPlayModeTests
    {
        [UnityTest]
        public IEnumerator AllTestPurchasesReachTheRealCombatSnapshotWithoutSpendingWallet()
        {
#if UNITY_EDITOR
            const string key = "cursor_hunter.progression.v1";
            string before = PlayerPrefs.GetString(key, ""); bool existed = PlayerPrefs.HasKey(key);
            string backupKey = key + ".before-v6";
            string backup = PlayerPrefs.GetString(backupKey, ""); bool backupExisted = PlayerPrefs.HasKey(backupKey);
            Assert.That(TraitProgressionStore.Save(200, new long[] { 200, 20, 30, 40, 50, 60 },
                new[] { "stat.attack.02" }, new Dictionary<string, int>(), new Dictionary<string, string>(),
                researchRanks: new Dictionary<string, int> { ["cursor.attack.start"] = 2 }), Is.True);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            var created = new List<GameObject>();
            try
            {
                yield return null;
                var map = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)).Single();
                map.gameObject.SetActive(true); yield return null;
                var version = UnityEditor.AssetDatabase.LoadAssetAtPath<ResearchMapDefinition>(
                    "Assets/CursorHunter/Progression/ResearchPrototype/Maps/ResearchMap_AllNodes_Test.asset");
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CursorHunter/Progression/Prefabs/ResearchNode.prefab");
                var nodes = new Dictionary<string, ResearchTestNode>();
                foreach (var placement in version.nodes)
                {
                    var go = Object.Instantiate(prefab, map.nodes[0].transform.parent); created.Add(go);
                    var node = go.GetComponent<ResearchTestNode>();
                    node.map = map; node.placementId = placement.id; node.definition = placement.definition;
                    node.usePlacementCosts = true; node.placementCosts = (ResearchCost[])placement.costs.Clone();
                    nodes.Add(placement.id, node);
                }
                foreach (var placement in version.nodes)
                {
                    nodes[placement.id].prerequisites = placement.prerequisites.Select(id => nodes[id]).ToArray();
                    nodes[placement.id].requiredPrerequisites = placement.requiredPrerequisites;
                }
                map.nodes = version.nodes.Select(x => nodes[x.id]).ToArray();
                Assert.That(map.progression.IsGemstoneAvailable(1), Is.False);
                var lockedUpgrade = nodes["research-test.skill.tornado.damage"];
                Assert.That(map.progression.TryPurchaseResearchRank(lockedUpgrade.placementId, 0, "gem.garnet", 0), Is.False);
                foreach (var node in map.nodes)
                {
                    if (node.definition.stableId == "field.duration")
                    {
                        map.Select(node);
                        Assert.That(map.beforeValue.text, Is.EqualTo("15"));
                        Assert.That(map.afterValue.text, Is.EqualTo("20"));
                    }
                    if (node.definition.stableId == "monster.02.production")
                    {
                        map.Select(node);
                        Assert.That(map.beforeValue.text, Is.EqualTo("1"));
                        Assert.That(map.afterValue.text, Is.EqualTo("3"));
                    }
                    while (node.rank < node.definition.ranks.Length)
                    {
                        Assert.That(map.progression.TryPurchaseResearchRank(node.placementId, node.rank, "gem.garnet", 0),
                            Is.True, node.placementId + " rank " + node.rank);
                        node.rank++;
                    }
                }
                var snapshot = map.progression.CreateCombatSnapshot();
                Assert.That(snapshot.IsValid, Is.True);
                Assert.That(snapshot.CursorStatBonuses.AttackPowerDelta, Is.EqualTo(35));
                Assert.That(snapshot.Skills.All(x => x.Unlocked), Is.True);
                Assert.That(snapshot.Skills.Single(x => x.SkillId == "skill.tornado").GetUpgrade("damage"), Is.EqualTo(7));
                Assert.That(snapshot.Monsters.All(x => x.Unlocked), Is.True);
                Assert.That(snapshot.NormalFieldDurationSeconds, Is.EqualTo(30));
                Assert.That(map.progression.GetResearchGemstoneBalance(0), Is.EqualTo(200));
                Assert.That(map.progression.IsGemstoneAvailable(1), Is.True);
                Assert.That(TraitProgressionStore.TryLoad(out var saved), Is.True);
                Assert.That(saved.purchasedNodeIds, Does.Contain("stat.attack.02"));
                Assert.That(saved.researchRanks.Single(x => x.placementId == "cursor.attack.start").rank, Is.EqualTo(2));
                Assert.That(saved.researchRanks.Count(x => x.placementId.StartsWith("research-test.")), Is.EqualTo(50));
                var hunt = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<CursorHunter.App.HuntManager>(true)).Single();
                hunt.BeginPrototypeRun();
                yield return null;
                var combat = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<CursorHunter.Combat.CombatRunController>(true)).Single();
                Assert.That(combat.IsRunActive, Is.True, "All unlocked skills and monsters must configure a real run.");
                Assert.That(combat.CombatSnapshot.AttackPower, Is.EqualTo(37));
                map.ResetResearchProgress();
                Assert.That(nodes.Values.All(x => x.rank == 0), Is.True);
                Assert.That(nodes["research-test.cursor.attack"].gameObject.activeSelf, Is.True);
                Assert.That(nodes["research-test.skill.tornado.unlock"].gameObject.activeSelf, Is.False);
                Assert.That(map.progression.GetResearchGemstoneBalance(0), Is.EqualTo(200));
                Assert.That(map.progression.IsGemstoneAvailable(1), Is.False);
                Assert.That(map.progression.CreateCombatSnapshot().CursorStatBonuses.AttackPowerDelta, Is.Zero);
                Assert.That(combat.CombatSnapshot.AttackPower, Is.EqualTo(37), "An active run keeps its original snapshot.");
                Assert.That(TraitProgressionStore.TryLoad(out saved), Is.True);
                Assert.That(saved.purchasedNodeIds, Does.Contain("stat.attack.02"));
                Assert.That(saved.researchRanks.Single(x => x.placementId == "cursor.attack.start").rank, Is.EqualTo(2));
                Assert.That(saved.researchRanks.Any(x => x.placementId.StartsWith("research-test.")), Is.False);
            }
            finally
            {
                foreach (var go in created) if (go != null) Object.Destroy(go);
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                if (existed) PlayerPrefs.SetString(key, before); else PlayerPrefs.DeleteKey(key);
                if (backupExisted) PlayerPrefs.SetString(backupKey, backup); else PlayerPrefs.DeleteKey(backupKey);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }
    }
}
