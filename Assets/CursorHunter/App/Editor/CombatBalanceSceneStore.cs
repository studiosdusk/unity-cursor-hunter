using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CursorHunter.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CursorHunter.App.Editor
{
    /// <summary>Only authored numeric fields; never serializes a running scene.</summary>
    [InitializeOnLoad]
    public static class CombatBalanceSceneStore
    {
        private const string PendingKey = "CursorHunter.CombatBalanceSceneStore.SavedScenes";
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly Type[] SkillTypes =
        {
            typeof(TornadoSkillController), typeof(HeadshotSkillController),
            typeof(ThunderSkillController), typeof(FlameExhaustSkillController),
            typeof(MagicBallSkillController), typeof(LaserSkillController), typeof(PetSkillController)
        };

        [Serializable] private sealed class SavedScene
        {
            public string Path;
            public CombatBalanceSettings Settings;
        }
        [Serializable] private sealed class SavedScenes
        {
            public List<SavedScene> Items = new List<SavedScene>();
        }
        private sealed class ComponentEdit
        {
            internal MonoBehaviour Component;
            internal readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        }

        static CombatBalanceSceneStore()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += SynchronizeRememberedScenes;
            };
            EditorSceneManager.sceneOpened += (scene, mode) => SynchronizeRememberedScenes();
            EditorApplication.delayCall += SynchronizeRememberedScenes;
        }

        public static bool TryCapture(string scenePath, out CombatBalanceSettings settings, out string error)
        {
            settings = null;
            error = string.Empty;
            CombatBalanceSceneSource source = null;
            try
            {
                scenePath = ValidatePath(scenePath);
                source = new CombatBalanceSceneSource(scenePath, SourceTypes());
                PlayerCombatStatsDefaults defaults = FindOne<PlayerCombatStatsDefaults>(source.ScratchScene);
                HuntManager hunt = FindOne<HuntManager>(source.ScratchScene);
                var cursor = defaults.Snapshot;
                settings = new CombatBalanceSettings
                {
                    AttackPower = cursor.AttackPower,
                    AttackRadiusWorldUnits = cursor.AttackRadiusWorldUnits,
                    AttackCooldownSeconds = cursor.AttackCooldownSeconds,
                    AutoAttackEnabled = cursor.AutoAttackEnabled,
                    CriticalChancePercent = cursor.CriticalChancePercent,
                    CriticalDamageMultiplier = cursor.CriticalDamageMultiplier,
                    BossDamageMultiplier = cursor.BossDamageMultiplier,
                    DurationSeconds = new SerializedObject(hunt).FindProperty("durationSeconds").floatValue
                };
                foreach (MonoBehaviour controller in FindSkills(source.ScratchScene))
                {
                    SkillBalanceSettings skill = ((ISkillBalanceController)controller).CaptureBalanceSettings();
                    // Availability belongs to the temporary test, not the authored defaults.
                    skill.Enabled = false;
                    settings.Skills.Add(skill);
                }
                return true;
            }
            catch (Exception exception)
            {
                settings = null;
                error = "씬 기본값을 읽지 못했습니다: " + exception.Message;
                return false;
            }
            finally
            {
                source?.Dispose();
            }
        }

        public static bool TryBuildSceneText(string scenePath, CombatBalanceSettings settings,
            out string original, out string updated, out string error)
        {
            original = updated = null;
            error = string.Empty;
            CombatBalanceSceneSource source = null;
            try
            {
                scenePath = ValidatePath(scenePath);
                source = new CombatBalanceSceneSource(scenePath, SourceTypes());
                original = source.Text;
                List<ComponentEdit> edits = BuildEdits(source.ScratchScene, settings);
                string result = original;
                foreach (ComponentEdit edit in edits)
                {
                    CombatBalanceSceneSource.Descriptor descriptor = source.Describe(edit.Component);
                    if (!TryPatchNumericFields(result, descriptor.ScriptGuid,
                        descriptor.PrefabGuid, descriptor.SourceComponentId,
                        edit.Values, out result, out string patchError))
                        throw new InvalidOperationException(patchError);
                }
                updated = result;
                return true;
            }
            catch (Exception exception)
            {
                updated = null;
                error = "씬 기본값 저장안을 만들지 못했습니다: " + exception.Message;
                return false;
            }
            finally
            {
                source?.Dispose();
            }
        }

        /// <summary>Pure text patch, also usable by tests. No assets or scenes are written.</summary>
        public static bool TryPatchNumericFields(string original, string scriptGuid,
            string prefabGuid, long sourceComponentId, IReadOnlyDictionary<string, string> values,
            out string updated, out string error)
        {
            updated = null;
            error = string.Empty;
            try
            {
                if (string.IsNullOrEmpty(original) || values == null || values.Count == 0 ||
                    !Regex.IsMatch(scriptGuid ?? string.Empty, "^[0-9a-f]{32}$"))
                    throw new ArgumentException("씬 패치 대상이 유효하지 않습니다.");
                bool prefab = !string.IsNullOrEmpty(prefabGuid);
                if (prefab && (!Regex.IsMatch(prefabGuid, "^[0-9a-f]{32}$") || sourceComponentId <= 0))
                    throw new ArgumentException("프리팹 원본 ID가 유효하지 않습니다.");
                string[] documents = Regex.Split(original, @"(?m)(?=^--- !u!)");
                int selected = -1;
                for (int i = 0; i < documents.Length; i++)
                {
                    bool match = prefab
                        ? documents[i].StartsWith("--- !u!1001 &", StringComparison.Ordinal) &&
                          Regex.IsMatch(documents[i], @"(?m)^  m_SourcePrefab: \{fileID: \d+, guid: " + prefabGuid + @", type: 3\}\r?$")
                        : documents[i].StartsWith("--- !u!114 &", StringComparison.Ordinal) &&
                          Regex.IsMatch(documents[i], @"(?m)^  m_Script: \{fileID: 11500000, guid: " + scriptGuid + @", type: 3\}\r?$");
                    if (!match) continue;
                    if (selected >= 0) throw new InvalidOperationException("같은 원본의 컴포넌트가 여러 개여서 저장 대상을 정할 수 없습니다.");
                    selected = i;
                }
                if (selected < 0) throw new InvalidOperationException("원본 씬에서 저장할 컴포넌트를 찾지 못했습니다.");
                string document = documents[selected];
                foreach (var pair in values)
                {
                    if (!Regex.IsMatch(pair.Key, @"^[A-Za-z][A-Za-z0-9]*(?:\.[A-Za-z][A-Za-z0-9]*)?$") ||
                        !double.TryParse(pair.Value, NumberStyles.Float, Invariant, out double number) ||
                        double.IsNaN(number) || double.IsInfinity(number))
                        throw new InvalidOperationException("숫자 필드만 저장할 수 있습니다.");
                    document = prefab ? PatchOverride(document, prefabGuid, sourceComponentId, pair.Key, pair.Value)
                        : PatchScalar(document, pair.Key, pair.Value);
                }
                documents[selected] = document;
                updated = string.Concat(documents);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static void SynchronizeLoadedScene(string scenePath, CombatBalanceSettings settings)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying) return;
            bool playing = EditorApplication.isPlaying;
            scenePath = ValidatePath(scenePath);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) return;
            List<ComponentEdit> edits = BuildEdits(scene, settings);
            // BuildEdits verifies every component and property before any mutation.
            foreach (ComponentEdit edit in edits)
            {
                var serialized = new SerializedObject(edit.Component);
                foreach (var pair in edit.Values)
                {
                    SerializedProperty property = serialized.FindProperty(pair.Key);
                    switch (property.propertyType)
                    {
                        case SerializedPropertyType.Boolean: property.boolValue = pair.Value == "1"; break;
                        case SerializedPropertyType.Integer: property.longValue = long.Parse(pair.Value, Invariant); break;
                        case SerializedPropertyType.Float: property.floatValue = float.Parse(pair.Value, Invariant); break;
                    }
                }
                // In Play Mode these are only the authored fields for the next
                // run. Current skill runtime structs and overrides stay intact.
                if (serialized.ApplyModifiedPropertiesWithoutUndo() && !playing &&
                    PrefabUtility.IsPartOfPrefabInstance(edit.Component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(edit.Component);
            }
        }

        public static void RememberSavedScene(string scenePath, CombatBalanceSettings settings)
        {
            scenePath = ValidatePath(scenePath);
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            SavedScenes remembered = ReadRemembered();
            remembered.Items.RemoveAll(item => item.Path == scenePath);
            remembered.Items.Add(new SavedScene { Path = scenePath, Settings = settings.Clone() });
            SessionState.SetString(PendingKey, JsonUtility.ToJson(remembered));
            // Consume immediately in Edit Mode so a later reload cannot replay
            // an old save over newer unsaved Inspector changes.
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                SynchronizeRememberedScenes();
        }

        private static SavedScenes ReadRemembered()
        {
            string json = SessionState.GetString(PendingKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return new SavedScenes();
            try { return JsonUtility.FromJson<SavedScenes>(json) ?? new SavedScenes(); }
            catch (ArgumentException) { return new SavedScenes(); }
        }

        private static void SynchronizeRememberedScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            SavedScenes remembered = ReadRemembered();
            for (int i = remembered.Items.Count - 1; i >= 0; i--)
            {
                SavedScene saved = remembered.Items[i];
                Scene scene = SceneManager.GetSceneByPath(saved.Path);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                try
                {
                    SynchronizeLoadedScene(saved.Path, saved.Settings);
                    remembered.Items.RemoveAt(i);
                }
                catch (Exception exception)
                {
                    Debug.LogError("저장한 전투 기본값의 편집 씬 동기화 실패: " + exception.Message);
                }
            }
            SessionState.SetString(PendingKey, JsonUtility.ToJson(remembered));
        }

        private static string ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("저장된 씬 경로가 필요합니다.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(project, path));
            string assets = Path.Combine(project, "Assets") + Path.DirectorySeparatorChar;
            if (!full.StartsWith(assets, StringComparison.Ordinal) ||
                !full.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                throw new ArgumentException("프로젝트 Assets 안에 저장된 .unity 씬만 사용할 수 있습니다.");
            return full.Substring(project.Length + 1).Replace('\\', '/');
        }

        private static T FindOne<T>(Scene scene) where T : MonoBehaviour
        {
            T result = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (T component in root.GetComponentsInChildren<T>(true))
                {
                    if (result != null) throw new InvalidOperationException(typeof(T).Name + "가 여러 개 있습니다.");
                    result = component;
                }
            if (result == null) throw new InvalidOperationException(typeof(T).Name + "가 없습니다.");
            return result;
        }

        private static List<MonoBehaviour> FindSkills(Scene scene)
        {
            var result = new List<MonoBehaviour>();
            foreach (Type type in SkillTypes)
            {
                MonoBehaviour selected = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Component component in root.GetComponentsInChildren(type, true))
                    {
                        if (selected != null) throw new InvalidOperationException(type.Name + "가 여러 개 있습니다.");
                        selected = (MonoBehaviour)component;
                    }
                if (selected == null) throw new InvalidOperationException(type.Name + "가 없습니다.");
                result.Add(selected);
            }
            return result;
        }

        private static List<ComponentEdit> BuildEdits(Scene scene, CombatBalanceSettings settings)
        {
            string error = string.Empty;
            if (settings == null || !settings.TryValidate(out error))
                throw new ArgumentException(settings == null ? "저장할 설정이 없습니다." : error);
            var result = new List<ComponentEdit>();
            var cursor = new ComponentEdit { Component = FindOne<PlayerCombatStatsDefaults>(scene) };
            cursor.Values.Add("attackPower", settings.AttackPower.ToString(Invariant));
            cursor.Values.Add("attackRadiusWorldUnits", Number(settings.AttackRadiusWorldUnits));
            cursor.Values.Add("attackCooldownSeconds", Number(settings.AttackCooldownSeconds));
            cursor.Values.Add("autoAttackEnabled", settings.AutoAttackEnabled ? "1" : "0");
            cursor.Values.Add("criticalChancePercent", Number(settings.CriticalChancePercent));
            cursor.Values.Add("criticalDamageMultiplier", Number(settings.CriticalDamageMultiplier));
            cursor.Values.Add("bossDamageMultiplier", Number(settings.BossDamageMultiplier));
            result.Add(cursor);
            var hunt = new ComponentEdit { Component = FindOne<HuntManager>(scene) };
            hunt.Values.Add("durationSeconds", Number(settings.DurationSeconds));
            result.Add(hunt);
            List<MonoBehaviour> controllers = FindSkills(scene);
            if (settings.Skills == null || settings.Skills.Count != controllers.Count)
                throw new ArgumentException("7종 스킬의 전체 기본값이 필요합니다.");
            var seen = new HashSet<string>();
            foreach (MonoBehaviour component in controllers)
            {
                var controller = (ISkillBalanceController)component;
                SkillBalanceSettings schema = controller.CaptureBalanceSettings();
                SkillBalanceSettings input = settings.Skills.Find(value => value != null && value.SkillId == schema.SkillId);
                if (input == null || !seen.Add(input.SkillId)) throw new ArgumentException("스킬 기본값이 중복되거나 없습니다.");
                input = input.Clone();
                input.Enabled = false;
                if (!controller.ValidateBalanceSettings(input, out error)) throw new ArgumentException(error);
                var edit = new ComponentEdit { Component = component };
                foreach (SkillBalanceValue value in input.Values)
                {
                    string path = "baseStat." + char.ToUpperInvariant(value.Id[0]) + value.Id.Substring(1);
                    edit.Values.Add(path, value.WholeNumber ? ((int)value.Value).ToString(Invariant) : Number(value.Value));
                }
                result.Add(edit);
            }
            foreach (ComponentEdit edit in result)
            {
                var serialized = new SerializedObject(edit.Component);
                foreach (var pair in edit.Values)
                {
                    SerializedProperty property = serialized.FindProperty(pair.Key);
                    if (property == null || property.propertyType != SerializedPropertyType.Integer &&
                        property.propertyType != SerializedPropertyType.Float && property.propertyType != SerializedPropertyType.Boolean)
                        throw new InvalidOperationException("기본값의 직렬화 필드가 일치하지 않습니다: " + pair.Key);
                }
            }
            return result;
        }

        private static string Number(float value) => value.ToString("R", Invariant);

        private static IEnumerable<Type> SourceTypes()
        {
            yield return typeof(PlayerCombatStatsDefaults);
            yield return typeof(HuntManager);
            foreach (Type type in SkillTypes) yield return type;
        }

        private static string PatchScalar(string document, string path, string value)
        {
            string[] parts = path.Split('.');
            if (parts.Length == 1) return ReplaceSingleValue(document, @"(?m)^  " + Regex.Escape(path) + @": (?<value>[^\r\n]*)", value);
            Match parent = Regex.Match(document, @"(?m)^  " + Regex.Escape(parts[0]) + @":\r?\n(?<body>(?:    [^\r\n]*\r?\n)*)");
            if (!parent.Success) throw new InvalidOperationException("기본값 묶음이 없습니다: " + parts[0]);
            Group body = parent.Groups["body"];
            string next = ReplaceSingleValue(body.Value, @"(?m)^    " + Regex.Escape(parts[1]) + @": (?<value>[^\r\n]*)", value);
            return document.Substring(0, body.Index) + next + document.Substring(body.Index + body.Length);
        }

        private static string PatchOverride(string document, string guid, long componentId, string path, string value)
        {
            string pattern = @"(?m)^    - target: \{fileID: " + componentId.ToString(Invariant) + @", guid: " + guid + @", type: 3\}\r?\n      propertyPath: " + Regex.Escape(path) + @"\r?\n      value: (?<value>[^\r\n]*)\r?\n      objectReference: (?<reference>[^\r\n]*)";
            MatchCollection existing = Regex.Matches(document, pattern);
            if (existing.Count > 1) throw new InvalidOperationException("프리팹 기본값 오버라이드가 중복됩니다: " + path);
            if (existing.Count == 1)
            {
                if (existing[0].Groups["reference"].Value.Trim() != "{fileID: 0}")
                    throw new InvalidOperationException("숫자 기본값에 객체 참조가 들어 있습니다: " + path);
                return ReplaceSingleValue(document, pattern, value);
            }
            string newline = document.Contains("\r\n") ? "\r\n" : "\n";
            document = document.Replace("    m_Modifications: []" + newline, "    m_Modifications:" + newline);
            Match marker = Regex.Match(document, @"(?m)^    m_RemovedComponents:");
            if (!marker.Success || !document.Contains("    m_Modifications:" + newline))
                throw new InvalidOperationException("지원하지 않는 프리팹 오버라이드 형식입니다.");
            string entry = "    - target: {fileID: " + componentId.ToString(Invariant) + ", guid: " + guid + ", type: 3}" + newline +
                "      propertyPath: " + path + newline + "      value: " + value + newline + "      objectReference: {fileID: 0}" + newline;
            return document.Insert(marker.Index, entry);
        }

        private static string ReplaceSingleValue(string text, string pattern, string value)
        {
            MatchCollection matches = Regex.Matches(text, pattern);
            if (matches.Count != 1) throw new InvalidOperationException("저장할 숫자 필드를 하나로 식별하지 못했습니다.");
            Group field = matches[0].Groups["value"];
            return text.Substring(0, field.Index) + value + text.Substring(field.Index + field.Length);
        }
    }
}
