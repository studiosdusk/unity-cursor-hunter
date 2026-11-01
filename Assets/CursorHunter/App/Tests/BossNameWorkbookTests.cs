using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using CursorHunter.App.Editor;
using CursorHunter.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CursorHunter.App.Tests
{
    public sealed class BossNameWorkbookTests
    {
        private string _path;
        private BossDefinition[] _bosses;
        private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        [SetUp]
        public void Setup()
        {
            LocalizationCatalog.SetLanguage("ko");
            _path = Path.Combine(Path.GetTempPath(), "boss-names-" + Guid.NewGuid() + ".xlsx");
            _bosses = Resources.LoadAll<BossDefinition>("BossDefinitions").OrderBy(x => x.Stage).Select(Object.Instantiate).ToArray();
        }

        [TearDown]
        public void Cleanup()
        {
            LocalizationCatalog.SetLanguage("ko");
            if (File.Exists(_path)) File.Delete(_path);
            foreach (BossDefinition boss in _bosses) Object.DestroyImmediate(boss);
        }

        [Test]
        public void NewLocaleColumnsIncludingRegionalAndDoubleLetterColumnsImportWithoutCodeChanges()
        {
            File.Copy(BossNameWorkbook.AssetPath, _path);
            EditSheet(doc =>
            {
                SetCell(Row(doc, 4), "E4", "JA"); SetCell(Row(doc, 5), "E5", "森の守護者");
                SetCell(Row(doc, 4), "AA4", "zh-Hans"); SetCell(Row(doc, 5), "AA5", "森林守卫");
                SetCell(Row(doc, 4), "AB4", "zh"); SetCell(Row(doc, 5), "AB5", "森林守衛");
                SetCell(Row(doc, 5), "D5", "Forest Guardian");
            });
            Assert.That(BossNameWorkbook.TryApply(_path, _bosses, out int count, out string error), Is.True, error);
            Assert.That(count, Is.EqualTo(1));
            BossDefinition boss = _bosses[0];
            Assert.That(boss.GetDisplayName("en-US"), Is.EqualTo("Forest Guardian"));
            Assert.That(boss.GetDisplayName("ja_JP"), Is.EqualTo("森の守護者"));
            Assert.That(boss.GetDisplayName("ZH-hans-CN"), Is.EqualTo("森林守卫"));
            Assert.That(boss.GetDisplayName("zh-Hant-TW"), Is.EqualTo("森林守衛"));
            Assert.That(boss.GetDisplayName("fr"), Is.EqualTo(boss.DefaultDisplayName));
            Assert.That(_bosses[1].GetDisplayName("ja"), Is.EqualTo(_bosses[1].DefaultDisplayName));
            LocalizationCatalog.SetLanguage("ja");
            Assert.That(boss.DisplayName, Is.EqualTo("森の守護者"));
            Assert.That(BossNameWorkbook.TryApply(_path, _bosses, out count, out error), Is.True, error);
            Assert.That(count, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClearedOrRemovedTranslationFallsBackInsteadOfKeepingStaleText(bool removeColumn)
        {
            File.Copy(BossNameWorkbook.AssetPath, _path);
            EditSheet(doc => SetCell(Row(doc, 5), "D5", "Forest Guardian"));
            Assert.That(BossNameWorkbook.TryApply(_path, _bosses, out _, out string error), Is.True, error);
            EditSheet(doc =>
            {
                SetCell(Row(doc, 5), "D5", "");
                if (removeColumn) SetCell(Row(doc, 4), "D4", "");
            });
            Assert.That(BossNameWorkbook.TryApply(_path, _bosses, out int count, out error), Is.True, error);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(_bosses[0].GetDisplayName("en"), Is.EqualTo(_bosses[0].DefaultDisplayName));
        }

        [Test]
        public void LanguageColumnsCanBeReordered()
        {
            File.Copy(BossNameWorkbook.AssetPath, _path);
            EditSheet(doc =>
            {
                SetCell(Row(doc, 4), "C4", "en"); SetCell(Row(doc, 4), "D4", "ko");
                for (int i = 0; i < 20; i++)
                {
                    SetCell(Row(doc, i + 5), "C" + (i + 5), "");
                    SetCell(Row(doc, i + 5), "D" + (i + 5), _bosses[i].DefaultDisplayName);
                }
            });
            Assert.That(BossNameWorkbook.TryApply(_path, _bosses, out int count, out string error), Is.True, error);
            Assert.That(count, Is.Zero);
        }

        [TestCase("duplicate-locale")]
        [TestCase("invalid-locale")]
        [TestCase("missing-ko")]
        [TestCase("missing-locale")]
        [TestCase("translation-formula")]
        [TestCase("translation-too-long")]
        public void InvalidLocaleOrTranslationRejectsTheWholeWorkbook(string problem)
        {
            File.Copy(BossNameWorkbook.AssetPath, _path);
            EditSheet(doc =>
            {
                SetCell(Row(doc, 5), "C5", "적용되면 안 되는 이름");
                if (problem == "duplicate-locale") SetCell(Row(doc, 4), "E4", "EN");
                if (problem == "invalid-locale") SetCell(Row(doc, 4), "E4", "영어");
                if (problem == "missing-ko") SetCell(Row(doc, 4), "C4", "ja");
                if (problem == "missing-locale") SetCell(Row(doc, 24), "E24", "No header");
                if (problem == "translation-formula") SetCell(Row(doc, 24), "D24", "Formula").Add(new XElement(Ns + "f", "D5"));
                if (problem == "translation-too-long") SetCell(Row(doc, 24), "D24", new string('a', 81));
            });
            string[] before = _bosses.Select(JsonUtility.ToJson).ToArray();
            Assert.That(BossNameWorkbook.TryApply(_path, _bosses, out int count, out string error), Is.False);
            Assert.That(count, Is.Zero);
            Assert.That(error, Is.Not.Empty);
            Assert.That(_bosses.Select(JsonUtility.ToJson).ToArray(), Is.EqualTo(before));
        }

        private void EditSheet(Action<XDocument> change)
        {
            using var archive = ZipFile.Open(_path, ZipArchiveMode.Update);
            ZipArchiveEntry entry = archive.GetEntry("xl/worksheets/sheet1.xml");
            XDocument doc;
            using (var stream = entry.Open()) doc = XDocument.Load(stream);
            change(doc);
            entry.Delete();
            using (var stream = archive.CreateEntry("xl/worksheets/sheet1.xml").Open()) doc.Save(stream);
        }

        private static XElement Row(XDocument doc, int number) => doc.Descendants(Ns + "row")
            .Single(x => (string)x.Attribute("r") == number.ToString());

        [Test]
        public void ShippedWorkbookContainsAllTwentyCurrentNames()
        {
            var rows = BossNameWorkbook.ReadRows(BossNameWorkbook.AssetPath);
            Assert.That(rows.Length, Is.EqualTo(20));
            foreach (var row in rows)
                Assert.That(_bosses.Single(x => x.BossId == row.Id).DisplayName, Is.EqualTo(row.Name));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void KoreanInlineAndSharedNamesApplyByIdWithoutChangingStats(bool shared)
        {
            File.Copy(BossNameWorkbook.AssetPath, _path);
            Rewrite(_path, shared, null, reverse: true);
            string[] before = _bosses.Select(JsonUtility.ToJson).ToArray();
            Assert.That(BossNameWorkbook.TryApply(_path, _bosses, out int count, out string error), Is.True, error);
            Assert.That(count, Is.EqualTo(20));
            for (int i = 0; i < _bosses.Length; i++)
            {
                Assert.That(_bosses[i].DisplayName, Is.EqualTo("변경한 보스 " + _bosses[i].Stage));
                var original = ScriptableObject.CreateInstance<BossDefinition>();
                try
                {
                    JsonUtility.FromJsonOverwrite(before[i], original);
                    var serialized = new SerializedObject(original);
                    serialized.FindProperty("displayName").stringValue = _bosses[i].DisplayName;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    Assert.That(JsonUtility.ToJson(_bosses[i]), Is.EqualTo(JsonUtility.ToJson(original)));
                }
                finally { Object.DestroyImmediate(original); }
            }
            Assert.That(BossNameWorkbook.TryApply(_path, _bosses, out count, out error), Is.True, error);
            Assert.That(count, Is.Zero, "Import is idempotent.");
        }

        [TestCase("blank")]
        [TestCase("duplicate")]
        [TestCase("missing")]
        [TestCase("mismatch")]
        [TestCase("formula")]
        public void InvalidLastRowDoesNotPartiallyApplyEarlierRows(string problem)
        {
            File.Copy(BossNameWorkbook.AssetPath, _path);
            Rewrite(_path, false, problem);
            string[] before = _bosses.Select(JsonUtility.ToJson).ToArray();
            Assert.That(BossNameWorkbook.TryApply(_path, _bosses, out int count, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(count, Is.Zero);
            Assert.That(_bosses.Select(JsonUtility.ToJson).ToArray(), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator SavedWorkbookAutomaticallyUpdatesItsBossAsset()
        {
            byte[] original = File.ReadAllBytes(BossNameWorkbook.AssetPath);
            try
            {
                Rewrite(BossNameWorkbook.AssetPath, false, null);
                AssetDatabase.ImportAsset(BossNameWorkbook.AssetPath, ImportAssetOptions.ForceUpdate);
                BossDefinition boss = Resources.Load<BossDefinition>("BossDefinitions/Boss02");
                double deadline = EditorApplication.timeSinceStartup + 5d;
                while (boss.DisplayName != "변경한 보스 2" && EditorApplication.timeSinceStartup < deadline)
                    yield return null;
                Assert.That(boss.DisplayName, Is.EqualTo("변경한 보스 2"));
            }
            finally
            {
                File.WriteAllBytes(BossNameWorkbook.AssetPath, original);
                Assert.That(BossNameWorkbook.TryApply(BossNameWorkbook.AssetPath,
                    Resources.LoadAll<BossDefinition>("BossDefinitions"), out _, out string error), Is.True, error);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(BossNameWorkbook.AssetPath, ImportAssetOptions.ForceUpdate);
            }
        }

        private static void Rewrite(string path, bool shared, string problem, bool reverse = false)
        {
            using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
            ZipArchiveEntry entry = archive.GetEntry("xl/worksheets/sheet1.xml");
            XDocument doc;
            using (var stream = entry.Open()) doc = XDocument.Load(stream);
            XElement[] rows = doc.Descendants(Ns + "row").Where(row =>
                int.TryParse((string)row.Attribute("r"), out int number) && number >= 5 && number <= 24).ToArray();
            XElement strings;
            if (archive.GetEntry("xl/sharedStrings.xml") is ZipArchiveEntry sharedEntry)
            {
                using var source = sharedEntry.Open();
                strings = XDocument.Load(source).Root;
            }
            else strings = new XElement(Ns + "sst");
            // Convert the table to inline strings first so mixed original exports are supported.
            for (int i = 0; i < rows.Length; i++)
            {
                string rowNumber = (string)rows[i].Attribute("r");
                SetCell(rows[i], "B" + rowNumber, "boss." + (i + 1).ToString("00"));
                XElement cell = SetCell(rows[i], "C" + rowNumber, "변경한 보스 " + (i + 1));
                if (shared)
                {
                    int index = strings.Elements(Ns + "si").Count();
                    strings.Add(new XElement(Ns + "si", new XElement(Ns + "t", "변경한 보스 " + (i + 1))));
                    cell.SetAttributeValue("t", "s");
                    cell.ReplaceNodes(new XElement(Ns + "v", index));
                }
            }
            // Header must not depend on the previous shared string table.
            XElement header = doc.Descendants(Ns + "row").Single(x => (string)x.Attribute("r") == "4");
            SetCell(header, "A4", "단계"); SetCell(header, "B4", "보스 ID"); SetCell(header, "C4", "ko");
            if (problem == "blank") SetCell(rows[19], "C24", " ");
            if (problem == "duplicate") SetCell(rows[19], "B24", "boss.01");
            if (problem == "missing") rows[19].Remove();
            if (problem == "mismatch") SetCell(rows[19], "B24", "boss.unknown");
            if (problem == "formula") SetCell(rows[19], "C24", "수식").Add(new XElement(Ns + "f", "C5"));
            if (reverse)
            {
                XElement parent = rows[0].Parent;
                foreach (XElement row in rows) row.Remove();
                foreach (XElement row in rows.Reverse()) parent.Add(row);
            }
            entry.Delete();
            using (var stream = archive.CreateEntry("xl/worksheets/sheet1.xml").Open()) doc.Save(stream);
            if (shared)
            {
                archive.GetEntry("xl/sharedStrings.xml")?.Delete();
                using var stream = archive.CreateEntry("xl/sharedStrings.xml").Open();
                new XDocument(strings).Save(stream);
            }
        }

        private static XElement SetCell(XElement row, string address, string text)
        {
            XElement cell = row.Elements(Ns + "c").SingleOrDefault(x => (string)x.Attribute("r") == address);
            if (cell == null) { cell = new XElement(Ns + "c", new XAttribute("r", address)); row.Add(cell); }
            cell.SetAttributeValue("t", "inlineStr");
            cell.ReplaceNodes(new XElement(Ns + "is", new XElement(Ns + "t", text)));
            return cell;
        }
    }
}
