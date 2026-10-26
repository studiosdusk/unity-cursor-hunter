using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CursorHunter.Combat;
using CursorHunter.Data;

namespace CursorHunter.App.Editor
{
    /// <summary>Builds source edits in memory. The caller owns the atomic disk transaction.</summary>
    public static class CombatBalanceInformationStore
    {
        private const string InformationSuffix = "/Assets/CursorHunter/Data/Resources/GameData/game-data.en.json";
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static bool TryBuildFiles(string informationPath, CombatBalanceSettings settings,
            out Dictionary<string, string> originals, out Dictionary<string, string> updated,
            out string error)
        {
            originals = new Dictionary<string, string>(StringComparer.Ordinal);
            updated = new Dictionary<string, string>(StringComparer.Ordinal);
            error = string.Empty;
            try
            {
                if (settings == null || !settings.TryValidate(out error))
                    throw new InvalidDataException(string.IsNullOrEmpty(error) ? "저장할 설정이 없습니다." : error);
                if (string.IsNullOrWhiteSpace(informationPath))
                    throw new InvalidDataException("전투 정보 원본 경로가 없습니다.");
                string path = Path.GetFullPath(informationPath);
                string before = File.ReadAllText(path, Encoding.UTF8);
                ValidateInformation(before);
                JsonNode source = JsonParser.Parse(before);
                var edits = new List<Replacement>();
                JsonNode stats = Member(source, "stats");
                Set(stats, "attackPower", Integer(settings.AttackPower), edits);
                Set(stats, "attackRadiusWorldUnits", Number(settings.AttackRadiusWorldUnits), edits);
                Set(stats, "attackCooldownSeconds", Number(settings.AttackCooldownSeconds), edits);
                Set(stats, "criticalChancePercent", Number(settings.CriticalChancePercent), edits);
                Set(stats, "bossDamageMultiplier", Number(settings.BossDamageMultiplier), edits);
                Set(stats, "normalFieldDurationSeconds", Number(settings.DurationSeconds), edits);
                JsonNode rules = Member(source, "rules");
                Set(rules, "globalAliveLimit", Integer(settings.GlobalAliveLimit), edits);
                Set(rules, "perMonsterAliveLimit", Integer(settings.PerMonsterAliveLimit), edits);

                var monsters = new Dictionary<string, MonsterBalanceSettings>(StringComparer.Ordinal);
                foreach (MonsterBalanceSettings monster in settings.Monsters)
                {
                    if (monster == null || string.IsNullOrWhiteSpace(monster.MonsterId) ||
                        monsters.ContainsKey(monster.MonsterId))
                        throw new InvalidDataException("중복되거나 비어 있는 몬스터 설정입니다.");
                    monsters.Add(monster.MonsterId, monster);
                }
                JsonNode sourceMonsters = Member(source, "monsters");
                Require(sourceMonsters.Kind == JsonKind.Array, "monsters는 배열이어야 합니다.");
                foreach (JsonNode row in sourceMonsters.Items)
                {
                    JsonNode id = Member(row, "id");
                    Require(id.Kind == JsonKind.String, "몬스터 ID는 문자열이어야 합니다.");
                    if (!monsters.TryGetValue(id.Value, out MonsterBalanceSettings monster)) continue;
                    Set(row, "hitPoints", Integer(monster.MaxHealth), edits);
                    Set(row, "spawnIntervalSeconds", Number(monster.SpawnIntervalSeconds), edits);
                    Set(row, "garnetReward", Integer(monster.GarnetReward), edits);
                    if (monster.BonusDropAmount != 0L || !string.IsNullOrWhiteSpace(monster.BonusDropCurrencyId))
                        Set(row, "gemstoneId", Quote(monster.BonusDropCurrencyId), edits);
                    Set(row, "gemstoneAmount", Integer(monster.BonusDropAmount), edits);
                    Set(row, "gemstoneChancePercent", Number(monster.BonusDropChancePercent), edits);
                }
                string after = Apply(before, edits);
                ValidateInformation(after);
                JsonNode nextSource = JsonParser.Parse(after);
                var originalFiles = new Dictionary<string, string>(StringComparer.Ordinal) { { path, before } };
                var updatedFiles = new Dictionary<string, string>(StringComparer.Ordinal) { { path, after } };

                string normalized = path.Replace('\\', '/');
                if (normalized.EndsWith(InformationSuffix, StringComparison.Ordinal))
                {
                    string projectRoot = path.Substring(0, path.Length - InformationSuffix.Length);
                    string keyPath = Path.Combine(projectRoot, "docs/collaboration/game-data.ko.keys.json");
                    string referencePath = Path.Combine(projectRoot, "docs/collaboration/game-data.ko.reference.json");
                    string keysBefore = File.ReadAllText(keyPath, Encoding.UTF8);
                    string referenceBefore = File.ReadAllText(referencePath, Encoding.UTF8);
                    JsonNode keys = JsonParser.Parse(keysBefore);
                    JsonNode reference = JsonParser.Parse(referenceBefore);
                    var keyEdits = new List<Replacement>();
                    var referenceEdits = new List<Replacement>();
                    var translations = new Dictionary<string, string>(StringComparer.Ordinal);
                    CollectTranslations(reference, translations);
                    Synchronize(nextSource, keys, false, keyEdits, translations);
                    Synchronize(nextSource, Member(reference, "전체데이터해설"), true, referenceEdits, translations);
                    string hash = CanonicalHash(nextSource);
                    Set(Member(reference, "문서안내"), "원본SHA256", Quote(hash), referenceEdits);
                    string keysAfter = Apply(keysBefore, keyEdits);
                    string referenceAfter = Apply(referenceBefore, referenceEdits);
                    // Verify every copied source value after patching, while retaining descriptions and keys.
                    VerifyReference(nextSource, JsonParser.Parse(keysAfter), false, translations);
                    JsonNode verifiedReference = JsonParser.Parse(referenceAfter);
                    VerifyReference(nextSource, Member(verifiedReference, "전체데이터해설"), true, translations);
                    Require(Member(Member(verifiedReference, "문서안내"), "원본SHA256").Value == hash,
                        "한국어 해설의 원본 해시를 확인할 수 없습니다.");
                    originalFiles.Add(keyPath, keysBefore);
                    originalFiles.Add(referencePath, referenceBefore);
                    updatedFiles.Add(keyPath, keysAfter);
                    updatedFiles.Add(referencePath, referenceAfter);
                }

                originals = originalFiles;
                updated = updatedFiles;
                return true;
            }
            catch (Exception exception)
            {
                error = "전투 정보 저장 준비 실패: " + exception.Message;
                return false;
            }
        }

        private static void ValidateInformation(string json)
        {
            if (!GameInformationJson.TryDeserialize(json, out _, out string error))
                throw new InvalidDataException(error);
        }

        private static string Integer(long value) => value.ToString(Invariant);
        private static string Number(float value)
        {
            Require(!float.IsNaN(value) && !float.IsInfinity(value), "유한한 수치를 입력하세요.");
            return value.ToString("R", Invariant);
        }

        private static JsonNode Member(JsonNode node, string key)
        {
            Require(node.Kind == JsonKind.Object && node.Members.ContainsKey(key), "원본 JSON 필드가 없습니다: " + key);
            return node.Members[key];
        }

        private static void Set(JsonNode node, string key, string literal, List<Replacement> edits)
        {
            JsonNode existing = Member(node, key);
            JsonNode replacement = JsonParser.Parse(literal);
            Require(existing.Kind == replacement.Kind, "원본 JSON 필드 형식이 다릅니다: " + key);
            Replace(existing, replacement, edits);
        }

        private static void Replace(JsonNode existing, JsonNode replacement, List<Replacement> edits)
        {
            if (Canonical(existing) == Canonical(replacement)) return;
            edits.Add(new Replacement(existing.Start, existing.Length, replacement.Raw));
        }

        private static string Apply(string source, List<Replacement> edits)
        {
            edits.Sort((first, second) => second.Start.CompareTo(first.Start));
            var result = new StringBuilder(source);
            int boundary = source.Length;
            foreach (Replacement edit in edits)
            {
                Require(edit.Start + edit.Length <= boundary, "JSON 변경 범위가 겹칩니다.");
                result.Remove(edit.Start, edit.Length);
                result.Insert(edit.Start, edit.Value);
                boundary = edit.Start;
            }
            return result.ToString();
        }

        private static void Synchronize(JsonNode source, JsonNode target, bool detailed,
            List<Replacement> edits, Dictionary<string, string> translations)
        {
            if (source.Kind == JsonKind.Object)
            {
                Require(target.Kind == JsonKind.Object && target.Members.Count == source.Members.Count,
                    "한국어 참고 문서의 객체 구조가 원본과 다릅니다.");
                var decoded = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
                foreach (var pair in target.Members)
                {
                    int separator = pair.Key.IndexOf(detailed ? " (" : "(", StringComparison.Ordinal);
                    Require(separator > 0, "한국어 참고 문서에 번역 키가 없습니다: " + pair.Key);
                    string key = pair.Key.Substring(0, separator);
                    Require(!decoded.ContainsKey(key), "한국어 참고 문서에 중복 키가 있습니다: " + key);
                    decoded.Add(key, pair.Value);
                }
                foreach (var pair in source.Members)
                {
                    Require(decoded.ContainsKey(pair.Key), "한국어 참고 문서에 필드가 없습니다: " + pair.Key);
                    Synchronize(pair.Value, decoded[pair.Key], detailed, edits, translations);
                }
            }
            else if (source.Kind == JsonKind.Array)
            {
                Require(target.Kind == JsonKind.Array && source.Items.Count == target.Items.Count,
                    "한국어 참고 문서의 배열이 원본과 다릅니다.");
                for (int index = 0; index < source.Items.Count; index++)
                    Synchronize(source.Items[index], target.Items[index], detailed, edits, translations);
            }
            else
            {
                JsonNode value = detailed ? Member(target, "원본값") : target;
                Require(value.Kind != JsonKind.Object && value.Kind != JsonKind.Array,
                    "한국어 참고 문서의 원본값 형식이 올바르지 않습니다.");
                Replace(value, source, edits);
                if (detailed && source.Kind == JsonKind.String &&
                    target.Members.TryGetValue("한국어뜻", out JsonNode meaning) &&
                    translations.TryGetValue(source.Value, out string translated))
                    Replace(meaning, JsonParser.Parse(Quote(translated)), edits);
            }
        }

        private static void VerifyReference(JsonNode source, JsonNode reference, bool detailed,
            Dictionary<string, string> translations)
        {
            var remaining = new List<Replacement>();
            Synchronize(source, reference, detailed, remaining, translations);
            Require(remaining.Count == 0, "한국어 참고 문서의 원본값 검증에 실패했습니다.");
        }

        private static void CollectTranslations(JsonNode node, Dictionary<string, string> translations)
        {
            if (node.Kind == JsonKind.Object)
            {
                if (node.Members.TryGetValue("원본값", out JsonNode value) && value.Kind == JsonKind.String &&
                    node.Members.TryGetValue("한국어뜻", out JsonNode meaning) && meaning.Kind == JsonKind.String)
                    translations[value.Value] = meaning.Value;
                foreach (JsonNode child in node.Members.Values) CollectTranslations(child, translations);
            }
            else if (node.Kind == JsonKind.Array)
                foreach (JsonNode child in node.Items) CollectTranslations(child, translations);
        }

        private static string CanonicalHash(JsonNode source)
        {
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(Canonical(source));
            using (SHA256 algorithm = SHA256.Create())
            {
                var result = new StringBuilder(64);
                foreach (byte value in algorithm.ComputeHash(bytes)) result.Append(value.ToString("x2", Invariant));
                return result.ToString();
            }
        }

        // Mirrors tools/build_korean_game_data_reference.py for the game's Int64
        // values and authored/Unity float32 decimal tokens, including Python's exponent spelling.
        private static string Canonical(JsonNode node)
        {
            if (node.Kind == JsonKind.String) return Quote(node.Value);
            if (node.Kind == JsonKind.Number) return CanonicalNumber(node.Value);
            if (node.Kind == JsonKind.Boolean || node.Kind == JsonKind.Null) return node.Value;
            var result = new StringBuilder();
            if (node.Kind == JsonKind.Array)
            {
                result.Append('[');
                for (int index = 0; index < node.Items.Count; index++)
                {
                    if (index != 0) result.Append(',');
                    result.Append(Canonical(node.Items[index]));
                }
                return result.Append(']').ToString();
            }
            result.Append('{');
            var keys = new List<string>(node.Members.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int index = 0; index < keys.Count; index++)
            {
                if (index != 0) result.Append(',');
                result.Append(Quote(keys[index])).Append(':').Append(Canonical(node.Members[keys[index]]));
            }
            return result.Append('}').ToString();
        }

        private static string CanonicalNumber(string raw)
        {
            if (raw.IndexOfAny(new[] { '.', 'e', 'E' }) < 0) return raw == "-0" ? "0" : raw;
            double value = double.Parse(raw, NumberStyles.Float, Invariant);
            Require(!double.IsNaN(value) && !double.IsInfinity(value), "JSON 숫자가 유한 범위를 벗어납니다.");
            if (value == 0d) return raw[0] == '-' ? "-0.0" : "0.0";
            string shortest = value.ToString("G17", Invariant);
            for (int precision = 1; precision <= 17; precision++)
            {
                string candidate = value.ToString("G" + precision, Invariant);
                if (double.TryParse(candidate, NumberStyles.Float, Invariant, out double parsed) &&
                    BitConverter.DoubleToInt64Bits(parsed) == BitConverter.DoubleToInt64Bits(value))
                {
                    shortest = candidate;
                    break;
                }
            }
            bool negative = shortest[0] == '-';
            string unsigned = negative ? shortest.Substring(1) : shortest;
            int exponentIndex = unsigned.IndexOfAny(new[] { 'e', 'E' });
            int explicitExponent = exponentIndex < 0 ? 0 : int.Parse(unsigned.Substring(exponentIndex + 1), Invariant);
            string mantissa = exponentIndex < 0 ? unsigned : unsigned.Substring(0, exponentIndex);
            int point = mantissa.IndexOf('.');
            int decimalPosition = (point < 0 ? mantissa.Length : point) + explicitExponent;
            string digits = mantissa.Replace(".", string.Empty);
            while (digits.Length > 1 && digits[0] == '0') { digits = digits.Substring(1); decimalPosition--; }
            digits = digits.TrimEnd('0');
            int exponent = decimalPosition - 1;
            string sign = negative ? "-" : string.Empty;
            if (exponent < -4 || exponent >= 16)
                return sign + digits[0] + (digits.Length > 1 ? "." + digits.Substring(1) : string.Empty) +
                    "e" + (exponent < 0 ? "-" : "+") + Math.Abs(exponent).ToString("D2", Invariant);
            if (decimalPosition <= 0) return sign + "0." + new string('0', -decimalPosition) + digits;
            if (decimalPosition >= digits.Length)
                return sign + digits + new string('0', decimalPosition - digits.Length) + ".0";
            return sign + digits.Insert(decimalPosition, ".");
        }

        private static string Quote(string value)
        {
            Require(value != null, "JSON 문자열은 null일 수 없습니다.");
            var result = new StringBuilder().Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': result.Append("\\\""); break;
                    case '\\': result.Append("\\\\"); break;
                    case '\b': result.Append("\\b"); break;
                    case '\f': result.Append("\\f"); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': result.Append("\\r"); break;
                    case '\t': result.Append("\\t"); break;
                    default:
                        if (character < 0x20) result.Append("\\u").Append(((int)character).ToString("x4", Invariant));
                        else result.Append(character);
                        break;
                }
            }
            return result.Append('"').ToString();
        }

        private static void Require(bool condition, string error)
        {
            if (!condition) throw new InvalidDataException(error);
        }

        private readonly struct Replacement
        {
            public Replacement(int start, int length, string value) { Start = start; Length = length; Value = value; }
            public int Start { get; }
            public int Length { get; }
            public string Value { get; }
        }

        private enum JsonKind { Object, Array, String, Number, Boolean, Null }

        private sealed class JsonNode
        {
            public JsonKind Kind;
            public int Start;
            public int Length;
            public string Raw;
            public string Value;
            public Dictionary<string, JsonNode> Members;
            public List<JsonNode> Items;
        }

        /// <summary>Retains token locations so replacing a value never serializes unrelated fields.</summary>
        private sealed class JsonParser
        {
            private readonly string _text;
            private int _position;
            private JsonParser(string text) { _text = text; }

            public static JsonNode Parse(string text)
            {
                Require(!string.IsNullOrWhiteSpace(text), "JSON 내용이 없습니다.");
                var parser = new JsonParser(text);
                JsonNode result = parser.ReadValue(0);
                parser.SkipWhitespace();
                Require(parser._position == text.Length, "JSON 뒤에 추가 내용이 있습니다.");
                return result;
            }

            private JsonNode ReadValue(int depth)
            {
                Require(depth <= 128, "JSON 중첩 깊이가 너무 큽니다.");
                SkipWhitespace();
                Require(_position < _text.Length, "JSON 값이 없습니다.");
                var node = new JsonNode { Start = _position };
                char first = _text[_position];
                if (first == '{')
                {
                    node.Kind = JsonKind.Object;
                    node.Members = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
                    _position++;
                    SkipWhitespace();
                    if (!Take('}'))
                    {
                        do
                        {
                            SkipWhitespace();
                            Require(_position < _text.Length && _text[_position] == '"', "JSON 객체 키가 없습니다.");
                            string key = ReadString();
                            SkipWhitespace();
                            Require(Take(':'), "JSON 키 뒤에 콜론이 없습니다.");
                            Require(!node.Members.ContainsKey(key), "JSON 중복 키: " + key);
                            node.Members.Add(key, ReadValue(depth + 1));
                            SkipWhitespace();
                            if (Take('}')) break;
                            Require(Take(','), "JSON 객체 구분자가 없습니다.");
                        } while (true);
                    }
                }
                else if (first == '[')
                {
                    node.Kind = JsonKind.Array;
                    node.Items = new List<JsonNode>();
                    _position++;
                    SkipWhitespace();
                    if (!Take(']'))
                    {
                        do
                        {
                            node.Items.Add(ReadValue(depth + 1));
                            SkipWhitespace();
                            if (Take(']')) break;
                            Require(Take(','), "JSON 배열 구분자가 없습니다.");
                        } while (true);
                    }
                }
                else if (first == '"') { node.Kind = JsonKind.String; node.Value = ReadString(); }
                else if (first == 't' || first == 'f' || first == 'n')
                {
                    string literal = first == 't' ? "true" : first == 'f' ? "false" : "null";
                    Require(_position + literal.Length <= _text.Length &&
                        string.CompareOrdinal(_text, _position, literal, 0, literal.Length) == 0, "잘못된 JSON 리터럴입니다.");
                    _position += literal.Length;
                    node.Kind = first == 'n' ? JsonKind.Null : JsonKind.Boolean;
                    node.Value = literal;
                }
                else
                {
                    node.Kind = JsonKind.Number;
                    ReadNumber();
                }
                node.Length = _position - node.Start;
                node.Raw = _text.Substring(node.Start, node.Length);
                if (node.Kind == JsonKind.Number) node.Value = node.Raw;
                return node;
            }

            private void ReadNumber()
            {
                Take('-');
                Require(_position < _text.Length, "JSON 숫자가 없습니다.");
                if (!Take('0'))
                {
                    Require(_text[_position] >= '1' && _text[_position] <= '9', "잘못된 JSON 숫자입니다.");
                    while (Digit()) _position++;
                }
                if (Take('.'))
                {
                    Require(Digit(), "JSON 소수부가 없습니다.");
                    while (Digit()) _position++;
                }
                if (Take('e') || Take('E'))
                {
                    if (!Take('+')) Take('-');
                    Require(Digit(), "JSON 지수부가 없습니다.");
                    while (Digit()) _position++;
                }
            }

            private string ReadString()
            {
                Require(Take('"'), "JSON 문자열이 없습니다.");
                var result = new StringBuilder();
                while (_position < _text.Length)
                {
                    char character = _text[_position++];
                    if (character == '"') return result.ToString();
                    Require(character >= 0x20, "JSON 문자열에 제어 문자가 있습니다.");
                    if (character != '\\') { result.Append(character); continue; }
                    Require(_position < _text.Length, "JSON 이스케이프가 끝나지 않았습니다.");
                    switch (_text[_position++])
                    {
                        case '"': result.Append('"'); break;
                        case '\\': result.Append('\\'); break;
                        case '/': result.Append('/'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u':
                            Require(_position + 4 <= _text.Length, "JSON 유니코드 이스케이프가 잘못됐습니다.");
                            Require(ushort.TryParse(_text.Substring(_position, 4), NumberStyles.HexNumber,
                                Invariant, out ushort code), "JSON 유니코드 값이 잘못됐습니다.");
                            result.Append((char)code);
                            _position += 4;
                            break;
                        default: throw new InvalidDataException("지원하지 않는 JSON 이스케이프입니다.");
                    }
                }
                throw new InvalidDataException("JSON 문자열이 끝나지 않았습니다.");
            }

            private bool Digit() => _position < _text.Length && _text[_position] >= '0' && _text[_position] <= '9';
            private bool Take(char value)
            {
                if (_position >= _text.Length || _text[_position] != value) return false;
                _position++;
                return true;
            }
            private void SkipWhitespace()
            {
                while (_position < _text.Length && (_text[_position] == ' ' || _text[_position] == '\t' ||
                    _text[_position] == '\r' || _text[_position] == '\n')) _position++;
            }
        }
    }
}
