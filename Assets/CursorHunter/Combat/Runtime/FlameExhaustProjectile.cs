using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>A piercing copy of the current cursor with its own hit history.</summary>
    [DisallowMultipleComponent]
    public sealed class FlameExhaustProjectile : MonoBehaviour
    {
        [SerializeField] private CircleCollider2D hitArea;
        [SerializeField] private SpriteRenderer visualRenderer;

        private readonly HashSet<MonsterCombatTarget> _hitTargets =
            new HashSet<MonsterCombatTarget>();
        private readonly List<RaycastHit2D> _sweepHits = new List<RaycastHit2D>(128);
        private readonly List<Collider2D> _overlaps = new List<Collider2D>(128);
        private FlameExhaustSkillController _owner;
        private RunId _runId;
        private Vector2 _direction;
        private float _lastTickAt;
        private float _expiresAt;
        private float _speed;
        private float _areaRadiusMultiplier = 1f;
        private bool _active;

        public bool IsActive => _active;
        internal bool HasVisualAndHitArea => hitArea != null && visualRenderer != null;

        internal void Activate(FlameExhaustSkillController owner, RunId runId,
            CircleCollider2D sourceHitArea, SpriteRenderer sourceVisual,
            Vector2 direction, float now, float duration, float speed,
            float visualSizeRatio, float hitRadiusMultiplier,
            float areaRadiusMultiplier = 1f)
        {
            _owner = owner;
            _runId = runId;
            _direction = direction;
            _lastTickAt = now;
            _expiresAt = now + duration;
            _speed = speed;
            _areaRadiusMultiplier = Mathf.Max(0.01f, areaRadiusMultiplier);
            _hitTargets.Clear();
            _active = true;

            Bounds sourceBounds = sourceHitArea.bounds;
            float visualRatio = Mathf.Max(0.01f, visualSizeRatio);
            transform.position = sourceBounds.center;
            hitArea.radius = Mathf.Max(0.01f,
                Mathf.Max(sourceBounds.extents.x, sourceBounds.extents.y) *
                Mathf.Max(0.01f, hitRadiusMultiplier) *
                _areaRadiusMultiplier);
            visualRenderer.sprite = sourceVisual.sprite;
            visualRenderer.color = sourceVisual.color;
            visualRenderer.sharedMaterial = sourceVisual.sharedMaterial;
            visualRenderer.sortingLayerID = sourceVisual.sortingLayerID;
            visualRenderer.sortingOrder = sourceVisual.sortingOrder;
            visualRenderer.flipX = sourceVisual.flipX;
            visualRenderer.flipY = sourceVisual.flipY;
            visualRenderer.transform.localScale =
                sourceVisual.transform.lossyScale * visualRatio;
            visualRenderer.transform.localPosition =
                (sourceVisual.transform.position - sourceBounds.center) * visualRatio;
            gameObject.SetActive(true);
        }

        internal bool Tick(float now, ContactFilter2D filter)
        {
            if (!_active || _owner == null || !_owner.IsCurrentRun(_runId))
                return false;

            float until = Mathf.Min(now, _expiresAt);
            float delta = Mathf.Max(0f, until - _lastTickAt);
            _lastTickAt = until;
            Vector2 start = hitArea.bounds.center;
            Vector2 movement = _direction * (_speed * delta);
            float distance = movement.magnitude;
            float radius = hitArea.radius;
            if (distance > 0f)
            {
                _sweepHits.Clear();
                Physics2D.CircleCast(start, radius, _direction, filter,
                    _sweepHits, distance);
                for (int i = 0; i < _sweepHits.Count; i++)
                    TryHit(_sweepHits[i].collider);
                transform.position += (Vector3)movement;
            }

            _overlaps.Clear();
            Physics2D.OverlapCircle((Vector2)transform.position, radius,
                filter, _overlaps);
            for (int i = 0; i < _overlaps.Count; i++)
                TryHit(_overlaps[i]);

            return now < _expiresAt;
        }

        private void TryHit(Collider2D collider)
        {
            if (collider == null || _owner == null) return;
            MonsterCombatTarget target = collider.GetComponentInParent<MonsterCombatTarget>();
            if (target == null || _hitTargets.Contains(target)) return;
            if (_owner.Hit(_runId, target)) _hitTargets.Add(target);
        }

        internal void RefreshBalance(float now, FlameExhaustRuntimeStat previous,
            FlameExhaustRuntimeStat next, CircleCollider2D sourceHitArea,
            SpriteRenderer sourceVisual)
        {
            if (!_active) return;
            _expiresAt = SkillBalanceMath.Retime(_expiresAt, now,
                previous.ProjectileDurationSeconds, next.ProjectileDurationSeconds);
            _speed = next.SpeedWorldUnitsPerSecond;
            RefreshGeometry(sourceHitArea, sourceVisual, next.CursorSizeRatio,
                next.HitRadiusMultiplier, _areaRadiusMultiplier);
        }

        internal void RefreshGeometry(CircleCollider2D sourceHitArea,
            SpriteRenderer sourceVisual, float cursorSizeRatio,
            float hitRadiusMultiplier, float areaRadiusMultiplier = 1f)
        {
            if (!_active) return;
            _areaRadiusMultiplier = Mathf.Max(0.01f, areaRadiusMultiplier);
            Bounds sourceBounds = sourceHitArea.bounds;
            hitArea.radius = Mathf.Max(0.01f,
                Mathf.Max(sourceBounds.extents.x, sourceBounds.extents.y) *
                hitRadiusMultiplier * _areaRadiusMultiplier);
            visualRenderer.transform.localScale = sourceVisual.transform.lossyScale *
                cursorSizeRatio;
            visualRenderer.transform.localPosition =
                (sourceVisual.transform.position - sourceBounds.center) *
                cursorSizeRatio;
        }

        internal void ReturnToPool()
        {
            _active = false;
            _owner = null;
            _runId = default;
            _areaRadiusMultiplier = 1f;
            _hitTargets.Clear();
            _sweepHits.Clear();
            _overlaps.Clear();
            if (visualRenderer != null) visualRenderer.sprite = null;
            gameObject.SetActive(false);
        }
    }
}
