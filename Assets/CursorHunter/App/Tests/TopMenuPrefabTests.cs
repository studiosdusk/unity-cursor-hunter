using System.Linq;
using CursorHunter.App;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.App.Tests
{
    public sealed class TopMenuPrefabTests
    {
        [Test]
        public void MenuButtonsShareAuthoredResearchGeometry()
        {
            const string path = "Assets/CursorHunter/App/Prefabs/TopMenuButton.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            var sourceRect = (RectTransform)prefab.transform;
            var sourceIcon = (RectTransform)prefab.transform.Find("Icon");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/CursorHunter/App/Scenes/Main.unity");
            try
            {
                var canvas = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                var menu = canvas.transform.Find("TopMenu/Menus/Button_MainMenu");
                string[] names = { "Button_Research", "Button_Artifact", "Button_TraitCard", "Button_Shop (3)" };
                Assert.That(menu.childCount, Is.EqualTo(names.Length));
                var sprites = new System.Collections.Generic.HashSet<Sprite>();
                for (int i = 0; i < names.Length; i++)
                {
                    var row = menu.GetChild(i);
                    Assert.That(row.name, Is.EqualTo(names[i]));
                    Assert.That(PrefabUtility.IsPartOfPrefabInstance(row), Is.True, row.name);
                    Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(row.gameObject)), Is.EqualTo(path));
                    var rect = (RectTransform)row;
                    var icon = (RectTransform)row.Find("Icon");
                    Assert.That(rect.sizeDelta, Is.EqualTo(sourceRect.sizeDelta), row.name);
                    Assert.That(icon.sizeDelta, Is.EqualTo(sourceIcon.sizeDelta), row.name);
                    Assert.That(icon.localScale, Is.EqualTo(sourceIcon.localScale), row.name);
                    Assert.That(icon.anchorMin, Is.EqualTo(sourceIcon.anchorMin), row.name);
                    Assert.That(icon.anchorMax, Is.EqualTo(sourceIcon.anchorMax), row.name);
                    Assert.That(icon.anchoredPosition, Is.EqualTo(sourceIcon.anchoredPosition), row.name);
                    var modifications = PrefabUtility.GetPropertyModifications(row.gameObject);
                    var sourceIconTransform = PrefabUtility.GetCorrespondingObjectFromSource(icon);
                    var sourceRowTransform = PrefabUtility.GetCorrespondingObjectFromSource(rect);
                    Assert.That(modifications == null || !modifications.Any(m => m.target == sourceRowTransform &&
                        m.propertyPath.StartsWith("m_SizeDelta")), Is.True, row.name + " must inherit the button size.");
                    Assert.That(modifications == null || !modifications.Any(m => m.target == sourceIconTransform &&
                        (m.propertyPath.StartsWith("m_SizeDelta") || m.propertyPath.StartsWith("m_LocalScale") ||
                         m.propertyPath.StartsWith("m_Anchor") || m.propertyPath.StartsWith("m_AnchoredPosition"))), Is.True, row.name);
                    sprites.Add(icon.GetComponent<Image>().sprite);
                }
                Assert.That(sprites.Count, Is.EqualTo(names.Length), "Each menu keeps its own icon.");
                Assert.That(canvas.GetComponent<ProgressionTopMenu>(), Is.Not.Null);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
