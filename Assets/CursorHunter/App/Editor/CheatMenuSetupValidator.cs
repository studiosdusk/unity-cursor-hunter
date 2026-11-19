using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CursorHunter.App.Editor
{
    /// <summary>Checks that table rows and pre-authored scene buttons form a complete one-to-one setup.</summary>
    public static class CheatMenuSetupValidator
    {
        private const string MainScenePath = "Assets/CursorHunter/App/Scenes/Main.unity";

        [MenuItem("Tools/Cursor Hunter/Cheats/Validate Main Cheat Menu")]
        public static void ValidateCurrentMain()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MainScenePath)
            {
                Debug.LogError("치트 검증을 위해 Main 씬을 열고 활성 씬으로 선택하세요.");
                return;
            }
            var controllers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CheatMenuController>(true)).ToArray();
            if (controllers.Length != 1)
            {
                Debug.LogError("Main 씬에 CheatMenuController가 정확히 하나 있어야 합니다.");
                return;
            }
            var errors = Validate(controllers[0]);
            if (errors.Count == 0) Debug.Log("치트 테이블과 씬 버튼 연결 검증을 통과했습니다.", controllers[0]);
            else foreach (string error in errors) Debug.LogError("치트 설정: " + error, controllers[0]);
        }

        public static IReadOnlyList<string> Validate(CheatMenuController controller)
        {
            var errors = new List<string>();
            if (controller == null) { errors.Add("CheatMenuController가 없습니다."); return errors; }
            var settings = new SerializedObject(controller);
            var table = settings.FindProperty("table").objectReferenceValue as CheatTable;
            var cheatRoot = settings.FindProperty("cheatRoot").objectReferenceValue as GameObject;
            if (table == null || table.Entries == null) errors.Add("치트 테이블이 없습니다.");
            if (cheatRoot == null) errors.Add("Cheat_Canvas가 연결되지 않았습니다.");
            Transform group = cheatRoot != null ? cheatRoot.transform.Find("CheatViewport/Group") : null;
            if (group == null) errors.Add("CheatViewport/Group을 찾을 수 없습니다.");
            if (table == null || table.Entries == null || group == null) return errors;

            var rowIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CheatRow row in table.Entries)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.id))
                { errors.Add("테이블에 빈 ID가 있습니다."); continue; }
                if (!rowIds.Add(row.id)) errors.Add("테이블 ID가 중복됩니다: " + row.id);
                if (!Enum.IsDefined(typeof(CheatAction), row.action)) errors.Add("알 수 없는 동작입니다: " + row.id);
                if (row.action == CheatAction.AddAllCurrencies && row.amount < 0)
                    errors.Add("재화 지급량은 0 이상이어야 합니다: " + row.id);
            }
            var bindingIds = new HashSet<string>(StringComparer.Ordinal);
            var boundButtons = new HashSet<Button>();
            var bindings = settings.FindProperty("buttonBindings");
            for (int i = 0; i < bindings.arraySize; i++)
            {
                var binding = bindings.GetArrayElementAtIndex(i);
                string id = binding.FindPropertyRelative("id").stringValue;
                var button = binding.FindPropertyRelative("button").objectReferenceValue as Button;
                if (string.IsNullOrWhiteSpace(id) || button == null)
                { errors.Add("Button Bindings의 ID 또는 버튼이 비어 있습니다: " + i); continue; }
                if (!bindingIds.Add(id)) errors.Add("버튼 연결 ID가 중복됩니다: " + id);
                if (!boundButtons.Add(button)) errors.Add("같은 버튼이 중복 연결됐습니다: " + button.name);
                if (!rowIds.Contains(id)) errors.Add("연결된 ID가 테이블에 없습니다: " + id);
                if (!button.transform.IsChildOf(group)) errors.Add("버튼이 Group 아래에 없습니다: " + id);
                if (button.name != "Cheat_" + id) errors.Add("버튼 이름과 연결 ID가 다릅니다: " + id);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label == null || string.IsNullOrWhiteSpace(label.text)) errors.Add("씬 버튼의 텍스트가 비어 있습니다: " + id);
                else if (!label.text.Any(c => c >= '\uAC00' && c <= '\uD7A3'))
                    errors.Add("씬 버튼에 한국어 문구가 없습니다: " + id);
            }
            foreach (string id in rowIds)
                if (!bindingIds.Contains(id)) errors.Add("테이블 행에 연결된 씬 버튼이 없습니다: " + id);
            foreach (Button button in group.GetComponentsInChildren<Button>(true))
                if (!boundButtons.Contains(button)) errors.Add("테이블에 연결되지 않은 씬 버튼이 있습니다: " + button.name);
            return errors;
        }
    }
}
