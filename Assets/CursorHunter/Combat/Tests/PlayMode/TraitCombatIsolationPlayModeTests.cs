using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CursorHunter.App;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
namespace CursorHunter.Combat.Tests
{
    public sealed class TraitCombatIsolationPlayModeTests
    {
        private const string MainPath = "Assets/CursorHunter/App/Scenes/Main.unity";
        private static readonly string[] ProfileKeys =
            { "cursor_hunter.progression.v1", "cursor_hunter.progression.v1.before-v4" };
        private readonly Dictionary<string, string> _profileBackup = new Dictionary<string, string>();
        private Scene _scene;
        private HuntManager _hunt;
        private CombatRunController _combat;
        private MonsterSpawner _spawner;
        private MonoBehaviour _traits;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _profileBackup.Clear();
            foreach (string key in ProfileKeys)
                _profileBackup[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
#if UNITY_EDITOR
            _scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                MainPath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _hunt = Find<HuntManager>();
            _combat = Find<CombatRunController>();
            _spawner = Find<MonsterSpawner>();
            _traits = Components().Single(value => value.GetType().Name == "TraitScreenController");
            Assert.That(_hunt.IsTraitScreenConnectedToCombat, Is.True,
                "Player combat must default to the real purchased trait snapshot.");
            // This fixture verifies the explicit authored/sandbox path. The
            // player path is exercised by TraitTooltipPlayModeTests.
            SetField(_hunt, "connectTraitScreenToCombat", false);
            SetField(_hunt, "defaultStage", UnityEditor.AssetDatabase.LoadAssetAtPath<CombatStageDefinition>(
                "Assets/CursorHunter/App/Stages/Stage01.asset"));
            _hunt.ClearCombatBalanceSettings();
            foreach (MonoBehaviour skill in Skills()) SetField(skill, "editorSkillAcquired", false);
            // This read initializes the real catalog and loads its profile once.
            // No public purchase/wallet setters are used by this fixture.
            ReadTraitInformation();
            AssertProfileUnchanged();
#else
            Assert.Ignore("Requires the Editor PlayMode scene loader.");
            yield break;
#endif
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_hunt != null) _hunt.ResetPrototypeRun();
            if (_scene.IsValid() && _scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_scene);
            // Restore only if a regression actually wrote the user's profile.
            bool changed = false;
            foreach (var pair in _profileBackup)
            {
                if (pair.Value == null)
                {
                    if (!PlayerPrefs.HasKey(pair.Key)) continue;
                    PlayerPrefs.DeleteKey(pair.Key);
                }
                else
                {
                    if (PlayerPrefs.HasKey(pair.Key) && PlayerPrefs.GetString(pair.Key) == pair.Value) continue;
                    PlayerPrefs.SetString(pair.Key, pair.Value);
                }
                changed = true;
            }
            if (changed) PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator AllRealTraitPurchasesCannotChangeMainStatsTimeSpawnPlanOrSkillUnlocks()
        {
            HashSet<string> purchased = Purchased();
            purchased.Clear();
            GameInformation beforeInformation = ReadTraitInformation();
            CombatSnapshot expected = _combat.PlayerCombatStatsRuntime.DefaultsCombatSnapshot;
            float expectedDuration = (float)Field(_hunt, "durationSeconds");
            _hunt.BeginPrototypeRun();
            RunView first = CaptureRun();
            Assert.That(first.Stats, Is.EqualTo(expected), "Main PlayerCombatStatsDefaults must win over raw JSON and traits.");
            Assert.That(first.Duration, Is.EqualTo(expectedDuration));
            AssertAuthoredStagePlan();
            AssertSkillsConfigured(null);
            _hunt.ResetPrototypeRun();

            AddAllRealPurchases();
            GameInformation afterInformation = ReadTraitInformation();
            Assert.That(afterInformation.stats.attackPower, Is.GreaterThan(beforeInformation.stats.attackPower),
                "Positive control: the in-memory purchases must really change the old trait producer.");
            Assert.That(afterInformation.stats.normalFieldDurationSeconds,
                Is.GreaterThan(beforeInformation.stats.normalFieldDurationSeconds));
            Assert.That(afterInformation.skills.Any(skill => skill.enabled), Is.True);
            Assert.That(afterInformation.monsters.Any(monster => monster.productionCount > 1), Is.True);

            // Exercise the old connection once, then disconnect in the same
            // session. A trait run must not overwrite the authored time limit.
            SetField(_hunt, "connectTraitScreenToCombat", true);
            _hunt.BeginPrototypeRun();
            RunView connected = CaptureRun();
            Assert.That(connected.Stats.AttackPower, Is.GreaterThan(first.Stats.AttackPower));
            Assert.That(connected.Duration, Is.GreaterThan(first.Duration));
            Assert.That((float)Field(_hunt, "durationSeconds"), Is.EqualTo(expectedDuration));
            _hunt.ResetPrototypeRun();
            SetField(_hunt, "connectTraitScreenToCombat", false);

            _hunt.BeginPrototypeRun();
            RunView second = CaptureRun();
            Assert.That(second.Stats, Is.EqualTo(first.Stats));
            Assert.That(second.Duration, Is.EqualTo(first.Duration));
            Assert.That(second.GlobalAliveLimit, Is.EqualTo(first.GlobalAliveLimit));
            Assert.That(second.Monsters, Is.EqualTo(first.Monsters),
                "Stage roster, HP, production count, profiles, limits and rewards must ignore purchases.");
            Assert.That(second.Skills, Is.EqualTo(first.Skills));
            AssertAuthoredStagePlan();
            AssertSkillsConfigured(null);
            Assert.That(Field(_traits, "_activeRunSnapshot"), Is.Null);
            AssertProfileUnchanged();
            yield break;
        }

        [UnityTest]
        public IEnumerator InspectorTornadoUsesItsBaseStatsWithoutAnyPurchasedAttackOrSkillBonuses()
        {
            AddAllRealPurchases();
            var tornado = Find<TornadoSkillController>();
            SetField(tornado, "editorSkillAcquired", true);
            SkillBalanceSettings basis = tornado.CaptureBalanceSettings();
            long attack = _combat.PlayerCombatStatsRuntime.Defaults.AttackPower;
            _hunt.BeginPrototypeRun();
            CaptureRun();
            AssertSkillsConfigured(TornadoSkillController.SkillId);
            object runtime = Field(tornado, "_runtime");
            float multiplier = basis.Values.Single(value => value.Id == "damageMultiplier").Value;
            double scaled = multiplier;
            long expectedDamage = scaled >= long.MaxValue ? long.MaxValue :
                Math.Max(1L, (long)Math.Round(scaled, MidpointRounding.AwayFromZero));
            Assert.That(Property<long>(runtime, "Damage"), Is.EqualTo(expectedDamage));
            Assert.That(expectedDamage, Is.Not.EqualTo(Math.Max(1L,
                (long)Math.Round(attack * (double)multiplier,
                    MidpointRounding.AwayFromZero))),
                "A purchased player attack increase must not scale the skill's authored damage.");
            Assert.That(Property<float>(runtime, "CooldownSeconds"), Is.EqualTo(
                basis.Values.Single(value => value.Id == "cooldownSeconds").Value));
            Assert.That(Property<float>(runtime, "MoveSpeedWorldUnitsPerSecond"), Is.EqualTo(
                basis.Values.Single(value => value.Id == "moveSpeedWorldUnitsPerSecond").Value));
            Assert.That(_combat.CombatSnapshot.AttackPower, Is.EqualTo(attack));
            AssertProfileUnchanged();
            yield break;
        }

        [UnityTest]
        public IEnumerator StageStillStartsWhenTheTraitControllerIsAbsent()
        {
            CombatSnapshot expected = _combat.PlayerCombatStatsRuntime.DefaultsCombatSnapshot;
            Object.Destroy(_traits);
            yield return null;
            Assert.That(Components().Any(value => value.GetType().Name == "TraitScreenController"), Is.False);
            _hunt.BeginPrototypeRun();
            RunView run = CaptureRun();
            Assert.That(run.Stats, Is.EqualTo(expected));
            AssertAuthoredStagePlan();
            AssertSkillsConfigured(null);
            AssertProfileUnchanged();
        }

        [UnityTest]
        public IEnumerator EligibleCombatRewardsAndHudInformationCannotReachTheTraitWalletOrSummary()
        {
            long garnet = (long)Field(_traits, "_garnetBalance");
            long[] gems = ((long[])Field(_traits, "_gemstoneBalances")).ToArray();
            _hunt.BeginPrototypeRun();
            CaptureRun();
            var hud = Find<PrototypeRunHud>();
            hud.ShowProgressionInfo();
            Assert.That(Property<bool>(_traits, "IsSummaryOpen"), Is.False);
            var infoButton = Field(hud, "_progressionInfoButton") as Component;
            Assert.That(infoButton == null || !infoButton.gameObject.activeInHierarchy, Is.True);

            MonsterCombatTarget victim = FindAll<MonsterCombatTarget>().First(value => value.IsActive && value.GarnetReward > 0L);
            Assert.That(_combat.TryApplySkillDamage(_combat.CurrentRunId, victim, victim.CurrentHealth), Is.True);
            Assert.That(_combat.GarnetEarned, Is.GreaterThan(0L), "The test must exercise a real earned reward.");
            var coordinator = (RunCoordinator)Field(_hunt, "_runCoordinator");
            RunResult? completed = null;
            coordinator.Completed += result => completed = result;
            _combat.AdvanceTime(_combat.DurationSeconds);
            Assert.That(completed.HasValue, Is.True);
            Assert.That(completed.Value.SettlementPolicy, Is.EqualTo(RunSettlementPolicy.Eligible));
            Assert.That(completed.Value.GarnetEarned, Is.GreaterThan(0L));
            Assert.That((long)Field(_traits, "_garnetBalance"), Is.EqualTo(garnet));
            Assert.That((long[])Field(_traits, "_gemstoneBalances"), Is.EqualTo(gems));
            hud.ShowProgressionInfo();
            Assert.That(Property<bool>(_traits, "IsSummaryOpen"), Is.False);
            Assert.That(infoButton == null || !infoButton.gameObject.activeInHierarchy, Is.True);
            AssertProfileUnchanged();
            yield break;
        }

        private sealed class RunView
        {
            internal CombatSnapshot Stats;
            internal float Duration;
            internal int GlobalAliveLimit;
            internal SpawnSnapshot[] Monsters;
            internal string[] Skills;
        }

        private RunView CaptureRun()
        {
            Assert.That(_combat.IsRunActive, Is.True);
            Assert.That(_hunt.IsCombatBalanceTestActive, Is.False);
            Assert.That(_spawner.CurrentSpawnPlan, Is.Not.Null);
            return new RunView
            {
                Stats = _combat.CombatSnapshot,
                Duration = _combat.DurationSeconds,
                GlobalAliveLimit = _spawner.CurrentSpawnPlan.GlobalAliveLimit,
                Monsters = _spawner.CurrentSpawnPlan.Entries.Select(entry => entry.Snapshot).ToArray(),
                Skills = _combat.CaptureSkillBalanceSettings().Select(JsonUtility.ToJson).ToArray()
            };
        }

        private void AssertAuthoredStagePlan()
        {
            object stage = Field(_hunt, "defaultStage");
            Assert.That(stage, Is.Not.Null);
            var rows = (Array)Field(stage, "monsters");
            GameInformation authored = JsonUtility.FromJson<GameInformation>(
                Resources.Load<TextAsset>("GameData/game-data.en").text);
            var expected = new List<SpawnSnapshot>();
            foreach (object row in rows)
            {
                if (!Property<bool>(row, "AllowSpawn")) continue;
                object definition = Property<object>(row, "Monster");
                int production = Convert.ToInt32(Property<object>(row, "Production"), CultureInfo.InvariantCulture);
                string monsterId = Property<string>(definition, "MonsterId");
                int resolvedProduction = production == 0
                    ? authored.monsters.Single(monster => monster.id == monsterId).productionCount
                    : production;
                int bonus = Math.Max(0, resolvedProduction - 1);
                MethodInfo create = definition.GetType().GetMethod("CreateSnapshotWithProductionBonus");
                Assert.That(create, Is.Not.Null);
                expected.Add((SpawnSnapshot)create.Invoke(definition,
                    new object[] { _spawner.CurrentSpawnPlan.Entries[0].Snapshot.AliveLimit, bonus }));
            }
            Assert.That(_spawner.CurrentSpawnPlan.Entries.Select(entry => entry.Snapshot).ToArray(),
                Is.EqualTo(expected.ToArray()), "The actual authored Stage must determine the roster and explicit production.");
        }

        private void AssertSkillsConfigured(string enabledId)
        {
            foreach (MonoBehaviour component in Skills())
            {
                string id = ((IRunSkillController)component).Id;
                Assert.That((bool)Field(component, "_configured"), Is.EqualTo(id == enabledId), id);
            }
        }

        private void AddAllRealPurchases()
        {
            HashSet<string> purchased = Purchased();
            purchased.Clear();
            object document = Field(_traits, "_document");
            object progression = Field(document, "progression");
            foreach (object category in (Array)Field(progression, "categories"))
                foreach (object node in (Array)Field(category, "nodes"))
                    purchased.Add((string)Field(node, "id"));
            Assert.That(purchased.Count, Is.GreaterThan(50));
        }

        private HashSet<string> Purchased() => (HashSet<string>)Field(_traits, "_purchased");

        private GameInformation ReadTraitInformation()
        {
            string json = (string)_traits.GetType().GetMethod("CreateGameInformationJson").Invoke(_traits, null);
            GameInformation information = JsonUtility.FromJson<GameInformation>(json);
            Assert.That(information, Is.Not.Null);
            Assert.That(information.TryValidate(out string error), Is.True, error);
            return information;
        }

        private void AssertProfileUnchanged()
        {
            foreach (var pair in _profileBackup)
            {
                Assert.That(PlayerPrefs.HasKey(pair.Key), Is.EqualTo(pair.Value != null), pair.Key);
                if (pair.Value != null) Assert.That(PlayerPrefs.GetString(pair.Key), Is.EqualTo(pair.Value), pair.Key);
            }
        }

        private IEnumerable<MonoBehaviour> Components() => FindAll<MonoBehaviour>().Where(value => value != null);
        private IEnumerable<MonoBehaviour> Skills() => Components().Where(value => value is ISkillBalanceController);
        private T Find<T>() where T : Component => FindAll<T>().Single();
        private IEnumerable<T> FindAll<T>() where T : Component =>
            _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

        private static object Field(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
            field.SetValue(target, value);
        }

        private static T Property<T>(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, target.GetType().Name + "." + name);
            return (T)property.GetValue(target);
        }
    }
}
#endif
