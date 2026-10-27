using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>One pooled tornado. Its visual is separate from its trigger hit area.</summary>
    [DisallowMultipleComponent]
    public sealed class TornadoObject : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D hitbox;
        private ParticleSystem[] _particles;
        private TornadoSkillController _owner;
        private RunId _runId;
        private Vector2 _direction;
        private float _lastUpdateAt;
        private float _expiresAt;
        private float _nextTurnAt;
        private float _nextHitAt;
        private float _stoppedUntil;
        private float _turnInterval;
        private float _hitInterval;
        private float _stopChance;
        private float _stopDuration;
        private float _shrinkOutSeconds;
        private float _areaRadiusMultiplier = 1f;
        private Vector3 _baseScale;
        private long _damage;
        private bool _isActive;

        public bool IsActive => _isActive;
        internal bool HasHitbox => hitbox != null && hitbox.enabled && hitbox.isTrigger;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            if (hitbox == null) hitbox = GetComponentInChildren<BoxCollider2D>(true);
        }

        internal void Activate(
            TornadoSkillController owner, RunId runId, Vector2 position,
            float now, float duration, float turnInterval,
            float hitInterval, float stopChance,
            float stopDuration, float shrinkOutSeconds, long damage,
            float areaRadiusMultiplier = 1f)
        {
            _owner = owner;
            _runId = runId;
            _isActive = true;
            _lastUpdateAt = now;
            _expiresAt = now + duration;
            _nextTurnAt = now + turnInterval;
            _nextHitAt = now;
            _stoppedUntil = 0f;
            _turnInterval = turnInterval;
            _hitInterval = hitInterval;
            _stopChance = stopChance;
            _stopDuration = stopDuration;
            _shrinkOutSeconds = Mathf.Clamp(shrinkOutSeconds, 0f, duration);
            _areaRadiusMultiplier = Mathf.Max(0.01f, areaRadiusMultiplier);
            _damage = damage;
            transform.localScale = _baseScale * _areaRadiusMultiplier;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            _direction = owner.RandomDirection();
            gameObject.SetActive(true);
            SetVisualPaused(false, true);
        }

        internal bool Tick(float now, Rect fieldBounds)
        {
            if (!_isActive || now >= _expiresAt || !_owner.IsCurrentRun(_runId))
            {
                return false;
            }

            float delta = Mathf.Max(0f, now - _lastUpdateAt);
            _lastUpdateAt = now;
            if (now >= _nextTurnAt)
            {
                _nextTurnAt = now + _turnInterval;
                if (_owner.RollStop(_stopChance))
                {
                    _stoppedUntil = now + _stopDuration;
                }
                else
                {
                    _stoppedUntil = 0f;
                    _direction = _owner.RandomDirection();
                }
            }

            if (_stoppedUntil > 0f && now >= _stoppedUntil)
            {
                _stoppedUntil = 0f;
                _direction = _owner.RandomDirection();
            }

            float moveSpeed = _owner.MoveSpeedWorldUnitsPerSecond;
            if (_stoppedUntil <= 0f && delta > 0f && moveSpeed > 0f)
            {
                Vector2 next = (Vector2)transform.position + _direction * (moveSpeed * delta);
                if (next.x < fieldBounds.xMin || next.x > fieldBounds.xMax)
                {
                    _direction.x = -_direction.x;
                }
                if (next.y < fieldBounds.yMin || next.y > fieldBounds.yMax)
                {
                    _direction.y = -_direction.y;
                }
                next = (Vector2)transform.position + _direction * (moveSpeed * delta);
                next.x = Mathf.Clamp(next.x, fieldBounds.xMin, fieldBounds.xMax);
                next.y = Mathf.Clamp(next.y, fieldBounds.yMin, fieldBounds.yMax);
                transform.position = new Vector3(next.x, next.y, transform.position.z);
            }

            transform.localScale = _shrinkOutSeconds > 0f
                ? _baseScale * _areaRadiusMultiplier *
                    Mathf.Clamp01((_expiresAt - now) / _shrinkOutSeconds)
                : _baseScale * _areaRadiusMultiplier;

            return true;
        }

        internal void TryHit(float now)
        {
            if (!_isActive || _owner == null || now < _nextHitAt) return;
            _nextHitAt = now + _hitInterval;
            _owner.HitAt(_runId, hitbox, _damage);
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

        internal void RefreshBalance(float now, TornadoRuntimeStat previous,
            TornadoRuntimeStat next)
        {
            if (!_isActive) return;
            _expiresAt = SkillBalanceMath.Retime(_expiresAt, now,
                previous.DurationSeconds, next.DurationSeconds);
            _nextTurnAt = SkillBalanceMath.Retime(_nextTurnAt, now,
                _turnInterval, next.DirectionChangeSeconds);
            _nextHitAt = SkillBalanceMath.Retime(_nextHitAt, now,
                _hitInterval, next.HitIntervalSeconds);
            if (_stoppedUntil > now)
                _stoppedUntil = SkillBalanceMath.Retime(_stoppedUntil, now,
                    _stopDuration, next.StopDurationSeconds);
            _turnInterval = next.DirectionChangeSeconds;
            _hitInterval = next.HitIntervalSeconds;
            _stopChance = next.StopChanceOnDirectionChange;
            _stopDuration = next.StopDurationSeconds;
            _shrinkOutSeconds = Mathf.Clamp(next.ShrinkOutSeconds, 0f,
                next.DurationSeconds);
            _damage = next.Damage;
            transform.localScale = _shrinkOutSeconds > 0f
                ? _baseScale * _areaRadiusMultiplier *
                    Mathf.Clamp01((_expiresAt - now) / _shrinkOutSeconds)
                : _baseScale * _areaRadiusMultiplier;
        }

        internal void ReturnToPool()
        {
            _isActive = false;
            _runId = default;
            _owner = null;
            _damage = 0L;
            _areaRadiusMultiplier = 1f;
            transform.localScale = _baseScale;
            if (_particles != null)
            {
                for (int i = 0; i < _particles.Length; i++)
                    if (_particles[i] != null)
                        _particles[i].Stop(false,
                            ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            gameObject.SetActive(false);
        }
    }
}
