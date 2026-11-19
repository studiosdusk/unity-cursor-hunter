using System;
using System.Globalization;
using CursorHunter.Data;

namespace CursorHunter.Progression
{
    /// <summary>Resolves workbook placeholders from the card's gameplay values.</summary>
    public static class TraitCardTextFormatter
    {
        public static string Format(string template, TraitCardGameplayDefinition gameplay)
        {
            if (string.IsNullOrEmpty(template) || gameplay == null) return template ?? string.Empty;
            switch (gameplay.effectKind)
            {
                case TraitCardEffectKind.BattleRewardMultiplier when gameplay.rewardMultiplier != null:
                    return template.Replace("{chance}", Percent(gameplay.rewardMultiplier.chancePercent))
                        .Replace("{multiplier}", Number(gameplay.rewardMultiplier.rewardMultiplier));
                case TraitCardEffectKind.TreasureChestAtBattleStart when gameplay.treasureChest != null:
                    return template.Replace("{chance}", Percent(gameplay.treasureChest.chancePercent))
                        .Replace("{count}", gameplay.treasureChest.spawnCount.ToString(CultureInfo.InvariantCulture));
                case TraitCardEffectKind.PeriodicCursorRangeBoost when gameplay.cursorRange != null:
                    return template.Replace("{interval}", Number(gameplay.cursorRange.intervalSeconds))
                        .Replace("{chance}", Percent(gameplay.cursorRange.chancePercent))
                        .Replace("{duration}", Number(gameplay.cursorRange.durationSeconds))
                        .Replace("{bonus}", Percent(gameplay.cursorRange.rangeIncreasePercent));
                case TraitCardEffectKind.NormalMonsterExperienceBonus when gameplay.normalMonsterExperience != null:
                    return template.Replace("{bonus}", Percent(gameplay.normalMonsterExperience.bonusPercent));
                default:
                    return template;
            }
        }

        public static bool TryValidateTemplate(string template, TraitCardEffectKind kind, out string error)
        {
            string[] required = kind switch
            {
                TraitCardEffectKind.BattleRewardMultiplier => new[] { "{chance}", "{multiplier}" },
                TraitCardEffectKind.TreasureChestAtBattleStart => new[] { "{chance}", "{count}" },
                TraitCardEffectKind.PeriodicCursorRangeBoost => new[] { "{interval}", "{chance}", "{duration}", "{bonus}" },
                TraitCardEffectKind.NormalMonsterExperienceBonus => new[] { "{bonus}" },
                _ => null
            };
            if (required == null || string.IsNullOrWhiteSpace(template))
            { error = "Description or effect kind is invalid."; return false; }
            string remainder = template;
            foreach (string token in required)
            {
                if (!remainder.Contains(token))
                { error = "Description is missing " + token; return false; }
                remainder = remainder.Replace(token, string.Empty);
            }
            if (remainder.IndexOf('{') >= 0 || remainder.IndexOf('}') >= 0)
            { error = "Description contains an unknown or malformed placeholder."; return false; }
            error = null;
            return true;
        }

        private static string Percent(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
        private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
