using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CursorHunter.App.Editor;
using CursorHunter.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace CursorHunter.App.Tests
{
    public sealed class CombatBalanceSceneStoreTests
    {
        private const string ScriptGuid = "11111111111111111111111111111111";
        private const string PrefabGuid = "22222222222222222222222222222222";
        private const string MainPath = "Assets/CursorHunter/App/Scenes/Main.unity";
        private string _testFolder;
        private Scene _loadedCopy;
        private GameObject _guardLight;

        [TearDown]
        public void CleanUp()
        {
            if (_guardLight != null) UnityEngine.Object.DestroyImmediate(_guardLight);
            if (_loadedCopy.IsValid() && _loadedCopy.isLoaded)
                EditorSceneManager.CloseScene(_loadedCopy, true);
            if (!string.IsNullOrEmpty(_testFolder)) AssetDatabase.DeleteAsset(_testFolder);
        }

        [TestCase("Default")]
        [TestCase("Skill")]
        public void ReadingAndPreparingSaveWithAnActiveGlobalLightPreservesLighting(string layerName)
        {
            string copy = CreateMainCopy();
            Assert.That(SortingLayer.layers.Any(layer => layer.name == layerName), Is.True);
            int layerId = SortingLayer.NameToID(layerName);
            bool alreadyLit = SceneLights().Any(light => light.isActiveAndEnabled &&
                light.lightType == Light2D.LightType.Global && light.blendStyleIndex == 0 &&
                (light.targetSortingLayers?.Contains(layerId) ?? false));
            if (!alreadyLit)
            {
                _guardLight = new GameObject("Existing scene global light");
                _guardLight.hideFlags = HideFlags.HideAndDontSave;
                _guardLight.SetActive(false);
                Light2D light = _guardLight.AddComponent<Light2D>();
                light.targetSortingLayers = new[] { layerId };
                light.blendStyleIndex = 0;
                light.lightType = Light2D.LightType.Global;
                _guardLight.SetActive(true);
            }

            Light2D[] before = SceneLights();
            string[] lightState = before.Select(LightState).ToArray();
            int sceneCount = SceneManager.sceneCount;
            int previewCount = EditorSceneManager.previewSceneCount;
            Scene active = SceneManager.GetActiveScene();
            bool wasDirty = active.isDirty;
            byte[] source = File.ReadAllBytes(copy);
            Assert.That(CombatBalanceSceneStore.TryCapture(copy, out var settings,
                out string error), Is.True, error);
            settings.AttackPower += 1L;
            Assert.That(CombatBalanceSceneStore.TryBuildSceneText(copy, settings,
                out _, out _, out error), Is.True, error);
            settings.Skills.Find(skill => skill.SkillId == TornadoSkillController.SkillId)
                .Values.Find(value => value.Id == "cooldownSeconds").Value = 0f;
            Assert.That(CombatBalanceSceneStore.TryBuildSceneText(copy, settings,
                out _, out _, out _), Is.False);

            Assert.That(SceneLights().Select(LightState).ToArray(), Is.EqualTo(lightState));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewCount));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
            Assert.That(active.isDirty, Is.EqualTo(wasDirty));
            Assert.That(File.ReadAllBytes(copy), Is.EqualTo(source));
            LogAssert.NoUnexpectedReceived();
        }

        private static Light2D[] SceneLights() => UnityEngine.Object.FindObjectsByType<Light2D>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);

        private static string LightState(Light2D light) => light.GetInstanceID() + ":" +
            light.enabled + ":" + light.gameObject.activeSelf + ":" + light.lightType + ":" +
            light.blendStyleIndex + ":" + light.color + ":" + light.intensity + ":" +
            string.Join(",", light.targetSortingLayers ?? Array.Empty<int>());

        [Test]
        public void ScalarPatchPreservesInt64FloatCultureAndUnrelatedText()
        {
            string original = ScalarDocument();
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var changes = new Dictionary<string, string>
                {
                    { "attackPower", long.MaxValue.ToString(CultureInfo.InvariantCulture) },
                    { "criticalChancePercent", "12.375" },
                    { "autoAttackEnabled", "0" }
                };
                Assert.That(CombatBalanceSceneStore.TryPatchNumericFields(original, ScriptGuid,
                    null, 0, changes, out string updated, out string error), Is.True, error);
                Assert.That(updated, Is.EqualTo(original.Replace("attackPower: 10", "attackPower: 9223372036854775807")
                    .Replace("criticalChancePercent: 5", "criticalChancePercent: 12.375")
                    .Replace("autoAttackEnabled: 1", "autoAttackEnabled: 0")));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void PlainComponentBaseStatPatchOnlyChangesTheRequestedNestedNumber()
        {
            string original = ScalarDocument().Replace("  attackPower: 10\n",
                "  baseStat:\n    DamageMultiplier: 2\n    CooldownSeconds: 8\n");
            Assert.That(CombatBalanceSceneStore.TryPatchNumericFields(original, ScriptGuid,
                null, 0, Values("baseStat.DamageMultiplier", "3.25"), out string updated,
                out string error), Is.True, error);
            Assert.That(updated, Is.EqualTo(original.Replace("DamageMultiplier: 2", "DamageMultiplier: 3.25")));
        }

        [TestCase("missing")]
        [TestCase("duplicate-field")]
        [TestCase("duplicate-component")]
        public void ScalarPatchRejectsMissingOrAmbiguousTargets(string fault)
        {
            string original = ScalarDocument();
            if (fault == "missing") original = original.Replace("  attackPower: 10\n", string.Empty);
            if (fault == "duplicate-field") original = original.Replace("  attackPower: 10\n", "  attackPower: 10\n  attackPower: 20\n");
            if (fault == "duplicate-component") original += ScalarDocument();
            Assert.That(CombatBalanceSceneStore.TryPatchNumericFields(original, ScriptGuid,
                null, 0, Values("attackPower", "99"), out string updated, out string error), Is.False);
            Assert.That(updated, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void PrefabPatchAddsAndUpdatesOnlyTheSpecifiedSourceComponentOverride()
        {
            string original = PrefabDocument(Override("baseStat.CooldownSeconds", "8") +
                Override("editorSkillAcquired", "1"));
            var changes = Values("baseStat.CooldownSeconds", "4.5");
            changes.Add("baseStat.DamageMultiplier", "3.125");
            Assert.That(CombatBalanceSceneStore.TryPatchNumericFields(original, ScriptGuid,
                PrefabGuid, 100002, changes, out string updated, out string error), Is.True, error);
            string expected = original.Replace("      value: 8\n", "      value: 4.5\n")
                .Replace("    m_RemovedComponents:", Override("baseStat.DamageMultiplier", "3.125") + "    m_RemovedComponents:");
            Assert.That(updated, Is.EqualTo(expected));
            Assert.That(CombatBalanceSceneStore.TryPatchNumericFields(updated, ScriptGuid,
                PrefabGuid, 100002, changes, out string second, out error), Is.True, error);
            Assert.That(second, Is.EqualTo(updated), "Repeating a save must not append duplicate overrides.");
        }

        [Test]
        public void PrefabPatchCanPopulateAnEmptyOverrideListWithCRLF()
        {
            string original = PrefabDocument(string.Empty).Replace("    m_Modifications:\n", "    m_Modifications: []\n").Replace("\n", "\r\n");
            Assert.That(CombatBalanceSceneStore.TryPatchNumericFields(original, ScriptGuid,
                PrefabGuid, 100002, Values("baseStat.Count", "4"), out string updated,
                out string error), Is.True, error);
            string expected = original.Replace("    m_Modifications: []\r\n", "    m_Modifications:\r\n")
                .Replace("    m_RemovedComponents:", Override("baseStat.Count", "4").Replace("\n", "\r\n") + "    m_RemovedComponents:");
            Assert.That(updated, Is.EqualTo(expected));
        }

        [TestCase("duplicate-override")]
        [TestCase("duplicate-prefab")]
        [TestCase("missing-prefab")]
        [TestCase("nonzero-reference")]
        public void PrefabPatchRejectsMalformedOrAmbiguousTargets(string fault)
        {
            string entry = Override("baseStat.CooldownSeconds", "8");
            string original = PrefabDocument(entry);
            if (fault == "duplicate-override") original = PrefabDocument(entry + entry);
            if (fault == "duplicate-prefab") original += original;
            if (fault == "missing-prefab") original = original.Replace("m_SourcePrefab:", "m_MissingSource:");
            if (fault == "nonzero-reference") original = original.Replace("objectReference: {fileID: 0}", "objectReference: {fileID: 7}");
            Assert.That(CombatBalanceSceneStore.TryPatchNumericFields(original, ScriptGuid,
                PrefabGuid, 100002, Values("baseStat.CooldownSeconds", "4"), out string updated,
                out string error), Is.False);
            Assert.That(updated, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void CopiedMainCapturesAndBuildsAllSevenAuthoredSkillsWithoutWritingFiles()
        {
            byte[] untouchedMain = File.ReadAllBytes(MainPath);
            string copy = CreateMainCopy();
            byte[] before = File.ReadAllBytes(copy);
            byte[] metaBefore = File.ReadAllBytes(copy + ".meta");
            int previewCount = EditorSceneManager.previewSceneCount;
            Assert.That(CombatBalanceSceneStore.TryCapture(copy, out var settings, out string error), Is.True, error);
            Assert.That(settings.Skills.Select(skill => skill.SkillId).Distinct().Count(), Is.EqualTo(7));
            settings.AttackPower = long.MaxValue;
            settings.DurationSeconds = 27f;
            foreach (SkillBalanceSettings skill in settings.Skills)
            {
                SkillBalanceValue multiplier = skill.Values.Find(value => value.Id == "damageMultiplier" || value.Id == "splashDamageMultiplier");
                Assert.That(multiplier, Is.Not.Null, skill.SkillId);
                multiplier.Value += 0.5f;
                skill.Enabled = true;
            }
            Assert.That(CombatBalanceSceneStore.TryBuildSceneText(copy, settings,
                out string original, out string updated, out error), Is.True, error);
            Assert.That(original, Is.EqualTo(Encoding.UTF8.GetString(before)));
            Assert.That(updated, Does.Contain("attackPower: 9223372036854775807"));
            Assert.That(updated, Does.Contain("durationSeconds: 27"));
            Assert.That(File.ReadAllBytes(copy), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(copy + ".meta"), Is.EqualTo(metaBefore));

            // Only the disposable copied scene is written to verify a real Unity reread.
            File.WriteAllText(copy, updated, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport);
            Assert.That(CombatBalanceSceneStore.TryCapture(copy, out var reread, out error), Is.True, error);
            Assert.That(reread.AttackPower, Is.EqualTo(long.MaxValue));
            Assert.That(reread.DurationSeconds, Is.EqualTo(27f));
            foreach (SkillBalanceSettings skill in settings.Skills)
            {
                SkillBalanceSettings saved = reread.Skills.Find(value => value.SkillId == skill.SkillId);
                Assert.That(saved, Is.Not.Null);
                foreach (SkillBalanceValue value in skill.Values)
                    Assert.That(saved.Values.Find(candidate => candidate.Id == value.Id).Value,
                        Is.EqualTo(value.Value), skill.SkillId + "." + value.Id);
            }
            Assert.That(File.ReadAllBytes(copy + ".meta"), Is.EqualTo(metaBefore));
            Assert.That(File.ReadAllBytes(MainPath), Is.EqualTo(untouchedMain));
            Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewCount));
        }

        [Test]
        public void SynchronizeOnlyChangesAuthoredFieldsAndPreservesOtherDirtySceneEdits()
        {
            string copy = CreateMainCopy();
            Assert.That(CombatBalanceSceneStore.TryCapture(copy, out var settings, out string error), Is.True, error);
            byte[] diskBefore = File.ReadAllBytes(copy);
            _loadedCopy = EditorSceneManager.OpenScene(copy, OpenSceneMode.Additive);
            PlayerCombatStatsDefaults defaults = Find<PlayerCombatStatsDefaults>(_loadedCopy);
            PlayerCombatStatsRuntime runtime = Find<PlayerCombatStatsRuntime>(_loadedCopy);
            TornadoSkillController tornado = Find<TornadoSkillController>(_loadedCopy);
            Vector3 unrelatedPosition = new Vector3(31f, -17f, 4f);
            defaults.transform.localPosition = unrelatedPosition;
            var runtimeSerialized = new SerializedObject(runtime);
            runtimeSerialized.FindProperty("attackPower").longValue = 1234L;
            runtimeSerialized.ApplyModifiedPropertiesWithoutUndo();
            bool acquired = new SerializedObject(tornado).FindProperty("editorSkillAcquired").boolValue;
            EditorSceneManager.MarkSceneDirty(_loadedCopy);
            settings.AttackPower = 47L;
            settings.DurationSeconds = 23f;
            foreach (SkillBalanceSettings skill in settings.Skills) skill.Enabled = !skill.Enabled;
            CombatBalanceSceneStore.SynchronizeLoadedScene(copy, settings);
            Assert.That(defaults.Snapshot.AttackPower, Is.EqualTo(47L));
            Assert.That(defaults.transform.localPosition, Is.EqualTo(unrelatedPosition));
            Assert.That(new SerializedObject(runtime).FindProperty("attackPower").longValue, Is.EqualTo(1234L));
            Assert.That(new SerializedObject(tornado).FindProperty("editorSkillAcquired").boolValue, Is.EqualTo(acquired));
            Assert.That(_loadedCopy.isDirty, Is.True);
            Assert.That(File.ReadAllBytes(copy), Is.EqualTo(diskBefore));
        }

        private string CreateMainCopy()
        {
            _testFolder = "Assets/__CombatBalanceSceneStoreTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", _testFolder.Substring("Assets/".Length));
            string copy = _testFolder + "/Main.unity";
            Assert.That(AssetDatabase.CopyAsset(MainPath, copy), Is.True);
            return copy;
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        }

        private static Dictionary<string, string> Values(string name, string value)
        {
            return new Dictionary<string, string> { { name, value } };
        }

        private static string ScalarDocument()
        {
            return "%YAML 1.1\n--- !u!114 &123\nMonoBehaviour:\n  m_Script: {fileID: 11500000, guid: " + ScriptGuid + ", type: 3}\n" +
                "  attackPower: 10\n  criticalChancePercent: 5\n  autoAttackEnabled: 1\n  unrelated: keep-me\n" +
                "--- !u!1 &456\nGameObject:\n  m_Name: Preserve this unrelated object\n";
        }

        private static string PrefabDocument(string entries)
        {
            return "%YAML 1.1\n--- !u!1001 &789\nPrefabInstance:\n  m_Modification:\n    serializedVersion: 3\n" +
                "    m_Modifications:\n" + entries + "    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n" +
                "  m_SourcePrefab: {fileID: 100100000, guid: " + PrefabGuid + ", type: 3}\n" +
                "--- !u!114 &999\nMonoBehaviour:\n  unrelatedTimer: 123.456\n";
        }

        private static string Override(string path, string value)
        {
            return "    - target: {fileID: 100002, guid: " + PrefabGuid + ", type: 3}\n      propertyPath: " + path +
                "\n      value: " + value + "\n      objectReference: {fileID: 0}\n";
        }
    }
}
