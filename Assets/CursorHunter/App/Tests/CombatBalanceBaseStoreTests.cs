using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CursorHunter.App.Editor;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CursorHunter.App.Tests
{
    public sealed class CombatBalanceBaseStoreTests
    {
        private const string SourceScene = "Assets/CursorHunter/App/Scenes/Main.unity";
        private const string SourceDefinitions = "Assets/CursorHunter/Data/Resources/MonsterDefinitions";
        private readonly Dictionary<string, byte[]> _sourceBytes = new Dictionary<string, byte[]>();
        private readonly List<string> _fixtureFiles = new List<string>();
        private readonly List<MonsterDefinition> _definitions = new List<MonsterDefinition>();
        private string _folder;
        private string _scenePath;
        private string _informationPath;

        [SetUp]
        public void CreateIsolatedCopies()
        {
            _sourceBytes.Clear();
            _fixtureFiles.Clear();
            _definitions.Clear();
            string name = "__CombatBalanceStoreTests-" + Guid.NewGuid().ToString("N");
            _folder = "Assets/" + name;
            Assert.That(AssetDatabase.CreateFolder("Assets", name), Is.Not.Empty);
            Assert.That(AssetDatabase.CreateFolder(_folder, "Resources"), Is.Not.Empty);
            string resources = _folder + "/Resources";
            Assert.That(AssetDatabase.CreateFolder(resources, "MonsterDefinitions"), Is.Not.Empty);
            string definitionsPath = resources + "/MonsterDefinitions";
            _scenePath = Copy(SourceScene, _folder + "/Main.unity");
            _informationPath = Copy(CombatBalanceBaseStore.InformationPath, _folder + "/game-data.en.json");
            string[] sourceDefinitions = AssetDatabase.FindAssets("t:MonsterDefinition", new[] { SourceDefinitions })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.That(sourceDefinitions, Has.Length.EqualTo(11));
            foreach (string source in sourceDefinitions)
            {
                string copied = Copy(source, definitionsPath + "/" + Path.GetFileName(source));
                MonsterDefinition definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(copied);
                Assert.That(definition, Is.Not.Null, copied);
                _definitions.Add(definition);
                foreach (MonsterBehaviorProfile profile in definition.BehaviorProfiles)
                    if (profile != null) RememberSource(AssetDatabase.GetAssetPath(profile));
            }
            _definitions.Sort((first, second) => string.CompareOrdinal(first.MonsterId, second.MonsterId));
            InstallPreservationSentinels();
        }

        [TearDown]
        public void RemoveOnlyFixtureAssets()
        {
            try
            {
                foreach (var source in _sourceBytes)
                    Assert.That(File.ReadAllBytes(source.Key), Is.EqualTo(source.Value),
                        "The authoring test must never modify the real source: " + source.Key);
            }
            finally
            {
                if (!string.IsNullOrEmpty(_folder) && AssetDatabase.IsValidFolder(_folder))
                    Assert.That(AssetDatabase.DeleteAsset(_folder), Is.True, _folder);
                _folder = null;
                _definitions.Clear();
            }
        }

        [Test]
        public void CommitReloadsExactFinalValuesAndPreservesAuthoredIdentityAndAvailability()
        {
            CombatBalanceSettings settings = CreateSettings();
            GameInformation priorInformation = ReadInformation();
            Dictionary<string, byte[]> originalMetas = _fixtureFiles.ToDictionary(
                path => path + ".meta", path => File.ReadAllBytes(path + ".meta"));
            Dictionary<string, string> originalGuids = _fixtureFiles.ToDictionary(
                path => path, AssetDatabase.AssetPathToGUID);
            string priorSceneContent = NonBalanceSceneContent(File.ReadAllText(_scenePath));
            MonsterDefinition profiled = _definitions.Find(definition => definition.MonsterId == "monster.07");
            Assert.That(profiled.BehaviorProfiles.Any(profile => profile != null &&
                Mathf.Approximately(profile.HealthMultiplier, 2.5f)), Is.True,
                "The real profile fixture must exercise the 2.5x HP composition policy.");

            Assert.That(CombatBalanceBaseStore.TryBuildFiles(_scenePath, _informationPath,
                _definitions, settings, out Dictionary<string, string> originals,
                out Dictionary<string, string> updated, out string error), Is.True, error);
            AssertFixturePaths(originals, updated);
            foreach (var original in originals)
                Assert.That(File.ReadAllText(original.Key), Is.EqualTo(original.Value),
                    "Building the candidate must be read-only.");
            Assert.That(NonBalanceSceneContent(updated[_scenePath]), Is.EqualTo(priorSceneContent),
                "Scene names, object references, Inspector acquisition toggles and other non-balance fields must survive.");

            Assert.That(CombatBalanceBaseStore.TryCommitFiles(originals, updated, out error), Is.True, error);
            foreach (string path in updated.Keys)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            // TryCapture reads authored numbers without loading the source scene's objects.
            Assert.That(CombatBalanceSceneStore.TryCapture(_scenePath,
                out CombatBalanceSettings reloaded, out error), Is.True, error);
            Assert.That(reloaded.AttackPower, Is.EqualTo(long.MaxValue));
            Assert.That(reloaded.AutoAttackEnabled, Is.EqualTo(settings.AutoAttackEnabled));
            Assert.That(reloaded.DurationSeconds, Is.EqualTo(23f));
            Assert.That(reloaded.Skills.Find(skill => skill.SkillId == TornadoSkillController.SkillId)
                .Values.Find(value => value.Id == "damageMultiplier").Value, Is.EqualTo(3f));
            GameInformation information = ReadInformation();
            Assert.That(information.stats.attackPower, Is.EqualTo(long.MaxValue));
            Assert.That(information.stats.normalFieldDurationSeconds, Is.EqualTo(23f));
            Assert.That(information.rules.globalAliveLimit, Is.EqualTo(31));
            Assert.That(information.rules.perMonsterAliveLimit, Is.EqualTo(13));
            Assert.That(information.monsters, Has.Length.EqualTo(priorInformation.monsters.Length));
            foreach (MonsterInformation previous in priorInformation.monsters)
            {
                MonsterInformation current = Array.Find(information.monsters, monster => monster.id == previous.id);
                Assert.That(current, Is.Not.Null, previous.id);
                Assert.That(current.enabled, Is.EqualTo(previous.enabled), previous.id + ".enabled");
                Assert.That(current.productionCount, Is.EqualTo(previous.productionCount), previous.id + ".productionCount");
                MonsterBalanceSettings requested = settings.Monsters.Find(monster => monster.MonsterId == previous.id);
                Assert.That(current.hitPoints, Is.EqualTo(requested.MaxHealth), previous.id + ".hitPoints");
            }
            foreach (SkillInformation previous in priorInformation.skills)
            {
                SkillInformation current = Array.Find(information.skills, skill => skill.id == previous.id);
                Assert.That(current, Is.Not.Null, previous.id);
                Assert.That(JsonUtility.ToJson(current), Is.EqualTo(JsonUtility.ToJson(previous)),
                    "Authored skill availability and upgrade deltas are not base stats: " + previous.id);
            }

            foreach (MonsterDefinition definition in _definitions)
            {
                string path = AssetDatabase.GetAssetPath(definition);
                MonsterDefinition loaded = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(path);
                MonsterBalanceSettings requested = settings.Monsters.Find(value => value.MonsterId == loaded.MonsterId);
                SpawnSnapshot actual = loaded.CreateSnapshot(settings.PerMonsterAliveLimit);
                Assert.That(loaded.BaseStatsAreFinalValues, Is.True, loaded.MonsterId);
                Assert.That(actual.IsValid, Is.True, loaded.MonsterId);
                Assert.That(actual.MaxHealth, Is.EqualTo(requested.MaxHealth), loaded.MonsterId);
                Assert.That(actual.PackSize, Is.EqualTo(requested.PackSize), loaded.MonsterId);
                Assert.That(actual.GarnetReward, Is.EqualTo(long.MaxValue), loaded.MonsterId);
            }
            MonsterDefinition reloadedProfiled = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(
                AssetDatabase.GetAssetPath(profiled));
            Assert.That(reloadedProfiled.CreateSnapshot(13).MaxHealth, Is.EqualTo(101L),
                "A final HP of 101 must remain 101 after the 2.5x profile is loaded again.");
            Assert.That(reloadedProfiled.BehaviorProfiles.Any(profile => profile != null &&
                Mathf.Approximately(profile.HealthMultiplier, 2.5f)), Is.True,
                "Saving the final HP must retain the original behavior profile.");
            foreach (var meta in originalMetas)
                Assert.That(File.ReadAllBytes(meta.Key), Is.EqualTo(meta.Value), meta.Key);
            foreach (var guid in originalGuids)
                Assert.That(AssetDatabase.AssetPathToGUID(guid.Key), Is.EqualTo(guid.Value), guid.Key);
        }

        [Test]
        public void InvalidSkillRejectsWholeBuildAndLeavesEveryFixtureFileUnchanged()
        {
            CombatBalanceSettings settings = CreateSettings();
            settings.Skills.Find(skill => skill.SkillId == TornadoSkillController.SkillId)
                .Values.Find(value => value.Id == "cooldownSeconds").Value = 0f;
            Dictionary<string, byte[]> before = _fixtureFiles.ToDictionary(
                path => path, path => File.ReadAllBytes(path));
            Assert.That(CombatBalanceBaseStore.TryBuildFiles(_scenePath, _informationPath,
                _definitions, settings, out Dictionary<string, string> originals,
                out Dictionary<string, string> updated, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(originals, Is.Empty);
            Assert.That(updated, Is.Empty);
            foreach (var file in before)
                Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value), file.Key);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ConcurrentEditOfEitherFileRejectsTheEntireTwoFileCommit(bool changeScene)
        {
            CombatBalanceSettings settings = CreateSettings();
            Assert.That(CombatBalanceBaseStore.TryBuildFiles(_scenePath, _informationPath,
                _definitions, settings, out Dictionary<string, string> allOriginals,
                out Dictionary<string, string> allUpdated, out string error), Is.True, error);
            AssertFixturePaths(allOriginals, allUpdated);
            var originals = new Dictionary<string, string>
            {
                { _scenePath, allOriginals[_scenePath] },
                { _informationPath, allOriginals[_informationPath] }
            };
            var updated = new Dictionary<string, string>
            {
                { _scenePath, allUpdated[_scenePath] },
                { _informationPath, allUpdated[_informationPath] }
            };
            string changed = changeScene ? _scenePath : _informationPath;
            File.AppendAllText(changed, "\nExternal fixture edit\n", new UTF8Encoding(false));
            Dictionary<string, byte[]> beforeCommit = _fixtureFiles.ToDictionary(
                path => path, path => File.ReadAllBytes(path));
            Assert.That(CombatBalanceBaseStore.TryCommitFiles(originals, updated, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            foreach (var file in beforeCommit)
                Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value),
                    "A rejected batch must preserve both files and the external edit: " + file.Key);
            Assert.That(Directory.GetFiles(_folder, "*.balance-*.tmp", SearchOption.AllDirectories), Is.Empty);
        }

        private CombatBalanceSettings CreateSettings()
        {
            Assert.That(CombatBalanceSceneStore.TryCapture(_scenePath,
                out CombatBalanceSettings settings, out string error), Is.True, error);
            settings.AttackPower = long.MaxValue;
            settings.AutoAttackEnabled = !settings.AutoAttackEnabled;
            settings.DurationSeconds = 23f;
            settings.GlobalAliveLimit = 31;
            settings.PerMonsterAliveLimit = 13;
            settings.Skills.Find(skill => skill.SkillId == TornadoSkillController.SkillId)
                .Values.Find(value => value.Id == "damageMultiplier").Value = 3f;
            foreach (SkillBalanceSettings skill in settings.Skills) skill.Enabled = true;
            foreach (MonsterDefinition definition in _definitions)
            {
                MonsterBalanceSettings monster = MonsterBalanceSettings.FromSnapshot(
                    definition.CreateSnapshot(settings.PerMonsterAliveLimit), true);
                monster.MaxHealth = definition.MonsterId == "monster.07" ? 101L : long.MaxValue;
                monster.PackSize = 4;
                monster.GarnetReward = long.MaxValue;
                settings.Monsters.Add(monster);
            }
            return settings;
        }

        private void InstallPreservationSentinels()
        {
            GameInformation information = ReadInformation();
            MonsterInformation monster = Array.Find(information.monsters, value => value.id == "monster.07");
            monster.enabled = false;
            monster.productionCount = 3;
            SkillInformation skill = Array.Find(information.skills, value => value.id == TornadoSkillController.SkillId);
            skill.enabled = true;
            skill.upgrades = new[] { new SkillStatUpgradeInformation { statId = "damageMultiplier", delta = 0.25f } };
            File.WriteAllText(_informationPath, GameInformationJson.Serialize(information), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(_informationPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private GameInformation ReadInformation()
        {
            Assert.That(GameInformationJson.TryDeserialize(File.ReadAllText(_informationPath),
                out GameInformation information, out string error), Is.True, error);
            return information;
        }

        private string Copy(string source, string destination)
        {
            RememberSource(source);
            Assert.That(AssetDatabase.CopyAsset(source, destination), Is.True, source);
            _fixtureFiles.Add(destination);
            return destination;
        }

        private void RememberSource(string path)
        {
            if (!_sourceBytes.ContainsKey(path)) _sourceBytes.Add(path, File.ReadAllBytes(path));
            if (!_sourceBytes.ContainsKey(path + ".meta"))
                _sourceBytes.Add(path + ".meta", File.ReadAllBytes(path + ".meta"));
        }

        private void AssertFixturePaths(IReadOnlyDictionary<string, string> originals,
            IReadOnlyDictionary<string, string> updated)
        {
            Assert.That(originals.Count, Is.EqualTo(updated.Count));
            Assert.That(originals.Keys, Is.EquivalentTo(updated.Keys));
            foreach (string path in originals.Keys)
                Assert.That(path.StartsWith(_folder + "/", StringComparison.Ordinal), Is.True,
                    "The fixture must never commit a production source or reference document: " + path);
        }

        private static string NonBalanceSceneContent(string text)
        {
            const string cursor = "attackPower|attackRadiusWorldUnits|attackCooldownSeconds|autoAttackEnabled|criticalChancePercent|criticalDamageMultiplier|bossDamageMultiplier|durationSeconds";
            text = Regex.Replace(text,
                @"(?m)^    - target:[^\r\n]*\r?\n      propertyPath: (?:baseStat\.[A-Za-z][A-Za-z0-9]*|" + cursor + @")\r?\n      value:[^\r\n]*\r?\n      objectReference:[^\r\n]*(?:\r?\n|$)", string.Empty);
            text = Regex.Replace(text, @"(?m)^  (?:" + cursor + @"):[^\r\n]*(?:\r?\n|$)", string.Empty);
            // The seven BaseStat structs expose only numeric fields with PascalCase names.
            return Regex.Replace(text, @"(?m)^    [A-Z][A-Za-z0-9]*:[^\r\n]*(?:\r?\n|$)", string.Empty);
        }
    }
}
