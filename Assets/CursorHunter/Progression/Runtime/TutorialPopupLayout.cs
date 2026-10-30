using UnityEngine;

namespace CursorHunter.Progression
{
    /// <summary>Keeps tutorial instructions attached to the top of their focused control.</summary>
    public static class TutorialPopupLayout
    {
        public static Vector2 AboveOrUpperLeft(
            Rect bounds, Rect focus, Vector2 popupSize, float gap = 18f, float margin = 14f)
        {
            Vector2 half = popupSize * 0.5f;
            float left = bounds.xMin + margin;
            float right = bounds.xMax - margin;
            float bottom = bounds.yMin + margin;
            float top = bounds.yMax - margin;
            float centeredX = Mathf.Clamp(focus.center.x, left + half.x, right - half.x);

            // Prefer directly above. Wide menu buttons must never push the
            // instructions into unrelated empty space below the menu.
            float aboveY = focus.yMax + gap + half.y;
            if (aboveY + half.y <= top)
                return new Vector2(centeredX, aboveY);

            // Near the top edge, attach to the target's upper-left corner.
            float upperLeftX = focus.xMin - gap - half.x;
            if (upperLeftX - half.x >= left)
                return new Vector2(upperLeftX,
                    Mathf.Clamp(focus.yMax, bottom + half.y, top - half.y));

            return new Vector2(centeredX,
                Mathf.Clamp(aboveY, bottom + half.y, top - half.y));
        }
    }
}
