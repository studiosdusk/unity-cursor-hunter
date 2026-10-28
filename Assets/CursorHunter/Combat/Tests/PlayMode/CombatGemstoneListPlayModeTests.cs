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
            yield return null; // HuntManager refreshes the authored list through PrototypeRunHud.
            AssertAmountsMatch(list, combat);
            Assert.That(combat.GetResourceEarned("gem.dragon"), Is.EqualTo(3L));

            _hunt.ReturnToMainMenu();
            Assert.That(list.GetComponentsInChildren<GemstoneIconView>(), Is.Empty);
            Assert.That(list.GetComponentsInChildren<TMP_Text>(true).All(text => text.text == "0"), Is.True);
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
    }
}
