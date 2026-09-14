using CursorHunter.Contracts;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Stable combat-facing adapter contract. Visual prefabs can change
    /// without changing CombatRunController's damage and run-ownership rules.
    /// </summary>
    public interface ICombatTarget
    {
        RunId RunId { get; }
        bool IsRegistered { get; }
        bool IsActive { get; }
        long GarnetReward { get; }

        bool ApplyDamage(
            RunId runId,
            long damage,
            out long effectiveDamage,
            out bool killed);
    }
}
