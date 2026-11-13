using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using CursorHunter.Progression.Editor;
using CursorHunter.Progression.ResearchPrototype;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.TestTools;
using UnityEngine;

namespace CursorHunter.App.Tests
{
    public class ResearchTextWorkbookTests
    {
        static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        [UnityTest]
        public IEnumerator WorkbookUpdatesTableAndRejectsDuplicateWithoutPartialChanges()
        {
            byte[] original = File.ReadAllBytes(ResearchTextWorkbook.Path);
            var table = LocalizationEditorSettings.GetStringTableCollection("Research").GetTable(new LocaleIdentifier("ko")) as StringTable;
            var english = LocalizationEditorSettings.GetStringTableCollection("Research").GetTable(new LocaleIdentifier("en")) as StringTable;
            const string key = "research.cursor.attack.title";
            string originalValue = table.GetEntry(key).Value;
            var window = ScriptableObject.CreateInstance<ResearchMapEditorWindow>();
            var ensure = typeof(ResearchMapEditorWindow).GetMethod("EnsureCache", BindingFlags.Instance | BindingFlags.NonPublic);
            var nameOf = typeof(ResearchMapEditorWindow).GetMethod("NameOf", BindingFlags.Instance | BindingFlags.NonPublic);
            var definition = AssetDatabase.LoadAssetAtPath<ResearchDefinition>("Assets/CursorHunter/Progression/ResearchPrototype/cursor.attack.asset");
            try
            {
                ensure.Invoke(window, null);
                Rewrite(ResearchTextWorkbook.Path, "B2", "엑셀 변경 테스트");
                Rewrite(ResearchTextWorkbook.Path, "C2", "Attack test");
                AssetDatabase.ImportAsset(ResearchTextWorkbook.Path, ImportAssetOptions.ForceUpdate);
                double deadline = EditorApplication.timeSinceStartup + 10;
                while (table.GetEntry(key).Value != "엑셀 변경 테스트" && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(table.GetEntry(key).Value, Is.EqualTo("엑셀 변경 테스트"));
                Assert.That(english.GetEntry(key).Value, Is.EqualTo("Attack test"));
                ensure.Invoke(window, null);
                Assert.That(nameOf.Invoke(window, new object[] { definition }), Is.EqualTo("엑셀 변경 테스트"));
                Rewrite(ResearchTextWorkbook.Path, "A3", key);
                Assert.Throws<InvalidDataException>(() => ResearchTextWorkbook.Apply());
                Assert.That(table.GetEntry(key).Value, Is.EqualTo("엑셀 변경 테스트"));
            }
            finally
            {
                File.WriteAllBytes(ResearchTextWorkbook.Path, original);
                ResearchTextWorkbook.Apply();
                AssetDatabase.ImportAsset(ResearchTextWorkbook.Path, ImportAssetOptions.ForceUpdate);
                UnityEngine.Object.DestroyImmediate(window);
                Assert.That(table.GetEntry(key).Value, Is.EqualTo(originalValue));
                Assert.That(english.GetEntry(key), Is.Null);
            }
        }
        static void Rewrite(string path, string address, string value)
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
