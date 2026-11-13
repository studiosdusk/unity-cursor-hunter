using System;
using System.Linq;
using System.IO;
using System.Reflection;
using CursorHunter.Progression.Editor;
using CursorHunter.Progression.ResearchPrototype;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CursorHunter.App.Tests
{
    public class ResearchMapEditorTests
    {
        [Test]
        public void ChangingSelectionReleasesPreviousInputWithoutChangingNodeData()
        {
            var window = ScriptableObject.CreateInstance<ResearchMapEditorWindow>();
            var map = ScriptableObject.CreateInstance<ResearchMapDefinition>();
            var type = typeof(ResearchMapEditorWindow);
            var select = type.GetMethod("SelectNode", BindingFlags.NonPublic | BindingFlags.Instance);
            var selected = type.GetField("selectedId", BindingFlags.NonPublic | BindingFlags.Instance);
            var clearFocus = type.GetField("clearNodeInputFocus", BindingFlags.NonPublic | BindingFlags.Instance);
            try
            {
                map.nodes.Add(new ResearchMapPlacement { id = "a", position = new Vector2(10, 20), costs = new[] { new ResearchCost { currencyId = "gem.garnet", amount = 100 } } });
                map.nodes.Add(new ResearchMapPlacement { id = "b", position = new Vector2(30, 40), costs = new[] { new ResearchCost { currencyId = "gem.topaz", amount = 250 } } });
                type.GetField("map", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window, map);
                select.Invoke(window, new object[] { "a" });
                clearFocus.SetValue(window, false);
                map.nodes[0].costs[0].amount = 500;
                select.Invoke(window, new object[] { "b" });
                Assert.That(selected.GetValue(window), Is.EqualTo("b"));
                Assert.That(clearFocus.GetValue(window), Is.True);
                Assert.That(map.nodes[0].costs[0].amount, Is.EqualTo(500));
                Assert.That(map.nodes[1].costs[0].amount, Is.EqualTo(250));
                Assert.That(map.nodes[1].position, Is.EqualTo(new Vector2(30, 40)));
                clearFocus.SetValue(window, false);
                select.Invoke(window, new object[] { "b" });
                Assert.That(clearFocus.GetValue(window), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(window); UnityEngine.Object.DestroyImmediate(map); }
        }
        [Test]
        public void SaveValidatesAllErrorsBeforeApplyingAndPersistsSceneWhenValid()
        {
            string folderName = "ResearchMapSaveTest_" + Guid.NewGuid().ToString("N");
            string folder = "Assets/" + folderName;
            string scenePath = folder + "/TestMain.unity";
            string assetPath = folder + "/TestMap.asset";
            AssetDatabase.CreateFolder("Assets", folderName);
            UnityEngine.SceneManagement.Scene scene = default;
            try
            {
                Assert.That(AssetDatabase.CopyAsset(ResearchMapEditorService.MainScene, scenePath), Is.True);
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                var sceneMap = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)).Single();
                var data = ResearchMapEditorService.Capture(sceneMap, folderName);
                var first = data.nodes[0];
                var added = new ResearchMapPlacement { id = "save.test", definition = first.definition,
                    position = first.position + new Vector2(350, 0), costs = (ResearchCost[])first.costs.Clone(), requiredPrerequisites = 1 };
                added.prerequisites.Add(first.id);
                added.costs[0] = new ResearchCost { currencyId = "", amount = -5 };
                data.nodes.Add(added);
                AssetDatabase.CreateAsset(data, assetPath);
                AssetDatabase.SaveAssetIfDirty(data);
                byte[] originalScene = File.ReadAllBytes(scenePath);
                var errors = ResearchMapEditorService.SaveAndApply(data, sceneMap);
                Assert.That(errors.Any(x => x.Contains("save.test") && x.Contains("1레벨") && x.Contains("재화")), Is.True);
                Assert.That(errors.Any(x => x.Contains("save.test") && x.Contains("1레벨") && x.Contains("-5")), Is.True);
                Assert.That(sceneMap.nodes.Any(x => x.placementId == "save.test"), Is.False);
                Assert.That(File.ReadAllBytes(scenePath), Is.EqualTo(originalScene));

                added.costs[0] = new ResearchCost { currencyId = "gem.topaz", amount = 7 };
                EditorUtility.SetDirty(data);
                EditorSceneManager.CloseScene(scene, true);
                scene = default;
                errors = ResearchMapEditorService.SaveToScene(data, scenePath);
                Assert.That(errors, Is.Empty, string.Join("\n", errors));
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                var reloaded = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)).Single();
                var savedCost = reloaded.nodes.Single(x => x.placementId == "save.test").placementCosts[0];
                Assert.That(savedCost.currencyId, Is.EqualTo("gem.topaz"));
                Assert.That(savedCost.amount, Is.EqualTo(7));
                Assert.That(reloaded.appliedVersion, Is.EqualTo(data));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(folder);
            }
        }
        [Test]
        public void DragUndoRestoresPositionAfterMultipleChanges()
        {
            var map = ScriptableObject.CreateInstance<ResearchMapDefinition>();
            try
            {
                map.nodes.Add(new ResearchMapPlacement { id = "drag", position = new Vector2(10, 20) });
                Undo.IncrementCurrentGroup();
                Undo.RegisterCompleteObjectUndo(map, "연구 노드 이동");
                map.nodes[0].position = new Vector2(30, 40);
                map.nodes[0].position = new Vector2(50, 60);
                Undo.PerformUndo();
                Assert.That(map.nodes[0].position, Is.EqualTo(new Vector2(10, 20)));
            }
            finally { UnityEngine.Object.DestroyImmediate(map); }
        }
        [Test]
        public void EditorCachesLookupsUntilAssetsAreInvalidated()
        {
            var window = ScriptableObject.CreateInstance<ResearchMapEditorWindow>();
            var type = typeof(ResearchMapEditorWindow);
            var ensure = type.GetMethod("EnsureCache", BindingFlags.NonPublic | BindingFlags.Instance);
            var invalidate = type.GetMethod("InvalidateCache", BindingFlags.NonPublic | BindingFlags.Instance);
            var versions = type.GetField("versions", BindingFlags.NonPublic | BindingFlags.Instance);
            var definitions = type.GetField("definitions", BindingFlags.NonPublic | BindingFlags.Instance);
            try
            {
                ensure.Invoke(window, null);
                var firstVersions = versions.GetValue(window);
                var firstDefinitions = definitions.GetValue(window);
                Assert.That(((ResearchDefinition[])firstDefinitions).Length, Is.GreaterThanOrEqualTo(6));
                ensure.Invoke(window, null);
                Assert.That(versions.GetValue(window), Is.SameAs(firstVersions));
                Assert.That(definitions.GetValue(window), Is.SameAs(firstDefinitions));
                invalidate.Invoke(window, null);
                ensure.Invoke(window, null);
                Assert.That(versions.GetValue(window), Is.Not.SameAs(firstVersions));
                Assert.That(definitions.GetValue(window), Is.Not.SameAs(firstDefinitions));
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }
        [Test]
        public void PlacementCostsAreIndependentAndMissingCostsAreRejected()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ResearchDefinition>("Assets/CursorHunter/Progression/ResearchPrototype/cursor.attack.asset");
            var a = new GameObject("a").AddComponent<ResearchTestNode>();
            var b = new GameObject("b").AddComponent<ResearchTestNode>();
            try
            {
                a.definition = b.definition = definition;
                a.usePlacementCosts = b.usePlacementCosts = true;
                a.placementCosts = definition.ranks.Select(r => r.cost).ToArray();
                b.placementCosts = definition.ranks.Select(r => r.cost).ToArray();
                a.placementCosts[0] = new ResearchCost { currencyId = "gem.topaz", amount = 7 };
                b.placementCosts[0] = new ResearchCost { currencyId = "gem.dragon", amount = 99 };
                Assert.That(a.TryGetCost(0, out var costA), Is.True);
                Assert.That(b.TryGetCost(0, out var costB), Is.True);
                Assert.That(costA.currencyId, Is.EqualTo("gem.topaz"));
                Assert.That(costB.amount, Is.EqualTo(99));
                b.placementCosts[0] = default;
                Assert.That(b.TryGetCost(0, out _), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(a.gameObject); UnityEngine.Object.DestroyImmediate(b.gameObject); }
        }

        [Test]
        public void MapValidationRejectsCyclesAndMissingCurrency()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ResearchDefinition>("Assets/CursorHunter/Progression/ResearchPrototype/cursor.attack.asset");
            var map = ScriptableObject.CreateInstance<ResearchMapDefinition>();
            try
            {
                map.versionId = "v1";
                var a = new ResearchMapPlacement { id = "a", definition = definition, costs = definition.ranks.Select(r => r.cost).ToArray() };
                var b = new ResearchMapPlacement { id = "b", definition = definition, costs = definition.ranks.Select(r => r.cost).ToArray() };
                map.nodes.Add(a); map.nodes.Add(b);
                b.prerequisites.Add("a"); b.requiredPrerequisites = 1;
                Assert.That(map.Validate(), Is.Empty);
                a.prerequisites.Add("b"); a.requiredPrerequisites = 1;
                Assert.That(map.Validate().Any(x => x.Contains("순환")), Is.True);
                a.prerequisites.Clear(); a.requiredPrerequisites = 0;
                b.costs[0] = default;
                Assert.That(map.Validate().Any(x => x.Contains("재화")), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(map); }
        }

        [Test]
        public void SceneCaptureAndApplyPreserveExistingNodesAndAddBranch()
        {
            var scene = EditorSceneManager.OpenPreviewScene(ResearchMapEditorService.MainScene);
            var map = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)).Single();
            var data = ResearchMapEditorService.Capture(map, "v1");
            try
            {
                int originalCount = map.nodes.Length;
                var existingLineNode = map.nodes.First(x => x.incomingLine != null || (x.incomingLines != null && x.incomingLines.Length == 1));
                var existingLine = existingLineNode.incomingLine != null ? existingLineNode.incomingLine : existingLineNode.incomingLines[0];
                var originalColor = existingLine.GetComponent<UnityEngine.UI.Image>().color;
                float originalWidth = ((RectTransform)existingLine.transform).sizeDelta.x;
                var first = data.nodes[0];
                var added = new ResearchMapPlacement { id = "test.added", definition = first.definition,
                    position = first.position + new Vector2(350, 0), costs = (ResearchCost[])first.costs.Clone(), requiredPrerequisites = 1 };
                added.costs[0] = new ResearchCost { currencyId = "gem.topaz", amount = 7 };
                added.prerequisites.Add(first.id); data.nodes.Add(added);
                Assert.That(ResearchMapEditorService.CheckApply(data, map), Is.Null);
                ResearchMapEditorService.Apply(data, map);
                Assert.That(map.nodes.Length, Is.EqualTo(originalCount + 1));
                var created = map.nodes.Single(x => x.placementId == "test.added");
                Assert.That(created.TryGetCost(0, out var cost), Is.True);
                Assert.That(cost.currencyId, Is.EqualTo("gem.topaz"));
                Assert.That(created.prerequisites.Single().placementId, Is.EqualTo(first.id));
                Assert.That(created.incomingLines.Length, Is.EqualTo(1));
                Assert.That(existingLineNode.incomingLines.Single(), Is.SameAs(existingLine));
                Assert.That(existingLine.GetComponent<UnityEngine.UI.Image>().color, Is.EqualTo(originalColor));
                Assert.That(((RectTransform)existingLine.transform).sizeDelta.x, Is.EqualTo(originalWidth));
                Assert.That(ResearchMapEditorService.Fingerprint(map), Is.EqualTo(ResearchMapEditorService.FingerprintDefinition(data)));
                Assert.That(ResearchMapEditorService.CheckApply(data, map), Is.Null);
                ((RectTransform)created.transform).anchoredPosition += Vector2.one;
                Assert.That(ResearchMapEditorService.CheckApply(data, map), Does.Contain("씬에서 연구 맵이 변경"));
            }
            finally { UnityEngine.Object.DestroyImmediate(data); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
