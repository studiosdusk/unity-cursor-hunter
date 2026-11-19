using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CursorHunter.Progression
{
    /// <summary>One manually placed Stage_Nomal instance in Artifact_Canvas.</summary>
    [DisallowMultipleComponent]
    public sealed class ArtifactRelicSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private string relicId;
        [SerializeField] private Image relicIcon;
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private TextMeshProUGUI newBadge;
        [SerializeField, Min(1f)] private float hoverScale = 1.1f;
        [SerializeField, Min(0.01f)] private float hoverDuration = 0.14f;

        private ArtifactCollectionCanvas _owner;
        private Vector3 _baseScale;
        private Tween _scaleTween;
        private Image _frame;

        public string RelicId => relicId;

        public void Configure(string id, Image icon, GameObject lockedIcon)
        {
            relicId = id;
            relicIcon = icon;
            lockIcon = lockedIcon;
            if (newBadge == null)
                newBadge = transform.Find("Text_Num")?.GetComponent<TextMeshProUGUI>();
        }

        private void Awake()
        {
            _baseScale = transform.localScale;
            if (relicIcon == null)
                relicIcon = transform.Find("MainIcon")?.GetComponent<Image>();
            if (lockIcon == null)
                lockIcon = transform.Find("Icon_Lock")?.gameObject;
            if (newBadge == null)
                newBadge = transform.Find("Text_Num")?.GetComponent<TextMeshProUGUI>();
            _frame = GetComponent<Image>();
        }

        private void OnDisable()
        {
            _scaleTween?.Kill();
            _scaleTween = null;
            transform.localScale = _baseScale;
            if (_owner != null)
                _owner.Hover(this, false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PlayHover(true);
            if (_owner != null)
                _owner.Hover(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PlayHover(false);
            if (_owner != null)
                _owner.Hover(this, false);
        }

        internal void Bind(ArtifactCollectionCanvas owner)
        {
            _owner = owner;
            if (_baseScale == Vector3.zero)
                _baseScale = transform.localScale;
        }

        internal void Apply(Sprite icon, bool owned, bool isNew)
        {
            if (relicIcon != null)
            {
                relicIcon.sprite = icon;
                relicIcon.gameObject.SetActive(owned && icon != null);
            }
            if (lockIcon != null)
                lockIcon.SetActive(!owned);
            if (newBadge != null)
            {
                newBadge.text = "NEW";
                newBadge.gameObject.SetActive(owned && isNew);
            }
            if (_frame != null)
                _frame.color = owned ? Color.white : new Color(0.72f, 0.78f, 0.9f, 0.72f);
        }

        private void PlayHover(bool hovering)
        {
            _scaleTween?.Kill();
            if (_baseScale == Vector3.zero)
                _baseScale = transform.localScale;
            Vector3 target = _baseScale * (hovering ? hoverScale : 1f);
            _scaleTween = transform.DOScale(target, hoverDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetRecyclable(false);
        }
    }
}
