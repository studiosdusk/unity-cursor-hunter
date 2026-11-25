using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>Chest-only hit feedback; the ground shadow and combat hitbox stay still.</summary>
    [DisallowMultipleComponent]
    public sealed class TreasureChestHitFeedback : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer flashOverlay;
        [SerializeField, Min(0.01f)] private float durationSeconds = 0.2f;
        [SerializeField, Range(0f, 30f)] private float shakeAngleDegrees = 8f;
        [SerializeField, Range(0f, 1f)] private float flashOpacity = 0.55f;

        private MonsterCombatTarget _owner;
        private CombatRunController _combat;
        private Quaternion _restRotation;
        private float _elapsed;
        private bool _playing;
        private bool _destroyAfterHit;
        private bool _initialized;

        internal bool Initialize(MonsterCombatTarget owner, CombatRunController combat)
        {
            if (owner == null || body == null || body.sprite == null || flashOverlay == null)
                return false;

            _owner = owner;
            _combat = combat;
            _restRotation = body.transform.localRotation;
            _initialized = true;
            flashOverlay.sprite = body.sprite;
            flashOverlay.flipX = body.flipX;
            flashOverlay.flipY = body.flipY;
            ResetVisual();

            // Size still comes from the MonsterDefinition snapshot. Its center
            // follows the visible chest instead of the generic monster's feet.
            Transform hitArea = owner.transform.Find("HitArea");
            BoxCollider2D hitbox = hitArea != null ? hitArea.GetComponent<BoxCollider2D>() : null;
            if (hitbox == null) return false;
            Vector3 center = hitbox.transform.InverseTransformPoint(body.bounds.center);
            hitbox.offset = new Vector2(center.x, center.y);
            return true;
        }

        internal bool Play(bool lethal)
        {
            if (!isActiveAndEnabled || _owner == null || body == null || flashOverlay == null)
                return false;
            _elapsed = 0f;
            _playing = true;
            _destroyAfterHit = lethal;
            Render(0f);
            return true;
        }

        private void Update()
        {
            if (!_playing || _combat != null && !_combat.IsRunning) return;
            Advance(Time.deltaTime);
        }

        internal void Advance(float seconds)
        {
            if (!_playing || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            _elapsed += seconds;
            float progress = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, durationSeconds));
            Render(progress);
            if (progress < 1f) return;

            bool destroy = _destroyAfterHit;
            _playing = false;
            _destroyAfterHit = false;
            ResetVisual();
            if (destroy && _owner != null && _owner.IsDead)
                Destroy(_owner.gameObject);
        }

        private void Render(float progress)
        {
            float remaining = 1f - progress;
            float angle = Mathf.Sin(progress * Mathf.PI * 6f) * shakeAngleDegrees * remaining;
            body.transform.localRotation = _restRotation * Quaternion.Euler(0f, 0f, angle);
            flashOverlay.color = new Color(1f, 1f, 1f, flashOpacity * remaining * remaining);
        }

        private void ResetVisual()
        {
            if (!_initialized) return;
            if (body != null) body.transform.localRotation = _restRotation;
            if (flashOverlay != null) flashOverlay.color = new Color(1f, 1f, 1f, 0f);
        }

        internal void Cancel()
        {
            _playing = false;
            _destroyAfterHit = false;
            ResetVisual();
        }

        private void OnDisable()
        {
            bool removeOwner = _destroyAfterHit;
            Cancel();
            if (removeOwner && Application.isPlaying && _owner != null && _owner.IsDead &&
                _owner.gameObject.activeInHierarchy)
                Destroy(_owner.gameObject);
        }
    }
}
