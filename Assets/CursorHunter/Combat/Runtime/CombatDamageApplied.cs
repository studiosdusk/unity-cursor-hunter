using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    public enum CombatDamageSource
    {
        Unknown,
        CursorAttack,
        Skill
    }

    /// <summary>Optional body bounds, sampled before damage can remove the target.</summary>
    public interface IHitEffectAnchor
    {
        bool TryGetHitEffectBounds(out Bounds bounds);
    }

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
            : this(runId, target, damage, critical, worldPosition, hasWorldPosition,
                CombatDamageSource.Unknown, default, false, 0f)
        {
        }

        public CombatDamageApplied(RunId runId, ICombatTarget target, long damage,
            bool critical, Vector3 worldPosition, bool hasWorldPosition,
            CombatDamageSource source, Vector3 hitEffectPosition,
            bool hasHitEffectPosition, float hitEffectSizeWorldUnits)
        {
            RunId = runId;
            Target = target;
            EffectiveDamage = damage;
            IsCritical = critical;
            WorldPosition = worldPosition;
            HasWorldPosition = hasWorldPosition;
            Source = source;
            HitEffectPosition = hitEffectPosition;
            HasHitEffectPosition = hasHitEffectPosition;
            HitEffectSizeWorldUnits = hitEffectSizeWorldUnits;
        }

        public RunId RunId { get; }
        public ICombatTarget Target { get; }
        public long EffectiveDamage { get; }
        public bool IsCritical { get; }
        public Vector3 WorldPosition { get; }
        public bool HasWorldPosition { get; }
        public CombatDamageSource Source { get; }
        public Vector3 HitEffectPosition { get; }
        public bool HasHitEffectPosition { get; }
        public float HitEffectSizeWorldUnits { get; }
    }
}
