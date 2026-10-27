using System.Collections;
using System.Reflection;
using CursorHunter.App;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.Combat.Tests
{
    public sealed class CombatBalanceMainScenePlayModeTests
    {
        private Scene _scene;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_scene.IsValid() && _scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_scene);
        }

        [UnityTest]
        public IEnumerator ReadingAuthoredDefaultsDuringMainCombatKeepsRunAndLightingUnchanged()
        {
#if UNITY_EDITOR
            const string path = "Assets/CursorHunter/App/Scenes/Main.unity";
            _scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                path, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var hunt = Object.FindFirstObjectByType<HuntManager>();
            var combat = Object.FindFirstObjectByType<CombatRunController>();
            long sourceAttack = combat.PlayerCombatStatsRuntime.Defaults.AttackPower;
            CombatBalanceSettings settings = hunt.CaptureCombatBalanceSettings();
            settings.AttackPower = sourceAttack == 77L ? 78L : 77L;
            foreach (SkillBalanceSettings skill in settings.Skills) skill.Enabled = false;
            Assert.That(hunt.TrySetCombatBalanceSettings(settings, out string error), Is.True, error);
            hunt.BeginPrototypeRun();
            Assert.That(hunt.IsCombatBalanceTestActive, Is.True);
            RunId run = combat.CurrentRunId;
            float elapsed = combat.ElapsedSeconds;
            byte[] sourceBytes = System.IO.File.ReadAllBytes(path);

            // Editor APIs are reflected so the PlayMode assembly remains usable
            // without a dependency on an Editor-only assembly in player builds.
            System.Type store = System.Type.GetType(
                "CursorHunter.App.Editor.CombatBalanceSceneStore, CursorHunter.App.Editor");
            Assert.That(store, Is.Not.Null);
            var capture = new object[] { path, null, null };
            Assert.That((bool)store.GetMethod("TryCapture").Invoke(null, capture), Is.True,
                capture[2] as string);
            var authored = (CombatBalanceSettings)capture[1];
            Assert.That(authored.AttackPower, Is.EqualTo(sourceAttack));
            var prepare = new object[] { path, authored, null, null, null };
            Assert.That((bool)store.GetMethod("TryBuildSceneText").Invoke(null, prepare), Is.True,
                prepare[4] as string);
            Assert.That(combat.CurrentRunId, Is.EqualTo(run));
            Assert.That(combat.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(combat.CombatSnapshot.AttackPower, Is.EqualTo(settings.AttackPower));
            Assert.That(System.IO.File.ReadAllBytes(path), Is.EqualTo(sourceBytes));
            LogAssert.NoUnexpectedReceived();
#else
            Assert.Ignore("Requires Editor PlayMode.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator MainUsesDirectValuesUpdatesSameRunAndNeverSettlesTestRewards()
        {
#if UNITY_EDITOR
            _scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var hunt = Object.FindFirstObjectByType<HuntManager>();
            var combat = Object.FindFirstObjectByType<CombatRunController>();
            Assert.That(hunt, Is.Not.Null);
            CombatBalanceSettings settings = hunt.CaptureCombatBalanceSettings();
            settings.AttackPower = 20L;
            settings.CriticalChancePercent = 0f;
            settings.DurationSeconds = 30f;
            foreach (SkillBalanceSettings skill in settings.Skills) skill.Enabled = false;
            foreach (MonsterBalanceSettings monster in settings.Monsters)
            {
                monster.Enabled = monster.MonsterId == "monster.01";
                monster.MaxHealth = 100L;
                monster.SpawnIntervalSeconds = 10f;
                monster.PackSize = 1;
            }
            string savedProfile = PlayerPrefs.GetString("cursor_hunter.progression.v1", "missing");
            Assert.That(hunt.TrySetCombatBalanceSettings(settings, out string error), Is.True, error);
            hunt.BeginPrototypeRun();
            Assert.That(hunt.IsCombatBalanceTestActive, Is.True);
            Assert.That(combat.CombatSnapshot.AttackPower, Is.EqualTo(20L));
            RunId run = combat.CurrentRunId;
            foreach (SkillBalanceSettings skill in combat.CaptureSkillBalanceSettings())
                Assert.That(skill.Enabled, Is.False, skill.SkillId + " ignored explicit OFF");
            MonsterCombatTarget target = Object.FindFirstObjectByType<MonsterCombatTarget>();
            Assert.That(target, Is.Not.Null);
            Assert.That(target.MaxHealth, Is.EqualTo(100L));
            Assert.That(combat.TryApplySkillDamage(run, target, 40L), Is.True);
            Assert.That(target.CurrentHealth, Is.EqualTo(60L));

            settings.AttackPower = 30L;
            foreach (MonsterBalanceSettings monster in settings.Monsters) monster.MaxHealth = 200L;
            float elapsed = combat.ElapsedSeconds;
            Assert.That(hunt.TrySetCombatBalanceSettings(settings, out error), Is.True, error);
            Assert.That(combat.CurrentRunId, Is.EqualTo(run));
            Assert.That(combat.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(target.CurrentHealth, Is.EqualTo(120L));
            Assert.That(combat.CombatSnapshot.AttackPower, Is.EqualTo(30L));
            Assert.That(hunt.AppliedCombatBalanceSettings.AttackPower, Is.EqualTo(30L));

            CombatBalanceSettings invalid = settings.Clone();
            invalid.AttackPower = 99L;
            invalid.Monsters[0].MaxHealth = 0L;
            Assert.That(hunt.TrySetCombatBalanceSettings(invalid, out _), Is.False);
            Assert.That(combat.CombatSnapshot.AttackPower, Is.EqualTo(30L));
            Assert.That(target.CurrentHealth, Is.EqualTo(120L));

            foreach (MonsterBalanceSettings monster in settings.Monsters) monster.Enabled = false;
            Assert.That(hunt.TrySetCombatBalanceSettings(settings, out error), Is.True, error);
            Assert.That(target.IsActive, Is.True, "Disabling spawn must keep current monsters.");
            Assert.That(combat.TryApplySkillDamage(run, target, 1000L), Is.True);
            settings.Monsters[0].Enabled = true;
            settings.AttackRadiusWorldUnits = 1.5f;
            settings.AttackCooldownSeconds = 0.25f;
            SkillBalanceSettings tornado = settings.Skills.Find(
                skill => skill.SkillId == TornadoSkillController.SkillId);
            tornado.Enabled = true;
            tornado.Values.Find(value => value.Id == "damageMultiplier").Value = 3f;
            Assert.That(hunt.TrySetCombatBalanceSettings(settings, out error), Is.True, error);
            var coordinator = (RunCoordinator)typeof(HuntManager).GetField("_runCoordinator",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hunt);
            RunResult? completed = null;
            coordinator.Completed += result => completed = result;
            combat.AdvanceTime(30f);
            Assert.That(completed.HasValue, Is.True);
            Assert.That(completed.Value.SettlementPolicy, Is.EqualTo(RunSettlementPolicy.Discard));
            Assert.That(hunt.HasCombatBalanceSettings, Is.True);
            Assert.That(hunt.IsCombatBalanceTestActive, Is.False);
            Assert.That(hunt.CaptureCombatBalanceSettings().AttackPower, Is.EqualTo(30L));
            Assert.That(PlayerPrefs.GetString("cursor_hunter.progression.v1", "missing"),
                Is.EqualTo(savedProfile), "A balance test must not change the real progression store.");

            // No new edit or Apply call: every subsequent attempt must keep the values.
            hunt.BeginPrototypeRun();
            Assert.That(hunt.IsCombatBalanceTestActive, Is.True);
            Assert.That(combat.CurrentRunId, Is.Not.EqualTo(run));
            Assert.That(combat.CombatSnapshot.AttackPower, Is.EqualTo(30L));
            Assert.That(combat.CombatSnapshot.AttackCooldownSeconds, Is.EqualTo(0.25f));
            CombatBalanceSettings secondRun = hunt.AppliedCombatBalanceSettings;
            Assert.That(secondRun.AttackRadiusWorldUnits, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(secondRun.Monsters[0].MaxHealth, Is.EqualTo(200L));
            Assert.That(secondRun.Monsters[0].Enabled, Is.True);
            Assert.That(secondRun.Skills.Find(skill => skill.SkillId == tornado.SkillId).Enabled,
                Is.True);
            Assert.That(secondRun.Skills.Find(skill => skill.SkillId == tornado.SkillId)
                .Values.Find(value => value.Id == "damageMultiplier").Value, Is.EqualTo(3f));

            hunt.ReturnToMainMenu();
            Assert.That(hunt.HasCombatBalanceSettings, Is.True,
                "Returning to the menu must preserve the planner's settings.");
            hunt.BeginPrototypeRun();
            Assert.That(hunt.IsCombatBalanceTestActive, Is.True);
            Assert.That(combat.CombatSnapshot.AttackPower, Is.EqualTo(30L));

            // The window close/clear path must leave a usable main menu.
            hunt.ClearCombatBalanceSettings();
            Assert.That(combat.IsRunActive, Is.False);
            Assert.That(hunt.HasCombatBalanceSettings, Is.False);
            Assert.That(Object.FindFirstObjectByType<AppUiRootController>().CurrentScreen,
                Is.EqualTo(UiScreenId.MainMenu));
            Assert.That(PlayerPrefs.GetString("cursor_hunter.progression.v1", "missing"),
                Is.EqualTo(savedProfile));
#else
            Assert.Ignore("Requires Editor PlayMode.");
            yield break;
#endif
        }
    }
}
