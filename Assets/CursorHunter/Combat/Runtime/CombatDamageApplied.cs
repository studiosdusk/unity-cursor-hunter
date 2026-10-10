using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>Optional presentation anchor; independent of the damage contract.</summary>
    public interface IDamageTextAnchor
    {
        Vector3 DamageTextPosition { get; }
    }

    /// <summary>Position is captured before damage can despawn the target.</summary>
    public readonly struct CombatDamageApplied
    {
        public CombatDamageApplied(RunId runId, ICombatTarget target, long damage,
            bool critical, Vector3 worldPosition, bool hasWorldPosition)
        {
            RunId = runId;
            Target = target;
            EffectiveDamage = damage;
            IsCritical = critical;
            WorldPosition = worldPosition;
            HasWorldPosition = hasWorldPosition;
        }

        public RunId RunId { get; }
        public ICombatTarget Target { get; }
        public long EffectiveDamage { get; }
        public bool IsCritical { get; }
        public Vector3 WorldPosition { get; }
        public bool HasWorldPosition { get; }
    }
}
