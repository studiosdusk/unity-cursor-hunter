using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CursorHunter.App
{
    public sealed class WorldStageButton : Button
    {
        public event Action<bool> MouseHoverChanged;
        public bool IsMouseClick { get; private set; }
        public bool IsMouseOver { get; private set; }
        private Vector3 restScale;
        private Tween scaleTween;
        private bool enlarged;

        protected override void Awake()
        {
            base.Awake();
            restScale = transform.localScale;
        }

        internal void SetHoverScale(bool hovered, float multiplier, float duration)
        {
            if (enlarged == hovered) return;
            enlarged = hovered;
            scaleTween?.Kill();
            scaleTween = transform.DOScale(restScale * (hovered ? multiplier : 1f), duration)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetRecyclable(false);
        }

        internal void ResetHoverScale()
        {
            if (scaleTween == null) return;
            scaleTween.Kill();
            scaleTween = null;
            enlarged = false;
            transform.localScale = restScale;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            IsMouseOver = false;
            ResetHoverScale();
        }

        internal static bool IsTouch(PointerEventData data) => data is ExtendedPointerEventData pointer
            ? pointer.pointerType == UIPointerType.Touch : data.pointerId >= 0;

        public override void OnPointerEnter(PointerEventData data)
        {
            base.OnPointerEnter(data);
            if (IsTouch(data)) return;
            IsMouseOver = true;
            MouseHoverChanged?.Invoke(true);
        }

        public override void OnPointerExit(PointerEventData data)
        {
            base.OnPointerExit(data);
            if (IsTouch(data)) return;
            IsMouseOver = false;
            MouseHoverChanged?.Invoke(false);
        }

        public override void OnPointerClick(PointerEventData data)
        {
            IsMouseClick = !IsTouch(data);
            try { base.OnPointerClick(data); }
            finally { IsMouseClick = false; }
        }
    }
}
