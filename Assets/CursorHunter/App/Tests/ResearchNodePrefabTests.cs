using System.Linq;
using CursorHunter.Progression.ResearchPrototype;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CursorHunter.App.Tests
{
    public sealed class ResearchNodePrefabTests
    {
        private const string PrefabPath = "Assets/CursorHunter/Progression/Prefabs/ResearchNode.prefab";

        [Test]
        public void MainCurrencyRowsSharePrefab()
        {
            const string path = "Assets/CursorHunter/Progression/Prefabs/CurrencyList.prefab";
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Not.Null);
            var scene = EditorSceneManager.OpenPreviewScene("Assets/CursorHunter/App/Scenes/Main.unity");
            try
            {
                var map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchTestMap>(true)).Single();
                var layout = map.transform.Find("CurrenctLayout");
                Assert.That(layout.childCount, Is.EqualTo(6));
                for (int i = 0; i < layout.childCount; i++)
                {
                    var row = layout.GetChild(i);
                    Assert.That(PrefabUtility.IsPartOfPrefabInstance(row), Is.True);
                    Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(row.gameObject)), Is.EqualTo(path));
                    Assert.That(map.gemstoneValues[i].transform.parent, Is.EqualTo(row));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase("Assets/CursorHunter/App/Scenes/Main.unity")]
        [TestCase("Assets/CursorHunter/Progression/Scenes/ResearchPrototype.unity")]
        public void SceneNodesUseSharedPrefabAndRetainTheirPlacementReferences(string scenePath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var template = prefab.GetComponent<ResearchTestNode>();
            Assert.That(template, Is.Not.Null);
            Assert.That(template.definition, Is.Null);
            Assert.That(template.map, Is.Null);
            Assert.That(template.icon, Is.Not.Null);
            var scene = EditorSceneManager.OpenPreviewScene(scenePath);
            try
            {
                var map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchTestMap>(true)).Single();
                Assert.That(map.nodes.Length, Is.EqualTo(12));
                foreach (var node in map.nodes)
                {
                    Assert.That(PrefabUtility.IsPartOfPrefabInstance(node), Is.True, node.name);
                    Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(node.gameObject)), Is.EqualTo(PrefabPath), node.name);
                    Assert.That(node.map, Is.EqualTo(map), node.name);
                    Assert.That(node.definition, Is.Not.Null, node.name);
                    Assert.That(node.icon.sprite, Is.EqualTo(node.definition.icon), node.name);
                    var sourceBorder = PrefabUtility.GetCorrespondingObjectFromSource(node.border);
                    var modifications = PrefabUtility.GetPropertyModifications(node.gameObject);
                    Assert.That(modifications == null || !modifications.Any(modification =>
                        modification.target == sourceBorder && modification.propertyPath == "m_Sprite"), Is.True,
                        node.name + " must inherit the shared border sprite");
                    Assert.That(node.prerequisites.All(prerequisite => prerequisite != null && map.nodes.Contains(prerequisite)), Is.True, node.name);
                    Assert.That(node.requiredPrerequisites == 0 || node.incomingLine != null, Is.True, node.name);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
