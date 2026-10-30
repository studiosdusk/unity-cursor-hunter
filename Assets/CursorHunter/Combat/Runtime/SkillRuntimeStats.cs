using System;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    internal static class SkillStatMath
    {
        internal static float Add(float baseValue, SkillCombatSnapshot skill,
            string statId, float minimum)
        {
            float result = baseValue + skill.GetUpgrade(statId);
            if (float.IsNaN(result) || float.IsInfinity(result))
                throw new ArgumentException("Non-finite skill stat: " + statId);
            return Mathf.Max(minimum, result);
        }

        internal static float AddClamped(float baseValue, SkillCombatSnapshot skill,
            string statId, float minimum, float maximum)
        {
            return Mathf.Min(maximum, Add(baseValue, skill, statId, minimum));
        }

        internal static int AddCount(int baseValue, SkillCombatSnapshot skill,
            string statId)
        {
            float delta = skill.GetUpgrade(statId);
            double total = baseValue + (double)delta;
            if (delta != Mathf.Floor(delta) || total > int.MaxValue ||
                total < int.MinValue)
                throw new ArgumentException("Invalid skill count upgrade.");
            return Mathf.Max(1, (int)total);
        }

        internal static long ScaleDamage(float skillDamage)
        {
            double value = Mathf.Max(0.01f, skillDamage);
            return value >= long.MaxValue
                ? long.MaxValue
                : Math.Max(1L, (long)Math.Round(value,
                    MidpointRounding.AwayFromZero));
        }
    }

    [Serializable]
    internal struct TornadoBaseStat
    {
        [Min(1)] public int Count;
        [Min(0.1f)] public float DurationSeconds;
        [Min(0.01f)] public float DamageMultiplier;
        [Min(0.1f)] public float RadiusMultiplier;
        [Min(0.1f)] public float CooldownSeconds;
        [Min(0f)] public float FirstCastDelaySeconds;
        [Min(0.1f)] public float HitIntervalSeconds;
        [Min(0f)] public float MoveSpeedWorldUnitsPerSecond;
        [Min(0.1f)] public float DirectionChangeSeconds;
        [Range(0f, 1f)] public float StopChanceOnDirectionChange;
        [Min(0f)] public float StopDurationSeconds;
        [Min(0f)] public float ShrinkOutSeconds;
    }

    internal readonly struct TornadoRuntimeStat
    {
        internal TornadoRuntimeStat(TornadoBaseStat basis,
            SkillCombatSnapshot upgrades, int maximumCount)
        {
            Count = Mathf.Min(SkillStatMath.AddCount(basis.Count, upgrades, "count"),
                Mathf.Max(1, maximumCount));
            DurationSeconds = SkillStatMath.Add(basis.DurationSeconds, upgrades,
                "durationSeconds", 0.1f);
            DamageMultiplier = SkillStatMath.Add(basis.DamageMultiplier, upgrades,
                "damage", 0.01f) + upgrades.GetUpgrade("damageMultiplier");
            Damage = SkillStatMath.ScaleDamage(DamageMultiplier);
            float radiusBase = basis.RadiusMultiplier > 0f
                ? basis.RadiusMultiplier
                : 1f;
            RadiusMultiplier = radiusBase * SkillStatMath.Add(1f, upgrades,
                "radius", 0.1f);
            CooldownSeconds = SkillStatMath.Add(basis.CooldownSeconds, upgrades,
                "cooldownSeconds", 0.1f);
            FirstCastDelaySeconds = SkillStatMath.Add(basis.FirstCastDelaySeconds,
                upgrades, "firstCastDelaySeconds", 0f);
            HitIntervalSeconds = SkillStatMath.Add(basis.HitIntervalSeconds,
                upgrades, "hitIntervalSeconds", 0.1f);
            MoveSpeedWorldUnitsPerSecond = SkillStatMath.Add(
                basis.MoveSpeedWorldUnitsPerSecond, upgrades,
                "moveSpeedWorldUnitsPerSecond", 0f);
            DirectionChangeSeconds = SkillStatMath.Add(
                basis.DirectionChangeSeconds, upgrades, "directionChangeSeconds", 0.1f);
            StopChanceOnDirectionChange = SkillStatMath.AddClamped(
                basis.StopChanceOnDirectionChange, upgrades,
                "stopChanceOnDirectionChange", 0f, 1f);
            StopDurationSeconds = SkillStatMath.Add(basis.StopDurationSeconds,
                upgrades, "stopDurationSeconds", 0f);
            ShrinkOutSeconds = SkillStatMath.Add(basis.ShrinkOutSeconds,
                upgrades, "shrinkOutSeconds", 0f);
        }

        internal int Count { get; }
        internal float DurationSeconds { get; }
        internal float DamageMultiplier { get; }
        internal long Damage { get; }
        internal float RadiusMultiplier { get; }
        internal float CooldownSeconds { get; }
        internal float FirstCastDelaySeconds { get; }
        internal float HitIntervalSeconds { get; }
        internal float MoveSpeedWorldUnitsPerSecond { get; }
        internal float DirectionChangeSeconds { get; }
        internal float StopChanceOnDirectionChange { get; }
        internal float StopDurationSeconds { get; }
        internal float ShrinkOutSeconds { get; }
    }

    [Serializable]
    internal struct HeadshotBaseStat
    {
        [Min(0.1f)] public float CooldownSeconds;
        [Min(0f)] public float FirstCastDelaySeconds;
        [Min(0f)] public float ExplosionDelaySeconds;
        [Min(0.1f)] public float EffectDurationSeconds;
        [Min(0.01f)] public float SplashDamageMultiplier;
        [Min(0.01f)] public float BossDamageMultiplier;
        [Min(0.1f)] public float RadiusMultiplier;
    }

    internal readonly struct HeadshotRuntimeStat
    {
        internal HeadshotRuntimeStat(HeadshotBaseStat basis,
            SkillCombatSnapshot upgrades)
        {
            CooldownSeconds = SkillStatMath.Add(basis.CooldownSeconds, upgrades,
                "cooldownSeconds", 0.1f);
            FirstCastDelaySeconds = SkillStatMath.Add(basis.FirstCastDelaySeconds,
                upgrades, "firstCastDelaySeconds", 0f);
            ExplosionDelaySeconds = SkillStatMath.Add(basis.ExplosionDelaySeconds,
                upgrades, "explosionDelaySeconds", 0f);
            EffectDurationSeconds = Mathf.Max(
                SkillStatMath.Add(basis.EffectDurationSeconds, upgrades,
                    "effectDurationSeconds", 0.1f), ExplosionDelaySeconds + 0.1f);
            SplashDamageMultiplier = SkillStatMath.Add(
                basis.SplashDamageMultiplier, upgrades,
                "damage", 0.01f) + upgrades.GetUpgrade("splashDamageMultiplier");
            BossDamageMultiplier = SkillStatMath.Add(
                basis.BossDamageMultiplier, upgrades,
                "bossDamageMultiplier", 0.01f);
            SplashDamage = SkillStatMath.ScaleDamage(SplashDamageMultiplier);
            BossDamage = SkillStatMath.ScaleDamage(
                SplashDamageMultiplier * BossDamageMultiplier);
            float radiusBase = basis.RadiusMultiplier > 0f
                ? basis.RadiusMultiplier
                : 1f;
            RadiusMultiplier = radiusBase * SkillStatMath.Add(1f, upgrades,
                "radius", 0.1f);
        }

        internal float CooldownSeconds { get; }
        internal float FirstCastDelaySeconds { get; }
        internal float ExplosionDelaySeconds { get; }
        internal float EffectDurationSeconds { get; }
        internal float SplashDamageMultiplier { get; }
        internal float BossDamageMultiplier { get; }
        internal long SplashDamage { get; }
        internal long BossDamage { get; }
        internal float RadiusMultiplier { get; }
    }

    [Serializable]
    internal struct ThunderBaseStat
    {
        [Min(0.1f)] public float CooldownSeconds;
        [Min(0f)] public float FirstCastDelaySeconds;
        [Min(0f)] public float ImpactDelaySeconds;
        [Min(0.1f)] public float EffectDurationSeconds;
        [Min(0.01f)] public float DamageMultiplier;
        [Min(0.1f)] public float RadiusWorldUnits;
        [Min(0.01f)] public float RadiusMultiplier;
    }

    internal readonly struct ThunderRuntimeStat
    {
        internal ThunderRuntimeStat(ThunderBaseStat basis,
            SkillCombatSnapshot upgrades)
        {
            CooldownSeconds = SkillStatMath.Add(basis.CooldownSeconds, upgrades,
                "cooldownSeconds", 0.1f);
            FirstCastDelaySeconds = SkillStatMath.Add(basis.FirstCastDelaySeconds,
                upgrades, "firstCastDelaySeconds", 0f);
            ImpactDelaySeconds = SkillStatMath.Add(basis.ImpactDelaySeconds,
                upgrades, "impactDelaySeconds", 0f);
            EffectDurationSeconds = Mathf.Max(
                SkillStatMath.Add(basis.EffectDurationSeconds, upgrades,
                    "effectDurationSeconds", 0.1f), ImpactDelaySeconds + 0.01f);
            DamageMultiplier = SkillStatMath.Add(basis.DamageMultiplier, upgrades,
                "damage", 0.01f) + upgrades.GetUpgrade("damageMultiplier");
            Damage = SkillStatMath.ScaleDamage(DamageMultiplier);
            float radiusBase = basis.RadiusWorldUnits > 0f
                ? basis.RadiusWorldUnits
                : 1.5f;
            float radiusMultiplier = basis.RadiusMultiplier > 0f
                ? basis.RadiusMultiplier
                : 1f;
            RadiusWorldUnits = radiusBase * radiusMultiplier *
                SkillStatMath.Add(1f, upgrades, "radius", 0.1f);
        }

        internal float CooldownSeconds { get; }
        internal float FirstCastDelaySeconds { get; }
        internal float ImpactDelaySeconds { get; }
        internal float EffectDurationSeconds { get; }
        internal float DamageMultiplier { get; }
        internal long Damage { get; }
        internal float RadiusWorldUnits { get; }
    }

    [Serializable]
    internal struct FlameExhaustBaseStat
    {
        [Min(0.1f)] public float CooldownSeconds;
        [Min(0f)] public float FirstCastDelaySeconds;
        [Min(0.1f)] public float ProjectileDurationSeconds;
        [Min(0f)] public float SpeedWorldUnitsPerSecond;
        [Range(0.05f, 1f)] public float CursorSizeRatio;
        [Min(0.01f)] public float HitRadiusMultiplier;
        [Min(0.01f)] public float DamageMultiplier;
        [Min(0.1f)] public float HitEffectDurationSeconds;
    }

    internal readonly struct FlameExhaustRuntimeStat
    {
        internal FlameExhaustRuntimeStat(FlameExhaustBaseStat basis,
            SkillCombatSnapshot upgrades)
        {
            CooldownSeconds = SkillStatMath.Add(basis.CooldownSeconds, upgrades,
                "cooldownSeconds", 0.1f);
            FirstCastDelaySeconds = Mathf.Max(0f, basis.FirstCastDelaySeconds);
            ProjectileDurationSeconds = Mathf.Max(0.1f,
                basis.ProjectileDurationSeconds);
            SpeedWorldUnitsPerSecond = Mathf.Max(0f,
                basis.SpeedWorldUnitsPerSecond);
            CursorSizeRatio = Mathf.Clamp(basis.CursorSizeRatio, 0.05f, 1f);
            HitRadiusMultiplier = (basis.HitRadiusMultiplier > 0f
                ? basis.HitRadiusMultiplier
                : 1.2f) * SkillStatMath.Add(1f, upgrades, "radius", 0.1f);
            DamageMultiplier = SkillStatMath.Add(basis.DamageMultiplier, upgrades,
                "damage", 0.01f) + upgrades.GetUpgrade("damageMultiplier");
            Damage = SkillStatMath.ScaleDamage(DamageMultiplier);
            HitEffectDurationSeconds = Mathf.Max(0.1f,
                basis.HitEffectDurationSeconds);
        }

        internal float CooldownSeconds { get; }
        internal float FirstCastDelaySeconds { get; }
        internal float ProjectileDurationSeconds { get; }
        internal float SpeedWorldUnitsPerSecond { get; }
        internal float CursorSizeRatio { get; }
        internal float HitRadiusMultiplier { get; }
        internal float DamageMultiplier { get; }
        internal long Damage { get; }
        internal float HitEffectDurationSeconds { get; }
    }

    [Serializable]
    internal struct MagicBallBaseStat
    {
        [Min(0f)] public float FirstCastDelaySeconds;
        [Min(0.01f)] public float MoveSpeedWorldUnitsPerSecond;
        [Min(0.01f)] public float SizeMultiplier;
        [Min(0.01f)] public float DamageMultiplier;
        [Min(0f)] public float SameTargetHitCooldownSeconds;
    }

    internal readonly struct MagicBallRuntimeStat
    {
        internal MagicBallRuntimeStat(MagicBallBaseStat basis,
            SkillCombatSnapshot upgrades)
        {
            FirstCastDelaySeconds = Mathf.Max(0f, basis.FirstCastDelaySeconds);
            MoveSpeedWorldUnitsPerSecond = SkillStatMath.Add(
                basis.MoveSpeedWorldUnitsPerSecond, upgrades,
                "moveSpeedWorldUnitsPerSecond", 0.01f);
            SizeMultiplier = SkillStatMath.Add(basis.SizeMultiplier,
                upgrades, "radius", 0.01f) + upgrades.GetUpgrade("sizeMultiplier");
            DamageMultiplier = SkillStatMath.Add(basis.DamageMultiplier,
                upgrades, "damage", 0.01f) + upgrades.GetUpgrade("damageMultiplier");
            Damage = SkillStatMath.ScaleDamage(DamageMultiplier);
            SameTargetHitCooldownSeconds = Mathf.Max(0f,
                basis.SameTargetHitCooldownSeconds);
        }

        internal float FirstCastDelaySeconds { get; }
        internal float MoveSpeedWorldUnitsPerSecond { get; }
        internal float SizeMultiplier { get; }
        internal float DamageMultiplier { get; }
        internal long Damage { get; }
        internal float SameTargetHitCooldownSeconds { get; }
    }

    [Serializable]
    internal struct LaserBaseStat
    {
        [Min(0.1f)] public float CooldownSeconds;
        [Min(0f)] public float FirstCastDelaySeconds;
        [Min(0.1f)] public float EffectDurationSeconds;
        [Min(0.01f)] public float SizeMultiplier;
        [Min(0.01f)] public float DamageMultiplier;
    }

    internal readonly struct LaserRuntimeStat
    {
        internal LaserRuntimeStat(LaserBaseStat basis,
            SkillCombatSnapshot upgrades)
        {
            CooldownSeconds = SkillStatMath.Add(basis.CooldownSeconds,
                upgrades, "cooldownSeconds", 0.1f);
            FirstCastDelaySeconds = Mathf.Max(0f, basis.FirstCastDelaySeconds);
            EffectDurationSeconds = Mathf.Max(0.1f, basis.EffectDurationSeconds);
            SizeMultiplier = SkillStatMath.Add(basis.SizeMultiplier,
                upgrades, "radius", 0.01f) + upgrades.GetUpgrade("sizeMultiplier");
            DamageMultiplier = SkillStatMath.Add(basis.DamageMultiplier,
                upgrades, "damage", 0.01f) + upgrades.GetUpgrade("damageMultiplier");
            Damage = SkillStatMath.ScaleDamage(DamageMultiplier);
        }

        internal float CooldownSeconds { get; }
        internal float FirstCastDelaySeconds { get; }
        internal float EffectDurationSeconds { get; }
        internal float SizeMultiplier { get; }
        internal float DamageMultiplier { get; }
        internal long Damage { get; }
    }

    [Serializable]
    internal struct PetBaseStat
    {
        [Min(1)] public int SimultaneousHits;
        [Min(0.01f)] public float DamageMultiplier;
        [Min(0.5f)] public float AttackIntervalSeconds;
        [Min(0.01f)] public float MoveSpeedWorldUnitsPerSecond;
        [Min(0.01f)] public float DetectionRadiusWorldUnits;
        [Min(0.01f)] public float AttackRangeWorldUnits;
    }

    internal readonly struct PetRuntimeStat
    {
        internal PetRuntimeStat(PetBaseStat basis,
            SkillCombatSnapshot upgrades,
            RelicCombatModifiers relics = default)
        {
            DamageMultiplier = SkillStatMath.Add(basis.DamageMultiplier,
                upgrades, "damage", 0.01f) + upgrades.GetUpgrade("damageMultiplier");
            Damage = SkillStatMath.ScaleDamage(DamageMultiplier);
            SimultaneousHits = SkillStatMath.AddCount(
                basis.SimultaneousHits > 0 ? basis.SimultaneousHits : 1,
                upgrades, "simultaneousHits");
            float attackInterval = SkillStatMath.Add(
                basis.AttackIntervalSeconds, upgrades,
                "attackIntervalSeconds", 0.5f);
            AttackIntervalSeconds = Mathf.Max(0.5f, attackInterval *
                (1f - Mathf.Clamp(relics.PetAttackIntervalReductionPercent, 0f, 95f) / 100f));
            MoveSpeedWorldUnitsPerSecond = SkillStatMath.Add(
                basis.MoveSpeedWorldUnitsPerSecond, upgrades,
                "moveSpeedWorldUnitsPerSecond", 0.01f) *
                relics.PetMoveSpeedMultiplier;
            DetectionRadiusWorldUnits = SkillStatMath.Add(
                basis.DetectionRadiusWorldUnits, upgrades,
                "detectionRadiusWorldUnits", 0.01f) *
                relics.PetDetectionRadiusMultiplier;
            AttackRangeWorldUnits = SkillStatMath.Add(
                basis.AttackRangeWorldUnits, upgrades,
                "attackRangeWorldUnits", 0.01f) *
                relics.PetAttackRadiusMultiplier;
        }

        internal float DamageMultiplier { get; }
        internal long Damage { get; }
        internal int SimultaneousHits { get; }
        internal float AttackIntervalSeconds { get; }
        internal float MoveSpeedWorldUnitsPerSecond { get; }
        internal float DetectionRadiusWorldUnits { get; }
        internal float AttackRangeWorldUnits { get; }
    }
}
