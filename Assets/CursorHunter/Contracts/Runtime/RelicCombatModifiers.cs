using System;
using System.Collections.Generic;

namespace CursorHunter.Contracts
{
    /// <summary>
    /// Aggregated effects from the relics owned when a run starts. Values are
    /// copied from GameInformation and never read from progression at runtime.
    /// Percent values are stored as percentage points (5 means +5%).
    /// </summary>
    public readonly struct RelicCombatModifiers
    {
        public RelicCombatModifiers(
            float cursorAttackDamagePercent = 0f,
            float autoSkillDamagePercent = 0f,
            float petDamagePercent = 0f,
            float petMoveSpeedPercent = 0f,
            float petAttackIntervalReductionPercent = 0f,
            float normalFieldDurationBonusSeconds = 0f,
            float areaSkillRadiusPercent = 0f,
            float criticalDamageMultiplierBonus = 0f,
            float petDoubleDamageChancePercent = 0f,
            float lowHealthExecuteChancePercent = 0f,
            float lowHealthExecuteThresholdPercent = 0f,
            float bonusGemDropChancePercent = 0f,
            float petAttackRadiusPercent = 0f,
            float garnetKillRewardPercent = 0f,
            float petDetectionRadiusPercent = 0f,
            float monsterSpawnIntervalReductionPercent = 0f,
            float cursorAttackRadiusPercent = 0f,
            float fullHealthFirstHitDamagePercent = 0f,
            float autoSkillCooldownReductionPercent = 0f,
            float bossDamagePercent = 0f,
            float monsterKillRewardPercent = 0f)
        {
            CursorAttackDamagePercent = cursorAttackDamagePercent;
            AutoSkillDamagePercent = autoSkillDamagePercent;
            PetDamagePercent = petDamagePercent;
            PetMoveSpeedPercent = petMoveSpeedPercent;
            PetAttackIntervalReductionPercent = petAttackIntervalReductionPercent;
            NormalFieldDurationBonusSeconds = normalFieldDurationBonusSeconds;
            AreaSkillRadiusPercent = areaSkillRadiusPercent;
            CriticalDamageMultiplierBonus = criticalDamageMultiplierBonus;
            PetDoubleDamageChancePercent = petDoubleDamageChancePercent;
            LowHealthExecuteChancePercent = lowHealthExecuteChancePercent;
            LowHealthExecuteThresholdPercent = lowHealthExecuteThresholdPercent;
            BonusGemDropChancePercent = bonusGemDropChancePercent;
            PetAttackRadiusPercent = petAttackRadiusPercent;
            GarnetKillRewardPercent = garnetKillRewardPercent;
            PetDetectionRadiusPercent = petDetectionRadiusPercent;
            MonsterSpawnIntervalReductionPercent = monsterSpawnIntervalReductionPercent;
            CursorAttackRadiusPercent = cursorAttackRadiusPercent;
            FullHealthFirstHitDamagePercent = fullHealthFirstHitDamagePercent;
            AutoSkillCooldownReductionPercent = autoSkillCooldownReductionPercent;
            BossDamagePercent = bossDamagePercent;
            MonsterKillRewardPercent = monsterKillRewardPercent;
        }

        public float CursorAttackDamagePercent { get; }
        public float AutoSkillDamagePercent { get; }
        public float PetDamagePercent { get; }
        public float PetMoveSpeedPercent { get; }
        public float PetAttackIntervalReductionPercent { get; }
        public float NormalFieldDurationBonusSeconds { get; }
        public float AreaSkillRadiusPercent { get; }
        public float CriticalDamageMultiplierBonus { get; }
        public float PetDoubleDamageChancePercent { get; }
        public float LowHealthExecuteChancePercent { get; }
        public float LowHealthExecuteThresholdPercent { get; }
        public float BonusGemDropChancePercent { get; }
        public float PetAttackRadiusPercent { get; }
        public float GarnetKillRewardPercent { get; }
        public float PetDetectionRadiusPercent { get; }
        public float MonsterSpawnIntervalReductionPercent { get; }
        public float CursorAttackRadiusPercent { get; }
        public float FullHealthFirstHitDamagePercent { get; }
        public float AutoSkillCooldownReductionPercent { get; }
        public float BossDamagePercent { get; }
        public float MonsterKillRewardPercent { get; }

        public float CursorAttackDamageMultiplier => Multiplier(CursorAttackDamagePercent);
        public float AutoSkillDamageMultiplier => Multiplier(AutoSkillDamagePercent);
        public float PetDamageMultiplier => Multiplier(PetDamagePercent);
        public float PetMoveSpeedMultiplier => Multiplier(PetMoveSpeedPercent);
        public float AreaSkillRadiusMultiplier => Multiplier(AreaSkillRadiusPercent);
        public float PetAttackRadiusMultiplier => Multiplier(PetAttackRadiusPercent);
        public float PetDetectionRadiusMultiplier => Multiplier(PetDetectionRadiusPercent);
        public float CursorAttackRadiusMultiplier => Multiplier(CursorAttackRadiusPercent);
        public float BossDamageMultiplier => Multiplier(BossDamagePercent);
        public float NormalMonsterRewardMultiplier => Multiplier(
            GarnetKillRewardPercent + MonsterKillRewardPercent);
        public float AllNormalMonsterRewardMultiplier => Multiplier(MonsterKillRewardPercent);
        public float AutoSkillCooldownMultiplier =>
            1f - ClampPercent(AutoSkillCooldownReductionPercent, 95f) / 100f;

        public CombatSnapshot ApplyTo(CombatSnapshot value)
        {
            return new CombatSnapshot(
                value.AttackPower,
                value.RangeMultiplier * CursorAttackRadiusMultiplier,
                value.AttackCooldownSeconds,
                value.HitsPerBundle,
                value.CriticalChancePercent,
                value.BossDamageMultiplier * BossDamageMultiplier,
                value.AutoAttackEnabled,
                value.AutoAttackIntervalSeconds,
                value.CriticalDamageMultiplier + CriticalDamageMultiplierBonus);
        }

        public static RelicCombatModifiers From(
            IReadOnlyList<RelicCombatSnapshot> relics)
        {
            if (relics == null || relics.Count == 0) return default;

            float cursorAttackDamage = 0f;
            float autoSkillDamage = 0f;
            float petDamage = 0f;
            float petMoveSpeed = 0f;
            float petAttackInterval = 0f;
            float normalDuration = 0f;
            float areaSkillRadius = 0f;
            float criticalDamage = 0f;
            float petDoubleDamage = 0f;
            float lowHealthExecuteChance = 0f;
            float lowHealthExecuteThreshold = 0f;
            float bonusGemDropChance = 0f;
            float petAttackRadius = 0f;
            float garnetReward = 0f;
            float petDetectionRadius = 0f;
            float spawnIntervalReduction = 0f;
            float cursorAttackRadius = 0f;
            float fullHealthFirstHitDamage = 0f;
            float autoSkillCooldownReduction = 0f;
            float bossDamage = 0f;
            float monsterKillReward = 0f;

            for (int i = 0; i < relics.Count; i++)
            {
                RelicCombatSnapshot relic = relics[i];
                if (!relic.IsValid)
                    throw new ArgumentException("Invalid relic combat snapshot.", nameof(relics));
                if (!relic.Owned) continue;

                switch (relic.EffectId)
                {
                    case "cursorAttackDamagePercent": cursorAttackDamage += relic.EffectValue; break;
                    case "autoSkillDamagePercent": autoSkillDamage += relic.EffectValue; break;
                    case "petDamagePercent": petDamage += relic.EffectValue; break;
                    case "petMoveSpeedPercent": petMoveSpeed += relic.EffectValue; break;
                    case "petAttackIntervalReductionPercent": petAttackInterval += relic.EffectValue; break;
                    case "normalFieldDurationBonusSeconds": normalDuration += relic.EffectValue; break;
                    case "areaSkillRadiusPercent": areaSkillRadius += relic.EffectValue; break;
                    case "criticalDamageMultiplierBonus": criticalDamage += relic.EffectValue; break;
                    case "petDoubleDamageChancePercent": petDoubleDamage += relic.EffectValue; break;
                    case "lowHealthExecuteChancePercent":
                        lowHealthExecuteChance += relic.EffectValue;
                        lowHealthExecuteThreshold = Math.Max(
                            lowHealthExecuteThreshold, relic.EffectValue2);
                        break;
                    case "bonusGemDropChancePercent": bonusGemDropChance += relic.EffectValue; break;
                    case "petAttackRadiusPercent": petAttackRadius += relic.EffectValue; break;
                    case "garnetKillRewardPercent": garnetReward += relic.EffectValue; break;
                    case "petDetectionRadiusPercent": petDetectionRadius += relic.EffectValue; break;
                    case "monsterSpawnIntervalReductionPercent": spawnIntervalReduction += relic.EffectValue; break;
                    case "cursorAttackRadiusPercent": cursorAttackRadius += relic.EffectValue; break;
                    case "fullHealthFirstHitDamagePercent": fullHealthFirstHitDamage += relic.EffectValue; break;
                    case "autoSkillCooldownReductionPercent": autoSkillCooldownReduction += relic.EffectValue; break;
                    case "bossDamagePercent": bossDamage += relic.EffectValue; break;
                    case "monsterKillRewardPercent": monsterKillReward += relic.EffectValue; break;
                    default:
                        throw new ArgumentException(
                            "Unknown relic combat effect: " + relic.EffectId, nameof(relics));
                }
            }

            return new RelicCombatModifiers(
                cursorAttackDamage, autoSkillDamage, petDamage, petMoveSpeed,
                petAttackInterval, normalDuration, areaSkillRadius, criticalDamage,
                petDoubleDamage, lowHealthExecuteChance, lowHealthExecuteThreshold,
                bonusGemDropChance, petAttackRadius, garnetReward, petDetectionRadius,
                spawnIntervalReduction, cursorAttackRadius, fullHealthFirstHitDamage,
                autoSkillCooldownReduction, bossDamage, monsterKillReward);
        }

        private static float Multiplier(float percent) => 1f + Math.Max(0f, percent) / 100f;

        private static float ClampPercent(float value, float maximum) =>
            Math.Max(0f, Math.Min(maximum, value));
    }
}
