using UnityEngine;
using UnityEngine.EventSystems;

namespace CursorHunter.Progression.ResearchPrototype
{
    /// <summary>Drag and pointer-centred zoom for the isolated research test board.</summary>
    public sealed class ResearchMapNavigation : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public RectTransform viewport;
        public RectTransform board;
        public ResearchTestMap map;
        [SerializeField, Min(.1f)] private float minimumZoom = .6f;
        [SerializeField, Min(.1f)] private float maximumZoom = 1.8f;
        [SerializeField, Min(1.01f)] private float zoomStep = 1.12f;
        [SerializeField, Min(0f)] private float minimumVisible = 160f;

        public bool IsDragging { get; private set; }
        public float Zoom { get; private set; } = 1f;
        private Vector3 baseScale;
        private Vector2 previousPointer;
        private int dragPointerId;

        private void Awake()
        {
            if (board != null) baseScale = board.localScale;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || viewport == null || board == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position,
                    eventData.pressEventCamera, out previousPointer)) return;
            dragPointerId = eventData.pointerId;
            IsDragging = true;
            if (map != null) map.DismissTooltipForNavigation();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging || eventData.pointerId != dragPointerId || board == null || viewport == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position,
                    eventData.pressEventCamera, out Vector2 pointer)) return;
            Vector3 position = board.localPosition;
            Vector2 delta = pointer - previousPointer;
            board.localPosition = ClampPosition(new Vector3(position.x + delta.x, position.y + delta.y, position.z));
            previousPointer = pointer;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (IsDragging && eventData.pointerId == dragPointerId) IsDragging = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (viewport == null || board == null || eventData.scrollDelta.y == 0f ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position,
                    eventData.enterEventCamera, out Vector2 pointer)) return;
            float min = Mathf.Max(.1f, minimumZoom);
            float max = Mathf.Max(min, maximumZoom);
            float next = Mathf.Clamp(Zoom * (eventData.scrollDelta.y > 0f ? zoomStep : 1f / zoomStep), min, max);
            if (Mathf.Approximately(next, Zoom)) return;
            float ratio = next / Zoom;
            Vector3 oldPosition = board.localPosition;
            Vector3 position = new Vector3(pointer.x - (pointer.x - oldPosition.x) * ratio,
                pointer.y - (pointer.y - oldPosition.y) * ratio, oldPosition.z);
            Zoom = next;
            board.localScale = new Vector3(baseScale.x * Zoom, baseScale.y * Zoom, baseScale.z);
            board.localPosition = ClampPosition(position);
            if (map != null) map.RefreshTooltipAfterNavigation();
        }

        private Vector3 ClampPosition(Vector3 position)
        {
            Rect visible = viewport.rect;
            Rect bounds = board.rect;
            float marginX = Mathf.Min(minimumVisible, visible.width * .5f);
            float marginY = Mathf.Min(minimumVisible, visible.height * .5f);
            float minX = visible.xMin + marginX - bounds.xMax * board.localScale.x;
            float maxX = visible.xMax - marginX - bounds.xMin * board.localScale.x;
            float minY = visible.yMin + marginY - bounds.yMax * board.localScale.y;
            float maxY = visible.yMax - marginY - bounds.yMin * board.localScale.y;
            position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : (minX + maxX) * .5f;
            position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : (minY + maxY) * .5f;
            return position;
        }

        private void OnDisable() { IsDragging = false; }
    }
}
