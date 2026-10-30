using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>One pooled visual hit. The vendor particle prefab deals no damage.</summary>
    [DisallowMultipleComponent]
    public sealed class FlameExhaustHitEffect : MonoBehaviour
    {
        [Tooltip("Effect size relative to the monster's visible body. 1 matches its longest side.")]
        [SerializeField, Min(0.01f)] private float sizeMultiplier = 1.2f;

        private ParticleSystem[] _particles;
        private bool[] _resumeParticles;
        private Vector3 _baseScale;
        private float _activatedAt;
        private float _returnAt;
        private bool _active;

        public bool IsActive => _active;
        internal float ActivatedAt => _activatedAt;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            _resumeParticles = new bool[_particles.Length];
            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem.MainModule main = _particles[i].main;
                main.loop = false;
            }
        }

        internal void Activate(Vector3 position, float zRotation, float now,
            float duration, float targetSizeWorldUnits)
        {
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f, zRotation);
            transform.localScale = _baseScale *
                (Mathf.Max(0.01f, targetSizeWorldUnits) *
                 Mathf.Max(0.01f, sizeMultiplier));
            _activatedAt = now;
            _returnAt = now + duration;
            _active = true;
            gameObject.SetActive(true);
            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem particle = _particles[i];
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(false);
                _resumeParticles[i] = false;
            }
        }

        internal bool Tick(float now) => _active && now < _returnAt;

        internal void SetVisualPaused(bool paused)
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem particle = _particles[i];
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

        internal void RefreshBalance(float now, float previousDuration,
            float nextDuration)
        {
            if (_active)
                _returnAt = SkillBalanceMath.Retime(_returnAt, now,
                    previousDuration, nextDuration);
        }

        internal void ReturnToPool()
        {
            _active = false;
            _activatedAt = 0f;
            transform.localScale = _baseScale;
            for (int i = 0; i < _particles.Length; i++)
            {
                _resumeParticles[i] = false;
                _particles[i].Stop(false,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            gameObject.SetActive(false);
        }
    }
}
