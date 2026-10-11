using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CursorHunter.App.Editor
{
    [CustomEditor(typeof(CursorSkinController))]
    public sealed class CursorSkinControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty selected = serializedObject.FindProperty("selectedSkin");
            SerializedProperty skins = serializedObject.FindProperty("availableSkins");
            var choices = new List<CursorSkinDefinition>();
            var labels = new List<string>();
            var ids = new HashSet<string>();
            bool invalidCatalog = false;

            for (int i = 0; i < skins.arraySize; i++)
            {
                var skin = skins.GetArrayElementAtIndex(i).objectReferenceValue as CursorSkinDefinition;
                if (skin == null) continue;
                invalidCatalog |= !skin.IsValid || !ids.Add(skin.SkinId);
                choices.Add(skin);
                labels.Add(skin.name);
            }

            var current = selected.objectReferenceValue as CursorSkinDefinition;
            int index = choices.IndexOf(current);
            if (index < 0)
            {
                choices.Add(current);
                labels.Add(current != null ? current.name + " (not in Available Skins)" : "Select a skin...");
                index = choices.Count - 1;
            }

            EditorGUI.BeginChangeCheck();
            int next = EditorGUILayout.Popup("Selected Skin", index, labels.ToArray());
            if (EditorGUI.EndChangeCheck()) selected.objectReferenceValue = choices[next];

            DrawPropertiesExcluding(serializedObject, "m_Script", "selectedSkin");
            if (serializedObject.ApplyModifiedProperties())
            {
                var controller = (CursorSkinController)target;
                SpriteRenderer renderer = controller.VisualRenderer;
                if (renderer != null)
                {
                    Undo.RecordObjects(new Object[] { renderer, renderer.transform }, "Change Cursor Skin");
                }

                controller.RefreshSkin();
                if (renderer != null)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.transform);
                }

                SceneView.RepaintAll();
            }

            EditorGUILayout.HelpBox(
                "Selected Skin에서 이미지를 선택합니다. 새 Cursor Skin 에셋을 Available Skins에 추가하면 선택지가 늘어납니다. 스킨 교체는 공격 범위를 변경하지 않습니다.",
                MessageType.Info);
            if (invalidCatalog)
            {
                EditorGUILayout.HelpBox("스킨마다 고유한 Skin Id와 Sprite가 필요합니다.", MessageType.Warning);
            }
        }
    }
}
