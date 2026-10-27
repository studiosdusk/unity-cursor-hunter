using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>One pooled, single-target strike. Child particles are visual only.</summary>
    [DisallowMultipleComponent]
    public sealed class ThunderEffect : MonoBehaviour
    {
        private ParticleSystem[] _particles;
        private bool[] _resumeParticles;
        private Transform _hitPoint;
        private ThunderSkillController _owner;
        private MonsterCombatTarget _target;
        private RunId _runId;
        private float _impactAt;
        private float _returnAt;
        private bool _struck;
        private bool _active;

        public bool IsActive => _active;

        private void Awake()
        {
            CacheParticles();
            _hitPoint = transform.Find("Visual_FX_Cartoon_Thunder/Particle System/HitPoint");
        }

        private void CacheParticles()
        {
            if (_particles != null) return;

            _particles = GetComponentsInChildren<ParticleSystem>(true);
            _resumeParticles = new bool[_particles.Length];
            for (int i = 0; i < _particles.Length; i++)
            {
                if (_particles[i] == null) continue;
                ParticleSystem.MainModule main = _particles[i].main;
                main.loop = false;
            }
        }

        internal void Activate(ThunderSkillController owner, RunId runId,
            MonsterCombatTarget target, float now, float impactDelay, float duration)
        {
            CacheParticles();
            _owner = owner;
            _runId = runId;
            _target = target;
            _impactAt = now + impactDelay;
            _returnAt = now + duration;
            _struck = false;
            _active = true;
            PlaceHitPointAtTargetCenter(target);
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

            if (!_struck && _target != null && _target.gameObject.activeInHierarchy &&
                _target.IsActive && _target.RunId == _runId)
                PlaceHitPointAtTargetCenter(_target);

            if (!_struck && now >= _impactAt)
            {
                _struck = true;
                _owner.Strike(_runId, _target);
                _target = null;
            }

            return now < _returnAt;
        }

        private void PlaceHitPointAtTargetCenter(MonsterCombatTarget target)
        {
            Vector3 center = target.TryGetHitEffectBounds(out Bounds bounds)
                ? bounds.center
                : target.transform.position;
            transform.position += center - (_hitPoint != null
                ? _hitPoint.position
                : transform.position);
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

        internal void RefreshBalance(float now, ThunderRuntimeStat previous,
            ThunderRuntimeStat next)
        {
            if (!_active) return;
            if (!_struck)
                _impactAt = SkillBalanceMath.Retime(_impactAt, now,
                    previous.ImpactDelaySeconds, next.ImpactDelaySeconds);
            _returnAt = SkillBalanceMath.Retime(_returnAt, now,
                previous.EffectDurationSeconds, next.EffectDurationSeconds);
            if (!_struck) _returnAt = Mathf.Max(_returnAt, _impactAt + 0.01f);
        }

        internal void ReturnToPool()
        {
            _active = false;
            _owner = null;
            _target = null;
            _runId = default;
            _struck = false;
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
