using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>A stationary piercing beam. Each target is hit once per activation.</summary>
    [DisallowMultipleComponent]
    public sealed partial class LaserEffect : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D hitbox;
        [SerializeField, InspectorName("Visual Particle Transform")]
        private Transform visualAnchor;
        [Tooltip("Combat area height where the visual and HitBox use their prefab sizes.")]
        [SerializeField, Min(0.01f)] private float referenceFieldHeightWorldUnits = 24f;
        [Tooltip("Visual width only. HitBox width is set by its BoxCollider2D.")]
        [SerializeField, Min(0.01f)] private float visualWidthMultiplier = 3f;
        [Tooltip("Visual height only. The HitBox always spans the combat area.")]
        [SerializeField, Min(0.01f)] private float visualHeightMultiplier = 3f;

        private readonly List<Collider2D> _overlaps = new List<Collider2D>(128);
        private readonly HashSet<MonsterCombatTarget> _hitTargets =
            new HashSet<MonsterCombatTarget>();
        private ParticleSystem[] _particles;
        private bool[] _resumeParticles;
        private Vector2 _baseHitboxSize;
        private Vector3 _baseVisualScale;
        private LaserSkillController _owner;
        private RunId _runId;
        private float _returnAt;
        private bool _active;
        private float _horizontalFraction;
        private Quaternion _baseRotation;

        public bool IsActive => _active;
        internal bool HasHitbox => hitbox != null && hitbox.enabled && hitbox.isTrigger &&
            hitbox.transform != transform && hitbox.transform.IsChildOf(transform) &&
            hitbox.size.x > 0f && visualAnchor != null &&
            visualAnchor.IsChildOf(transform) &&
            visualAnchor.GetComponent<ParticleSystem>() != null &&
            referenceFieldHeightWorldUnits > 0f &&
            visualWidthMultiplier > 0f && visualHeightMultiplier > 0f;

        private void Awake()
        {
            _baseHitboxSize = hitbox.size;
            _baseVisualScale = visualAnchor.localScale;
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            _resumeParticles = new bool[_particles.Length];
            _baseRotation = transform.localRotation;
            InitializeDiagonalVisual();
        }

        internal void Activate(LaserSkillController owner, RunId runId,
            Rect fieldBounds, float horizontalFraction, float z, float now,
            float duration, float sizeMultiplier)
        {
            _owner = owner;
            _runId = runId;
            _returnAt = now + duration;
            _hitTargets.Clear();
            _horizontalFraction = horizontalFraction;
            float fieldScale = fieldBounds.height / referenceFieldHeightWorldUnits;
            hitbox.size = new Vector2(
                _baseHitboxSize.x * sizeMultiplier * fieldScale, fieldBounds.height);
            visualAnchor.localScale = new Vector3(
                _baseVisualScale.x * sizeMultiplier * fieldScale * visualWidthMultiplier,
                _baseVisualScale.y * fieldScale * visualHeightMultiplier,
                _baseVisualScale.z);
            transform.position = new Vector3(fieldBounds.center.x, fieldBounds.center.y, z);
            gameObject.SetActive(true);
            Physics2D.SyncTransforms();
            float offsetX = hitbox.bounds.center.x - transform.position.x;
            float halfWidth = hitbox.bounds.extents.x;
            float minX = fieldBounds.xMin + halfWidth - offsetX;
            float maxX = fieldBounds.xMax - halfWidth - offsetX;
            float x = minX < maxX
                ? Mathf.Lerp(minX, maxX, horizontalFraction)
                : fieldBounds.center.x - offsetX;
            transform.position = new Vector3(x, fieldBounds.center.y, z);
            FitToBattlefield();
            _active = true;
            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem particle = _particles[i];
                if (particle == null) continue;
                _resumeParticles[i] = false;
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(false);
            }
        }

        internal bool Tick(float now, ContactFilter2D filter)
        {
            if (!_active || _owner == null || !_owner.IsCurrentRun(_runId)) return false;
            if (_diagonal) return TickDiagonal(now, filter);
            if (now >= _returnAt) return false;
            return HitOverlaps(filter, now);
        }

        private bool HitOverlaps(ContactFilter2D filter, float now)
        {
            _overlaps.Clear();
            hitbox.Overlap(filter, _overlaps);
            for (int i = 0; i < _overlaps.Count; i++)
            {
                Collider2D collider = _overlaps[i];
                MonsterCombatTarget target = collider == null ? null :
                    collider.GetComponentInParent<MonsterCombatTarget>();
                if (target == null || _hitTargets.Contains(target)) continue;
                Vector2 hitPoint = target.GetClosestHitPoint(transform.position);
                RunId runId = _runId;
                bool hit = _owner.Hit(runId, target);
                if (!_active || _owner == null || _runId != runId ||
                    !_owner.IsCurrentRun(runId)) return false;
                if (hit)
                {
                    _hitTargets.Add(target);
                    if (_diagonal) _beamVisual.Pulse(hitPoint, now, _beamWidth);
                }
            }
            return true;
        }

        private void FitToBattlefield()
        {
            BattlefieldArea field = _owner.Battlefield;
            if (field == null) return;
            Physics2D.SyncTransforms();
            float halfWidth = hitbox.bounds.extents.x;
            Vector2 offset = (Vector2)hitbox.bounds.center - (Vector2)transform.position;
            Rect area = field.WorldBounds;
            // Avoid a zero-height stripe at the left/right tips of the diamond.
            float reach = Mathf.Max(0f, area.width * 0.5f - halfWidth - field.GroundMargin) * 0.85f;
            float centerX = area.center.x + Mathf.Lerp(-reach, reach, _horizontalFraction);
            if (!field.TryGetVerticalSpan(centerX, halfWidth, out float minY, out float maxY))
            {
                centerX = area.center.x;
                if (!field.TryGetVerticalSpan(centerX, halfWidth, out minY, out maxY))
                {
                    hitbox.enabled = false;
                    return;
                }
            }
            hitbox.enabled = true;
            float height = maxY - minY;
            hitbox.size = new Vector2(hitbox.size.x, height / Mathf.Abs(hitbox.transform.lossyScale.y));
            Vector3 scale = visualAnchor.localScale;
            scale.y = _baseVisualScale.y * height / referenceFieldHeightWorldUnits * visualHeightMultiplier;
            visualAnchor.localScale = scale;
            transform.position = new Vector3(centerX - offset.x,
                (minY + maxY) * 0.5f - offset.y, transform.position.z);
        }

        internal void SetVisualPaused(bool paused)
        {
            if (_particles == null) return;
            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem particle = _particles[i];
                if (particle == null) continue;
                if (paused)
                {
                    _resumeParticles[i] = particle.isPlaying;
                    if (_resumeParticles[i]) particle.Pause(false);
                }
                else if (_resumeParticles[i] && particle.isPaused)
                {
                    particle.Play(false);
                    _resumeParticles[i] = false;
                }
            }
        }

        internal void RefreshBalance(float now, LaserRuntimeStat previous,
            LaserRuntimeStat next, Rect fieldBounds)
        {
            if (!_active) return;
            if (_diagonal)
            {
                RefreshDiagonal(now, next.EffectDurationSeconds);
                return;
            }
            _returnAt = SkillBalanceMath.Retime(_returnAt, now,
                previous.EffectDurationSeconds, next.EffectDurationSeconds);
            float fieldScale = fieldBounds.height / referenceFieldHeightWorldUnits;
            hitbox.size = new Vector2(
                _baseHitboxSize.x * _owner.EffectiveSizeMultiplier * fieldScale, fieldBounds.height);
            visualAnchor.localScale = new Vector3(
                _baseVisualScale.x * _owner.EffectiveSizeMultiplier * fieldScale * visualWidthMultiplier,
                _baseVisualScale.y * fieldScale * visualHeightMultiplier,
                _baseVisualScale.z);
            FitToBattlefield();
        }

        internal void ReturnToPool()
        {
            _active = false;
            _owner = null;
            _runId = default;
            _hitTargets.Clear();
            _overlaps.Clear();
            ResetDiagonal();
            transform.localRotation = _baseRotation;
            hitbox.size = _baseHitboxSize;
            hitbox.enabled = true;
            visualAnchor.localScale = _baseVisualScale;
            if (_particles != null)
                for (int i = 0; i < _particles.Length; i++)
                {
                    _resumeParticles[i] = false;
                    if (_particles[i] != null)
                        _particles[i].Stop(false,
                            ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            gameObject.SetActive(false);
        }
    }
}
