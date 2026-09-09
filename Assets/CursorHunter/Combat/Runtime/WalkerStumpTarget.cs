using System.Collections.Generic;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Prototype combat state for a Walker_Stump instance.
    ///
    /// The component is intentionally independent from the cursor and UI. The
    /// cursor attack sends one hit at a time, while this component owns hit
    /// counting, Animator parameters, and the final destruction event.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WalkerStumpTarget : MonoBehaviour
    {
        public const string HitParameterName = "hit";
        public const string DeadParameterName = "dead";

        private static readonly List<WalkerStumpTarget> ActiveTargets =
            new List<WalkerStumpTarget>();

        [SerializeField] private Animator animator;
        [SerializeField, Min(1)] private int maxHits = 3;

        private bool _hasHitParameter;
        private bool _hasDeadParameter;
        private bool _isDead;
        private int _hitCount;

        public static int ActiveCount => ActiveTargets.Count;

        public bool IsDead => _isDead;
        public int HitCount => _hitCount;

        public static WalkerStumpTarget GetActiveAt(int index)
        {
            return ActiveTargets[index];
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            maxHits = Mathf.Max(1, maxHits);

            if (animator == null)
            {
                Debug.LogWarning("WalkerStumpTarget requires an Animator on the Walker_Stump root.", this);
                return;
            }

            _hasHitParameter = HasParameter(HitParameterName, AnimatorControllerParameterType.Trigger);
            _hasDeadParameter = HasParameter(DeadParameterName, AnimatorControllerParameterType.Bool);

            if (!_hasHitParameter || !_hasDeadParameter)
            {
                Debug.LogWarning(
                    "Walker_Stump Animator must contain hit (Trigger) and dead (Bool) parameters.",
                    this);
            }
        }

        private void OnEnable()
        {
            if (!ActiveTargets.Contains(this))
            {
                ActiveTargets.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveTargets.Remove(this);
        }

        /// <summary>
        /// Applies exactly one click hit. Additional clicks are accepted while
        /// the Hit animation is playing; animation timing does not gate damage.
        /// </summary>
        public bool ReceiveHit()
        {
            if (_isDead || animator == null || !_hasHitParameter || !_hasDeadParameter)
            {
                return false;
            }

            _hitCount++;

            if (_hitCount >= maxHits)
            {
                _isDead = true;
                animator.SetBool(DeadParameterName, true);
                return true;
            }

            animator.SetTrigger(HitParameterName);
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

            Destroy(gameObject);
        }

        private bool HasParameter(string parameterName, AnimatorControllerParameterType parameterType)
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
