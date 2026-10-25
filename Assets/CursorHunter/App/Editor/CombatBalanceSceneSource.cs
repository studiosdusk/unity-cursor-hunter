using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CursorHunter.App.Editor
{
    /// <summary>Reads authored numbers without loading the source scene's objects.</summary>
    internal sealed class CombatBalanceSceneSource : IDisposable
    {
        internal sealed class Descriptor
        {
            internal MonoBehaviour Component;
            internal string ScriptGuid;
            internal string PrefabGuid;
            internal long SourceComponentId;
        }

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private readonly Dictionary<MonoBehaviour, Descriptor> _descriptors =
            new Dictionary<MonoBehaviour, Descriptor>();
        private readonly Dictionary<string, string> _prefabTexts = new Dictionary<string, string>();
        private readonly string[] _documents;
        internal readonly string Text;
        internal Scene ScratchScene { get; private set; }

        internal CombatBalanceSceneSource(string scenePath, IEnumerable<Type> componentTypes)
        {
            string full = ProjectFile(scenePath);
            Text = Encoding.UTF8.GetString(File.ReadAllBytes(full));
            if (!Text.TrimStart('\uFEFF').StartsWith("%YAML", StringComparison.Ordinal))
                throw new InvalidOperationException("텍스트로 직렬화된 Unity 씬이 필요합니다.");
            _documents = Regex.Split(Text, @"(?m)(?=^--- !u!)");
            // This scene is empty: it contains no cameras, renderers or Light2D.
            ScratchScene = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (Type type in componentTypes) ReadComponent(type);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal Descriptor Describe(MonoBehaviour component) => _descriptors[component];

        public void Dispose()
        {
            if (ScratchScene.IsValid()) EditorSceneManager.ClosePreviewScene(ScratchScene);
            ScratchScene = default;
            _descriptors.Clear();
        }

        private void ReadComponent(Type type)
        {
            string scriptGuid = FindScriptGuid(type);
            Descriptor selected = null;
            foreach (string document in _documents)
            {
                if (document.StartsWith("--- !u!114 &", StringComparison.Ordinal) &&
                    HasScript(document, scriptGuid))
                {
                    RejectDuplicate(selected, type);
                    MonoBehaviour component = CreateScratch(type);
                    ApplyPlainNumbers(component, document);
                    selected = new Descriptor { Component = component, ScriptGuid = scriptGuid };
                }
                if (!document.StartsWith("--- !u!1001 &", StringComparison.Ordinal)) continue;
                Match reference = Regex.Match(document,
                    @"(?m)^  m_SourcePrefab: \{fileID: \d+, guid: (?<guid>[0-9a-f]{32}), type: 3\}\r?$");
                if (!reference.Success) continue;
                string prefabGuid = reference.Groups["guid"].Value;
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab", StringComparison.Ordinal)) continue;
                if (!_prefabTexts.TryGetValue(prefabPath, out string prefabText))
                {
                    prefabText = Encoding.UTF8.GetString(File.ReadAllBytes(ProjectFile(prefabPath)));
                    _prefabTexts.Add(prefabPath, prefabText);
                }
                // Inspect text before loading an asset; unrelated scene prefabs
                // (including lighting prefabs) are never loaded for this reader.
                if (!HasScript(prefabText, scriptGuid)) continue;
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (asset == null) throw new InvalidOperationException("스킬 프리팹을 읽을 수 없습니다: " + prefabPath);
                Component[] sources = asset.GetComponentsInChildren(type, true);
                if (sources.Length != 1) throw new InvalidOperationException(type.Name + " 원본을 하나로 식별하지 못했습니다.");
                RejectDuplicate(selected, type);
                var source = (MonoBehaviour)sources[0];
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string sourceGuid, out long sourceId) ||
                    sourceGuid != prefabGuid)
                    throw new InvalidOperationException("직접 연결된 스킬 프리팹 원본이 필요합니다: " + prefabPath);
                MonoBehaviour scratch = CreateScratch(type);
                CopyNumericFields(source, scratch);
                ApplyOverrides(scratch, document, sourceGuid, sourceId);
                selected = new Descriptor
                {
                    Component = scratch, ScriptGuid = scriptGuid,
                    PrefabGuid = sourceGuid, SourceComponentId = sourceId
                };
            }
            if (selected == null) throw new InvalidOperationException(type.Name + "의 원본 컴포넌트가 없습니다.");
            _descriptors.Add(selected.Component, selected);
        }

        private MonoBehaviour CreateScratch(Type type)
        {
            GameObject root = EditorUtility.CreateGameObjectWithHideFlags(
                "Balance source " + type.Name, HideFlags.HideAndDontSave);
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, ScratchScene);
            var component = (MonoBehaviour)root.AddComponent(type);
            component.enabled = false;
            component.hideFlags = HideFlags.HideAndDontSave;
            return component;
        }

        private static void CopyNumericFields(MonoBehaviour source, MonoBehaviour destination)
        {
            var original = new SerializedObject(source);
            var target = new SerializedObject(destination);
            SerializedProperty iterator = original.GetIterator();
            while (iterator.Next(true))
            {
                if (!AllowedPath(iterator.propertyPath) || !Numeric(iterator.propertyType)) continue;
                SerializedProperty output = target.FindProperty(iterator.propertyPath);
                if (output == null || output.propertyType != iterator.propertyType)
                    throw new InvalidOperationException("프리팹의 숫자 필드 형식이 일치하지 않습니다.");
                switch (iterator.propertyType)
                {
                    case SerializedPropertyType.Boolean: output.boolValue = iterator.boolValue; break;
                    case SerializedPropertyType.Integer: output.longValue = iterator.longValue; break;
                    case SerializedPropertyType.Float: output.floatValue = iterator.floatValue; break;
                }
            }
            target.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyPlainNumbers(MonoBehaviour component, string document)
        {
            var serialized = new SerializedObject(component);
            var paths = new List<string>();
            SerializedProperty iterator = serialized.GetIterator();
            while (iterator.Next(true))
                if (AllowedPath(iterator.propertyPath) && Numeric(iterator.propertyType)) paths.Add(iterator.propertyPath);
            foreach (string path in paths)
            {
                string[] pieces = path.Split('.');
                string container = document;
                string indent = "  ";
                if (pieces.Length == 2)
                {
                    Match parent = Regex.Match(document, @"(?m)^  " + Regex.Escape(pieces[0]) +
                        @":\r?\n(?<body>(?:    [^\r\n]*\r?\n)*)");
                    if (!parent.Success) continue;
                    container = parent.Groups["body"].Value;
                    indent = "    ";
                }
                MatchCollection matches = Regex.Matches(container, @"(?m)^" + indent +
                    Regex.Escape(pieces[pieces.Length - 1]) + @": (?<value>[^\r\n]*)");
                if (matches.Count > 1) throw new InvalidOperationException("숫자 필드가 중복됩니다: " + path);
                if (matches.Count == 1) SetNumber(serialized.FindProperty(path), matches[0].Groups["value"].Value);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyOverrides(MonoBehaviour component, string document, string guid, long sourceId)
        {
            var serialized = new SerializedObject(component);
            string pattern = @"(?m)^    - target: \{fileID: " + sourceId.ToString(Invariant) +
                @", guid: " + guid + @", type: 3\}\r?\n      propertyPath: (?<path>[^\r\n]*)\r?\n      value: (?<value>[^\r\n]*)\r?\n      objectReference: (?<reference>[^\r\n]*)";
            var seen = new HashSet<string>();
            foreach (Match entry in Regex.Matches(document, pattern))
            {
                string path = entry.Groups["path"].Value;
                if (!AllowedPath(path)) continue;
                SerializedProperty property = serialized.FindProperty(path);
                if (property == null || !Numeric(property.propertyType)) continue;
                if (!seen.Add(path)) throw new InvalidOperationException("스킬 숫자 오버라이드가 중복됩니다: " + path);
                if (entry.Groups["reference"].Value.Trim() != "{fileID: 0}")
                    throw new InvalidOperationException("숫자 필드에 객체 참조가 있습니다: " + path);
                SetNumber(property, entry.Groups["value"].Value);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetNumber(SerializedProperty property, string text)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    if (text != "0" && text != "1") throw new InvalidOperationException("올바른 bool 값이 아닙니다: " + property.propertyPath);
                    property.boolValue = text == "1";
                    break;
                case SerializedPropertyType.Integer:
                    if (!long.TryParse(text, NumberStyles.Integer, Invariant, out long integer))
                        throw new InvalidOperationException("올바른 정수가 아닙니다: " + property.propertyPath);
                    if (property.numericType != SerializedPropertyNumericType.Int64 &&
                        (integer < int.MinValue || integer > int.MaxValue))
                        throw new InvalidOperationException("정수 범위를 벗어났습니다: " + property.propertyPath);
                    property.longValue = integer;
                    break;
                case SerializedPropertyType.Float:
                    if (!float.TryParse(text, NumberStyles.Float, Invariant, out float number) || float.IsNaN(number) || float.IsInfinity(number))
                        throw new InvalidOperationException("올바른 실수가 아닙니다: " + property.propertyPath);
                    property.floatValue = number;
                    break;
            }
        }

        private static bool Numeric(SerializedPropertyType type) => type == SerializedPropertyType.Boolean ||
            type == SerializedPropertyType.Integer || type == SerializedPropertyType.Float;

        private static bool AllowedPath(string path) => !path.StartsWith("m_", StringComparison.Ordinal) &&
            Regex.IsMatch(path, @"^[A-Za-z][A-Za-z0-9]*(?:\.[A-Za-z][A-Za-z0-9]*)?$");

        private static bool HasScript(string text, string guid) => Regex.IsMatch(text,
            @"(?m)^  m_Script: \{fileID: 11500000, guid: " + guid + @", type: 3\}\r?$");

        private static string FindScriptGuid(Type type)
        {
            string result = null;
            foreach (string guid in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script == null || script.GetClass() != type) continue;
                if (result != null) throw new InvalidOperationException(type.Name + " 스크립트가 중복됩니다.");
                result = guid;
            }
            if (result == null) throw new InvalidOperationException(type.Name + " 스크립트를 찾지 못했습니다.");
            return result;
        }

        private static void RejectDuplicate(Descriptor selected, Type type)
        {
            if (selected != null) throw new InvalidOperationException(type.Name + " 원본이 여러 개 있습니다.");
        }

        private static string ProjectFile(string path) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
    }
}
