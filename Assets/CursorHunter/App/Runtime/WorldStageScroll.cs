using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CursorHunter.App
{
    public sealed class WorldStageScroll : ScrollRect
    {
        public event Action DragStarted;
        public bool Dragging { get; private set; }
        [SerializeField, Min(0f)] private float inertiaStopSpeed = 32f;
        public bool Moving => Dragging || velocity != Vector2.zero;

        protected override void LateUpdate()
        {
            float previousX = content != null ? content.anchoredPosition.x : 0f;
            base.LateUpdate();
            if (Dragging || content == null || velocity == Vector2.zero) return;
            // A clamped ScrollRect can retain velocity even after hitting the end of the list.
            bool atBoundary = movementType == MovementType.Clamped && content.anchoredPosition.x == previousX;
            if (atBoundary || velocity.sqrMagnitude <= inertiaStopSpeed * inertiaStopSpeed)
                StopMovement();
        }

        public override void OnBeginDrag(PointerEventData data)
        {
            base.OnBeginDrag(data);
            if (data.button != PointerEventData.InputButton.Left) return;
            Dragging = true;
            data.eligibleForClick = false;
            DragStarted?.Invoke();
        }

        public override void OnEndDrag(PointerEventData data)
        {
            base.OnEndDrag(data);
            Dragging = false;
        }

        protected override void OnDisable()
        {
            Dragging = false;
            base.OnDisable();
        }
    }
}
