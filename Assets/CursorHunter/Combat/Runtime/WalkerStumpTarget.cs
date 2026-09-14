using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Runtime combat state for the Walker_Stump visual used as the prototype
    /// slime. The definition and run snapshot provide max HP and rewards;
    /// this component owns only the instance's mutable health and animation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WalkerStumpTarget : MonoBehaviour
    {
        public const string HitParameterName = "hit";
        public const string DeadParameterName = "dead";

        [SerializeField] private Animator animator;

        private bool _hasHitParameter;
        private bool _hasDeadParameter;
        private bool _isInitialized;
        private bool _isRegistered;
        private bool _isDead;
        private RunId _runId;
        private string _monsterId;
        private long _maxHealth;
        private long _currentHealth;
        private long _garnetReward;

        public bool IsInitialized => _isInitialized;
        public bool IsRegistered => _isRegistered;
        public bool IsActive => _isInitialized && _isRegistered && !_isDead;
        public bool IsDead => _isDead;
        public RunId RunId => _runId;
        public string MonsterId => _monsterId;
        public long MaxHealth => _maxHealth;
        public long CurrentHealth => _currentHealth;
        public long GarnetReward => _garnetReward;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (animator == null)
            {
                Debug.LogWarning(
                    "WalkerStumpTarget requires an Animator on the Walker_Stump root.",
                    this);
                return;
            }

            _hasHitParameter = HasParameter(
                HitParameterName,
                AnimatorControllerParameterType.Trigger);
            _hasDeadParameter = HasParameter(
                DeadParameterName,
                AnimatorControllerParameterType.Bool);

            if (!_hasHitParameter || !_hasDeadParameter)
            {
                Debug.LogWarning(
                    "Walker_Stump Animator must contain hit (Trigger) and dead (Bool) parameters.",
                    this);
            }
        }

        /// <summary>
        /// Initializes one spawned instance from the immutable run snapshot.
        /// </summary>
        public void Initialize(
            SpawnSnapshot snapshot,
            RunId runId)
        {
            if (!runId.IsValid || !snapshot.IsValid)
            {
                Unregister();
                Debug.LogError(
                    "WalkerStumpTarget requires a valid RunId and SpawnSnapshot.",
                    this);
                return;
            }

            _runId = runId;
            _monsterId = snapshot.MonsterId;
            _maxHealth = snapshot.MaxHealth;
            _currentHealth = snapshot.MaxHealth;
            _garnetReward = snapshot.GarnetReward;
            _isDead = false;
            _isInitialized = true;
            _isRegistered = true;

            if (animator == null)
            {
                return;
            }

            if (_hasDeadParameter)
            {
                animator.SetBool(DeadParameterName, false);
            }

            if (_hasHitParameter)
            {
                animator.ResetTrigger(HitParameterName);
            }
        }

        /// <summary>
        /// Removes this instance from the current run immediately. Unity's
        /// Destroy is deferred, so this guard is required before pooled or
        /// stale animation callbacks can run.
        /// </summary>
        public void Unregister()
        {
            _isRegistered = false;
        }

        public bool BelongsTo(RunId runId)
        {
            return _isRegistered && _runId == runId;
        }

        /// <summary>
        /// Applies one logical hit. Damage is accepted while the Hit animation
        /// is playing; animation timing never gates health changes.
        /// </summary>
        public bool ApplyDamage(
            RunId runId,
            long damage,
            out long effectiveDamage,
            out bool killed)
        {
            effectiveDamage = 0;
            killed = false;

            if (!IsActive || !BelongsTo(runId) ||
                damage <= 0 || _currentHealth <= 0)
            {
                return false;
            }

            effectiveDamage = damage < _currentHealth ? damage : _currentHealth;
            _currentHealth -= effectiveDamage;

            if (_currentHealth <= 0)
            {
                _currentHealth = 0;
                _isDead = true;
                _isRegistered = false;
                killed = true;

                if (animator != null && _hasDeadParameter)
                {
                    animator.SetBool(DeadParameterName, true);
                }

                return true;
            }

            if (animator != null && _hasHitParameter)
            {
                animator.SetTrigger(HitParameterName);
            }

            return true;
        }

        /// <summary>
        /// Called by the final Animation Event on the Dead clip.
        /// </summary>
        public void DestroySelf()
        {
            if (!_isDead)
            {
                return;
            }

            Unregister();
            Destroy(gameObject);
        }

        private bool HasParameter(
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name == parameterName && parameter.type == parameterType)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
