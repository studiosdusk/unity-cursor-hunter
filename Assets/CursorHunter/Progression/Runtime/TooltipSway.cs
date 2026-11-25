using DG.Tweening;
using UnityEngine;

namespace CursorHunter.Progression
{
    public static class TooltipSway
    {
        public static Tween Play(RectTransform panel, Quaternion restRotation)
        {
            float angle = 0f;
            Tween RotateTo(float target, float duration) => DOTween.To(() => angle, value =>
            {
                angle = value;
                if (panel != null) panel.localRotation = restRotation * Quaternion.Euler(0f, 0f, angle);
            }, target, duration).SetEase(Ease.OutSine);

            return DOTween.Sequence().SetUpdate(true).SetRecyclable(false)
                .Append(RotateTo(3f, .07f)).Append(RotateTo(-3f, .11f))
                .Append(RotateTo(1.2f, .09f)).Append(RotateTo(0f, .09f));
        }
    }
}
