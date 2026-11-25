using System;
using System.Collections.Generic;
using System.Linq;
using CursorHunter.Contracts;
using CursorHunter.Data;
using CursorHunter.Progression.Editor;
using CursorHunter.Progression.ResearchPrototype;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CursorHunter.App.Tests
{
    public class ResearchExpandedTests
    {
        const string Path = "Assets/CursorHunter/Progression/ResearchPrototype/Maps/ResearchMap_AllNodes_Test.asset";

        [Test]
        public void UpgradeRequiresItsUnlockAsAnUnavoidablePrerequisite()
        {
            var source = AssetDatabase.LoadAssetAtPath<ResearchMapDefinition>(Path);
            var map = ScriptableObject.CreateInstance<ResearchMapDefinition>();
            try
            {
                map.versionId = "check";
                var unlock = source.nodes.Single(x => x.definition.stableId == "skill.tornado.unlock");
                var damage = source.nodes.Single(x => x.definition.stableId == "skill.tornado.damage");
                var root = source.nodes.Single(x => x.definition.stableId == "cursor.attack");
                map.nodes.Add(new ResearchMapPlacement { id = "root", definition = root.definition, costs = root.costs });
                map.nodes.Add(new ResearchMapPlacement { id = "unlock", definition = unlock.definition, costs = unlock.costs });
                var upgrade = new ResearchMapPlacement { id = "upgrade", definition = damage.definition, costs = damage.costs, requiredPrerequisites = 1 };
                upgrade.prerequisites.Add("root"); upgrade.prerequisites.Add("unlock");
                map.nodes.Add(upgrade);
                Assert.That(map.Validate().Any(x => x.Contains("구매하지 않고 도달")), Is.True);
                upgrade.requiredPrerequisites = 2;
                Assert.That(map.Validate(), Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(map); }
        }

        [Test]
        public void AllFiftyDefinitionsProduceExpectedCombatData()
        {
            var map = AssetDatabase.LoadAssetAtPath<ResearchMapDefinition>(Path);
            Assert.That(map, Is.Not.Null);
            Assert.That(map.isolatedTest, Is.True);
            Assert.That(map.nodes.Count, Is.EqualTo(50));
            Assert.That(map.nodes.Select(x => x.definition.stableId).Distinct().Count(), Is.EqualTo(50));
            Assert.That(map.nodes.Count(x => x.prerequisites.Count == 0), Is.EqualTo(1));
            Assert.That(map.nodes.SelectMany(x => x.costs).All(x => x.amount == 0), Is.True);
            Assert.That(ResearchMapEditorService.Validate(map), Is.Empty);
            var objects = new List<GameObject>();
            try
            {
                var byId = new Dictionary<string, ResearchTestNode>();
                foreach (var placement in map.nodes)
                {
                    var go = new GameObject(placement.id); objects.Add(go);
                    var node = go.AddComponent<ResearchTestNode>();
                    node.placementId = placement.id; node.definition = placement.definition;
                    byId.Add(placement.id, node);
                }
                foreach (var placement in map.nodes)
                {
                    var node = byId[placement.id];
                    node.prerequisites = placement.prerequisites.Select(x => byId[x]).ToArray();
                    node.requiredPrerequisites = placement.requiredPrerequisites;
                }
                var nodes = byId.Values.ToArray();
                var info = GameDataDocument.Load().information;
                info.monsters[0].enabled = true;
                ResearchEffectCalculator.ApplyAdditional(nodes, _ => 0, info);
                Assert.That(info.skills.All(x => !x.enabled), Is.True);
                Assert.That(info.monsters.Skip(1).All(x => !x.enabled), Is.True);
                Assert.That(info.stats.normalFieldDurationSeconds, Is.EqualTo(15));
                int Max(string id) => byId[id].definition.ranks.Length;
                var bonuses = ResearchEffectCalculator.Calculate(nodes, Max);
                Assert.That(bonuses.AttackPowerDelta, Is.EqualTo(35));
                Assert.That(bonuses.AttackRadiusWorldUnitsDelta, Is.EqualTo(1.95f).Within(.0001f));
                Assert.That(bonuses.CriticalChancePercentDelta, Is.EqualTo(40));
                ResearchEffectCalculator.ApplyAdditional(nodes, Max, info);
                Assert.That(info.skills.All(x => x.enabled), Is.True);
                Assert.That(info.monsters.All(x => x.enabled), Is.True);
                Assert.That(info.monsters[0].productionCount, Is.EqualTo(5));
                Assert.That(info.monsters[9].productionCount, Is.EqualTo(16));
                Assert.That(info.stats.normalFieldDurationSeconds, Is.EqualTo(30));
                var tornado = info.skills.Single(x => x.id == "skill.tornado");
                Assert.That(tornado.upgrades.Single(x => x.statId == "damage").delta, Is.EqualTo(7));
                Assert.That(tornado.upgrades.Single(x => x.statId == "radius").delta, Is.EqualTo(.2f).Within(.0001f));
                var pet = info.skills.Single(x => x.id == "skill.pet_baby_dragon");
                Assert.That(pet.upgrades.Single(x => x.statId == "simultaneousHits").delta, Is.EqualTo(2));
                Assert.That(info.TryValidate(out string error), Is.True, error);
                var damageNode = nodes.Single(x => x.definition.stableId == "skill.tornado.damage");
                Assert.That(ResearchEffectCalculator.RequiredTargetUnlocked(nodes, damageNode.definition, _ => 0), Is.False);
                Assert.That(ResearchEffectCalculator.RequiredTargetUnlocked(nodes, damageNode.definition, Max), Is.True);
            }
            finally { foreach (var go in objects) UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void TestMapSwitchRestoresOriginalNodesAndLinks()
        {
            var scene = EditorSceneManager.OpenPreviewScene(ResearchMapEditorService.MainScene);
            var map = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)).Single();
            var original = ResearchMapEditorService.Capture(map, "restore");
            try
            {
                var originalNode = map.nodes[0];
                var nodeWithLine = map.nodes.First(x => x.incomingLine != null || (x.incomingLines != null && x.incomingLines.Length == 1));
                var originalLine = nodeWithLine.incomingLine != null ? nodeWithLine.incomingLine : nodeWithLine.incomingLines[0];
                var test = AssetDatabase.LoadAssetAtPath<ResearchMapDefinition>(Path);
                Assert.That(ResearchMapEditorService.CheckApply(test, map), Is.Null);
                ResearchMapEditorService.Apply(test, map);
                Assert.That(map.nodes.Length, Is.EqualTo(50));
                Assert.That(originalNode.transform.IsChildOf(map.navigation.board.Find("ResearchMapArchive")), Is.True);
                Assert.That(ResearchMapEditorService.CheckApply(original, map), Is.Null);
                ResearchMapEditorService.Apply(original, map);
                Assert.That(map.nodes.Any(x => x == originalNode), Is.True);
                Assert.That(map.nodes.Any(x => x == nodeWithLine), Is.True);
                Assert.That(nodeWithLine.incomingLines.Single(), Is.SameAs(originalLine));
            }
            finally { UnityEngine.Object.DestroyImmediate(original); EditorSceneManager.ClosePreviewScene(scene); }
        }
        [Test]
        public void SavingTestMapBacksUpAndRestoresAuthoredScene()
        {
            string folderName = "ResearchSwitchTest_" + Guid.NewGuid().ToString("N");
            string folder = "Assets/" + folderName;
            string scenePath = folder + "/TestScene.unity";
            AssetDatabase.CreateFolder("Assets", folderName);
            UnityEngine.SceneManagement.Scene scene = default;
            try
            {
                Assert.That(AssetDatabase.CopyAsset(ResearchMapEditorService.MainScene, scenePath), Is.True);
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                var originalMap = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)).Single();
                var originalNode = originalMap.nodes[0];
                originalNode.gameObject.name = "사용자 지정 노드";
                ((RectTransform)originalNode.transform).anchoredPosition = new Vector2(24, 36);
                PrefabUtility.RecordPrefabInstancePropertyModifications(originalNode.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(originalNode.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
                EditorSceneManager.CloseScene(scene, true); scene = default;
                var test = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ResearchMapDefinition>(Path));
                test.versionId = folderName;
                AssetDatabase.CreateAsset(test, folder + "/TestMap.asset");
                var errors = ResearchMapEditorService.SaveToScene(test, scenePath);
                Assert.That(errors, Is.Empty, string.Join("\n", errors));
                var backup = AssetDatabase.FindAssets("t:ResearchMapDefinition", new[] { folder })
                    .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ResearchMapDefinition>)
                    .Single(x => x != test);
                Assert.That(backup.nodes[0].position, Is.EqualTo(new Vector2(24, 36)));
                errors = ResearchMapEditorService.SaveToScene(backup, scenePath);
                Assert.That(errors, Is.Empty, string.Join("\n", errors));
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                var restored = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)).Single();
                Assert.That(restored.nodes[0].name, Is.EqualTo("사용자 지정 노드"));
                Assert.That(((RectTransform)restored.nodes[0].transform).anchoredPosition, Is.EqualTo(new Vector2(24, 36)));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
