using System.Text.RegularExpressions;
using CursorHunter.Data;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace CursorHunter.Progression
{
    /// <summary>Displays the authored relic-style tooltip using the trait card's localized text and SO values.</summary>
    [DisallowMultipleComponent]
    public sealed class TraitCardTooltip : MonoBehaviour
    {
        [SerializeField] private GameObject tooltipRoot;
        [SerializeField] private RectTransform tooltipPanel;
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI description;

        private RectTransform canvasRect;
        private Canvas parentCanvas;
        private TraitCardDefinition definition;
        private LocalizedString localizedTitle, localizedDescription;
        private Tween tween;
        private Quaternion baseRotation;
        private static readonly Regex Words = new Regex(@"\S+");

        private void Awake()
        {
            canvasRect = transform as RectTransform;
            parentCanvas = GetComponentInParent<Canvas>();
            if (tooltipPanel != null) baseRotation = tooltipPanel.localRotation;
            if (tooltipRoot != null) tooltipRoot.SetActive(false);
        }

        public void Show(TraitCardDefinition card, RectTransform anchor)
        {
            if (!isActiveAndEnabled || card == null || anchor == null || tooltipRoot == null || tooltipPanel == null ||
                title == null || description == null) return;
            Hide();
            definition = card;
            title.text = description.text = string.Empty;
            tooltipRoot.transform.SetAsLastSibling();
            tooltipRoot.SetActive(true);
            Canvas.ForceUpdateCanvases();
            PositionTooltip(anchor);
            localizedTitle = new LocalizedString("TraitCard", card.presentation.textKey + ".title");
            localizedDescription = new LocalizedString("TraitCard", card.presentation.textKey + ".description");
            localizedTitle.StringChanged += SetTitle;
            localizedDescription.StringChanged += SetDescription;
            tween = TooltipSway.Play(tooltipPanel, baseRotation);
        }

        public void Hide()
        {
            if (localizedTitle != null) localizedTitle.StringChanged -= SetTitle;
            if (localizedDescription != null) localizedDescription.StringChanged -= SetDescription;
            localizedTitle = localizedDescription = null;
            definition = null;
            tween?.Kill();
            tween = null;
            if (tooltipPanel != null) tooltipPanel.localRotation = baseRotation;
            if (tooltipRoot != null) tooltipRoot.SetActive(false);
        }

        private void OnDisable() => Hide();
        private void OnDestroy() => Hide();

        private void SetTitle(string value) { if (title != null) title.text = value ?? string.Empty; }

        private void SetDescription(string value)
        {
            if (description != null && definition != null)
                description.text = Words.Replace(TraitCardTextFormatter.Format(value, definition.gameplay), "<nobr>$0</nobr>");
        }

        private void PositionTooltip(RectTransform anchor)
        {
            if (canvasRect == null) return;
            Camera camera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? parentCanvas.worldCamera : null;
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                RectTransformUtility.WorldToScreenPoint(camera, corners[0]), camera, out Vector2 bottomLeft);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                RectTransformUtility.WorldToScreenPoint(camera, corners[2]), camera, out Vector2 topRight);
            tooltipPanel.GetWorldCorners(corners);
            Vector3 panelMin = canvasRect.InverseTransformPoint(corners[0]);
            Vector3 panelMax = canvasRect.InverseTransformPoint(corners[2]);
            float halfWidth = Mathf.Abs(panelMax.x - panelMin.x) * .5f;
            float halfHeight = Mathf.Abs(panelMax.y - panelMin.y) * .5f;
            Rect bounds = canvasRect.rect;
            const float margin = 20, gap = 24;
            float x = topRight.x + gap + halfWidth;
            if (x + halfWidth > bounds.xMax - margin) x = bottomLeft.x - gap - halfWidth;
            float y = (bottomLeft.y + topRight.y) * .5f;
            x = ClampInside(x, bounds.xMin + margin + halfWidth, bounds.xMax - margin - halfWidth, bounds.center.x);
            y = ClampInside(y, bounds.yMin + margin + halfHeight, bounds.yMax - margin - halfHeight, bounds.center.y);
            tooltipPanel.anchoredPosition = new Vector2(x, y);
        }

        private static float ClampInside(float value, float minimum, float maximum, float fallback) =>
            minimum > maximum ? fallback : Mathf.Clamp(value, minimum, maximum);
    }
}
