using System;
using System.Collections;
using System.Text.RegularExpressions;
using CursorHunter.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;

namespace CursorHunter.Progression
{
    /// <summary>Presentation only; the canvas commits a clicked card before animating it.</summary>
    [DisallowMultipleComponent]
    public sealed class TraitCardSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI description;
        [SerializeField] private ImageReferences visuals;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private CanvasGroup lockCover;
        [SerializeField] private UnityEngine.UI.RawImage revealBurst;
        [SerializeField] private Vector2Int burstGrid = new Vector2Int(6, 4);
        [SerializeField, Min(1)] private int burstFrames = 24;
        [SerializeField, Min(1)] private float hoverScale = 1.06f;
        [SerializeField, Min(.01f)] private float hoverSeconds = .14f;

        [Serializable]
        public sealed class ImageReferences
        {
            public UnityEngine.UI.Image icon;
        }

        private LocalizedString titleString, descriptionString;
        private TraitCardDefinition definition;
        private Action<TraitCardSlot> select;
        private bool selectable, hoverable;
        private Vector3 restingScale;
        private bool hasRestingScale;
        private Coroutine hoverAnimation;
        private static readonly Regex Words = new Regex(@"\S+");
        public string CardId => definition == null ? null : definition.stableId;
        public UnityEngine.UI.Image Icon => visuals.icon;
        public bool IsSelectable => selectable;
        public bool IsLocked => lockCover != null && lockCover.gameObject.activeSelf && lockCover.alpha > 0;
        public string DisplayedTitle => title.text;
        public string DisplayedDescription => description.text;

        public void Bind(Action<TraitCardSlot> onSelect)
        {
            select = onSelect;
            RememberScale();
        }

        public void Display(TraitCardDefinition card, bool revealed, bool allowSelection)
        {
            Unsubscribe();
            definition = card;
            selectable = card != null && revealed && allowSelection;
            hoverable = card != null && (!revealed || allowSelection);
            group.alpha = card == null ? 0 : 1;
            group.blocksRaycasts = card != null;
            group.interactable = selectable;
            title.gameObject.SetActive(card != null && revealed);
            description.gameObject.SetActive(card != null && revealed);
            visuals.icon.gameObject.SetActive(card != null && revealed);
            title.text = description.text = string.Empty;
            visuals.icon.sprite = card == null ? null : card.presentation.icon;
            visuals.icon.enabled = true;
            visuals.icon.preserveAspect = true;
            title.raycastTarget = description.raycastTarget = visuals.icon.raycastTarget = false;
            lockCover.gameObject.SetActive(card != null && !revealed);
            lockCover.alpha = 1;
            ResetHover();
            SetBurst(1);
            if (card == null || !revealed) return;
            titleString = new LocalizedString("TraitCard", card.presentation.textKey + ".title");
            descriptionString = new LocalizedString("TraitCard", card.presentation.textKey + ".description");
            titleString.StringChanged += SetTitle;
            descriptionString.StringChanged += SetDescription;
        }

        public void BeginReveal()
        {
            Display(definition, true, false);
            lockCover.gameObject.SetActive(true);
            lockCover.alpha = 1;
        }

        public void RevealFrame(float progress)
        {
            lockCover.alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.10f, .27f, progress));
            SetBurst(progress);
            if (progress >= 1) lockCover.gameObject.SetActive(false);
        }

        public void SelectionFrame(float progress, bool selected)
        {
            selectable = false;
            hoverable = false;
            group.interactable = false;
            group.alpha = selected ? 1 : 1 - Mathf.SmoothStep(0, 1, progress);
            ResetHover();
            SetBurst(selected ? Mathf.Clamp01(progress / .65f) : 1);
        }

        private void SetBurst(float progress)
        {
            if (revealBurst == null || revealBurst.texture == null) return;
            revealBurst.enabled = progress > 0 && progress < 1;
            if (!revealBurst.enabled) return;
            int columns = Mathf.Max(1, burstGrid.x), rows = Mathf.Max(1, burstGrid.y);
            int count = Mathf.Clamp(burstFrames, 1, columns * rows);
            int frame = Mathf.Min(count - 1, Mathf.FloorToInt(progress * count));
            // Half-texel inset keeps adjacent atlas frames out of bilinear filtering.
            float xInset = .5f / revealBurst.texture.width, yInset = .5f / revealBurst.texture.height;
            revealBurst.uvRect = new Rect((float)(frame % columns) / columns + xInset,
                1f - (float)(frame / columns + 1) / rows + yInset,
                1f / columns - xInset * 2, 1f / rows - yInset * 2);
        }

        private void RememberScale()
        {
            if (hasRestingScale) return;
            restingScale = transform.localScale;
            hasRestingScale = true;
        }

        private void ResetHover()
        {
            RememberScale();
            if (hoverAnimation != null) StopCoroutine(hoverAnimation);
            hoverAnimation = null;
            transform.localScale = restingScale;
        }

        private void Hover(bool entered)
        {
            if (!hoverable) return;
            RememberScale();
            if (hoverAnimation != null) StopCoroutine(hoverAnimation);
            hoverAnimation = StartCoroutine(ScaleTo(restingScale * (entered ? hoverScale : 1)));
        }

        private IEnumerator ScaleTo(Vector3 destination)
        {
            Vector3 start = transform.localScale;
            float elapsed = 0;
            while (elapsed < hoverSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / hoverSeconds);
                transform.localScale = Vector3.LerpUnclamped(start, destination, 1 - Mathf.Pow(1 - t, 3));
                yield return null;
            }
            transform.localScale = destination;
            hoverAnimation = null;
        }

        private void SetTitle(string text) { if (title != null) title.text = text; }
        private void SetDescription(string text)
        {
            if (description != null && definition != null)
                // Preserve whole Korean words locally, without changing the project's TMP settings.
                description.text = Words.Replace(TraitCardTextFormatter.Format(text, definition.gameplay), "<nobr>$0</nobr>");
        }
        private void Unsubscribe()
        {
            if (titleString != null) titleString.StringChanged -= SetTitle;
            if (descriptionString != null) descriptionString.StringChanged -= SetDescription;
            titleString = descriptionString = null;
        }
        private void OnDisable() { Unsubscribe(); selectable = hoverable = false; ResetHover(); SetBurst(1); }
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && selectable) select?.Invoke(this);
        }
        public void OnPointerEnter(PointerEventData e) => Hover(true);
        public void OnPointerExit(PointerEventData e) => Hover(false);
    }
}
