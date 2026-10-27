using System;
using CursorHunter.Contracts;

namespace CursorHunter.Combat
{
    /// <summary>Transient, final combat values. Does not modify authored monster data.</summary>
    [Serializable]
    public sealed class MonsterBalanceSettings
    {
        public string MonsterId;
        public bool Enabled = true;
        public long MaxHealth = 1L;
        public float SpawnIntervalSeconds = 1f;
        public int PackSize = 1;
        public float MoveSpeed = 0.45f;
        public float VisualScale = 1f;
        public float HitAreaWidth = 1f;
        public float HitAreaHeight = 1.2f;
        public long GarnetReward;
        public string BonusDropCurrencyId = string.Empty;
        public long BonusDropAmount;
        public float BonusDropChancePercent;

        public static MonsterBalanceSettings FromSnapshot(SpawnSnapshot snapshot, bool enabled = true)
        {
            return new MonsterBalanceSettings
            {
                MonsterId = snapshot.MonsterId,
                Enabled = enabled,
                MaxHealth = snapshot.MaxHealth,
                SpawnIntervalSeconds = snapshot.SpawnIntervalSeconds,
                PackSize = snapshot.PackSize,
                MoveSpeed = snapshot.MoveSpeed,
                VisualScale = snapshot.VisualScale,
                HitAreaWidth = snapshot.HitAreaWidth,
                HitAreaHeight = snapshot.HitAreaHeight,
                GarnetReward = snapshot.GarnetReward,
                BonusDropCurrencyId = snapshot.BonusDropCurrencyId,
                BonusDropAmount = snapshot.BonusDropAmount,
                BonusDropChancePercent = snapshot.BonusDropChancePercent
            };
        }

        public bool TryCreateSnapshot(
            SpawnSnapshot reference,
            int aliveLimit,
            out SpawnSnapshot snapshot,
            out string error)
        {
            snapshot = default;
            error = string.Empty;
            if (!reference.IsValid || MonsterId != reference.MonsterId)
                error = "몬스터 ID와 원본 전투 데이터가 일치하지 않습니다.";
            else if (MaxHealth < 1L || GarnetReward < 0L || BonusDropAmount < 0L)
                error = "HP는 1 이상, 보상은 0 이상이어야 합니다.";
            else if (!Positive(SpawnIntervalSeconds) || PackSize < 1 || aliveLimit < 1 || aliveLimit > 80)
                error = "생성 간격과 마릿수는 양수, 생존 상한은 1~80이어야 합니다.";
            else if (!Positive(MoveSpeed) || !Positive(VisualScale) ||
                     !Positive(HitAreaWidth) || !Positive(HitAreaHeight))
                error = "이동 속도·크기·피격 영역은 유한한 양수여야 합니다.";
            else if (!Finite(BonusDropChancePercent) || BonusDropChancePercent < 0f ||
                     BonusDropChancePercent > 100f)
                error = "추가 보상 확률은 0~100%여야 합니다.";
            else if (BonusDropAmount > 0L &&
                     (string.IsNullOrWhiteSpace(BonusDropCurrencyId) || BonusDropChancePercent <= 0f))
                error = "추가 보상 수량이 있으면 재화 ID와 0%보다 큰 확률이 필요합니다.";
            if (error.Length != 0) return false;

            // The reference already contains all profile composition. These values
            // replace its final stats rather than being multiplied by the profiles again.
            snapshot = new SpawnSnapshot(
                reference.MonsterId, reference.PrefabKey, MaxHealth,
                SpawnIntervalSeconds, PackSize, aliveLimit, GarnetReward,
                BonusDropCurrencyId, BonusDropAmount, BonusDropChancePercent,
                reference.BehaviorType, reference.MovementMode, MoveSpeed,
                VisualScale, HitAreaWidth, HitAreaHeight, reference.OrbitRadius,
                reference.OrbitAngularSpeedDegrees, reference.SpecialAbility,
                reference.SpecialAbilityIntervalSeconds, reference.SpecialAbilityDurationSeconds,
                reference.SpecialAbilityRadius, reference.SpecialAbilityStrength);
            if (snapshot.IsValid) return true;
            error = "몬스터의 최종 전투 데이터가 유효하지 않습니다.";
            return false;
        }

        private static bool Positive(float value) => value > 0f && Finite(value);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
