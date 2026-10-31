using System.Globalization;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.App
{
    /// <summary>Styles the authored BossSlider's labels and health feedback without editing the supplier prefab.</summary>
    public sealed class BossHealthPresenter : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField, Min(1f)] private float healthHitScale = 1.2f;
        [SerializeField, Min(0.01f)] private float healthHitDuration = 0.18f;
        private MonsterCombatTarget _target;
        private CombatRunController _combat;
        private RunId _runId;
        private long _shownHealth = -1;
        private long _shownMaxHealth = -1;
        private BossDefinition _definition;
        private Vector3 _healthTextBaseScale = Vector3.one;
        private float _healthHitRemaining;
        private Material _titleMaterial, _healthMaterial;

        private void OnEnable() { LocalizationCatalog.Changed += RefreshTitle; }

        private void Awake()
        {
            if (slider == null)
                foreach (Slider candidate in FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (candidate.name == "BossSlider") { slider = candidate; break; }
            if (slider == null) return;
            foreach (TMP_Text label in slider.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.name == "Text_Value") healthText = label;
                if (label.name == "Text_Title") titleText = label;
            }
            ApplyTypography();
            // Reserve horizontal room for large HP values and the existing hit-scale animation.
            if (healthText != null) SetHorizontalSpan(healthText.rectTransform, 0.1f, 0.9f);
            if (titleText != null) SetHorizontalSpan(titleText.rectTransform, 0.06f, 0.94f);
            if (healthText != null) _healthTextBaseScale = healthText.rectTransform.localScale;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.minValue = 0f; slider.maxValue = 1f; slider.wholeNumbers = false;
            foreach (Graphic graphic in slider.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            Hide();
        }

        public void Show(CombatRunController combat, MonsterCombatTarget target, BossDefinition definition)
        {
            Hide();
            if (slider == null || combat == null || target == null || definition == null) return;
            _combat = combat; _target = target; _runId = combat.CurrentRunId; _shownHealth = -1;
            _definition = definition;
            RefreshTitle();
            slider.gameObject.SetActive(true);
            Refresh();
        }
        private void LateUpdate()
        {
            if (_combat == null) return;
            if (!_combat.IsBossRun || _combat.CurrentRunId != _runId || _target == null) { Hide(); return; }
            AdvanceHealthHit();
            Refresh();
        }
        private void Refresh()
        {
            if (_target == null || _shownHealth == _target.CurrentHealth && _shownMaxHealth == _target.MaxHealth) return;
            bool tookDamage = _shownHealth >= 0 && _shownMaxHealth == _target.MaxHealth && _target.CurrentHealth < _shownHealth;
            _shownHealth = _target.CurrentHealth;
            _shownMaxHealth = _target.MaxHealth;
            slider.SetValueWithoutNotify((float)((double)_shownHealth / _target.MaxHealth));
            if (healthText != null) healthText.text = _shownHealth.ToString("N0", CultureInfo.InvariantCulture) +
                " / " + _target.MaxHealth.ToString("N0", CultureInfo.InvariantCulture);
            if (tookDamage && healthText != null)
            {
                // Restart from a fixed peak so rapid hits cannot compound the text scale.
                _healthHitRemaining = Mathf.Max(0.01f, healthHitDuration);
                healthText.rectTransform.localScale = _healthTextBaseScale * Mathf.Max(1f, healthHitScale);
            }
        }
        private void AdvanceHealthHit()
        {
            if (_healthHitRemaining <= 0f || healthText == null) return;
            _healthHitRemaining = Mathf.Max(0f, _healthHitRemaining - Time.unscaledDeltaTime);
            float remaining = Mathf.Clamp01(_healthHitRemaining / Mathf.Max(0.01f, healthHitDuration));
            float scale = 1f + (Mathf.Max(1f, healthHitScale) - 1f) * remaining * remaining * remaining;
            healthText.rectTransform.localScale = _healthTextBaseScale * scale;
        }
        private void RefreshTitle()
        {
            if (_definition == null) return;
            ApplyTypography();
            if (titleText != null)
            {
                titleText.text = _definition.DisplayName;
            }
        }

        private void ApplyTypography()
        {
            // Match the result screen: localized bold headings and a distinct, sturdy numeric face.
            StyleLabel(titleText, LocalizedTypography.GetFont(null), 38f, 26f, ref _titleMaterial);
            StyleLabel(healthText, LocalizedTypography.GetNumericFont(), 28f, 14f, ref _healthMaterial);
        }

        private static void StyleLabel(TMP_Text label, TMP_FontAsset font, float size, float minimum,
            ref Material material)
        {
            if (label == null || font == null) return;
            if (label.font != font || material == null)
            {
                if (material != null) Destroy(material);
                label.font = font;
                // Own the HUD material so the result screen and shared font assets stay unchanged.
                material = new Material(font.material) { name = "Boss HUD " + font.name };
                material.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(24, 25, 23, 210));
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
                label.fontSharedMaterial = material;
            }
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.fontSize = size;
            label.fontSizeMin = minimum;
            label.fontSizeMax = size;
            label.enableAutoSizing = true;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.Center;
            label.extraPadding = true;
        }

        private static void SetHorizontalSpan(RectTransform rect, float left, float right)
        {
            rect.anchorMin = new Vector2(left, rect.anchorMin.y);
            rect.anchorMax = new Vector2(right, rect.anchorMax.y);
            rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
            rect.sizeDelta = new Vector2(0f, rect.sizeDelta.y);
        }

        private void OnDestroy()
        {
            if (_titleMaterial != null) Destroy(_titleMaterial);
            if (_healthMaterial != null) Destroy(_healthMaterial);
        }
        public void Hide()
        {
            _target = null; _combat = null; _definition = null; _runId = default;
            _shownHealth = -1;
            _shownMaxHealth = -1;
            _healthHitRemaining = 0f;
            if (healthText != null) healthText.rectTransform.localScale = _healthTextBaseScale;
            if (slider != null) slider.gameObject.SetActive(false);
        }
        private void OnDisable() { LocalizationCatalog.Changed -= RefreshTitle; Hide(); }
    }
}
