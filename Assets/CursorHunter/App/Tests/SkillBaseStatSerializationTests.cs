using CursorHunter.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CursorHunter.App.Tests
{
    public sealed class SkillBaseStatSerializationTests
    {
        [Test]
        public void MainSceneKeepsSkillBaseStatOverrides()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/CursorHunter/App/Scenes/Main.unity",
                OpenSceneMode.Additive);
            try
            {
                Assert.That(Read(scene, "TornadoSkillController",
                    "baseStat.MoveSpeedWorldUnitsPerSecond"), Is.EqualTo(4f));
                Assert.That(Read(scene, "HeadshotSkillController",
                    "baseStat.CooldownSeconds"), Is.EqualTo(5f));
                Assert.That(Read(scene, "ThunderSkillController",
                    "baseStat.CooldownSeconds"), Is.EqualTo(3f));
                Assert.That(Read(scene, "FlameExhaustSkillController",
                    "baseStat.CooldownSeconds"), Is.EqualTo(3f));
                Assert.That(Read(scene, "FlameExhaustSkillController",
                    "baseStat.ProjectileDurationSeconds"), Is.EqualTo(3f));
                Assert.That(Read(scene, "FlameExhaustSkillController",
                    "baseStat.CursorSizeRatio"), Is.EqualTo(0.6f));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static float Read(Scene scene, string typeName,
            string propertyPath)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour component in
                    root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null ||
                        component.GetType().Name != typeName) continue;
                    SerializedProperty property = new SerializedObject(component)
                        .FindProperty(propertyPath);
                    Assert.That(property, Is.Not.Null,
                        typeName + "." + propertyPath);
                    return property.floatValue;
                }
            }
            Assert.Fail("Missing " + typeName + " in Main scene.");
            return 0f;
        }
    }
}
