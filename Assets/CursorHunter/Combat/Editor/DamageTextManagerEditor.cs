using UnityEditor;

namespace CursorHunter.Combat.Editor
{
    [CustomEditor(typeof(DamageTextManager))]
    public sealed class DamageTextManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            DamageTextManager manager = (DamageTextManager)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Pool statistics (current session)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Created", manager.CreatedCount);
                EditorGUILayout.IntField("Active", manager.ActiveCount);
                EditorGUILayout.IntField("Peak Active", manager.PeakActiveCount);
                EditorGUILayout.IntField("Merged", manager.MergedCount);
                EditorGUILayout.IntField("Recycled Early", manager.RecycledCount);
                EditorGUILayout.IntField("Suppressed Normal", manager.SuppressedCount);
            }
        }

        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;
    }
}
