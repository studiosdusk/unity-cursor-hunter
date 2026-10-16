using CursorHunter.Contracts;

namespace CursorHunter.Combat
{
    // Only the run lifecycle is shared. Each skill owns its cast and hit rules.
    public interface IRunSkillController
    {
        string Id { get; }
        bool IsAcquiredForRun(bool savedAcquired);
        bool ConfigureForRun(CombatRunController combat, RunId runId,
            ulong seed, SkillCombatSnapshot skill);
        void StopSkill();
    }
}
