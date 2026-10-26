using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>One persistent ball with separate wall and enemy contact circles.</summary>
    [DisallowMultipleComponent]
    public sealed class MagicBallObject : MonoBehaviour
    {
        [SerializeField] private CircleCollider2D boundaryCollider;
        [SerializeField] private CircleCollider2D hitbox;

        private readonly List<RaycastHit2D> _sweepHits = new List<RaycastHit2D>(128);
        private readonly List<Collider2D> _overlaps = new List<Collider2D>(128);
        private readonly HashSet<MonsterCombatTarget> _contactsLastFrame =
            new HashSet<MonsterCombatTarget>();
        private readonly HashSet<MonsterCombatTarget> _contactsThisFrame =
            new HashSet<MonsterCombatTarget>();
        private readonly HashSet<MonsterCombatTarget> _hitsThisFrame =
            new HashSet<MonsterCombatTarget>();
        private readonly Dictionary<MonsterCombatTarget, float> _lastHitAt =
            new Dictionary<MonsterCombatTarget, float>();

        private ParticleSystem[] _particles;
        private MagicBallSkillController _owner;
        private RunId _runId;
        private Vector2 _direction;
        private Vector3 _baseScale;
        private float _lastTickAt;
        private bool _active;

        public bool IsActive => _active;
        internal bool HasHitbox => boundaryCollider != null &&
            boundaryCollider.transform == transform && boundaryCollider.enabled &&
            boundaryCollider.isTrigger && boundaryCollider.radius > 0f &&
            hitbox != null && hitbox != boundaryCollider &&
            hitbox.transform != transform &&
            hitbox.transform.IsChildOf(transform) && hitbox.enabled &&
            hitbox.isTrigger && hitbox.radius > 0f;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            if (boundaryCollider == null) boundaryCollider = GetComponent<CircleCollider2D>();
            if (hitbox == null)
            {
                CircleCollider2D[] circles = GetComponentsInChildren<CircleCollider2D>(true);
                for (int i = 0; i < circles.Length; i++)
                    if (circles[i].transform != transform)
                    {
                        hitbox = circles[i];
                        break;
                    }
            }
        }

        internal bool Activate(MagicBallSkillController owner, RunId runId,
            Rect fieldBounds, Vector2 spawnFraction, float z, Vector2 direction,
            float now, float sizeMultiplier)
        {
            _owner = owner;
            _runId = runId;
            _direction = direction;
            _lastTickAt = now;
            _contactsLastFrame.Clear();
            _contactsThisFrame.Clear();
            _hitsThisFrame.Clear();
            _lastHitAt.Clear();
            transform.localScale = _baseScale * sizeMultiplier;
            transform.position = new Vector3(fieldBounds.center.x, fieldBounds.center.y, z);
            gameObject.SetActive(true);
            Physics2D.SyncTransforms();
            if (!TryGetMovementBounds(fieldBounds, out Rect movementBounds))
            {
                ReturnToPool();
                return false;
            }
            transform.position = new Vector3(
                Mathf.Lerp(movementBounds.xMin, movementBounds.xMax, spawnFraction.x),
                Mathf.Lerp(movementBounds.yMin, movementBounds.yMax, spawnFraction.y), z);
            _active = true;
            SetVisualPaused(false, true);
            return true;
        }

        private bool TryGetMovementBounds(Rect fieldBounds, out Rect movementBounds)
        {
            Bounds bodyBounds = boundaryCollider.bounds;
            Vector2 bodyOffset = (Vector2)bodyBounds.center - (Vector2)transform.position;
            float minX = fieldBounds.xMin + bodyBounds.extents.x - bodyOffset.x;
            float maxX = fieldBounds.xMax - bodyBounds.extents.x - bodyOffset.x;
            float minY = fieldBounds.yMin + bodyBounds.extents.y - bodyOffset.y;
            float maxY = fieldBounds.yMax - bodyBounds.extents.y - bodyOffset.y;
            movementBounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return minX < maxX && minY < maxY;
        }

        internal void Tick(float now, Rect fieldBounds, ContactFilter2D filter,
            float speed, float sameTargetCooldown)
        {
            if (!_active || _owner == null || !_owner.IsCurrentRun(_runId)) return;
            if (!TryGetMovementBounds(fieldBounds, out Rect movementBounds))
            {
                _lastTickAt = now;
                return;
            }
            Bounds attackBounds = hitbox.bounds;
            Vector2 attackOffset = (Vector2)attackBounds.center - (Vector2)transform.position;
            float attackRadius = Mathf.Max(attackBounds.extents.x, attackBounds.extents.y);
            float minX = movementBounds.xMin;
            float maxX = movementBounds.xMax;
            float minY = movementBounds.yMin;
            float maxY = movementBounds.yMax;

            Vector2 position = transform.position;
            position.x = Mathf.Clamp(position.x, minX, maxX);
            position.y = Mathf.Clamp(position.y, minY, maxY);
            _hitsThisFrame.Clear();
            float remaining = Mathf.Max(0f, now - _lastTickAt);
            _lastTickAt = now;

            // Break the path at each wall so fast frames cannot skip a bounce or a target.
            for (int bounce = 0; bounce < 64 && remaining > 0.000001f && speed > 0f; bounce++)
            {
                float tx = _direction.x > 0f ? (maxX - position.x) / (_direction.x * speed)
                    : _direction.x < 0f ? (minX - position.x) / (_direction.x * speed)
                    : float.PositiveInfinity;
                float ty = _direction.y > 0f ? (maxY - position.y) / (_direction.y * speed)
                    : _direction.y < 0f ? (minY - position.y) / (_direction.y * speed)
                    : float.PositiveInfinity;
                float step = Mathf.Min(remaining, Mathf.Max(0f, Mathf.Min(tx, ty)));
                float distance = step * speed;
                if (distance > 0f)
                {
                    _sweepHits.Clear();
                    Physics2D.CircleCast(position + attackOffset, attackRadius, _direction, filter,
                        _sweepHits, distance);
                    for (int i = 0; i < _sweepHits.Count; i++)
                        TryHit(_sweepHits[i].collider, now, sameTargetCooldown);
                    if (!_active || _owner == null) return;
                    position += _direction * distance;
                    remaining -= step;
                }
                bool hitX = tx <= step + 0.000001f;
                bool hitY = ty <= step + 0.000001f;
                position.x = Mathf.Clamp(position.x, minX, maxX);
                position.y = Mathf.Clamp(position.y, minY, maxY);
                Vector2 xNormal = _direction.x > 0f ? Vector2.left : Vector2.right;
                Vector2 yNormal = _direction.y > 0f ? Vector2.down : Vector2.up;
                if (hitX) _direction = Vector2.Reflect(_direction, xNormal);
                if (hitY) _direction = Vector2.Reflect(_direction, yNormal);
                if (hitX || hitY) _direction.Normalize();
                if (distance <= 0f && !hitX && !hitY) break;
            }
            transform.position = new Vector3(position.x, position.y, transform.position.z);

            _contactsThisFrame.Clear();
            _overlaps.Clear();
            Physics2D.OverlapCircle(position + attackOffset, attackRadius, filter, _overlaps);
            for (int i = 0; i < _overlaps.Count; i++)
            {
                Collider2D collider = _overlaps[i];
                MonsterCombatTarget target = collider == null ? null :
                    collider.GetComponentInParent<MonsterCombatTarget>();
                if (target == null) continue;
                _contactsThisFrame.Add(target);
                TryHit(collider, now, sameTargetCooldown);
                if (!_active || _owner == null) return;
            }
            _contactsLastFrame.Clear();
            foreach (MonsterCombatTarget target in _contactsThisFrame)
                _contactsLastFrame.Add(target);
        }

        private void TryHit(Collider2D collider, float now, float cooldown)
        {
            if (collider == null || _owner == null) return;
            MonsterCombatTarget target = collider.GetComponentInParent<MonsterCombatTarget>();
            if (target == null || !target.IsActive || !target.IsRegistered ||
                target.RunId != _runId || _hitsThisFrame.Contains(target) ||
                _contactsLastFrame.Contains(target)) return;
            if (_lastHitAt.TryGetValue(target, out float previous) &&
                now - previous < cooldown) return;
            if (_owner.Hit(_runId, target))
            {
                _hitsThisFrame.Add(target);
                _lastHitAt[target] = now;
            }
        }

        internal void SetVisualPaused(bool paused, bool restart = false)
        {
            if (_particles == null) return;
            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem particle = _particles[i];
                if (particle == null) continue;
                if (restart)
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (paused) particle.Pause(false);
                else particle.Play(false);
            }
        }

        internal void RefreshBalance(float now, float previousHitCooldown,
            float nextHitCooldown, float sizeMultiplier)
        {
            if (!_active) return;
            transform.localScale = _baseScale * sizeMultiplier;
            // Keep contact history and the same ball; only move its future hit deadlines.
            var targets = new List<MonsterCombatTarget>(_lastHitAt.Keys);
            for (int i = 0; i < targets.Count; i++)
            {
                MonsterCombatTarget target = targets[i];
                float nextAllowed = SkillBalanceMath.Retime(
                    _lastHitAt[target] + previousHitCooldown, now,
                    previousHitCooldown, nextHitCooldown);
                _lastHitAt[target] = nextAllowed - nextHitCooldown;
            }
        }

        internal void ReturnToPool()
        {
            _active = false;
            _owner = null;
            _runId = default;
            _contactsLastFrame.Clear();
            _contactsThisFrame.Clear();
            _hitsThisFrame.Clear();
            _lastHitAt.Clear();
            _sweepHits.Clear();
            _overlaps.Clear();
            transform.localScale = _baseScale;
            if (_particles != null)
                for (int i = 0; i < _particles.Length; i++)
                    if (_particles[i] != null)
                        _particles[i].Stop(false,
                            ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
        }
    }
}
