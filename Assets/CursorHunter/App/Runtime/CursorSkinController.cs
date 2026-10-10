using System;
using System.Collections.Generic;
using UnityEngine;

namespace CursorHunter.App
{
    /// <summary>
    /// Changes only the cursor's visual child. The attack collider and its
    /// parent scale remain owned by the existing cursor/range controllers.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class CursorSkinController : MonoBehaviour
    {
        [SerializeField] private CircleCollider2D hitArea;
        [Tooltip("SpriteRenderer on a direct child of the attack collider object.")]
        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private CursorSkinDefinition[] availableSkins =
            Array.Empty<CursorSkinDefinition>();
        [SerializeField] private CursorSkinDefinition selectedSkin;

        [Header("Breathing")]
        [SerializeField] private bool breathingEnabled = true;
        [SerializeField, Min(0.01f)] private float breathingMinScale = 0.95f;
        [SerializeField, Min(0.01f)] private float breathingMaxScale = 1.05f;
        [Tooltip("Seconds for one complete shrink-and-expand cycle. Uses unscaled time.")]
        [SerializeField, Min(0.1f)] private float breathingPeriodSeconds = 2f;

        private float _breathingPhase;

        public CursorSkinDefinition SelectedSkin => selectedSkin;
        public IReadOnlyList<CursorSkinDefinition> AvailableSkins => availableSkins;
        public SpriteRenderer VisualRenderer => visualRenderer;

        private void OnEnable()
        {
            _breathingPhase = 0f;
            RefreshSkin();
        }

        private void LateUpdate()
        {
            if (breathingEnabled && Application.IsPlaying(gameObject))
            {
                _breathingPhase = Mathf.Repeat(
                    _breathingPhase + Time.unscaledDeltaTime / breathingPeriodSeconds, 1f);
            }

            // Also previews Inspector/asset edits and radius changes. No
            // allocations, object creation or collider writes occur here.
            RefreshSkin();
        }

        private void OnDisable()
        {
            _breathingPhase = 0f;
            RefreshSkin();
        }

        private void OnValidate()
        {
            breathingMinScale = Mathf.Max(0.01f, breathingMinScale);
            breathingMaxScale = Mathf.Max(breathingMinScale, breathingMaxScale);
            breathingPeriodSeconds = Mathf.Max(0.1f, breathingPeriodSeconds);
        }

        private float GetBreathingScale()
        {
            if (!breathingEnabled || !isActiveAndEnabled || !Application.IsPlaying(gameObject))
            {
                return 1f;
            }

            // A sine wave slows at both ends and starts at the nominal size.
            float blend = 0.5f - 0.5f * Mathf.Sin(_breathingPhase * Mathf.PI * 2f);
            return Mathf.Lerp(breathingMinScale, breathingMaxScale, blend);
        }

        /// <summary>String argument works with a Button's UnityEvent.</summary>
        public void SelectSkin(string skinId)
        {
            TrySelectSkin(skinId);
        }

        public bool TrySelectSkin(string skinId)
        {
            if (string.IsNullOrWhiteSpace(skinId) || availableSkins == null)
            {
                return false;
            }

            CursorSkinDefinition candidate = null;
            foreach (CursorSkinDefinition skin in availableSkins)
            {
                if (skin == null || skin.SkinId != skinId)
                {
                    continue;
                }

                // An ambiguous ID must not select a different skin after a reorder.
                if (candidate != null)
                {
                    return false;
                }

                candidate = skin;
            }

            if (!ApplySkin(candidate))
            {
                return false;
            }

            selectedSkin = candidate;
            return true;
        }

        public bool RefreshSkin()
        {
            return ApplySkin(selectedSkin);
        }

        private bool ApplySkin(CursorSkinDefinition skin)
        {
            if (skin == null || !skin.IsValid || hitArea == null || visualRenderer == null)
            {
                return false;
            }

            Transform visual = visualRenderer.transform;
            if (visual.parent != hitArea.transform)
            {
                // Never resize the hit area when the renderer is wired incorrectly.
                return false;
            }

            Sprite sprite = skin.Sprite;
            Vector2 spriteSize = sprite.rect.size / sprite.pixelsPerUnit;
            float diameter = Mathf.Max(spriteSize.x, spriteSize.y) * skin.RangeDiameterRatio;
            if (diameter <= 0f)
            {
                return false;
            }

            // Rebuild from the fitted size each time so the pulse never accumulates.
            float scale = hitArea.radius * 2f / diameter * GetBreathingScale();
            Vector3 localScale = new Vector3(scale, scale, 1f);
            // rect center compensates for non-centered sprite pivots as well.
            Vector2 center = (sprite.rect.size * 0.5f - sprite.pivot) / sprite.pixelsPerUnit;
            Vector3 localPosition = (Vector3)(hitArea.offset - center * scale);

            if (visualRenderer.sprite != sprite) visualRenderer.sprite = sprite;
            if (visual.localScale != localScale) visual.localScale = localScale;
            if (visual.localPosition != localPosition) visual.localPosition = localPosition;
            if (visual.localRotation != Quaternion.identity) visual.localRotation = Quaternion.identity;
            return true;
        }
    }
}
