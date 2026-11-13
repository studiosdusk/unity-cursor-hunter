using System;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Progression.ResearchPrototype
{
    // These targets describe additive deltas, not copies of the player's base values.
    public enum ResearchStat { Attack = 0, Radius = 1, Interval = 2, CriticalChance = 3, CriticalDamage = 4, BossDamage = 5 }
    public enum ResearchValueDisplay { Number = 0, PercentPoints = 1, FractionPercent = 2, RadiusRatio = 3 }
    public enum ResearchEffectKind { CursorStat = 0, SkillUnlock = 1, SkillStat = 2, MonsterUnlock = 3, MonsterProduction = 4, FieldDuration = 5 }

    [Serializable]
    public sealed class ResearchPresentation
    {
        [Tooltip("ResearchText.xlsx의 키 접두사. 제목과 설명은 각각 .title, .description 키를 사용합니다.")]
        public string textKey;
        public Sprite icon;
        [Tooltip("How the node's cumulative contribution is presented, independent of combat units.")]
        public ResearchValueDisplay valueDisplay;
    }

    [Serializable]
    public struct ResearchCost
    {
        [Tooltip("Gemstone ID, for example gem.garnet or gem.topaz.")]
        public string currencyId;
        [Min(0)] public long amount;
    }

    [Serializable]
    public struct ResearchRank
    {
        [InspectorName("레벨별 증가량"), Tooltip("이 레벨을 구매할 때 추가되는 효과. 예: 매 레벨 공격력 +3이면 각 레벨에 3을 입력합니다.")]
        public float levelDelta;
        [HideInInspector] public ResearchCost cost; // Legacy scene compatibility; authored costs belong to map placements.
        // Existing test builders use garnet; live data uses cost.currencyId and cost.amount.
        public long garnetCost { get => cost.amount; set { cost.currencyId = "gem.garnet"; cost.amount = value; } }
    }

    [Serializable]
    public sealed class ResearchGameplay
    {
        public ResearchEffectKind kind;
        [Tooltip("스킬 또는 몬스터 고유 ID")]
        public string targetId;
        [Tooltip("스킬 강화 스탯 ID")]
        public string statId;
        [Tooltip("Attack: whole points; Radius: world units; Interval: seconds; CriticalChance: percentage points; damage: multiplier delta.")]
        public ResearchStat target;
        [InspectorName("기준값 (생성량·필드 시간)"), Tooltip("몬스터 생성량과 필드 시간의 연구 기준값입니다. 레벨별 증가량을 이 값에 더합니다.")]
        public float baselineValue;
        [Tooltip("각 레벨을 구매할 때 추가되는 효과입니다. 배열 길이가 최대 레벨입니다.")]
        public ResearchRank[] ranks = Array.Empty<ResearchRank>();
    }

    [CreateAssetMenu(menuName = "Cursor Hunter/Research/Node Definition")]
    public sealed class ResearchDefinition : ScriptableObject
    {
        [Tooltip("Stable definition ID. Each map placement also requires its own stable placement ID.")]
        public string stableId;
        public ResearchPresentation presentation = new ResearchPresentation();
        public ResearchGameplay gameplay = new ResearchGameplay();

        // Compatibility with existing tools; only the two grouped fields are serialized.
        public Sprite icon { get => presentation?.icon; set { presentation ??= new ResearchPresentation(); presentation.icon = value; } }
        public ResearchStat stat { get => gameplay == null ? default : gameplay.target; set { gameplay ??= new ResearchGameplay(); gameplay.target = value; } }
        public ResearchRank[] ranks { get => gameplay?.ranks ?? Array.Empty<ResearchRank>(); set { gameplay ??= new ResearchGameplay(); gameplay.ranks = value; } }

        // The saved rank is the only state needed; sum its authored increments on demand.
        public float GetEffectValue(int rank)
        {
            if (gameplay == null || gameplay.ranks == null || rank <= 0) return 0f;
            double value = gameplay.kind == ResearchEffectKind.MonsterProduction || gameplay.kind == ResearchEffectKind.FieldDuration
                ? gameplay.baselineValue : 0d;
            for (int i = 0; i < Math.Min(rank, gameplay.ranks.Length); i++) value += gameplay.ranks[i].levelDelta;
            return (float)value;
        }

        public static bool IsSupportedCurrency(string currencyId)
        {
            switch (currencyId)
            {
                case "gem.garnet": case "gem.topaz": case "gem.amethyst":
                case "gem.sapphire": case "gem.diamond": case "gem.dragon": return true;
                default: return false;
            }
        }

        public bool TryValidate(out string error)
        {
            if (!TryValidateGameplay(out error)) return false;
            if (presentation == null || string.IsNullOrWhiteSpace(presentation.textKey) ||
                !Enum.IsDefined(typeof(ResearchValueDisplay), presentation.valueDisplay))
            { error = "Research presentation or value display is invalid."; return false; }
            return true;
        }

        public bool TryValidateGameplay(out string error)
        {
            if (string.IsNullOrWhiteSpace(stableId)) { error = "Research definition ID is empty."; return false; }
            if (gameplay == null || gameplay.ranks == null || gameplay.ranks.Length == 0)
            { error = "Research gameplay or ranks are missing."; return false; }
            if (!Enum.IsDefined(typeof(ResearchEffectKind), gameplay.kind) ||
                (gameplay.kind == ResearchEffectKind.CursorStat && !Enum.IsDefined(typeof(ResearchStat), gameplay.target)))
            { error = "Research target is invalid."; return false; }
            if ((gameplay.kind == ResearchEffectKind.SkillUnlock || gameplay.kind == ResearchEffectKind.SkillStat) &&
                (!SkillStatUpgrade.IsKnownSkill(gameplay.targetId) ||
                 (gameplay.kind == ResearchEffectKind.SkillStat && !SkillStatUpgrade.IsAllowed(gameplay.targetId, gameplay.statId))))
            { error = "Research skill target is invalid."; return false; }
            if ((gameplay.kind == ResearchEffectKind.MonsterUnlock || gameplay.kind == ResearchEffectKind.MonsterProduction) &&
                (gameplay.targetId == null || gameplay.targetId.Length != 10 ||
                 !gameplay.targetId.StartsWith("monster.", StringComparison.Ordinal) ||
                 !int.TryParse(gameplay.targetId.Substring(8), out int monsterNumber) || monsterNumber < 1 || monsterNumber > 10 ||
                 gameplay.targetId != "monster." + monsterNumber.ToString("00", System.Globalization.CultureInfo.InvariantCulture) ||
                 (gameplay.kind == ResearchEffectKind.MonsterUnlock && monsterNumber == 1)))
            { error = "Research monster target is invalid."; return false; }
            bool unlock = gameplay.kind == ResearchEffectKind.SkillUnlock || gameplay.kind == ResearchEffectKind.MonsterUnlock;
            if (unlock && gameplay.ranks.Length != 1) { error = "Unlock research must have one rank."; return false; }
            if (float.IsNaN(gameplay.baselineValue) || float.IsInfinity(gameplay.baselineValue) ||
                (gameplay.kind == ResearchEffectKind.FieldDuration && (gameplay.baselineValue < 15f || gameplay.baselineValue > 30f)) ||
                (gameplay.kind == ResearchEffectKind.MonsterProduction && (gameplay.baselineValue < 1f || gameplay.baselineValue > 16f || gameplay.baselineValue != Math.Floor(gameplay.baselineValue))))
            { error = "Research baseline is invalid."; return false; }
            for (int i = 0; i < gameplay.ranks.Length; i++)
            {
                ResearchRank rank = gameplay.ranks[i];
                float increment = rank.levelDelta;
                float value = GetEffectValue(i + 1);
                bool interval = gameplay.kind == ResearchEffectKind.CursorStat && gameplay.target == ResearchStat.Interval;
                if (!IsSupportedCurrency(rank.cost.currencyId) || rank.cost.amount < 0 || float.IsNaN(increment) || float.IsInfinity(increment) || float.IsNaN(value) || float.IsInfinity(value) ||
                    (interval ? increment > 0f : increment < 0f) ||
                    (unlock && value != 1f) ||
                    (gameplay.kind == ResearchEffectKind.FieldDuration && (value < 15f || value > 30f)) ||
                    (gameplay.kind == ResearchEffectKind.MonsterProduction && (value < 1 || value > 16 || value != Math.Floor(value))) ||
                    (gameplay.kind == ResearchEffectKind.SkillStat && gameplay.statId == "simultaneousHits" && value != Math.Floor(value)) ||
                    (gameplay.kind == ResearchEffectKind.CursorStat && gameplay.target == ResearchStat.Attack && (value != Math.Floor(value) || (double)value >= long.MaxValue)))
                { error = "Research ranks contain an invalid cost or cumulative effect."; return false; }
            }
            error = null;
            return true;
        }
    }
}
