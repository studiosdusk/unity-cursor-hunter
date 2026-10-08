using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Compatibility alias for scenes and tests authored against the original
    /// prototype component name. New monster prefabs should use
    /// <see cref="MonsterCombatTarget"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WalkerStumpTarget : MonsterCombatTarget
    {
    }
}
