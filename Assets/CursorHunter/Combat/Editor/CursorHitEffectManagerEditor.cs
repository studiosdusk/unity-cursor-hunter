using UnityEditor;
using UnityEngine;

namespace CursorHunter.Combat.Editor
{
    [CustomEditor(typeof(CursorHitEffectManager))]
    public sealed class CursorHitEffectManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var manager = (CursorHitEffectManager)target;
            int configuredVariants = manager.ConfiguredVariantCount;
            EditorGUILayout.HelpBox(
                "Effect Variants에서 항목을 추가·삭제·이동하세요. 빈 항목은 제외합니다. " +
                "Material Override를 비우면 프리팹의 재질을 사용합니다. " +
                "Play 중 설정을 바꾸면 현재 이펙트를 정리하고 다음 프레임에 반영합니다.", MessageType.Info);
            if (configuredVariants == 0)
                EditorGUILayout.HelpBox("재생 가능한 항목이 없습니다. 프리팹과 재질을 확인하세요.", MessageType.Warning);
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Configured Variants", configuredVariants);
                EditorGUILayout.IntField("Emitters", manager.EmitterCount);
                EditorGUILayout.IntField("Active Effects", manager.ActiveCount);
                EditorGUILayout.IntField("Peak Active", manager.PeakActiveCount);
                EditorGUILayout.IntField("Emitted", manager.EmittedCount);
                EditorGUILayout.IntField("Suppressed", manager.SuppressedCount);
            }
            if (Application.isPlaying) Repaint();
        }
    }
}
