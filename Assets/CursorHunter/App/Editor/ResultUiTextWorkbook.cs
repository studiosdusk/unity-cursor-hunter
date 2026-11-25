using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using CursorHunter.Data;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace CursorHunter.App.Editor
{
    // ResultUIText.xlsx is authoritative; the table assets are generated build inputs.
    [InitializeOnLoad]
    public static class ResultUiTextWorkbook
    {
        public const string Path = "Assets/CursorHunter/Data/Authoring/ResultUIText.xlsx";
        private const string Folder = "Assets/CursorHunter/App/Localization";
        private const string TableName = "ResultUI";
        private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly string[] RequiredKeys =
        {
            "ui.result.retired", "ui.result.boss.clear", "ui.result.boss.failed", "ui.result.timeout", "ui.result.hunt.over",
            "ui.result.boss.retired.title", "ui.result.boss.clear.title", "ui.result.boss.failed.title", "ui.result.retired.title", "ui.result.title",
            "ui.result.boss.retired.subtitle", "ui.result.boss.failed.subtitle", "ui.result.test.no.reward",
            "ui.result.action.upgrade", "ui.result.action.lobby", "ui.result.action.boss.next", "ui.result.action.boss.field", "ui.result.action.boss.retry", "ui.result.action.retry",
            "ui.result.rewards.heading", "ui.result.experience",
            "ui.result.gem.garnet", "ui.result.gem.topaz", "ui.result.gem.amethyst", "ui.result.gem.sapphire", "ui.result.gem.diamond", "ui.result.gem.dragon",
            "ui.result.hint", "ui.result.empty.gems", "ui.result.confirm", "ui.result.action.retry.failed",
            "ui.result.relic.discovery"
        };
        private static readonly HashSet<string> FormattedKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "ui.result.boss.retired.title", "ui.result.boss.clear.title", "ui.result.boss.failed.title"
        };
        private static bool pending;
        public sealed class Row
        {
            public string Key;
            public Dictionary<string, string> Values;
        }
        static ResultUiTextWorkbook()
        {
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode && pending) Queue(); };
            Queue();
        }
        internal static void Queue()
        {
            pending = true;
            EditorApplication.delayCall -= ImportWhenReady;
            EditorApplication.delayCall += ImportWhenReady;
        }
        private static void ImportWhenReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Queue(); return; }
            pending = false;
            try { Apply(); }
            catch (Exception e) { Debug.LogError("결과창 텍스트 엑셀 적용 실패: " + e.Message); }
        }
        [MenuItem("Tools/Cursor Hunter/Result UI Text/Open Excel")]
        private static void Open() => EditorUtility.OpenWithDefaultApp(System.IO.Path.GetFullPath(Path));
        [MenuItem("Tools/Cursor Hunter/Result UI Text/Apply Excel")]
        public static void Apply()
        {
            var rows = ReadRows(Path);
            var keys = new HashSet<string>(rows.Select(x => x.Key), StringComparer.Ordinal);
            var missing = RequiredKeys.Where(key => !keys.Contains(key)).ToArray();
            if (missing.Length != 0)
                throw new InvalidDataException("필수 결과창 키가 없습니다: " + string.Join(", ", missing));
            foreach (var row in rows)
            {
                if (!FormattedKeys.Contains(row.Key)) continue;
                foreach (var translation in row.Values.Where(x => !string.IsNullOrEmpty(x.Value)))
                {
                    try
                    {
                        string rendered = string.Format(CultureInfo.InvariantCulture, translation.Value, "RESULT_STAGE");
                        if (!rendered.Contains("RESULT_STAGE")) throw new FormatException();
                    }
                    catch (FormatException)
                    {
                        throw new InvalidDataException($"{row.Key}의 {translation.Key} 문구에는 보스 단계 {{0}}이 필요합니다.");
                    }
                }
            }
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            if (LocalizationEditorSettings.ActiveLocalizationSettings == null)
            {
                var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, Folder + "/LocalizationSettings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
                LocalizationSettings.StartupLocaleSelectors.Clear();
                LocalizationSettings.StartupLocaleSelectors.Add(new SpecificLocaleSelector { LocaleId = new LocaleIdentifier("ko") });
                EditorUtility.SetDirty(settings);
            }
            var codes = rows.SelectMany(x => x.Values.Keys).Distinct().OrderBy(x => x == "ko" ? "" : x).ToArray();
            Locale ko = EnsureLocale("ko", null);
            if (LocalizationSettings.ProjectLocale == null) LocalizationSettings.ProjectLocale = ko;
            var locales = new List<Locale> { ko };
            foreach (string code in codes.Where(x => x != "ko"))
                locales.Add(EnsureLocale(code, ko));
            var collection = LocalizationEditorSettings.GetStringTableCollection(TableName) ??
                LocalizationEditorSettings.CreateStringTableCollection(TableName, Folder, locales);
            foreach (Locale locale in locales)
            {
                var table = collection.GetTable(locale.Identifier) as StringTable ??
                    (StringTable)collection.AddNewTable(locale.Identifier);
                foreach (var row in rows)
                {
                    string value = row.Values[locale.Identifier.Code.ToLowerInvariant()];
                    var entry = table.GetEntry(row.Key);
                    if (string.IsNullOrEmpty(value))
                    {
                        if (entry != null) table.RemoveEntry(entry.KeyId);
                    }
                    else if (entry == null) table.AddEntry(row.Key, value);
                    else if (entry.Value != value) entry.Value = value;
                }
                EditorUtility.SetDirty(table);
                LocalizationEditorSettings.SetPreloadTableFlag(table, true);
            }
            // A removed language column must not leave old translations in generated tables.
            var importedLocales = new HashSet<string>(locales.Select(x => x.Identifier.Code), StringComparer.OrdinalIgnoreCase);
            foreach (var table in collection.StringTables)
            {
                if (table == null || importedLocales.Contains(table.LocaleIdentifier.Code)) continue;
                foreach (var entry in collection.SharedData.Entries.ToArray()) table.RemoveEntry(entry.Id);
                EditorUtility.SetDirty(table);
            }
            foreach (var old in collection.SharedData.Entries.ToArray())
                if (!keys.Contains(old.Key)) collection.RemoveEntry(old.Key);
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();
        }
        private static Locale EnsureLocale(string code, Locale fallback)
        {
            var locale = LocalizationEditorSettings.GetLocale(new LocaleIdentifier(code));
            if (locale != null) return locale;
            locale = Locale.CreateLocale(code);
            AssetDatabase.CreateAsset(locale, Folder + "/Locale-" + code + ".asset");
            if (fallback != null) locale.Metadata.AddMetadata(new FallbackLocale(fallback));
            EditorUtility.SetDirty(locale);
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }
        public static Row[] ReadRows(string path)
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var zip = new ZipArchive(file, ZipArchiveMode.Read);
            var book = Xml(zip, "xl/workbook.xml");
            var sheet = book.Descendants(Ns + "sheet").SingleOrDefault(x => (string)x.Attribute("name") == "결과창 표시 정보");
            if (sheet == null) throw new InvalidDataException("'결과창 표시 정보' 시트가 없습니다.");
            XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            string id = (string)sheet.Attribute(rel + "id");
            var link = Xml(zip, "xl/_rels/workbook.xml.rels").Root.Elements().SingleOrDefault(x => (string)x.Attribute("Id") == id);
            string target = (string)link?.Attribute("Target");
            if (string.IsNullOrEmpty(target) || (string)link.Attribute("TargetMode") == "External") throw new InvalidDataException("시트 경로가 올바르지 않습니다.");
            string sheetPath = new Uri(new Uri("https://xlsx.local/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
            if (!sheetPath.StartsWith("xl/", StringComparison.Ordinal)) throw new InvalidDataException("시트 경로가 올바르지 않습니다.");
            string[] shared = zip.GetEntry("xl/sharedStrings.xml") == null ? Array.Empty<string>() :
                Xml(zip, "xl/sharedStrings.xml").Descendants(Ns + "si").Select(Text).ToArray();
            var rows = new List<Row>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, string> columns = null;
            foreach (var row in Xml(zip, sheetPath).Descendants(Ns + "row"))
            {
                var cells = row.Elements(Ns + "c").ToDictionary(x => new string(((string)x.Attribute("r") ?? "").TakeWhile(char.IsLetter).ToArray()));
                string Value(string column)
                {
                    if (!cells.TryGetValue(column, out var cell)) return "";
                    if (cell.Element(Ns + "f") != null || (string)cell.Attribute("t") == "e") throw new InvalidDataException("수식이나 오류 대신 텍스트를 입력하세요.");
                    string value = (string)cell.Attribute("t") == "inlineStr" ? Text(cell) : (string)cell.Element(Ns + "v") ?? "";
                    if ((string)cell.Attribute("t") == "s")
                    {
                        if (!int.TryParse(value, out int i) || i < 0 || i >= shared.Length) throw new InvalidDataException("엑셀 문자열 참조가 잘못되었습니다.");
                        value = shared[i];
                    }
                    return value.Trim();
                }
                if (columns == null)
                {
                    if (Value("A") != "키") continue;
                    columns = new Dictionary<string, string>();
                    var codes = new HashSet<string>(StringComparer.Ordinal);
                    foreach (string col in cells.Keys.Where(x => x != "A"))
                    {
                        string code = Value(col);
                        if (code == "") continue;
                        if (!LocaleCode.TryNormalize(code, out code) || !codes.Add(code))
                            throw new InvalidDataException("언어 코드를 확인하세요. 예: ko, en, ja");
                        columns.Add(col, code);
                    }
                    if (!codes.Contains("ko") || !codes.Contains("en")) throw new InvalidDataException("ko와 en 열이 필요합니다.");
                    continue;
                }
                if (cells.Keys.All(x => Value(x) == "")) continue;
                string key = Value("A");
                if (string.IsNullOrWhiteSpace(key) || !keys.Add(key)) throw new InvalidDataException("키가 비어 있거나 중복됩니다: " + key);
                foreach (string col in cells.Keys)
                    if (col != "A" && !columns.ContainsKey(col) && Value(col) != "")
                        throw new InvalidDataException(col + "열에 언어 코드가 없습니다.");
                var values = columns.ToDictionary(x => x.Value, x => Value(x.Key), StringComparer.Ordinal);
                if (string.IsNullOrWhiteSpace(values["ko"]) || string.IsNullOrWhiteSpace(values["en"])) throw new InvalidDataException(key + "의 한국어 또는 영어 문구가 비어 있습니다.");
                rows.Add(new Row { Key = key, Values = values });
            }
            if (columns == null || rows.Count == 0)
                throw new InvalidDataException("키 / ko / en 열과 결과창 문구 행이 필요합니다.");
            return rows.ToArray();
        }
        private static string Text(XElement x) => string.Concat(x.Descendants(Ns + "t").Select(t => t.Value));
        private static XDocument Xml(ZipArchive zip, string path)
        {
            var entry = zip.GetEntry(path);
            if (entry == null || entry.Length > 4 * 1024 * 1024) throw new InvalidDataException("엑셀 구성 파일을 읽을 수 없습니다: " + path);
            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 });
            return XDocument.Load(reader);
        }
    }
    internal sealed class ResultUiTextPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Contains(ResultUiTextWorkbook.Path) || moved.Contains(ResultUiTextWorkbook.Path) ||
                deleted.Contains(ResultUiTextWorkbook.Path) || movedFrom.Contains(ResultUiTextWorkbook.Path)) ResultUiTextWorkbook.Queue();
        }
    }
    internal sealed class ResultUiTextBuildCheck : BuildPlayerProcessor
    {
        public override int callbackOrder => -1000;
        public override void PrepareForBuild(BuildPlayerContext context)
        {
            try { ResultUiTextWorkbook.Apply(); }
            catch (Exception e) { throw new BuildFailedException("결과창 텍스트 엑셀을 적용할 수 없습니다: " + e.Message); }
        }
    }
}
