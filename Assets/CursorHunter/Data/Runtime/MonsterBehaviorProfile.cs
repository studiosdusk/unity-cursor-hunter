using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Data
{
    /// <summary>
    /// Reusable trait contribution. Stat and presentation multipliers compose
    /// multiplicatively; movement strategy is an independent optional slot.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MonsterBehaviorProfile",
        menuName = "Cursor Hunter/Data/Monster Behavior Profile")]
    public sealed class MonsterBehaviorProfile : ScriptableObject
    {
        [SerializeField] private string profileId = "behavior.basic";

        [Header("Base stat multipliers")]
        [SerializeField, Min(0.01f)] private float healthMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float moveSpeedMultiplier = 1f;

        [Header("Presentation multipliers")]
        [SerializeField, Min(0.01f)] private float visualScaleMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float hitAreaScaleMultiplier = 1f;

        [Header("Optional movement strategy")]
        [SerializeField] private bool overridesMovementMode;
        [SerializeField] private MonsterMovementMode movementMode =
            MonsterMovementMode.BoundedWander;
        [SerializeField, Min(0.01f)] private float orbitRadius = 0.65f;
        [SerializeField, Min(0.01f)] private float orbitAngularSpeedDegrees = 90f;

        public string ProfileId => profileId ?? string.Empty;
        public float HealthMultiplier => SanitizeMultiplier(healthMultiplier);
        public float MoveSpeedMultiplier => SanitizeMultiplier(moveSpeedMultiplier);
        public float VisualScaleMultiplier => SanitizeMultiplier(visualScaleMultiplier);
        public float HitAreaScaleMultiplier => SanitizeMultiplier(hitAreaScaleMultiplier);
        public bool OverridesMovementMode => overridesMovementMode;
        public MonsterMovementMode MovementMode => System.Enum.IsDefined(
            typeof(MonsterMovementMode), movementMode)
                ? movementMode
                : MonsterMovementMode.BoundedWander;
        public float OrbitRadius => SanitizeMultiplier(orbitRadius, 0.65f);
        public float OrbitAngularSpeedDegrees =>
            SanitizeMultiplier(orbitAngularSpeedDegrees, 90f);

        private void OnValidate()
        {
            profileId = profileId == null ? string.Empty : profileId.Trim();
            healthMultiplier = SanitizeMultiplier(healthMultiplier);
            moveSpeedMultiplier = SanitizeMultiplier(moveSpeedMultiplier);
            visualScaleMultiplier = SanitizeMultiplier(visualScaleMultiplier);
            hitAreaScaleMultiplier = SanitizeMultiplier(hitAreaScaleMultiplier);
            orbitRadius = SanitizeMultiplier(orbitRadius, 0.65f);
            orbitAngularSpeedDegrees = SanitizeMultiplier(
                orbitAngularSpeedDegrees,
                90f);
            if (!System.Enum.IsDefined(typeof(MonsterMovementMode), movementMode))
            {
                movementMode = MonsterMovementMode.BoundedWander;
            }
        }

        private static float SanitizeMultiplier(float value, float fallback = 1f)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value)
                ? value
                : fallback;
        }
    }
}
