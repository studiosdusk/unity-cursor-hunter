using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    [DisallowMultipleComponent]
    public sealed class HeadshotEffect : MonoBehaviour
    {
        private ParticleSystem[] _particles;
        private bool[] _resumeParticles;
        private BoxCollider2D _hitbox;
        private HeadshotSkillController _owner;
        private MonsterCombatTarget _target;
        private RunId _runId;
        private Vector3 _lastPosition;
        private float _explodeAt;
        private float _returnAt;
        private bool _exploded;
        private bool _active;

        public bool IsActive => _active;

        internal bool HasHitbox
        {
            get
            {
                FindHitbox();
                return _hitbox != null && _hitbox.enabled && _hitbox.isTrigger;
            }
        }

        private void Awake() => CacheComponents();

        private void CacheComponents()
        {
            if (_particles == null)
            {
                _particles = GetComponentsInChildren<ParticleSystem>(true);
                _resumeParticles = new bool[_particles.Length];
                for (int i = 0; i < _particles.Length; i++)
                {
                    if (_particles[i] == null) continue;
                    ParticleSystem.MainModule main = _particles[i].main;
                    main.loop = false;
                }
            }
            FindHitbox();
        }

        private void FindHitbox()
        {
            if (_hitbox != null) return;
            Transform hitboxTransform = transform.Find("Visual_FX_Aim_Explode_Visual/HitBox");
            if (hitboxTransform != null)
                _hitbox = hitboxTransform.GetComponent<BoxCollider2D>();
        }

        internal void Activate(HeadshotSkillController owner, RunId runId,
            MonsterCombatTarget target, float now, float explosionDelay, float duration)
        {
            CacheComponents();
            _owner = owner;
            _runId = runId;
            _target = target;
            _lastPosition = target.transform.position;
            _explodeAt = now + explosionDelay;
            _returnAt = now + duration;
            _exploded = false;
            _active = true;
            transform.position = _lastPosition;
            gameObject.SetActive(true);
            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem particle = _particles[i];
                if (particle == null) continue;
                _resumeParticles[i] = false;
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(false);
            }
        }

        internal bool Tick(float now)
        {
            if (!_active || _owner == null || !_owner.IsCurrentRun(_runId))
                return false;
            if (!_exploded && _target != null && _target.gameObject.activeInHierarchy &&
                _target.IsActive &&
                _target.RunId == _runId)
            {
                _lastPosition = _target.transform.position;
                transform.position = _lastPosition;
            }
            if (!_exploded && now >= _explodeAt)
            {
                _exploded = true;
                _owner.Explode(_runId, _target, _hitbox);
                _target = null;
            }
            return now < _returnAt;
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
            _target = null;
            _runId = default;
            _exploded = false;
            if (_particles != null)
            {
                for (int i = 0; i < _particles.Length; i++)
                {
                    _resumeParticles[i] = false;
                    if (_particles[i] != null)
                        _particles[i].Stop(false,
                            ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            gameObject.SetActive(false);
        }
    }
}
