using System.Globalization;
using CursorHunter.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CursorHunter.App
{
    public sealed class WorldStageCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private WorldStageButton mapButton, nodeButton, infoButton, unlockButton;
        [SerializeField] private Image preview;
        [SerializeField] private Sprite lockedIcon;
        [SerializeField] private GameObject checkIcon, connector;
        [SerializeField] private TMP_Text stageName, price, reason;
        [SerializeField] private Color lockedTint = new Color(.35f, .35f, .35f);
        [SerializeField] private Color lockedNodeTint = new Color(.27f, .36f, .48f);
        [SerializeField, Min(1f)] private float hoverScale = 1.08f;
        [SerializeField, Min(.01f)] private float hoverDuration = .14f;
        [SerializeField] private Color affordableColor = new Color32(184, 245, 138, 255);
        [SerializeField] private Color unavailableColor = new Color32(255, 120, 100, 255);

        private WorldStageScreen screen;
        private Sprite infoIcon;
        private Color unlockedNodeTint;
        private bool unlocked, mouseOver;
        public WorldStageDefinition Stage { get; private set; }
        public RectTransform InfoAnchor => infoButton.image.rectTransform;

        public void Bind(WorldStageScreen owner, WorldStageDefinition stage, bool last)
        {
            screen = owner;
            Stage = stage;
            unlockedNodeTint = nodeButton.image.color;
            infoIcon = infoButton.image.sprite;
            connector.SetActive(!last);
            preview.sprite = stage.preview;
            mapButton.onClick.AddListener(Choose);
            nodeButton.onClick.AddListener(Choose);
            mapButton.MouseHoverChanged += _ => RefreshMapHover();
            unlockButton.MouseHoverChanged += _ => RefreshMapHover();
            nodeButton.MouseHoverChanged += HoverNode;
            infoButton.MouseHoverChanged += HoverInfo;
            infoButton.onClick.AddListener(ActivateInfo);
            unlockButton.onClick.AddListener(() => screen.Purchase(this));
        }

        private void Choose() => screen.Choose(this);

        private void HoverInfo(bool entered)
        {
            if (!unlocked || screen.DeferMouseHover()) return;
            if (entered) screen.ShowInfo(this);
            else screen.Leave(this);
        }

        private void ActivateInfo()
        {
            if (!unlocked) screen.ShowPurchase(this);
            else if (!infoButton.IsMouseClick) screen.ToggleInfo(this);
        }

        public void Refresh(bool isUnlocked, bool selected, string name)
        {
            unlocked = isUnlocked;
            preview.color = unlocked ? Color.white : lockedTint;
            nodeButton.image.color = unlocked ? unlockedNodeTint : lockedNodeTint;
            infoButton.image.sprite = unlocked ? infoIcon : lockedIcon;
            stageName.text = name;
            checkIcon.SetActive(selected);
        }

        public void ShowPurchase(int cost, bool canBuy, string notice)
        {
            price.text = cost.ToString(CultureInfo.InvariantCulture);
            reason.text = notice;
            unlockButton.interactable = canBuy;
            price.color = canBuy ? affordableColor : unavailableColor;
            unlockButton.gameObject.SetActive(true);
            reason.gameObject.SetActive(!string.IsNullOrEmpty(notice));
        }

        public void HidePurchase()
        {
            unlockButton.gameObject.SetActive(false);
            reason.gameObject.SetActive(false);
        }

        private void RefreshMapHover()
        {
            if (!screen.DeferMouseHover())
                mapButton.SetHoverScale(mapButton.IsMouseOver || unlockButton.IsMouseOver, hoverScale, hoverDuration);
        }

        private void HoverNode(bool entered)
        {
            if (!screen.DeferMouseHover()) nodeButton.SetHoverScale(entered, hoverScale, hoverDuration);
        }

        internal void ResetHoverScales()
        {
            mapButton.ResetHoverScale();
            nodeButton.ResetHoverScale();
        }

        internal void RestoreMouseHover()
        {
            if (!mouseOver) return;
            if (!unlocked) screen.ShowPurchase(this);
            else if (infoButton.IsMouseOver) screen.ShowInfo(this);
            RefreshMapHover();
            HoverNode(nodeButton.IsMouseOver);
        }

        private void OnDisable() => mouseOver = false;

        public void OnPointerEnter(PointerEventData data)
        {
            if (WorldStageButton.IsTouch(data)) return;
            mouseOver = true;
            if (screen != null && !screen.DeferMouseHover() && !unlocked) screen.ShowPurchase(this);
        }

        public void OnPointerExit(PointerEventData data)
        {
            if (WorldStageButton.IsTouch(data)) return;
            mouseOver = false;
            if (screen != null) screen.Leave(this);
        }
    }
}
