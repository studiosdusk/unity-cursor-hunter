using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using CursorHunter.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.Progression
{
    [DisallowMultipleComponent]
    public sealed class TraitCardCanvas : MonoBehaviour
    {
        [SerializeField] private TraitScreenController progression;
        [SerializeField] private RectTransform cardLayout;
        [SerializeField] private RectTransform ownedLayout;
        [SerializeField] private GameObject traitIconPrefab;
        [SerializeField] private Image flyingIcon;
        [SerializeField] private TraitCardTooltip ownedTooltip;
        [Header("사용자가 배치한 UI를 연결 (미연결 상태 허용)")]
        [SerializeField] private Button revealButton;
        [SerializeField] private TMP_Text pointsText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text revealCostText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text revealButtonText;
        [SerializeField] private GameObject revealButtonCostView;
        [SerializeField] private TMP_Text revealButtonCostHeading;
        [SerializeField] private TMP_Text revealButtonCostValue;
        [SerializeField] private Color revealAffordableColor = new Color32(184, 245, 138, 255);
        [SerializeField] private Color revealUnaffordableColor = new Color32(255, 120, 100, 255);
        [SerializeField] private string levelTextFormat = "{0}";
        [Header("연출 시간")]
        [SerializeField, Min(.1f)] private float revealSeconds = .65f;
        [SerializeField, Min(.1f)] private float collectionSeconds = .55f;

        private TraitCardSlot[] slots;
        private readonly Dictionary<string, Image> ownedIcons = new Dictionary<string, Image>();
        private CanvasGroup arrivingIcon;
        private TraitCardOwnedIcon hoveredOwnedIcon;
        private bool initialized, busy, available;
        public bool IsAnimating => busy;

        private void Awake() => Initialize();
        private void Initialize()
        {
            if (initialized) return;
            if (cardLayout == null || ownedLayout == null || traitIconPrefab == null || progression == null) return;
            slots = cardLayout.GetComponentsInChildren<TraitCardSlot>(true);
            foreach (var slot in slots) slot.Bind(Select);
            foreach (var label in new[] { pointsText, levelText, revealCostText, statusText, revealButtonText, revealButtonCostHeading, revealButtonCostValue })
                if (label != null) label.raycastTarget = false;
            // Authored examples remain available for editing but are not owned cards.
            foreach (Transform placeholder in ownedLayout) placeholder.gameObject.SetActive(false);
            if (flyingIcon != null) flyingIcon.gameObject.SetActive(false);
            initialized = true;
        }

        private void OnEnable()
        {
            Initialize();
            if (!initialized) return;
            progression.TraitCardStateChanged += Refresh;
            progression.TraitCardProgressReset += HandleProgressReset;
            if (revealButton != null) revealButton.onClick.AddListener(RevealCards);
            try { available = progression.PrepareTraitCards(); }
            catch (Exception exception)
            {
                available = false;
                Debug.LogError("Trait card state could not be loaded: " + exception.Message, this);
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (progression != null)
            {
                progression.TraitCardStateChanged -= Refresh;
                progression.TraitCardProgressReset -= HandleProgressReset;
            }
            if (revealButton != null) revealButton.onClick.RemoveListener(RevealCards);
            StopPresentation();
        }

        private void HandleProgressReset()
        {
            StopPresentation();
            available = true;
        }

        private void StopPresentation()
        {
            HideOwnedTooltip();
            StopAllCoroutines();
            busy = false;
            if (flyingIcon != null) flyingIcon.gameObject.SetActive(false);
            if (arrivingIcon != null)
            {
                arrivingIcon.alpha = 1;
                arrivingIcon.blocksRaycasts = true;
            }
            arrivingIcon = null;
        }

        /// <summary>Also available for an authored Button's OnClick event.</summary>
        public void RevealCards()
        {
            if (!initialized || busy || !isActiveAndEnabled) return;
            if (!available)
            {
                try { available = progression.PrepareTraitCards(); }
                catch (Exception) { ShowFailure(); return; }
                if (available) Refresh();
            }
            if (!available || !progression.TraitCards.CanReveal) { Refresh(); return; }
            busy = true;
            if (!progression.RevealTraitCards())
            {
                busy = false; Refresh(); ShowFailure(); return;
            }
            RefreshCounters();
            StartCoroutine(RevealAnimation());
        }

        private IEnumerator RevealAnimation()
        {
            foreach (var slot in slots) if (slot.CardId != null) slot.BeginReveal();
            float elapsed = 0;
            while (elapsed < revealSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                foreach (var slot in slots)
                    if (slot.CardId != null) slot.RevealFrame(Mathf.Clamp01(elapsed / revealSeconds));
                yield return null;
            }
            busy = false;
            Refresh();
        }

        private void Select(TraitCardSlot selected)
        {
            if (busy || !available || !selected.IsSelectable) return;
            string id = selected.CardId;
            Vector3 source = transform.InverseTransformPoint(selected.Icon.rectTransform.position);
            Vector2 sourceSize = SizeInCanvas(selected.Icon.rectTransform);
            Sprite sprite = selected.Icon.sprite;
            busy = true;
            if (!progression.SelectTraitCard(id))
            {
                busy = false; Refresh(); ShowFailure(); return;
            }
            RefreshCounters();
            RefreshOwned();
            StartCoroutine(CollectionAnimation(selected, ownedIcons[id], sprite, source, sourceSize));
        }

        private IEnumerator CollectionAnimation(TraitCardSlot selected, Image destination,
            Sprite sprite, Vector3 source, Vector2 sourceSize)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(ownedLayout);
            arrivingIcon = destination.transform.parent.GetComponent<CanvasGroup>();
            if (arrivingIcon == null) arrivingIcon = destination.transform.parent.gameObject.AddComponent<CanvasGroup>();
            if (flyingIcon != null)
            {
                arrivingIcon.alpha = 0;
                arrivingIcon.blocksRaycasts = false;
                flyingIcon.sprite = sprite;
                flyingIcon.preserveAspect = true;
                flyingIcon.gameObject.SetActive(true);
                flyingIcon.transform.SetAsLastSibling();
                flyingIcon.color = new Color(1, 1, 1, 0);
            }
            Vector2 targetSize = SizeInCanvas(destination.rectTransform);
            float elapsed = 0;
            while (elapsed < collectionSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / collectionSeconds);
                // Let the selected card flash briefly before its icon leaves for the collection.
                float flight = Mathf.InverseLerp(.18f, 1, t);
                float eased = Mathf.SmoothStep(0, 1, flight);
                foreach (var slot in slots) slot.SelectionFrame(t, slot == selected);
                if (flyingIcon != null)
                {
                    selected.Icon.enabled = flight <= 0;
                    flyingIcon.color = new Color(1, 1, 1, flight > 0 ? 1 : 0);
                    flyingIcon.rectTransform.localPosition = Vector3.Lerp(source,
                        transform.InverseTransformPoint(destination.rectTransform.position), eased) +
                        Vector3.up * (Mathf.Sin(flight * Mathf.PI) * 50);
                    flyingIcon.rectTransform.sizeDelta = Vector2.Lerp(sourceSize, targetSize, eased);
                }
                yield return null;
            }
            if (flyingIcon != null) flyingIcon.gameObject.SetActive(false);
            arrivingIcon.alpha = 1;
            arrivingIcon.blocksRaycasts = true;
            arrivingIcon = null;
            busy = false;
            Refresh();
        }

        private Vector2 SizeInCanvas(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 size = transform.InverseTransformVector(corners[2] - corners[0]);
            return new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
        }

        public void Refresh()
        {
            if (!initialized || !isActiveAndEnabled || busy) return;
            if (!available)
            {
                foreach (var slot in slots) slot.Display(null, false, false);
                if (revealButton != null) revealButton.interactable = true; // retry a failed initial save
                ShowFailure();
                return;
            }
            var state = progression.TraitCards;
            var candidates = state.CandidateIds;
            for (int i = 0; i < slots.Length; i++)
                slots[i].Display(i < candidates.Count ? progression.GetTraitCard(candidates[i]) : null,
                    state.Revealed, state.Revealed);
            RefreshOwned();
            RefreshCounters();
        }

        private void RefreshCounters()
        {
            var state = progression.TraitCards;
            if (pointsText != null) pointsText.text = state.Points.ToString(CultureInfo.InvariantCulture);
            if (levelText != null) levelText.text = string.Format(CultureInfo.InvariantCulture, levelTextFormat, state.Level);
            if (revealCostText != null) revealCostText.text = state.Complete ? "—" : state.RevealCost.ToString();
            if (revealButton != null) revealButton.interactable = !busy && state.CanReveal;
            bool showCost = !state.Complete && !busy && !state.Revealed;
            bool hasCostView = revealButtonCostView != null && revealButtonCostHeading != null && revealButtonCostValue != null;
            if (hasCostView)
            {
                revealButtonCostValue.text = state.RevealCost.ToString(CultureInfo.InvariantCulture);
                Color costColor = state.CanReveal ? revealAffordableColor : revealUnaffordableColor;
                revealButtonCostHeading.color = costColor;
                revealButtonCostValue.color = costColor;
                revealButtonCostView.SetActive(showCost);
            }
            if (revealButtonText != null)
            {
                revealButtonText.gameObject.SetActive(!hasCostView || !showCost);
                revealButtonText.text = state.Complete ? "모든 특성 획득" :
                    busy ? (state.Revealed ? "공개 중…" : "획득 중…") :
                    state.Revealed ? "카드를 선택하세요" :
                    "특성 카드 공개\n" + state.RevealCost.ToString(CultureInfo.InvariantCulture);
            }
            if (statusText != null)
                statusText.text = state.Complete ? "모든 특성을 획득했습니다." :
                    state.Revealed ? "카드를 눌러 특성을 획득하세요." :
                    state.Points < state.RevealCost ? "특성 포인트가 부족합니다." : "";
        }

        private void RefreshOwned()
        {
            var owned = progression.TraitCards.OwnedIds;
            foreach (var pair in ownedIcons)
                pair.Value.transform.parent.gameObject.SetActive(Contains(owned, pair.Key));
            foreach (string id in owned)
            {
                if (ownedIcons.TryGetValue(id, out var existing))
                {
                    existing.transform.parent.gameObject.SetActive(true);
                    existing.transform.parent.SetAsLastSibling();
                    continue;
                }
                var view = Instantiate(traitIconPrefab, ownedLayout, false);
                view.name = "Owned_" + id;
                foreach (string childName in new[] { "Count", "Notify_New", "Focus" })
                {
                    var child = view.transform.Find(childName);
                    if (child != null) child.gameObject.SetActive(false);
                }
                var icon = view.transform.Find("ItemIcon").GetComponent<Image>();
                icon.sprite = progression.GetTraitCard(id).presentation.icon;
                icon.preserveAspect = true;
                foreach (var graphic in view.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                var hitArea = view.GetComponent<Image>();
                if (hitArea != null) hitArea.raycastTarget = true;
                var ownedIcon = view.GetComponent<TraitCardOwnedIcon>();
                if (ownedIcon != null) ownedIcon.Bind(this, id);
                ownedIcons.Add(id, icon);
            }
        }

        internal void HoverOwnedIcon(TraitCardOwnedIcon icon, bool entered)
        {
            if (!entered)
            {
                if (hoveredOwnedIcon == icon) HideOwnedTooltip();
                else if (icon != null) icon.SetHovered(false);
                return;
            }
            if (!isActiveAndEnabled || !available || icon == null ||
                !Contains(progression.TraitCards.OwnedIds, icon.CardId) ||
                (arrivingIcon != null && arrivingIcon.gameObject == icon.gameObject && arrivingIcon.alpha < 1)) return;
            var definition = progression.GetTraitCard(icon.CardId);
            if (definition == null) return;
            if (hoveredOwnedIcon != null && hoveredOwnedIcon != icon) hoveredOwnedIcon.SetHovered(false);
            hoveredOwnedIcon = icon;
            icon.SetHovered(true);
            if (ownedTooltip != null) ownedTooltip.Show(definition, icon.transform as RectTransform);
        }

        private void HideOwnedTooltip()
        {
            if (hoveredOwnedIcon != null) hoveredOwnedIcon.SetHovered(false);
            hoveredOwnedIcon = null;
            if (ownedTooltip != null) ownedTooltip.Hide();
        }

        private static bool Contains(IReadOnlyList<string> ids, string target)
        {
            foreach (string id in ids) if (id == target) return true;
            return false;
        }

        private void ShowFailure()
        {
            if (statusText != null) statusText.text = "특성 정보를 저장하거나 불러오지 못했습니다. 다시 시도해주세요.";
        }
    }
}
