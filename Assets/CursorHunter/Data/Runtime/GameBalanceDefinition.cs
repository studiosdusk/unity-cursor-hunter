using System;
using UnityEngine;

namespace CursorHunter.Data
{
    // Historical v2 compatibility data only. Production uses GameDataDocument.
    [Obsolete("Use GameDataDocument.Load; balance-v2.json is a historical reference.")]
    [Serializable]
    public sealed class GameBalanceDefinition
    {
        public int schemaVersion;
        public int balanceVersion;
        public long[] attackPower;
        public float[] radiusMultiplier;
        public float[] attackCooldownSeconds;
        public float[] criticalChancePercent;
        public float[] bossDamageMultiplier;
        public float[] normalFieldDurationSeconds;
        public MonsterBalanceData[] monsters;
        public SkillBalanceData[] skills;

        public static GameBalanceDefinition Load()
        {
            var asset = Resources.Load<TextAsset>("GameData/balance-v2");
            if (asset == null) throw new InvalidOperationException("Missing GameData/balance-v2.json.");
            var data = JsonUtility.FromJson<GameBalanceDefinition>(asset.text);
            if (data == null || data.schemaVersion != 2 || data.balanceVersion < 1 ||
                data.monsters == null || data.monsters.Length != 10 || data.skills == null || data.skills.Length != 7 ||
                data.attackPower == null || data.attackPower.Length != 10 ||
                data.radiusMultiplier == null || data.radiusMultiplier.Length != 11 ||
                data.attackCooldownSeconds == null || data.attackCooldownSeconds.Length != 5 ||
                data.criticalChancePercent == null || data.criticalChancePercent.Length != 8 ||
                data.bossDamageMultiplier == null || data.bossDamageMultiplier.Length != 6 ||
                data.normalFieldDurationSeconds == null || data.normalFieldDurationSeconds.Length != 4)
                throw new InvalidOperationException("Invalid balance-v2.json shape.");
            return data;
        }
    }

    [Serializable]
    public sealed class MonsterBalanceData
    {
        public string id;
        public string displayName;
        public string concept;
        public string prefabKey;
        public long hitPoints;
        public float spawnIntervalSeconds;
        public string gemstoneId;
        public long gemstoneAmount;
        public float gemstoneChancePercent;
        public long garnetReward;
        public int unlockCost;
        public string unlockCurrencyId;
    }

    [Serializable]
    public sealed class SkillBalanceData
    {
        public string id;
        public string displayName;
        public float damageCoefficient;
        public float radiusWorldUnits;
        public float cooldownSeconds;
        public string currencyId;
        public int unlockCost;
    }
}
