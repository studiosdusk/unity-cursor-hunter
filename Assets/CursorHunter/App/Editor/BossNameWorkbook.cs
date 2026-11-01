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
using UnityEngine;

namespace CursorHunter.App.Editor
{
    /// <summary>Imports the designer's Excel names into build-ready boss assets.</summary>
    [InitializeOnLoad]
    public static class BossNameWorkbook
    {
        public const string AssetPath = "Assets/CursorHunter/Data/Authoring/BossNames.xlsx";
        public const string SheetName = "보스 이름";
        private const string BossFolder = "Assets/CursorHunter/Data/Resources/BossDefinitions";
        private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static bool _pending;

        public readonly struct Row
        {
            public Row(int stage, string id, IReadOnlyDictionary<string, string> names)
            { Stage = stage; Id = id; Names = names; }
            public int Stage { get; }
            public string Id { get; }
            public IReadOnlyDictionary<string, string> Names { get; }
            public string Name => Names["ko"];
        }

        static BossNameWorkbook()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && _pending) QueueImport();
            };
            QueueImport();
        }

        internal static void QueueImport()
        {
            _pending = true;
            EditorApplication.delayCall -= ImportWhenReady;
            EditorApplication.delayCall += ImportWhenReady;
        }

        private static void ImportWhenReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueImport();
                return;
            }
            _pending = false;
            if (!File.Exists(AssetPath)) return;
            if (!TryApply(AssetPath, LoadDefinitions(), out int changed, out string error))
                Debug.LogError("보스 이름 엑셀 적용 실패: " + error);
            else if (changed > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"보스 이름 엑셀 적용 완료: {changed}개. 다음 보스전부터 표시됩니다.");
            }
        }

        [MenuItem("Tools/Cursor Hunter/Boss Names/Open Excel")]
        private static void OpenExcel() => EditorUtility.OpenWithDefaultApp(Path.GetFullPath(AssetPath));

        [MenuItem("Tools/Cursor Hunter/Boss Names/Apply Excel")]
        private static void ApplyExcel() => QueueImport();

        private static BossDefinition[] LoadDefinitions() => AssetDatabase.FindAssets("t:BossDefinition", new[] { BossFolder })
            .Select(guid => AssetDatabase.LoadAssetAtPath<BossDefinition>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();

        public static bool TryApply(string path, IReadOnlyList<BossDefinition> definitions,
            out int changed, out string error)
        {
            changed = 0;
            error = null;
            try
            {
                Row[] rows = ReadRows(path);
                if (definitions == null || definitions.Count != BossDefinition.StageCount)
                    throw new InvalidDataException("1~20단계 보스 SO가 모두 필요합니다.");
                var byId = new Dictionary<string, BossDefinition>(StringComparer.Ordinal);
                foreach (BossDefinition boss in definitions)
                {
                    if (boss == null || byId.ContainsKey(boss.BossId))
                        throw new InvalidDataException("보스 SO의 ID가 비어 있거나 중복됩니다.");
                    byId.Add(boss.BossId, boss);
                }
                // Validate every row before changing any asset. Row order is irrelevant.
                foreach (Row row in rows)
                    if (!byId.TryGetValue(row.Id, out BossDefinition boss) || boss.Stage != row.Stage)
                        throw new InvalidDataException($"{row.Stage}단계의 보스 ID '{row.Id}'가 SO와 일치하지 않습니다.");
                foreach (Row row in rows)
                {
                    BossDefinition boss = byId[row.Id];
                    var serialized = new SerializedObject(boss);
                    var translations = row.Names.Where(x => x.Key != "ko" && !string.IsNullOrEmpty(x.Value))
                        .OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
                    SerializedProperty names = serialized.FindProperty("localizedNames");
                    bool same = boss.DefaultDisplayName == row.Name && names.arraySize == translations.Length;
                    for (int i = 0; same && i < translations.Length; i++)
                    {
                        SerializedProperty name = names.GetArrayElementAtIndex(i);
                        same = name.FindPropertyRelative("locale").stringValue == translations[i].Key &&
                            name.FindPropertyRelative("text").stringValue == translations[i].Value;
                    }
                    if (same) continue;
                    serialized.FindProperty("displayName").stringValue = row.Name;
                    names.arraySize = translations.Length;
                    for (int i = 0; i < translations.Length; i++)
                    {
                        SerializedProperty name = names.GetArrayElementAtIndex(i);
                        name.FindPropertyRelative("locale").stringValue = translations[i].Key;
                        name.FindPropertyRelative("text").stringValue = translations[i].Value;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(boss);
                    changed++;
                }
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is XmlException ||
                exception is InvalidOperationException || exception is ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        public static Row[] ReadRows(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            XDocument workbook = ReadXml(zip, "xl/workbook.xml");
            XElement sheet = workbook.Descendants(Spreadsheet + "sheet")
                .SingleOrDefault(x => (string)x.Attribute("name") == SheetName);
            if (sheet == null) throw new InvalidDataException($"'{SheetName}' 시트가 없습니다.");
            XNamespace relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            string relationId = (string)sheet.Attribute(relationships + "id");
            XDocument rels = ReadXml(zip, "xl/_rels/workbook.xml.rels");
            XElement relation = rels.Root.Elements().SingleOrDefault(x => (string)x.Attribute("Id") == relationId);
            string target = (string)relation?.Attribute("Target");
            if (string.IsNullOrEmpty(target) || (string)relation.Attribute("TargetMode") == "External")
                throw new InvalidDataException("엑셀 시트 참조가 올바르지 않습니다.");
            string sheetPath = new Uri(new Uri("https://xlsx.local/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
            if (!sheetPath.StartsWith("xl/", StringComparison.Ordinal))
                throw new InvalidDataException("엑셀 시트 경로가 올바르지 않습니다.");
            string[] shared = zip.GetEntry("xl/sharedStrings.xml") == null ? Array.Empty<string>() :
                ReadXml(zip, "xl/sharedStrings.xml").Descendants(Spreadsheet + "si").Select(Text).ToArray();
            var result = new List<Row>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var stages = new HashSet<int>();
            Dictionary<string, string> localeColumns = null;
            foreach (XElement row in ReadXml(zip, sheetPath).Descendants(Spreadsheet + "row"))
            {
                var cells = new Dictionary<string, XElement>(StringComparer.Ordinal);
                foreach (XElement cell in row.Elements(Spreadsheet + "c"))
                {
                    string address = (string)cell.Attribute("r") ?? string.Empty;
                    string column = new string(address.TakeWhile(c => c >= 'A' && c <= 'Z').ToArray());
                    if (!string.IsNullOrEmpty(column)) cells.Add(column, cell);
                }
                string CellValue(string column) => cells.TryGetValue(column, out XElement cell) ? Value(cell, shared).Trim() : string.Empty;
                if (localeColumns == null)
                {
                    if (CellValue("A") != "단계" || CellValue("B") != "보스 ID") continue;
                    localeColumns = new Dictionary<string, string>(StringComparer.Ordinal);
                    var locales = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var cell in cells)
                    {
                        if (cell.Key == "A" || cell.Key == "B") continue;
                        string header = CellValue(cell.Key);
                        if (string.IsNullOrEmpty(header)) continue;
                        if (cell.Value.Element(Spreadsheet + "f") != null ||
                            !LocaleCode.TryNormalize(header, out string locale) || !locales.Add(locale))
                            throw new InvalidDataException($"언어 열 '{header}'가 잘못되었거나 중복됩니다. ko, en, ja, zh-Hans처럼 입력하세요.");
                        localeColumns.Add(cell.Key, locale);
                    }
                    if (!locales.Contains("ko")) throw new InvalidDataException("기본 한국어 이름을 위한 ko 열이 필요합니다.");
                    continue;
                }
                if (cells.Keys.All(column => string.IsNullOrEmpty(CellValue(column)))) continue;
                foreach (var cell in cells)
                {
                    if (cell.Value.Element(Spreadsheet + "f") != null || (string)cell.Value.Attribute("t") == "e")
                        throw new InvalidDataException("이름 표에는 수식·오류 값 대신 단계 숫자와 ID·이름 텍스트를 입력하세요.");
                    if (cell.Key != "A" && cell.Key != "B" && !localeColumns.ContainsKey(cell.Key) && !string.IsNullOrEmpty(CellValue(cell.Key)))
                        throw new InvalidDataException($"{cell.Key}열의 언어 코드가 비어 있습니다.");
                }
                if (!int.TryParse(CellValue("A"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int stage) ||
                    stage < 1 || stage > BossDefinition.StageCount || !stages.Add(stage))
                    throw new InvalidDataException($"단계 '{CellValue("A")}'가 범위를 벗어나거나 중복됩니다.");
                if (string.IsNullOrEmpty(CellValue("B")) || !ids.Add(CellValue("B")))
                    throw new InvalidDataException($"{stage}단계 보스 ID가 비어 있거나 중복됩니다.");
                var names = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var column in localeColumns)
                {
                    string name = CellValue(column.Key);
                    if ((column.Value == "ko" && string.IsNullOrWhiteSpace(name)) || name.Length > 80 || name.Any(char.IsControl))
                        throw new InvalidDataException($"{stage}단계 {column.Value} 이름은 줄바꿈 없는 1~80자입니다. ko 외의 번역은 비워둘 수 있습니다.");
                    names.Add(column.Value, name);
                }
                result.Add(new Row(stage, CellValue("B"), names));
            }
            if (localeColumns == null || result.Count != BossDefinition.StageCount)
                throw new InvalidDataException("단계 / 보스 ID / ko 열과 1~20단계 행을 유지하세요.");
            return result.ToArray();
        }

        private static string Value(XElement cell, string[] shared)
        {
            if (cell == null) return string.Empty;
            if ((string)cell.Attribute("t") == "inlineStr") return Text(cell);
            string value = (string)cell.Element(Spreadsheet + "v") ?? string.Empty;
            if ((string)cell.Attribute("t") != "s") return value;
            if (!int.TryParse(value, out int index) || index < 0 || index >= shared.Length)
                throw new InvalidDataException("엑셀 문자열 참조가 올바르지 않습니다.");
            return shared[index];
        }

        private static string Text(XElement element) => string.Concat(element.Descendants(Spreadsheet + "t").Select(x => x.Value));

        private static XDocument ReadXml(ZipArchive zip, string path)
        {
            ZipArchiveEntry entry = zip.GetEntry(path);
            if (entry == null || entry.Length > 4 * 1024 * 1024)
                throw new InvalidDataException($"엑셀 구성 파일을 읽을 수 없습니다: {path}");
            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024
            });
            return XDocument.Load(reader);
        }
    }

    internal sealed class BossNameWorkbookPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Contains(BossNameWorkbook.AssetPath) || moved.Contains(BossNameWorkbook.AssetPath))
                BossNameWorkbook.QueueImport();
        }
    }
}
