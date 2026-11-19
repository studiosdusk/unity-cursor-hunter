using System.Collections.Generic;
using System.Globalization;
using CursorHunter.Data;
using CursorHunter.Progression;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace CursorHunter.App
{
    public sealed class WorldStageScreen : MonoBehaviour
    {
        [SerializeField] private WorldStageCard cardPrefab;
        [SerializeField] private WorldStageScroll scroll;
        [SerializeField] private Button back, home, help;
        [SerializeField] private TMP_Text selectedName, points, backLabel, homeLabel, helpLabel;
        [SerializeField] private RectTransform tooltip;
        [SerializeField] private TMP_Text tooltipTitle, tooltipBody;
        [SerializeField] private TMP_Text message;
        private readonly List<WorldStageCard> cards = new();
        private TraitScreenController progression;
        private ProgressionTopMenu menu;
        private WorldStageCard open;
        private StringTable local, korean;
        private readonly Dictionary<TMP_Text, (TMP_FontAsset font, Material material)> authoredType = new();
        private readonly Vector3[] anchorCorners = new Vector3[4];
        private string messageKey;
        private Quaternion tooltipRotation;
        private Tween tooltipSway;
        private const float HoverResumeDelay = .08f;
        private float resumeHoverAt, lastContentX;
        private bool restoreHover;
        internal bool DeferMouseHover()
        {
            // At a clamped end inertia may block pointer-enter without changing the content position.
            if (scroll.Moving) restoreHover = true;
            return restoreHover;
        }

        public void Connect(TraitScreenController state, ProgressionTopMenu navigation)
        {
            progression = state;
            menu = navigation;
            if (isActiveAndEnabled && cards.Count == 0) Open();
        }

        private void Awake()
        {
            back.onClick.AddListener(() => menu.LeaveWorld());
            home.onClick.AddListener(() => menu.ShowResearch());
            help.onClick.AddListener(() => ShowMessage(messageKey == "instructions" ? null : "instructions"));
            tooltipRotation = tooltip.localRotation;
            scroll.DragStarted += OnScroll;
            scroll.onValueChanged.AddListener(OnContentMoved);
        }

        private void OnEnable()
        {
            if (progression != null) Open();
        }

        private void Open()
        {
            if (cards.Count == 0)
            {
                var stages = progression.WorldStages;
                for (int i = 0; i < stages.Count; i++)
                {
                    var card = Instantiate(cardPrefab, scroll.content, false);
                    card.Bind(this, stages[i], i == stages.Count - 1);
                    card.HidePurchase();
                    cards.Add(card);
                }
                foreach (var label in GetComponentsInChildren<TMP_Text>(true))
                    authoredType[label] = (label.font, label.fontSharedMaterial);
            }
            progression.TraitCardStateChanged += Refresh;
            LocalizationCatalog.Changed += ChangeLanguage;
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            ChangeLanguage();
            Canvas.ForceUpdateCanvases();
            var current = cards.Find(c => c.Stage == progression.SelectedWorldStage);
            if (current != null && scroll.content.rect.width > scroll.viewport.rect.width)
            {
                float x = scroll.content.InverseTransformPoint(current.transform.position).x - scroll.content.rect.xMin;
                scroll.horizontalNormalizedPosition = Mathf.Clamp01((x - scroll.viewport.rect.width * .5f) /
                    (scroll.content.rect.width - scroll.viewport.rect.width));
            }
            lastContentX = scroll.content.anchoredPosition.x;
        }

        private void OnDisable()
        {
            restoreHover = false;
            if (progression != null) progression.TraitCardStateChanged -= Refresh;
            LocalizationCatalog.Changed -= ChangeLanguage;
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            Dismiss();
        }

        private void OnLocaleChanged(Locale locale) => ChangeLanguage();

        private void ChangeLanguage()
        {
            var db = LocalizationSettings.StringDatabase;
            var locales = LocalizationSettings.AvailableLocales;
            korean = db.GetTable("WorldStage", locales.GetLocale("ko"));
            Locale locale = null;
            foreach (string code in LocaleCode.Fallbacks(LocalizationCatalog.Language))
            {
                locale = locales.GetLocale(code);
                if (locale != null) break;
            }
            local = locale == null || locale.Identifier.Code == "ko" ? korean : db.GetTable("WorldStage", locale);
            var settings = Resources.Load<GameTypographySettings>("GameData/TypographySettings");
            var localeFont = settings != null && settings.GetSourceFont(LocalizationCatalog.Language) != settings.sourceFont
                ? LocalizedTypography.GetFont(null) : null;
            foreach (var entry in authoredType)
            {
                var font = localeFont != null ? localeFont : entry.Value.font;
                if (entry.Key.font == font) continue;
                entry.Key.font = font;
                if (localeFont == null) entry.Key.fontSharedMaterial = entry.Value.material;
            }
            Refresh();
        }

        private string Text(string key) => local?.GetEntry(key)?.Value ?? korean?.GetEntry(key)?.Value ?? string.Empty;
        private string Ui(string key) => Text("stage.ui." + key);
        private string Format(string key, object value) => string.Format(CultureInfo.InvariantCulture, Ui(key), value);

        private void Refresh()
        {
            var selected = progression.SelectedWorldStage;
            selectedName.text = Format("selected", Text(selected.textKey + ".name"));
            points.text = progression.TraitCards.Points.ToString(CultureInfo.InvariantCulture);
            backLabel.text = LocalizationCatalog.Get("ui.back", string.Empty);
            homeLabel.text = Ui("home");
            helpLabel.text = Ui("help");
            foreach (var card in cards)
            {
                bool unlocked = progression.IsStageUnlocked(card.Stage);
                card.Refresh(unlocked, selected == card.Stage, unlocked ? Text(card.Stage.textKey + ".name") : Ui("locked_title"));
            }
            if (open != null)
            {
                bool unlocked = progression.IsStageUnlocked(open.Stage);
                if (unlocked != tooltip.gameObject.activeSelf) Dismiss();
                else if (unlocked) RefreshInfo(open);
                else RefreshPurchase(open);
            }
            if (messageKey != null) message.text = Ui(messageKey);
        }

        public void Choose(WorldStageCard card)
        {
            if (scroll.Moving) return;
            if (!progression.IsStageUnlocked(card.Stage)) { ShowPurchase(card); return; }
            Dismiss();
            if (!progression.TryChooseStage(card.Stage, false)) ShowMessage("save_failed");
        }

        public void Purchase(WorldStageCard card)
        {
            if (scroll.Moving || open != card || tooltip.gameObject.activeSelf || !progression.CanUnlockStage(card.Stage)) return;
            if (progression.TryChooseStage(card.Stage, true)) Dismiss();
            else ShowMessage("save_failed");
        }

        public void ToggleInfo(WorldStageCard card)
        {
            if (open == card)
                Dismiss();
            else ShowInfo(card);
        }

        public void ShowPurchase(WorldStageCard card)
        {
            if (scroll.Moving || progression.IsStageUnlocked(card.Stage) || open == card) return;
            Dismiss();
            open = card;
            RefreshPurchase(card);
        }

        private void RefreshPurchase(WorldStageCard card)
        {
            var stage = card.Stage;
            bool pointsMissing = progression.IsStageUnlocked(stage.prerequisite) && progression.TraitCards.Points < stage.pointCost;
            card.ShowPurchase(stage.pointCost, progression.CanUnlockStage(stage), pointsMissing ? Ui("insufficient_points") : "");
        }

        private void RefreshInfo(WorldStageCard card)
        {
            var stage = card.Stage;
            tooltipTitle.text = Text(stage.textKey + ".name");
            tooltipBody.text = Format("monster_xp", stage.monsterExperienceMultiplier.ToString("F2", CultureInfo.InvariantCulture)) +
                "\n" + Format("monster_hp", stage.monsterHealthMultiplier.ToString("F2", CultureInfo.InvariantCulture));
        }

        public void ShowInfo(WorldStageCard card)
        {
            if (scroll.Moving || !progression.IsStageUnlocked(card.Stage) || open == card) return;
            Dismiss();
            open = card;
            RefreshInfo(card);
            tooltip.gameObject.SetActive(true);
            var parent = (RectTransform)tooltip.parent;
            card.InfoAnchor.GetWorldCorners(anchorCorners);
            Vector2 min = parent.InverseTransformPoint(anchorCorners[0]);
            Vector2 max = parent.InverseTransformPoint(anchorCorners[2]);
            Rect bounds = parent.rect, panel = tooltip.rect;
            const float gap = 24f;
            float x = max.x + gap - panel.xMin;
            if (x + panel.xMax > bounds.xMax - gap) x = min.x - gap - panel.xMax;
            tooltip.localPosition = new Vector3(Mathf.Clamp(x, bounds.xMin + gap - panel.xMin, bounds.xMax - gap - panel.xMax),
                Mathf.Clamp((min.y + max.y) * .5f, bounds.yMin + gap - panel.yMin, bounds.yMax - gap - panel.yMax), 0);
            tooltipSway = TooltipSway.Play(tooltip, tooltipRotation);
        }

        public void Leave(WorldStageCard card)
        {
            if (open == card) Dismiss();
        }

        private void Update()
        {
            if (open == null && messageKey == null) return;
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            Vector2 pos = pointer.position.ReadValue();
            var canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (open != null)
            {
                var anchor = tooltip.gameObject.activeSelf ? open.InfoAnchor : (RectTransform)open.transform;
                if (RectTransformUtility.RectangleContainsScreenPoint(anchor, pos, cam)) return;
            }
            if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)help.transform, pos, cam)) return;
            Dismiss();
        }

        private void ShowMessage(string key)
        {
            Dismiss();
            messageKey = key;
            if (key == null) return;
            message.text = Ui(key);
            message.transform.parent.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (!restoreHover || scroll.Moving || Time.unscaledTime < resumeHoverAt) return;
            restoreHover = false;
            // uGUI keeps pointer enter/exit state current while content moves beneath a stationary mouse.
            if (Pointer.current is Mouse || Pointer.current is Pen)
                foreach (var card in cards) card.RestoreMouseHover();
        }

        private void OnContentMoved(Vector2 _)
        {
            float x = scroll.content.anchoredPosition.x;
            if (Mathf.Approximately(x, lastContentX)) return;
            lastContentX = x;
            OnScroll();
        }

        private void OnScroll()
        {
            restoreHover = true;
            resumeHoverAt = Time.unscaledTime + HoverResumeDelay;
            Dismiss();
            foreach (var card in cards) card.ResetHoverScales();
        }

        private void Dismiss()
        {
            if (open != null) open.HidePurchase();
            tooltipSway?.Kill();
            tooltipSway = null;
            tooltip.localRotation = tooltipRotation;
            open = null;
            messageKey = null;
            tooltip.gameObject.SetActive(false);
            message.transform.parent.gameObject.SetActive(false);
        }
    }
}
