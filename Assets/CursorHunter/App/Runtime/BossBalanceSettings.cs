using System;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;

namespace CursorHunter.App
{
    /// <summary>Editable boss body stats; identity, behavior and clear rewards stay authored.</summary>
    [Serializable]
    public sealed class BossBalanceSettings
    {
        public string BossId;
        public long MaxHealth;
        public float MoveSpeed;
        public float VisualScale;
        public float HitAreaWidth;
        public float HitAreaHeight;

        public static BossBalanceSettings FromSnapshot(SpawnSnapshot snapshot) => new BossBalanceSettings
        {
            BossId = snapshot.MonsterId, MaxHealth = snapshot.MaxHealth,
            MoveSpeed = snapshot.MoveSpeed, VisualScale = snapshot.VisualScale,
            HitAreaWidth = snapshot.HitAreaWidth, HitAreaHeight = snapshot.HitAreaHeight
        };

        public bool TryCreateSnapshot(BossDefinition definition, out SpawnSnapshot snapshot, out string error)
        {
            snapshot = default;
            error = string.Empty;
            if (definition == null || !definition.IsValid || BossId != definition.BossId)
            {
                error = "보스 ID와 원본 데이터를 확인하세요.";
                return false;
            }
            SpawnSnapshot reference = definition.CreateSnapshot();
            MonsterBalanceSettings body = MonsterBalanceSettings.FromSnapshot(reference);
            body.MaxHealth = MaxHealth; body.MoveSpeed = MoveSpeed; body.VisualScale = VisualScale;
            body.HitAreaWidth = HitAreaWidth; body.HitAreaHeight = HitAreaHeight;
            return body.TryCreateSnapshot(reference, 1, out snapshot, out error);
        }
    }
}
