using System.Collections;
using System.Linq;
using System.Reflection;
using CursorHunter.App;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.Combat.Tests
{
    public sealed class CombatGemstoneListPlayModeTests
    {
        private Scene _scene;
        private HuntManager _hunt;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_hunt != null) _hunt.ResetPrototypeRun();
            if (_scene.IsValid() && _scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_scene);
        }

        [UnityTest]
        public IEnumerator MainHudTracksCurrentRunDropsAndClearsWhenReturningToMenu()
        {
#if UNITY_EDITOR
            _scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _hunt = Object.FindFirstObjectByType<HuntManager>();
            var combat = Object.FindFirstObjectByType<CombatRunController>();
            var list = Object.FindFirstObjectByType<CombatGemstoneList>(FindObjectsInactive.Include);
            string savedProfile = PlayerPrefs.GetString("cursor_hunter.progression.v1", "missing");

            // Exercise the scene's actual trait-connected entry point first.
            _hunt.BeginPrototypeRun();
            Assert.That(combat.IsRunActive, Is.True);
            Assert.That(list.GetComponentsInChildren<GemstoneIconView>(), Is.Not.Empty);
            AssertAmountsMatch(list, combat);
            _hunt.ResetPrototypeRun();

            // Then use a fixed test roster, with settlement disabled, for a guaranteed bonus drop.
            typeof(HuntManager).GetField("connectTraitScreenToCombat",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_hunt, false);
            CombatBalanceSettings settings = _hunt.CaptureCombatBalanceSettings();
            settings.AttackPower = 1L;
            settings.CriticalChancePercent = 0f;
            settings.DurationSeconds = 30f;
            foreach (SkillBalanceSettings skill in settings.Skills) skill.Enabled = false;
            foreach (MonsterBalanceSettings monster in settings.Monsters)
            {
                monster.Enabled = monster.MonsterId == "monster.01";
                monster.MaxHealth = 10000L;
                monster.PackSize = 1;
                monster.GarnetReward = 2L;
                monster.BonusDropCurrencyId = "gem.dragon";
                monster.BonusDropAmount = 3L;
                monster.BonusDropChancePercent = 100f;
            }
            Assert.That(_hunt.TrySetCombatBalanceSettings(settings, out string error), Is.True, error);
            _hunt.BeginPrototypeRun();
            Assert.That(_hunt.IsCombatBalanceTestActive, Is.True);
            Assert.That(list.GetComponentsInChildren<GemstoneIconView>().Select(icon => icon.GemstoneId),
                Is.EqualTo(new[] { "gem.garnet", "gem.dragon" }));

            MonsterCombatTarget target = Object.FindFirstObjectByType<MonsterCombatTarget>();
            Assert.That(combat.TryApplySkillDamage(combat.CurrentRunId, target, 20000L), Is.True);
            var collection = Object.FindFirstObjectByType<GemstoneCollectionController>();
            Assert.That(collection, Is.Not.Null);
            Assert.That(collection.Shown("gem.dragon"), Is.Zero, "Only arrival may update the visible number.");
            for (int frame = 0; frame < 90; frame++) collection.AdvancePresentation(1f / 60f);
            AssertAmountsMatch(list, combat);
            Assert.That(combat.GetResourceEarned("gem.dragon"), Is.EqualTo(3L));
            Assert.That(collection.ArrivedObjects, Is.EqualTo(5));
            foreach (var binding in collection.Bindings)
            {
                var rect = binding.amountText.rectTransform;
                Assert.That(Vector3.Distance(binding.targetPoint.position, rect.TransformPoint(rect.rect.center)), Is.LessThan(.01f));
            }

            _hunt.ReturnToMainMenu();
            Assert.That(list.GetComponentsInChildren<GemstoneIconView>(), Is.Empty);
            Assert.That(list.GetComponentsInChildren<TMP_Text>(true).All(text => text.text == "0"), Is.True);
            Assert.That(PlayerPrefs.GetString("cursor_hunter.progression.v1", "missing"), Is.EqualTo(savedProfile));
            Assert.That(collection.Preview(48), Is.True);
            Assert.That(Object.FindFirstObjectByType<AppUiRootController>().CurrentScreen, Is.EqualTo(UiScreenId.Combat));
            for (int frame = 0; frame < 90; frame++) collection.AdvancePresentation(1f / 60f);
            Assert.That(collection.Shown("gem.garnet"), Is.EqualTo(48));
            Assert.That((collection.EmittedLarge, collection.EmittedMedium, collection.EmittedSmall), Is.EqualTo((4L,1L,3L)));
            collection.CancelPresentation(false);
            Assert.That(Object.FindFirstObjectByType<AppUiRootController>().CurrentScreen, Is.EqualTo(UiScreenId.MainMenu));
            Assert.That(PlayerPrefs.GetString("cursor_hunter.progression.v1", "missing"), Is.EqualTo(savedProfile));
#else
            Assert.Ignore("Requires the Editor PlayMode scene loader.");
            yield break;
#endif
        }

        private static void AssertAmountsMatch(CombatGemstoneList list, CombatRunController combat)
        {
            foreach (GemstoneIconView icon in list.GetComponentsInChildren<GemstoneIconView>())
                Assert.That(icon.transform.parent.GetComponentInChildren<TMP_Text>().text,
                    Is.EqualTo(combat.GetResourceEarned(icon.GemstoneId).ToString()), icon.GemstoneId);
        }

        [UnityTest]
        public IEnumerator MainTimerTracksCombatClockThroughPauseExpiryAndRestart()
        {
#if UNITY_EDITOR
            _scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _hunt = Object.FindFirstObjectByType<HuntManager>();
            var combat = Object.FindFirstObjectByType<CombatRunController>();
            var hud = Object.FindFirstObjectByType<PrototypeRunHud>();
            var spawner = Object.FindFirstObjectByType<MonsterSpawner>();
            var timer = _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true))
                .Single(text => text.transform.parent.name == "Timer" &&
                    text.transform.parent.parent.name == "InGameObjects");
            Color normalColor = timer.color;
            Color warningColor = new Color32(255, 102, 77, 255);
            string savedProfile = PlayerPrefs.GetString("cursor_hunter.progression.v1", "missing");

            typeof(HuntManager).GetField("connectTraitScreenToCombat",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_hunt, false);
            CombatBalanceSettings settings = _hunt.CaptureCombatBalanceSettings();
            settings.DurationSeconds = 15f;
            foreach (SkillBalanceSettings skill in settings.Skills) skill.Enabled = false;
            Assert.That(_hunt.TrySetCombatBalanceSettings(settings, out string error), Is.True, error);
            _hunt.BeginPrototypeRun();
            Assert.That(timer.gameObject.activeInHierarchy, Is.True);
            Assert.That(timer.text, Is.EqualTo("15:00"));
            Assert.That(timer.color, Is.EqualTo(normalColor));
            Assert.That(Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(text => text.name == "PrototypeTimerText"), Is.False);

            combat.AdvanceTime(.25f);
            hud.ShowRunning(combat, spawner);
            Assert.That(timer.text, Is.EqualTo("14:75"));
            Assert.That(combat.PauseRun(), Is.True);
            combat.AdvanceTime(5f);
            hud.ShowRunning(combat, spawner);
            Assert.That(timer.text, Is.EqualTo("14:75"));
            Assert.That(combat.ResumeRun(), Is.True);
            combat.AdvanceTime(11.5f);
            hud.ShowRunning(combat, spawner);
            Assert.That(timer.text, Is.EqualTo("03:25"));
            Assert.That(timer.color, Is.EqualTo(normalColor));
            combat.AdvanceTime(.25f);
            hud.ShowRunning(combat, spawner);
            Assert.That(timer.text, Is.EqualTo("03:00"));
            Assert.That(timer.color, Is.EqualTo(warningColor));
            combat.AdvanceTime(2.75f);
            hud.ShowRunning(combat, spawner);
            Assert.That(timer.text, Is.EqualTo("00:25"), "Do not show zero while combat still has time.");
            Assert.That(timer.color, Is.EqualTo(warningColor));
            combat.AdvanceTime(.25f);
            Assert.That(combat.IsRunActive, Is.False);
            Assert.That(timer.text, Is.EqualTo("00:00"), "The terminal callback must update the last frame.");
            Assert.That(timer.gameObject.activeInHierarchy, Is.True);

            _hunt.ReturnToMainMenu();
            Assert.That(timer.gameObject.activeInHierarchy, Is.False);
            Assert.That(timer.color, Is.EqualTo(normalColor));
            _hunt.BeginPrototypeRun();
            Assert.That(timer.text, Is.EqualTo("15:00"));
            Assert.That(timer.color, Is.EqualTo(normalColor));
            Assert.That(timer.gameObject.activeInHierarchy, Is.True);
            Assert.That(PlayerPrefs.GetString("cursor_hunter.progression.v1", "missing"), Is.EqualTo(savedProfile));
#else
            Assert.Ignore("Requires the Editor PlayMode scene loader.");
            yield break;
#endif
        }
    }
}
