using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>Shared screen-Y order for world characters below hit VFX.</summary>
    internal static class ViewportVisualSorting
    {
        internal const int MaximumOrder = 199;

        internal static int OrderAt(Camera camera, Vector3 worldPosition)
        {
            float viewportY = camera.WorldToViewportPoint(worldPosition).y;
            return Mathf.Clamp(
                Mathf.RoundToInt((1f - viewportY) * MaximumOrder),
                0, MaximumOrder);
        }
    }
}
