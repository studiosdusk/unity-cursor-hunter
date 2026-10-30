using System;
using System.Collections.Generic;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.App
{
    /// <summary>Transient enemy values and an optional manual player override; never saved as progression.</summary>
    [Serializable]
    public sealed class CombatBalanceSettings
    {
        // Enemy edits must not replace purchased player stats or skill unlocks by default.
        public bool UsePlayerProgression = true;
        public long AttackPower = 10;
        public float AttackRadiusWorldUnits = GameInformation.BaseAttackRadiusWorldUnits;
        public float AttackCooldownSeconds = 0.8f;
        public bool AutoAttackEnabled = true;
        public float CriticalChancePercent = 5f;
        public float CriticalDamageMultiplier = 2f;
        public float BossDamageMultiplier = 1f;
        public float DurationSeconds = 15f;
        public int GlobalAliveLimit = 80;
        public int PerMonsterAliveLimit = 80;
        public List<SkillBalanceSettings> Skills = new List<SkillBalanceSettings>();
        public List<MonsterBalanceSettings> Monsters = new List<MonsterBalanceSettings>();
        public List<BossBalanceSettings> Bosses = new List<BossBalanceSettings>();

        public CombatBalanceSettings Clone() => JsonUtility.FromJson<CombatBalanceSettings>(
            JsonUtility.ToJson(this));

        public CombatSnapshot ToCombatSnapshot() => new CombatSnapshot(AttackPower,
            AttackRadiusWorldUnits / GameInformation.BaseAttackRadiusWorldUnits,
            AttackCooldownSeconds, 1, CriticalChancePercent, BossDamageMultiplier,
            AutoAttackEnabled, AttackCooldownSeconds, CriticalDamageMultiplier);

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (AttackPower < 1L) error = "공격력은 정수 1 이상이어야 합니다.";
            else if (!Positive(AttackRadiusWorldUnits) ||
                AttackRadiusWorldUnits < GameInformation.BaseAttackRadiusWorldUnits * 0.01f)
                error = "공격 반경은 기본 반경의 0.01배 이상이어야 합니다.";
            else if (!Finite(AttackCooldownSeconds) || AttackCooldownSeconds < 0.05f)
                error = "커서 재타격 간격은 0.05초 이상이어야 합니다.";
            else if (!Finite(CriticalChancePercent) || CriticalChancePercent < 0f || CriticalChancePercent > 100f)
                error = "치명타 확률은 0~100%여야 합니다.";
            else if (!Finite(CriticalDamageMultiplier) || CriticalDamageMultiplier < 1f)
                error = "치명타 피해 배율은 1 이상이어야 합니다.";
            else if (!Positive(BossDamageMultiplier)) error = "보스 피해 배율은 양수여야 합니다.";
            else if (!Finite(DurationSeconds) || DurationSeconds < 15f || DurationSeconds > 30f)
                error = "일반 전투 시간은 15~30초여야 합니다.";
            else if (GlobalAliveLimit < 1 || GlobalAliveLimit > 80 ||
                PerMonsterAliveLimit < 1 || PerMonsterAliveLimit > GlobalAliveLimit)
                error = "전체 상한은 1~80, 종류별 공통 상한은 1~전체 상한이어야 합니다.";
            else if (Skills == null || Monsters == null) error = "스킬·몬스터 설정이 필요합니다.";
            return error.Length == 0;
        }

        private static bool Positive(float value) => Finite(value) && value > 0f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
