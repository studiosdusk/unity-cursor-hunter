using System.IO;
using System.Linq;
using CursorHunter.Progression.Editor;
using NUnit.Framework;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace CursorHunter.App.Tests
{
    public sealed class WorldStageTextWorkbookTests
    {
        [Test]
        public void MalformedKeysAndPlaceholdersAreRejectedBeforeTablesChange()
        {
            var table = (StringTable)LocalizationEditorSettings.GetStringTableCollection("WorldStage").GetTable(new LocaleIdentifier("ko"));
            const string key = "stage.ui.selected";
            string good = table.GetEntry(key).Value;
            var rows = WorldStageTextWorkbook.ReadValidated(WorldStageTextWorkbook.Path);
            int row = System.Array.FindIndex(rows, r => r.Key == key) + 2;
            string temp = Path.GetTempFileName();
            try
            {
                foreach (string bad in new[] { "", "Missing argument", "{1}", "{0}{0}" })
                {
                    File.Copy(WorldStageTextWorkbook.Path, temp, true);
                    ResearchTextWorkbookTests.Rewrite(temp, "B" + row, bad);
                    Assert.Throws<InvalidDataException>(() => WorldStageTextWorkbook.ReadValidated(temp));
                }
                File.Copy(WorldStageTextWorkbook.Path, temp, true);
                ResearchTextWorkbookTests.Rewrite(temp, "A" + row, "stage.forest.name");
                Assert.Throws<InvalidDataException>(() => WorldStageTextWorkbook.ReadValidated(temp));
                Assert.That(table.GetEntry(key).Value, Is.EqualTo(good));
                File.Copy(WorldStageTextWorkbook.Path, temp, true);
                ResearchTextWorkbookTests.Rewrite(temp, "C" + row, "");
                Assert.That(WorldStageTextWorkbook.ReadValidated(temp).Single(r => r.Key == key).Values["ko"], Is.EqualTo(good));
            }
            finally { File.Delete(temp); }
        }
    }
}
