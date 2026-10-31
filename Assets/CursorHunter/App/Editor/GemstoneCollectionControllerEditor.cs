using CursorHunter.App;
using UnityEditor;
using UnityEngine;

namespace CursorHunter.App.Editor
{
    [CustomEditor(typeof(GemstoneCollectionController))]
    public sealed class GemstoneCollectionControllerEditor : UnityEditor.Editor
    {
        private const string CopyKey = "CursorHunter.GemstoneCollection.Tuning";
        public override void OnInspectorGUI()
        {
            var controller = (GemstoneCollectionController)target;
            serializedObject.Update();
            EditorGUILayout.HelpBox("일반: 소 = 1개 · 중 = 5개 · 대 = 10개\n보스: Boss Burst 예산 안에서 보상을 묶어 동시 분출\n최종 착점: 수량 Text (TMP) 중심\n동작·크기 변경은 새 발사부터 반영. 연결·풀 변경은 다음 활성화부터 반영됩니다.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                foreach (string field in new[] { "combat", "worldCamera", "hudCanvas", "uiRootController", "effectRoot", "flyPrefab", "iconCatalog", "bindings" })
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(field), true);
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("settings"), true);
            foreach (string field in new[] { "previewOrigin", "previewCurrency", "previewAmount", "previewKills" })
                EditorGUILayout.PropertyField(serializedObject.FindProperty(field), true);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties(); controller.SettingsChanged();
            }
            string error = controller.ValidateConfiguration();
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
            EditorGUILayout.Space();
            if (GUILayout.Button("현재 조정값 복사"))
                SessionState.SetString(CopyKey, JsonUtility.ToJson(controller.Settings));
            using (new EditorGUI.DisabledScope(Application.isPlaying || string.IsNullOrEmpty(SessionState.GetString(CopyKey, ""))))
            {
                if (GUILayout.Button("조정값 붙여넣기 (Edit Mode에서 씬에 저장)"))
                {
                    Undo.RecordObject(controller, "Paste gemstone collection tuning");
                    JsonUtility.FromJsonOverwrite(SessionState.GetString(CopyKey, ""), controller.Settings);
                    controller.SettingsChanged(); EditorUtility.SetDirty(controller);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
                }
            }
            EditorGUILayout.HelpBox("Play Mode 변경은 종료 시 복원됩니다. 종료 전에 복사하고 Edit Mode에서 붙여넣은 뒤 씬을 저장하세요. 테스트는 실제 전투가 없을 때만 실행되며 재화를 지급하지 않습니다.", MessageType.None);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("설정한 수량 테스트")) controller.PreviewConfigured();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("43개")) controller.Preview(43);
                if (GUILayout.Button("48개")) controller.Preview(48);
                if (GUILayout.Button("80마리 × 43개")) controller.Preview(43, 80);
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("테스트 비우기") && controller.IsPreviewing) controller.CancelPresentation(false);
            }
            EditorGUILayout.LabelField("활성 / 최고 활성 / 대기 묶음", $"{controller.ActiveCount} / {controller.PeakActiveCount} / {controller.PendingBatches}");
            EditorGUILayout.LabelField("발사 소 / 중 / 대", $"{controller.EmittedSmall} / {controller.EmittedMedium} / {controller.EmittedLarge}");
            EditorGUILayout.LabelField("도착 오브젝트 / 런 중 큐 확장", $"{controller.ArrivedObjects} / {controller.QueueGrowthCount}");
            if (Application.isPlaying) Repaint();
        }
    }
}
