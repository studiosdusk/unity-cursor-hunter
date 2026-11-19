using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Progression;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CursorHunter.App.Tests
{
    public sealed class PlayerExperiencePresenterPlayModeTests
    {
        [UnityTest]
        public IEnumerator AuthoredSliderTracksKillsLevelUpAndSettlementWithoutGrantingTwice()
        {
#if UNITY_EDITOR
            const string key = "cursor_hunter.progression.v1";
            string[] keys = { key, key + ".before-v6" };
            var previous = keys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
            var created = new List<GameObject>();
            Scene scene = default;
            try
            {
                foreach (string k in keys) PlayerPrefs.DeleteKey(k);
                scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null; yield return null;
                var ui = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                ui.GetComponent<ProgressionTopMenu>().ShowTalents(); yield return null;
                var progression = Find<TraitScreenController>(scene);
                var presenter = Find<PlayerExperiencePresenter>(scene);
                var hud = Find<PrototypeRunHud>(scene);
                Assert.That(presenter.gameObject.name, Is.EqualTo("InGameObjects"));
                var slider = Field<Slider>(presenter, "experienceSlider");
                var level = Field<TMP_Text>(presenter, "levelText");
                var amount = Field<TMP_Text>(presenter, "experienceText");
                Assert.That(slider.name, Is.EqualTo("ExpSlider"));
                Assert.That(level.transform.IsChildOf(slider.transform), Is.True);
                Assert.That(amount.transform.IsChildOf(slider.transform), Is.True);
                presenter.gameObject.SetActive(true);
                Assert.That(progression.GrantRunRewards(null, null, 240), Is.True);
                Assert.That(amount.text, Is.EqualTo("240 / 250"));
                Assert.That(level.text, Is.EqualTo("1"));
                Assert.That(slider.value, Is.EqualTo(.96f).Within(.0001f));
                Assert.That(slider.interactable, Is.False);
                Assert.That(slider.GetComponentsInChildren<Graphic>(true).Any(g => g.raycastTarget), Is.False);

                var combatObject = new GameObject("ExperienceTestCombat");
                created.Add(combatObject);
                var combat = combatObject.AddComponent<CombatRunController>();
                var request = Start(combat);
                hud.ShowRunning(combat, null);
                var first = Target(created, request.RunId, 5);
                Assert.That(combat.TryApplySkillDamage(request.RunId, first, 1), Is.True);
                Assert.That(amount.text, Is.EqualTo("240 / 250"), "A hit without a kill must not grant XP.");
                Assert.That(combat.TryApplySkillDamage(request.RunId, first, 1), Is.True);
                Assert.That(amount.text, Is.EqualTo("245 / 250"), "The kill notification must update immediately.");
                Assert.That(combat.TryApplySkillDamage(request.RunId, first, 2), Is.False);
                Assert.That(presenter.DisplayedExperience, Is.EqualTo(245));

                var second = Target(created, request.RunId, 15);
                Assert.That(combat.TryApplySkillDamage(request.RunId, second, 2), Is.True);
                Assert.That(amount.text, Is.EqualTo("10 / 320"));
                Assert.That(level.text, Is.EqualTo("2"));
                Assert.That(slider.value, Is.EqualTo(10f / 320f).Within(.0001f));
                Assert.That(progression.ExperiencePoints, Is.EqualTo(240), "Live presentation must not save a second payout.");
                Assert.That(progression.TraitCards.Points, Is.Zero);
                hud.ShowRunning(combat, null);
                Assert.That(presenter.DisplayedExperience, Is.EqualTo(260));

                // Reopening the HUD catches up without duplicating subscriptions or resetting its run baseline.
                presenter.gameObject.SetActive(false);
                var third = Target(created, request.RunId, 20);
                Assert.That(combat.TryApplySkillDamage(request.RunId, third, 2), Is.True);
                presenter.gameObject.SetActive(true);
                Assert.That(amount.text, Is.EqualTo("30 / 320"));
                Assert.That(combat.TryCompleteRun(RunEndReason.TimeExpired, RunSettlementPolicy.Eligible, out var result), Is.True);
                Assert.That(progression.GrantSettledRun(result), Is.True);
                hud.ShowResult(result);
                Assert.That(presenter.DisplayedExperience, Is.EqualTo(280));
                Assert.That(progression.ExperiencePoints, Is.EqualTo(280));
                Assert.That(progression.TraitCards.Points, Is.EqualTo(1));
                Assert.That(progression.GrantSettledRun(result), Is.True);
                hud.ShowResult(result);
                Assert.That(presenter.DisplayedExperience, Is.EqualTo(280));

                request = Start(combat);
                hud.ShowRunning(combat, null);
                var fourth = Target(created, request.RunId, 1000);
                Assert.That(combat.TryApplySkillDamage(request.RunId, fourth, 2), Is.True);
                Assert.That(level.text, Is.EqualTo("4"), "One kill may cross multiple levels.");
                Assert.That(amount.text, Is.EqualTo("320 / 460"));
                Assert.That(combat.TryCompleteRun(RunEndReason.UserExit, RunSettlementPolicy.Discard, out result), Is.True);
                hud.ShowResult(result);
                Assert.That(presenter.DisplayedExperience, Is.EqualTo(280), "Discarded runs return to the saved total.");
                hud.Reset();
                Assert.That(progression.GrantRunRewards(null, null, long.MaxValue), Is.True);
                Assert.That(level.text, Is.EqualTo("25"));
                Assert.That(amount.text, Is.EqualTo("MAX"));
                Assert.That(slider.value, Is.EqualTo(1));
                request = Start(combat);
                hud.ShowRunning(combat, null);
                var fifth = Target(created, request.RunId, 5);
                Assert.That(combat.TryApplySkillDamage(request.RunId, fifth, 2), Is.True);
                Assert.That(presenter.DisplayedExperience, Is.EqualTo(long.MaxValue));
                Assert.That(amount.text, Is.EqualTo("MAX"));
            }
            finally
            {
                foreach (var go in created) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                for (int i = 0; i < keys.Length; i++)
                    if (previous[i] == null) PlayerPrefs.DeleteKey(keys[i]); else PlayerPrefs.SetString(keys[i], previous[i]);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }

        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

        private static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static RunRequest Start(CombatRunController combat)
        {
            var request = new RunRequest("experience-test-" + Guid.NewGuid(), 60);
            Assert.That(combat.StartRun(request, new CombatSnapshot(1, 1, 1, 1, 0, 1, true, 1)), Is.True);
            return request;
        }

        private static MonsterCombatTarget Target(List<GameObject> created, RunId runId, long xp)
        {
            var go = new GameObject("ExperienceTarget", typeof(BoxCollider2D));
            created.Add(go);
            var target = go.AddComponent<MonsterCombatTarget>();
            target.Initialize(new SpawnSnapshot("experience-monster", "test-prefab", 2, 1, 1, 1, 0,
                string.Empty, 0, 0, experienceReward: xp), runId);
            return target;
        }
    }
}
