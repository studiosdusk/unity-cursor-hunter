using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using CursorHunter.App.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.TestTools;

namespace CursorHunter.App.Tests
{
    public sealed class ResultUiTextWorkbookTests
    {
        private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        [Test]
        public void AdditionalLanguageColumnsAreReadWithoutChangingOtherTranslations()
        {
            string copy = Path.GetTempFileName();
            try
            {
                File.Copy(ResultUiTextWorkbook.Path, copy, true);
                Rewrite(copy, "D1", "ja");
                Rewrite(copy, "D2", "撤退");
                var rows = ResultUiTextWorkbook.ReadRows(copy);
                Assert.That(rows.Length, Is.GreaterThanOrEqualTo(31));
                Assert.That(rows.Single(x => x.Key == "ui.result.retired").Values["ja"], Is.EqualTo("撤退"));
                Assert.That(rows.Single(x => x.Key == "ui.result.boss.clear").Values["ja"], Is.Empty);
                Assert.That(rows.Single(x => x.Key == "ui.result.retired").Values["en"], Is.EqualTo("Retired"));
            }
            finally { File.Delete(copy); }
        }

        [Test]
        public void WorkbookUpdatesBothTablesAndInvalidRowsNeverPartiallyApply()
        {
            byte[] original = File.ReadAllBytes(ResultUiTextWorkbook.Path);
            var collection = LocalizationEditorSettings.GetStringTableCollection("ResultUI");
            var korean = collection.GetTable(new LocaleIdentifier("ko")) as StringTable;
            var english = collection.GetTable(new LocaleIdentifier("en")) as StringTable;
            const string key = "ui.result.retired";
            string originalKorean = korean.GetEntry(key).Value;
            string originalEnglish = english.GetEntry(key).Value;
            try
            {
                Rewrite(ResultUiTextWorkbook.Path, "B2", "엑셀 변경 테스트");
                Rewrite(ResultUiTextWorkbook.Path, "C2", "Excel edit test");
                ResultUiTextWorkbook.Apply();
                Assert.That(korean.GetEntry(key).Value, Is.EqualTo("엑셀 변경 테스트"));
                Assert.That(english.GetEntry(key).Value, Is.EqualTo("Excel edit test"));

                Rewrite(ResultUiTextWorkbook.Path, "A3", key);
                Assert.Throws<InvalidDataException>(() => ResultUiTextWorkbook.Apply());
                Assert.That(korean.GetEntry(key).Value, Is.EqualTo("엑셀 변경 테스트"));

                File.WriteAllBytes(ResultUiTextWorkbook.Path, original);
                Rewrite(ResultUiTextWorkbook.Path, "B2", "적용되면 안 되는 문구");
                Rewrite(ResultUiTextWorkbook.Path, "C7", "Boss Stage {1}");
                Assert.Throws<InvalidDataException>(() => ResultUiTextWorkbook.Apply());
                Assert.That(korean.GetEntry(key).Value, Is.EqualTo("엑셀 변경 테스트"));
            }
            finally
            {
                File.WriteAllBytes(ResultUiTextWorkbook.Path, original);
                ResultUiTextWorkbook.Apply();
                Assert.That(korean.GetEntry(key).Value, Is.EqualTo(originalKorean));
                Assert.That(english.GetEntry(key).Value, Is.EqualTo(originalEnglish));
            }
        }

        [UnityTest]
        public IEnumerator WorkbookImportAutomaticallyUpdatesTable()
        {
            byte[] original = File.ReadAllBytes(ResultUiTextWorkbook.Path);
            var table = LocalizationEditorSettings.GetStringTableCollection("ResultUI")
                .GetTable(new LocaleIdentifier("ko")) as StringTable;
            const string key = "ui.result.retired";
            string previous = table.GetEntry(key).Value;
            try
            {
                Rewrite(ResultUiTextWorkbook.Path, "B2", "엑셀 자동 반영");
                AssetDatabase.ImportAsset(ResultUiTextWorkbook.Path, ImportAssetOptions.ForceUpdate);
                double deadline = EditorApplication.timeSinceStartup + 10;
                while (table.GetEntry(key).Value != "엑셀 자동 반영" && EditorApplication.timeSinceStartup < deadline)
                    yield return null;
                Assert.That(table.GetEntry(key).Value, Is.EqualTo("엑셀 자동 반영"));
            }
            finally
            {
                File.WriteAllBytes(ResultUiTextWorkbook.Path, original);
                ResultUiTextWorkbook.Apply();
                AssetDatabase.ImportAsset(ResultUiTextWorkbook.Path, ImportAssetOptions.ForceUpdate);
                Assert.That(table.GetEntry(key).Value, Is.EqualTo(previous));
            }
        }

        private static void Rewrite(string path, string address, string value)
        {
            using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
            var entry = zip.GetEntry("xl/worksheets/sheet1.xml");
            XDocument doc;
            using (var stream = entry.Open()) doc = XDocument.Load(stream);
            var cell = doc.Descendants(Ns + "c").SingleOrDefault(x => (string)x.Attribute("r") == address);
            if (cell == null)
            {
                string number = new string(address.Where(char.IsDigit).ToArray());
                var row = doc.Descendants(Ns + "row").Single(x => (string)x.Attribute("r") == number);
                cell = new XElement(Ns + "c", new XAttribute("r", address));
                row.Add(cell);
            }
            cell.SetAttributeValue("t", "inlineStr");
            cell.ReplaceNodes(new XElement(Ns + "is", new XElement(Ns + "t", value)));
            entry.Delete();
            using var output = zip.CreateEntry("xl/worksheets/sheet1.xml").Open();
            doc.Save(output);
        }
    }
}
