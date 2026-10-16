using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>A stationary screen-height beam. Each target is hit once per activation.</summary>
    [DisallowMultipleComponent]
    public sealed class LaserEffect : MonoBehaviour
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
        }

        internal void Activate(LaserSkillController owner, RunId runId,
            Rect fieldBounds, float horizontalFraction, float z, float now,
            float duration, float sizeMultiplier)
        {
            _owner = owner;
            _runId = runId;
            _returnAt = now + duration;
            _hitTargets.Clear();
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
            if (!_active || _owner == null || !_owner.IsCurrentRun(_runId) ||
                now >= _returnAt) return false;

            _overlaps.Clear();
            hitbox.Overlap(filter, _overlaps);
            for (int i = 0; i < _overlaps.Count; i++)
            {
                Collider2D collider = _overlaps[i];
                MonsterCombatTarget target = collider == null ? null :
                    collider.GetComponentInParent<MonsterCombatTarget>();
                if (target == null || _hitTargets.Contains(target)) continue;
                if (_owner.Hit(_runId, target)) _hitTargets.Add(target);
                if (!_owner.IsCurrentRun(_runId)) return false;
            }
            return true;
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

        internal void ReturnToPool()
        {
            _active = false;
            _owner = null;
            _runId = default;
            _hitTargets.Clear();
            _overlaps.Clear();
            hitbox.size = _baseHitboxSize;
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
