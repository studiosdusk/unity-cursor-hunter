using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.Progression
{
    /// <summary>
    /// Binds the scene-authored Artifact_Canvas slots and tooltip to saved relic state.
    /// It never creates or destroys collection UI objects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArtifactCollectionCanvas : MonoBehaviour
    {
        [SerializeField] private TraitScreenController progression;
        [SerializeField] private ArtifactRelicSlot[] slots = Array.Empty<ArtifactRelicSlot>();
        [SerializeField] private GameObject tooltipRoot;
        [SerializeField] private RectTransform tooltipPanel;
        [SerializeField] private TextMeshProUGUI tooltipTitle;
        [SerializeField] private TextMeshProUGUI tooltipDescription;
        [SerializeField] private TextMeshProUGUI collectionCount;

        private RectTransform _canvasRect;
        private ArtifactRelicSlot _hoveredSlot;
        private Tween _tooltipTween;
        private Quaternion _tooltipBaseRotation = Quaternion.identity;
        private bool _initialized;

        public void Configure(
            TraitScreenController stateSource,
            ArtifactRelicSlot[] authoredSlots,
            GameObject authoredTooltipRoot,
            RectTransform authoredTooltipPanel,
            TextMeshProUGUI authoredTooltipTitle,
            TextMeshProUGUI authoredTooltipDescription,
            TextMeshProUGUI authoredCollectionCount)
        {
            progression = stateSource;
            slots = authoredSlots ?? Array.Empty<ArtifactRelicSlot>();
            tooltipRoot = authoredTooltipRoot;
            tooltipPanel = authoredTooltipPanel;
            tooltipTitle = authoredTooltipTitle;
            tooltipDescription = authoredTooltipDescription;
            collectionCount = authoredCollectionCount;
        }

        private void Awake() => Initialize();

        private void Start() => Refresh();

        private void OnEnable()
        {
            Initialize();
            if (progression != null)
                progression.RelicStateChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (progression != null)
                progression.RelicStateChanged -= Refresh;
            HideTooltip();
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            _canvasRect = transform as RectTransform;
            if (tooltipPanel != null)
                _tooltipBaseRotation = tooltipPanel.localRotation;
            if (progression == null)
                progression = FindFirstObjectByType<TraitScreenController>(FindObjectsInactive.Include);
            if (slots == null || slots.Length == 0)
                slots = GetComponentsInChildren<ArtifactRelicSlot>(true);

            foreach (ArtifactRelicSlot slot in slots)
                if (slot != null)
                    slot.Bind(this);

            if (tooltipRoot != null)
            {
                CanvasGroup group = tooltipRoot.GetComponent<CanvasGroup>();
                if (group != null)
                    group.blocksRaycasts = false;
                tooltipRoot.SetActive(false);
            }
            _initialized = true;
        }

        public void Refresh()
        {
            if (!_initialized || progression == null)
                return;

            int ownedCount = 0;
            int validCount = 0;
            foreach (ArtifactRelicSlot slot in slots)
            {
                if (slot == null || string.IsNullOrWhiteSpace(slot.RelicId))
                    continue;
                validCount++;
                if (progression.TryGetRelicView(slot.RelicId, out RelicViewState view))
                {
                    slot.Apply(view.Presentation.Icon, view.IsOwned, view.IsNew);
                    if (view.IsOwned)
                        ownedCount++;
                }
                else
                {
                    slot.Apply(null, false, false);
                }
            }

            if (collectionCount != null)
                collectionCount.SetText("보유 {0:00} / {1:00}", ownedCount, validCount);
        }

        internal void Hover(ArtifactRelicSlot slot, bool entered)
        {
            if (slot == null)
                return;

            if (!entered)
            {
                if (_hoveredSlot == slot)
                    HideTooltip();
                return;
            }

            if (progression == null || !progression.TryGetRelicView(slot.RelicId, out RelicViewState view))
            {
                HideTooltip();
                return;
            }

            bool opening = tooltipRoot != null && (!tooltipRoot.activeSelf || _hoveredSlot != slot);
            _hoveredSlot = slot;
            SetTooltipText(tooltipTitle,
                view.IsOwned ? view.Presentation.TextKey + ".title" : null,
                view.IsOwned ? string.Empty : "미획득 유물");
            SetTooltipText(tooltipDescription,
                view.IsOwned ? view.Presentation.TextKey + ".description" : null,
                view.IsOwned ? string.Empty : "???");

            PositionTooltip(slot.transform as RectTransform);
            if (tooltipRoot != null)
            {
                tooltipRoot.transform.SetAsLastSibling();
                tooltipRoot.SetActive(true);
                if (opening)
                    AnimateTooltip();
            }

            if (view.IsOwned && view.IsNew)
            {
                progression.MarkRelicSeen(slot.RelicId);
                slot.Apply(view.Presentation.Icon, true, false);
            }
        }

        private void PositionTooltip(RectTransform slotRect)
        {
            if (_canvasRect == null || tooltipPanel == null || slotRect == null)
                return;

            Vector3[] corners = new Vector3[4];
            slotRect.GetWorldCorners(corners);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, RectTransformUtility.WorldToScreenPoint(null, corners[0]), null, out Vector2 bottomLeft);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, RectTransformUtility.WorldToScreenPoint(null, corners[2]), null, out Vector2 topRight);

            Rect bounds = _canvasRect.rect;
            float halfWidth = tooltipPanel.rect.width * 0.5f;
            float halfHeight = tooltipPanel.rect.height * 0.5f;
            const float margin = 20f;
            const float gap = 24f;
            float x = topRight.x + gap + halfWidth;
            if (x + halfWidth > bounds.xMax - margin)
                x = bottomLeft.x - gap - halfWidth;
            x = Mathf.Clamp(x, bounds.xMin + margin + halfWidth, bounds.xMax - margin - halfWidth);
            float y = (bottomLeft.y + topRight.y) * 0.5f;
            y = Mathf.Clamp(y, bounds.yMin + margin + halfHeight, bounds.yMax - margin - halfHeight);
            tooltipPanel.anchoredPosition = new Vector2(x, y);
        }

        private void HideTooltip()
        {
            _hoveredSlot = null;
            StopTooltipAnimation();
            if (tooltipRoot != null && tooltipRoot.activeSelf)
                tooltipRoot.SetActive(false);
        }

        private static void SetTooltipText(TextMeshProUGUI label, string key, string fallback)
        {
            if (label == null)
                return;

            RelicLocalizedLabel localized = label.GetComponent<RelicLocalizedLabel>();
            if (localized == null)
                localized = label.gameObject.AddComponent<RelicLocalizedLabel>();

            if (string.IsNullOrWhiteSpace(key))
            {
                localized.Clear();
                label.text = fallback ?? string.Empty;
                return;
            }

            localized.Bind(key);
        }

        private void AnimateTooltip()
        {
            if (!Application.isPlaying || tooltipPanel == null)
                return;

            StopTooltipAnimation();
            _tooltipTween = TooltipSway.Play(tooltipPanel, _tooltipBaseRotation);
        }

        private void StopTooltipAnimation()
        {
            _tooltipTween?.Kill();
            _tooltipTween = null;
            if (tooltipPanel != null)
                tooltipPanel.localRotation = _tooltipBaseRotation;
        }
    }

}
