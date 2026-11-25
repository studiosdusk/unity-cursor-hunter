using CursorHunter.Data;
using UnityEditor;

namespace CursorHunter.Progression.Editor
{
    [CustomEditor(typeof(TraitCardDefinition))]
    internal sealed class TraitCardDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("stableId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("presentation"), true);
            SerializedProperty gameplay = serializedObject.FindProperty("gameplay");
            SerializedProperty kind = gameplay.FindPropertyRelative("effectKind");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("게임 효과", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(kind);
            string valueName = ((TraitCardEffectKind)kind.enumValueIndex) switch
            {
                TraitCardEffectKind.BattleRewardMultiplier => "rewardMultiplier",
                TraitCardEffectKind.TreasureChestAtBattleStart => "treasureChest",
                TraitCardEffectKind.PeriodicCursorRangeBoost => "cursorRange",
                TraitCardEffectKind.NormalMonsterExperienceBonus => "normalMonsterExperience",
                _ => null
            };
            if (valueName != null) EditorGUILayout.PropertyField(gameplay.FindPropertyRelative(valueName), true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
