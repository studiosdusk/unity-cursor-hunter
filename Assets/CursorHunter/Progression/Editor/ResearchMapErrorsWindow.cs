using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CursorHunter.Progression.Editor
{
    public sealed class ResearchMapErrorsWindow : EditorWindow
    {
        private IReadOnlyList<string> errors = new string[0];
        private Vector2 scroll;

        public static void Open(IReadOnlyList<string> messages)
        {
            var window = GetWindow<ResearchMapErrorsWindow>(true, "연구 맵 확인이 필요합니다");
            window.minSize = new Vector2(480, 300);
            window.errors = messages;
            window.scroll = Vector2.zero;
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("저장할 수 없는 항목 " + errors.Count + "개", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("아래 내용을 수정한 뒤 저장을 다시 눌러 주세요.", EditorStyles.wordWrappedLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < errors.Count; i++)
                EditorGUILayout.HelpBox((i + 1) + ". " + errors[i], MessageType.Error);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("오류 내용 복사")) EditorGUIUtility.systemCopyBuffer = string.Join("\n\n", errors);
            if (GUILayout.Button("확인")) Close();
            EditorGUILayout.EndHorizontal();
        }
    }
}
