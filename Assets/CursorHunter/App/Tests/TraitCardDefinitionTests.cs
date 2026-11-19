using System.Linq;
using CursorHunter.Data;
using CursorHunter.Progression;
using CursorHunter.Progression.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace CursorHunter.App.Tests
{
    public sealed class TraitCardDefinitionTests
    {
        private const string IconFolder = "Assets/DownLoadAssets/IconPack/2D Minimal-IconPack/Icons/512/";

        [Test]
        public void FourAuthoredDefinitionsExposeValidEffectsAndIcons()
        {
            Assert.That(TraitCardDefinitionResources.TryLoad(out var cards, out string error), Is.True, error);
            Assert.That(cards.Length, Is.EqualTo(4));
            Assert.That(cards.Select(x => x.stableId), Is.EqualTo(new[] {
                "trait_card.lucky_settlement", "trait_card.treasure_chest",
                "trait_card.expanding_focus", "trait_card.hunting_wisdom" }));
            foreach (var card in cards)
            {
                Assert.That(card.TryValidate(out error), Is.True, error);
                Assert.That(AssetDatabase.GetAssetPath(card.presentation.icon), Does.StartWith(IconFolder));
                Assert.That(card.presentation.textKey, Is.EqualTo(card.stableId));
            }
            Assert.That(cards[0].gameplay.effectKind, Is.EqualTo(TraitCardEffectKind.BattleRewardMultiplier));
            Assert.That(cards[0].gameplay.rewardMultiplier.chancePercent, Is.InRange(0f, 100f));
            Assert.That(cards[0].gameplay.rewardMultiplier.rewardMultiplier, Is.GreaterThan(0f));
            Assert.That(cards[1].gameplay.effectKind, Is.EqualTo(TraitCardEffectKind.TreasureChestAtBattleStart));
            Assert.That(cards[1].gameplay.treasureChest.chancePercent, Is.InRange(0f, 100f));
            Assert.That(cards[1].gameplay.treasureChest.spawnCount,
                Is.InRange(1, CursorHunter.Contracts.GameInformation.MaximumAliveMonsters));
            Assert.That(cards[2].gameplay.effectKind, Is.EqualTo(TraitCardEffectKind.PeriodicCursorRangeBoost));
            Assert.That(cards[2].gameplay.cursorRange.intervalSeconds, Is.GreaterThan(0f));
            Assert.That(cards[2].gameplay.cursorRange.chancePercent, Is.InRange(0f, 100f));
            Assert.That(cards[2].gameplay.cursorRange.durationSeconds, Is.GreaterThan(0f));
            Assert.That(cards[2].gameplay.cursorRange.rangeIncreasePercent, Is.GreaterThanOrEqualTo(0f));
            Assert.That(cards[3].gameplay.effectKind, Is.EqualTo(TraitCardEffectKind.NormalMonsterExperienceBonus));
            Assert.That(cards[3].gameplay.normalMonsterExperience.bonusPercent, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void WorkbookTextsMatchDefinitionsAndRenderCurrentGameplayValues()
        {
            Assert.That(TraitCardDefinitionResources.TryLoad(out var cards, out string error), Is.True, error);
            var rows = TraitCardTextWorkbook.ReadRows(TraitCardTextWorkbook.Path).ToDictionary(x => x.Key);
            Assert.That(rows.Count, Is.EqualTo(8));
            foreach (var card in cards)
            {
                Assert.That(rows.ContainsKey(card.stableId + ".title"), Is.True);
                var row = rows[card.stableId + ".description"];
                foreach (string code in new[] { "ko", "en" })
                {
                    Assert.That(TraitCardTextFormatter.TryValidateTemplate(row.Values[code], card.gameplay.effectKind, out error), Is.True, error);
                    string rendered = TraitCardTextFormatter.Format(row.Values[code], card.gameplay);
                    Assert.That(rendered, Does.Not.Contain("{"));
                    Assert.That(rendered, Does.Not.Contain("}"));
                }
            }
            var modified = Object.Instantiate(cards[2]);
            try
            {
                modified.gameplay.cursorRange.chancePercent = 27.5f;
                string output = TraitCardTextFormatter.Format(rows[modified.stableId + ".description"].Values["ko"], modified.gameplay);
                Assert.That(output, Does.Contain("27.50%"));
                Assert.That(output, Does.Contain("50.00%"));
                Assert.That(TraitCardTextFormatter.TryValidateTemplate("{chance} {bonu}", modified.gameplay.effectKind, out error), Is.False);
                modified.gameplay.cursorRange.chancePercent = 101f;
                Assert.That(modified.TryValidate(out error), Is.False);
            }
            finally { Object.DestroyImmediate(modified); }

            var chest = Object.Instantiate(cards[1]);
            try
            {
                chest.gameplay.treasureChest.chancePercent = 27.5f;
                chest.gameplay.treasureChest.spawnCount = 2;
                var description = rows[chest.stableId + ".description"];
                foreach (string code in new[] { "ko", "en" })
                {
                    string output = TraitCardTextFormatter.Format(description.Values[code], chest.gameplay);
                    Assert.That(output, Does.Contain("27.50%"), code);
                    Assert.That(output, Does.Not.Contain("{chance}"), code);
                    Assert.That(output, Does.Not.Contain("{count}"), code);
                }
                Assert.That(TraitCardTextFormatter.Format(description.Values["ko"], chest.gameplay), Does.Contain("보물상자 2개"));
                Assert.That(TraitCardTextFormatter.TryValidateTemplate("{count}", chest.gameplay.effectKind, out error), Is.False);
                Assert.That(TraitCardTextFormatter.TryValidateTemplate("{chance} {count} {unknown}", chest.gameplay.effectKind, out error), Is.False);
                foreach (float valid in new[] { 0f, 100f })
                {
                    chest.gameplay.treasureChest.chancePercent = valid;
                    Assert.That(chest.TryValidate(out error), Is.True, error);
                }
                foreach (float invalid in new[] { -1f, 101f, float.NaN, float.PositiveInfinity })
                {
                    chest.gameplay.treasureChest.chancePercent = invalid;
                    Assert.That(chest.TryValidate(out error), Is.False);
                }
            }
            finally { Object.DestroyImmediate(chest); }
        }

        [Test]
        public void WorkbookImportProducesMatchingKoreanAndEnglishTables()
        {
            TraitCardTextWorkbook.Apply();
            var collection = LocalizationEditorSettings.GetStringTableCollection("TraitCard");
            Assert.That(collection, Is.Not.Null);
            var rows = TraitCardTextWorkbook.ReadRows(TraitCardTextWorkbook.Path);
            foreach (string code in new[] { "ko", "en" })
            {
                var table = collection.GetTable(new LocaleIdentifier(code)) as StringTable;
                Assert.That(table, Is.Not.Null, code);
                foreach (var row in rows)
                {
                    var entry = table.GetEntry(row.Key);
                    Assert.That(entry, Is.Not.Null, row.Key);
                    Assert.That(entry.Value, Is.EqualTo(row.Values[code]), row.Key);
                }
            }
        }
    }
}
