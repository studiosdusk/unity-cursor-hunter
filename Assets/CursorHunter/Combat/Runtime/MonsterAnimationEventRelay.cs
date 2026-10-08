using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Forwards legacy AnimationEvents on a nested supplier visual to the
    /// run-owned combat target on MonsterRoot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterAnimationEventRelay : MonoBehaviour
    {
        private MonsterCombatTarget _target;

        public void Initialize(MonsterCombatTarget target)
        {
            _target = target;
        }

        public void DestroySelf()
        {
            if (_target != null)
            {
                _target.DestroySelf();
            }
        }
    }
}
