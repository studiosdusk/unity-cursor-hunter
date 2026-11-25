using System;
using System.Collections;
using System.Linq;
using System.Reflection;
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
    public sealed class TraitCardPersistencePlayModeTests
    {
        [UnityTest]
        public IEnumerator LegacyProfileMigratesOnceAndAuthoredButtonRestoresPaidDraftAfterReload()
        {
#if UNITY_EDITOR
            const string key = "cursor_hunter.progression.v1";
            string[] keys = { key, key + ".before-v6" };
            var originals = keys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
            Scene scene = default;
            try
            {
                foreach (string k in keys) PlayerPrefs.DeleteKey(k);
                PlayerPrefs.SetString(key, "{\"version\":11,\"garnetBalance\":25," +
                    "\"experiencePoints\":" + ProgressionLevelCurve.TotalExperienceForLevel(11) +
                    ",\"availableTraitCards\":6,\"ownedTraitCardIds\":[\"talent.might\"]}");
                scene = LoadMain(); yield return null; yield return null;
                var ui = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                ui.GetComponent<ProgressionTopMenu>().ShowTalents(); yield return null;
                var progression = Progression(scene);
                Assert.That(progression.TraitCards.Points, Is.EqualTo(10));
                Assert.That(TraitProgressionStore.TryLoad(out var migrated), Is.True);
                Assert.That(migrated.hasNewTraitCardProgress, Is.True);
                Assert.That(migrated.ownedTraitCardIds, Is.EqualTo(new[] { "talent.might" }));
                Assert.That(migrated.availableTraitCards, Is.EqualTo(6));

                var canvas = ui.GetComponentInChildren<TraitCardCanvas>();
                var button = canvas.transform.Find("TraitCard_OpenButton").GetComponent<Button>();
                var counter = canvas.transform.Find("TraitPointView").GetComponentInChildren<TMP_Text>();
                Assert.That(counter.text, Is.EqualTo("10"));
                button.onClick.Invoke(); button.onClick.Invoke();
                Assert.That(counter.text, Is.EqualTo("9"));
                Assert.That(button.interactable, Is.False);
                string[] candidates = progression.TraitCards.CandidateIds.ToArray();
                var unload = SceneManager.UnloadSceneAsync(scene);
                while (!unload.isDone) yield return null;

                scene = LoadMain(); yield return null; yield return null;
                var secondUi = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                secondUi.GetComponent<ProgressionTopMenu>().ShowTalents(); yield return null;
                progression = Progression(scene);
                Assert.That(progression.TraitCards.Points, Is.EqualTo(9));
                Assert.That(progression.TraitCards.Level, Is.Zero);
                Assert.That(progression.TraitCards.Revealed, Is.True);
                Assert.That(progression.TraitCards.CandidateIds, Is.EqualTo(candidates));

                var request = new RunRequest("trait-persistence-" + Guid.NewGuid(), 20);
                var result = new RunResult(request, RunEndReason.TimeExpired, RunSettlementPolicy.Eligible,
                    20, 1, 0, 0, null, null, 1000);
                Assert.That(progression.GrantSettledRun(result), Is.True);
                long experience = progression.ExperiencePoints;
                long points = progression.TraitCards.Points;
                typeof(TraitScreenController).GetMethod("LoadSavedProgress", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(progression, null);
                Assert.That(progression.GrantSettledRun(result), Is.True);
                Assert.That(progression.ExperiencePoints, Is.EqualTo(experience));
                Assert.That(progression.TraitCards.Points, Is.EqualTo(points));
                TraitProgressionStore.TryLoad(out var afterRun);
                Assert.That(afterRun.availableTraitCards, Is.EqualTo(6), "Archived points must remain unchanged.");
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                for (int i = 0; i < keys.Length; i++)
                    if (originals[i] == null) PlayerPrefs.DeleteKey(keys[i]); else PlayerPrefs.SetString(keys[i], originals[i]);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }

#if UNITY_EDITOR
        private static Scene LoadMain() => UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
        private static TraitScreenController Progression(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<TraitScreenController>(true)).Single();
#endif
    }
}
