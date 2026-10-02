using CursorHunter.Combat;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerCombatStatsRuntime))]
public sealed class PlayerCombatStatsRuntimeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox(
            "Play Mode test controls. Cursor attack stat edits are applied to the active run.",
            MessageType.Info);

        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();
    }
}
