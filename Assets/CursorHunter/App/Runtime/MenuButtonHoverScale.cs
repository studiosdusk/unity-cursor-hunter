using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CursorHunter.App
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class MenuButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Min(1f)] private float hoverScale = 1.08f;
        [SerializeField, Min(0.01f)] private float hoverDuration = 0.14f;

        private Vector3 baseScale;
        private Tween scaleTween;

        private void OnEnable() => baseScale = transform.localScale;

        private void OnDisable()
        {
            scaleTween?.Kill();
            scaleTween = null;
            transform.localScale = baseScale;
        }

        public void OnPointerEnter(PointerEventData eventData) => SetHovered(eventData, true);
        public void OnPointerExit(PointerEventData eventData) => SetHovered(eventData, false);

        private void SetHovered(PointerEventData eventData, bool hovered)
        {
            if (!isActiveAndEnabled || eventData == null || WorldStageButton.IsTouch(eventData)) return;
            scaleTween?.Kill();
            scaleTween = transform.DOScale(baseScale * (hovered ? hoverScale : 1f), hoverDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetRecyclable(false);
        }
    }
}
