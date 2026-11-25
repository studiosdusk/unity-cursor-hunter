using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CursorHunter.Progression
{
    /// <summary>Animates an owned card icon and routes its hover events to the canvas.</summary>
    [DisallowMultipleComponent]
    public sealed class TraitCardOwnedIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Min(1f)] private float hoverScale = 1.08f;
        [SerializeField, Min(.01f)] private float hoverDuration = .14f;

        private TraitCardCanvas owner;
        private Vector3 baseScale;
        private Tween scaleTween;
        public string CardId { get; private set; }

        private void Awake() => baseScale = transform.localScale;

        public void Bind(TraitCardCanvas canvas, string cardId)
        {
            owner = canvas;
            CardId = cardId;
        }

        public void OnPointerEnter(PointerEventData eventData) { if (owner != null) owner.HoverOwnedIcon(this, true); }
        public void OnPointerExit(PointerEventData eventData) { if (owner != null) owner.HoverOwnedIcon(this, false); }

        internal void SetHovered(bool hovered)
        {
            if (!isActiveAndEnabled) return;
            scaleTween?.Kill();
            scaleTween = transform.DOScale(baseScale * (hovered ? hoverScale : 1f), hoverDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetRecyclable(false);
        }

        private void OnDisable()
        {
            scaleTween?.Kill();
            scaleTween = null;
            transform.localScale = baseScale;
            if (owner != null) owner.HoverOwnedIcon(this, false);
        }
    }
}
