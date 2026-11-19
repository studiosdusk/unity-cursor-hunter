using System.Linq;
using CursorHunter.App;
using CursorHunter.App.Editor;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.App.Tests
{
    public sealed class CheatMenuSceneTests
    {
        [Test]
        public void MainSceneContainsAuthoredButtonsAndKoreanLabelsBeforePlay()
        {
            var scene = EditorSceneManager.OpenScene("Assets/CursorHunter/App/Scenes/Main.unity", OpenSceneMode.Additive);
            try
            {
                var ui = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                var cheat = ui.transform.Find("Cheat_Canvas");
                Assert.That(cheat.gameObject.activeSelf, Is.False);
                var group = cheat.Find("CheatViewport/Group");
                Assert.That(group, Is.Not.Null);
                Assert.That(group.GetComponent<ContentSizeFitter>(), Is.Not.Null);
                var buttons = group.GetComponentsInChildren<Button>(true);
                var expected = new System.Collections.Generic.Dictionary<string, string> {
                    ["reset_trait_cards"] = "특성 카드 초기화", ["reset_research"] = "연구 진행 상황 초기화",
                    ["reset_relics"] = "유물 진행 상황 초기화", ["grant_currencies"] = "모든 재화 999,999 지급",
                    ["clear_currencies"] = "모든 재화 0으로", ["increase_user_level"] = "유저 레벨 1 올리기" };
                foreach (var entry in expected)
                {
                    var button = buttons.Single(b => b.name == "Cheat_" + entry.Key);
                    Assert.That(button.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo(entry.Value));
                }
                Assert.That(cheat.Find("CheatTitle").GetComponent<TMP_Text>().text, Is.EqualTo("테스트 치트   |   = 닫기"));
                Assert.That(cheat.Find("CheatStatusSuccess").GetComponent<TMP_Text>().text, Is.EqualTo("적용 완료"));
                Assert.That(cheat.Find("CheatStatusFailure").GetComponent<TMP_Text>().text, Does.Contain("실패"));
                Assert.That(cheat.Find("CheatStatusMaxLevel").GetComponent<TMP_Text>().text, Does.Contain("최대 레벨"));

                var controller = new SerializedObject(ui.GetComponent<CheatMenuController>());
                var bindings = controller.FindProperty("buttonBindings");
                Assert.That(bindings.arraySize, Is.EqualTo(buttons.Length));
                for (int i = 0; i < buttons.Length; i++)
                {
                    var binding = bindings.GetArrayElementAtIndex(i);
                    var button = (Button)binding.FindPropertyRelative("button").objectReferenceValue;
                    Assert.That(buttons, Does.Contain(button));
                    Assert.That("Cheat_" + binding.FindPropertyRelative("id").stringValue, Is.EqualTo(button.name));
                }
                Assert.That(CheatMenuSetupValidator.Validate(ui.GetComponent<CheatMenuController>()), Is.Empty);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void ValidatorReportsMismatchedSceneAndTableIds()
        {
            var scene = EditorSceneManager.OpenScene("Assets/CursorHunter/App/Scenes/Main.unity", OpenSceneMode.Additive);
            try
            {
                var ui = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                var component = ui.GetComponent<CheatMenuController>();
                var settings = new SerializedObject(component);
                settings.FindProperty("buttonBindings").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("id").stringValue = "unregistered_test_id";
                settings.ApplyModifiedPropertiesWithoutUndo();
                var errors = CheatMenuSetupValidator.Validate(component);
                Assert.That(errors.Any(error => error.Contains("unregistered_test_id")), Is.True);
                Assert.That(errors.Any(error => error.Contains("reset_trait_cards")), Is.True);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
