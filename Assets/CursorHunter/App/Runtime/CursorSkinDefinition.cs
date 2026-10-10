using UnityEngine;

namespace CursorHunter.App
{
    [CreateAssetMenu(fileName = "CursorSkin", menuName = "Cursor Hunter/Cursor Skin")]
    public sealed class CursorSkinDefinition : ScriptableObject
    {
        [Tooltip("Stable ID used by the skin selection UI and future saved preferences.")]
        [SerializeField] private string skinId;
        [SerializeField] private Sprite sprite;
        [Tooltip("Diameter of the range circle / longest side of the sprite rectangle. Excludes transparent padding and decorations outside the circle.")]
        [SerializeField, Range(0.01f, 1f)] private float rangeDiameterRatio = 1f;

        public string SkinId => skinId;
        public Sprite Sprite => sprite;
        public float RangeDiameterRatio => rangeDiameterRatio;
        public bool IsValid => !string.IsNullOrWhiteSpace(skinId) && sprite != null &&
                               rangeDiameterRatio >= 0.01f && rangeDiameterRatio <= 1f;
    }
}
