using System;
using System.Collections.Generic;
using System.Globalization;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Progression
{
    public readonly struct RelicViewState
    {
        public RelicCollectionCatalogEntry Presentation { get; }
        public string Description { get; }
        public bool IsOwned { get; }
        public bool IsNew { get; }

        public RelicViewState(RelicCollectionCatalogEntry presentation, string description, bool isOwned, bool isNew)
        {
            Presentation = presentation;
            Description = description;
            IsOwned = isOwned;
            IsNew = isNew;
        }
    }

    [Serializable]
    public sealed class RelicCollectionCatalogEntry
    {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField] private Sprite icon;
        // Kept only to preserve existing asset data; runtime text comes from combat values.
        [SerializeField, HideInInspector] private string description;

        public string Id => id;
        public string Title => title;
        public Sprite Icon => icon;
    }

    /// <summary>
    /// Runtime-safe source of relic names and icons. Keeping this under
    /// Resources lets the collection recover its presentation if an open
    /// Unity scene still has older, empty serialized relic entries.
    /// </summary>
    public sealed class RelicCollectionCatalog : ScriptableObject
    {
        [SerializeField] private List<RelicCollectionCatalogEntry> entries =
            new List<RelicCollectionCatalogEntry>();

        public IReadOnlyList<RelicCollectionCatalogEntry> Entries => entries;

        public RelicCollectionCatalogEntry FindById(string relicId)
        {
            if (string.IsNullOrWhiteSpace(relicId) || entries == null)
            {
                return null;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                RelicCollectionCatalogEntry entry = entries[i];
                if (entry != null && string.Equals(entry.Id, relicId, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        public RelicCollectionCatalogEntry FindLegacyEntry(string title, Sprite icon)
        {
            if (entries == null)
            {
                return null;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                RelicCollectionCatalogEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if ((!string.IsNullOrWhiteSpace(title) &&
                     string.Equals(entry.Title, title, StringComparison.Ordinal)) ||
                    (icon != null && entry.Icon == icon))
                {
                    return entry;
                }
            }

            return null;
        }
    }

    /// <summary>Formats the active combat definition, rather than cached display text.</summary>
    public static class RelicEffectDescription
    {
        public static string Format(RelicInformation relic)
        {
            if (relic == null) return string.Empty;
            string percent = relic.effectValue.ToString("0.00", CultureInfo.InvariantCulture) + "%";
            string value = relic.effectValue.ToString("0.##", CultureInfo.InvariantCulture);
            string threshold = relic.effectValue2.ToString("0.00", CultureInfo.InvariantCulture) + "%";
            switch (relic.effectId)
            {
                case "cursorAttackDamagePercent": return "커서 기본 공격 피해 +" + percent;
                case "autoSkillDamagePercent": return "자동 스킬 피해 +" + percent;
                case "petDamagePercent": return "펫 공격 피해 +" + percent;
                case "petMoveSpeedPercent": return "펫 이동 속도 +" + percent;
                case "petAttackIntervalReductionPercent": return "펫 공격 간격 -" + percent;
                case "normalFieldDurationBonusSeconds": return "일반 필드 시간 +" + value + "초 (최대 30초)";
                case "areaSkillRadiusPercent": return "범위형 스킬 반경 +" + percent;
                case "criticalDamageMultiplierBonus": return "치명타 피해 배율 +" + value + "배";
                case "petDoubleDamageChancePercent": return "펫 공격이 " + percent + " 확률로 2배 피해";
                case "lowHealthExecuteChancePercent": return "보스 외 몬스터, 체력 " + threshold + " 이하일 때 " + percent + " 확률로 즉사";
                case "relicDropChanceBonusPercent": return "유물 발견 확률 +" + relic.effectValue.ToString("0.00", CultureInfo.InvariantCulture) + "%p";
                case "cursorAttackIntervalReductionPercent": return "기본 공격 간격 -" + percent;
                case "garnetKillRewardPercent": return "처치 가넷 보상 +" + percent;
                case "petAttackRadiusPercent": return "펫 공격 범위 +" + percent;
                case "monsterSpawnIntervalReductionPercent": return "일반 몬스터 생성 간격 -" + percent;
                case "cursorAttackRadiusPercent": return "커서 공격 반경 +" + percent;
                case "fullHealthFirstHitDamagePercent": return "최대 체력 대상 첫 타격 피해 +" + percent;
                case "autoSkillCooldownReductionPercent": return "자동 스킬 쿨타임 -" + percent;
                case "bossDamagePercent": return "보스 피해 +" + percent;
                case "monsterKillRewardPercent": return "일반 몬스터 처치 보상 +" + percent;
                default: return string.Empty;
            }
        }
    }
}
