using System.Collections;
using System.Linq;
using System.Reflection;
using CursorHunter.Combat;
using CursorHunter.Data;
using CursorHunter.Progression;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CursorHunter.App.Tests
{
    public sealed class ProfileLoadRecoveryPlayModeTests
    {
        private const string MainPath = "Assets/CursorHunter/App/Scenes/Main.unity";
        private const string SaveKey = "cursor_hunter.progression.v1";
        private const string BackupKey = SaveKey + ".before-v6";
        private const string ValidProfile = "{\"version\":13,\"garnetBalance\":717,\"gemstoneBalances\":[717,0,0,0,0,0]," +
            "\"highestBossStageCleared\":2,\"hasNewTraitCardProgress\":true,\"newTraitCards\":{\"points\":5," +
            "\"selectedStageId\":\"desert\",\"purchasedStageIds\":[\"desert\"]}}";

        private Scene scene;
        private string previousSave, previousBackup;
        private GameObject canvas;
        private TraitScreenController progression;

        [SetUp]
        public void StashProfile()
        {
            previousSave = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;
            previousBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
            PlayerPrefs.SetString(BackupKey, "migration backup fixture");
        }

        [UnityTearDown]
        public IEnumerator RestoreProfile()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                if (SceneManager.sceneCount == 1) SceneManager.CreateScene("Profile test teardown");
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            if (previousSave == null) PlayerPrefs.DeleteKey(SaveKey); else PlayerPrefs.SetString(SaveKey, previousSave);
            if (previousBackup == null) PlayerPrefs.DeleteKey(BackupKey); else PlayerPrefs.SetString(BackupKey, previousBackup);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator FailedFirstLaunchResetBlocksPlayAndCanBeRetried()
        {
            PlayerPrefs.SetString(SaveKey, ValidProfile);
            yield return LoadMain();
            string original = PlayerPrefs.GetString(SaveKey);
            typeof(TraitScreenController).GetField("_profileLoadStatus", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(progression, ProgressionLoadStatus.ResetFailed);
            var app = Find<AppUiRootController>();
            Assert.That(app.ShowScreen(UiScreenId.ProfileLoadError), Is.True);
            Assert.That(progression.IsProfileLoadBlocked, Is.True);
            var message = canvas.transform.Find("ProfileLoadError_Canvas/Message").GetComponent<TMP_Text>();
            Assert.That(message.text, Is.EqualTo(LocalizationCatalog.Get("ui.profile.reset_failed", string.Empty)));
            app.ShowTrait();
            Find<HuntManager>().BeginPrototypeRun();
            Assert.That(app.CurrentScreen, Is.EqualTo(UiScreenId.ProfileLoadError));
            Assert.That(Find<CombatRunController>().IsRunActive, Is.False);
            Assert.That(PlayerPrefs.GetString(SaveKey), Is.EqualTo(original));
            Assert.That(PlayerPrefs.GetString(BackupKey), Is.EqualTo("migration backup fixture"));

            yield return RestartWith(RetryButton());
            Assert.That(progression.IsProfileLoadBlocked, Is.False);
            Assert.That(progression.SelectedWorldStage.stableId, Is.EqualTo("desert"));
            Assert.That(progression.GetResearchGemstoneBalance(0), Is.EqualTo(717));
        }

        [UnityTest]
        public IEnumerator FutureSaveBlocksGameplayAndRetryReadsTheRepairedProfile()
        {
            const string future = "{\"version\":999,\"garnetBalance\":123}";
            PlayerPrefs.SetString(SaveKey, future);
            yield return LoadMain();
            AssertBlocked(future, ProgressionLoadStatus.Unsupported);
            PlayerPrefs.SetString(SaveKey, ValidProfile);
            yield return RestartWith(RetryButton());
            Assert.That(progression.IsProfileLoadBlocked, Is.False);
            Assert.That(progression.ProfileLoadStatus, Is.EqualTo(ProgressionLoadStatus.Loaded));
            Assert.That(Find<AppUiRootController>().CurrentScreen, Is.EqualTo(UiScreenId.Trait));
            Assert.That(progression.GetResearchGemstoneBalance(0), Is.EqualTo(717));
            Assert.That(progression.TraitCards.Points, Is.EqualTo(5));
            Assert.That(progression.SelectedWorldStage.stableId, Is.EqualTo("desert"));
            Assert.That(Find<HuntManager>().NextBossStage, Is.EqualTo(3));
            Assert.That(PlayerPrefs.GetString(BackupKey), Is.EqualTo("migration backup fixture"));
        }

        [UnityTest]
        public IEnumerator InvalidDraftAndUnknownCardKeepTheSourceUntilExplicitFullReset()
        {
            const string corrupt = "{\"version\":13,\"hasNewTraitCardProgress\":true,\"newTraitCards\":{\"revealed\":true,\"candidateIds\":[]}}";
            const string unsupported = "{\"version\":13,\"hasNewTraitCardProgress\":true,\"newTraitCards\":{\"ownedIds\":[\"retired.unmapped.card\"]}}";
            PlayerPrefs.SetString(SaveKey, corrupt);
            yield return LoadMain();
            AssertBlocked(corrupt, ProgressionLoadStatus.Corrupt);
            PlayerPrefs.SetString(SaveKey, unsupported);
            yield return RestartWith(RetryButton());
            AssertBlocked(unsupported, ProgressionLoadStatus.Unsupported);

            Find<CheatMenuController>().Toggle();
            var reset = canvas.transform.Find("Cheat_Canvas/CheatViewport/Group/Cheat_reset_all_progress").GetComponent<Button>();
            yield return RestartWith(reset);
            Assert.That(progression.IsProfileLoadBlocked, Is.False);
            Assert.That(Find<AppUiRootController>().CurrentScreen, Is.EqualTo(UiScreenId.Trait));
            Assert.That(progression.PlayerLevel, Is.EqualTo(1));
            Assert.That(progression.SelectedWorldStage.stableId, Is.EqualTo("forest"));
            Assert.That(Find<HuntManager>().NextBossStage, Is.EqualTo(1));
            Assert.That(PlayerPrefs.HasKey(BackupKey), Is.False);
        }

        [UnityTest]
        public IEnumerator PreparingCardsInAnUnsavedPreviewPreservesThePlayerProfile()
        {
            PlayerPrefs.SetString(SaveKey, ValidProfile);
            yield return LoadMain();
            string original = PlayerPrefs.GetString(SaveKey);
            typeof(TraitScreenController).GetField("loadSavedProgress", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(progression, false);
            progression.TraitCards.Restore(new TraitCardProgressData());
            canvas.GetComponent<ProgressionTopMenu>().ShowTalents();
            yield return null;
            Assert.That(progression.TraitCards.CandidateIds.Count, Is.EqualTo(3));
            Assert.That(PlayerPrefs.GetString(SaveKey), Is.EqualTo(original));
            Assert.That(PlayerPrefs.GetString(BackupKey), Is.EqualTo("migration backup fixture"));
        }

        private void AssertBlocked(string source, ProgressionLoadStatus expected)
        {
            var app = Find<AppUiRootController>();
            Assert.That(progression.ProfileLoadStatus, Is.EqualTo(expected));
            Assert.That(progression.IsProfileLoadBlocked, Is.True);
            var panel = canvas.transform.Find("ProfileLoadError_Canvas");
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.Find("Message").GetComponent<TMP_Text>().text, Is.Not.Empty);
            Assert.That(RetryButton().IsInteractable(), Is.True);
            app.ShowTrait();
            var hunt = Find<HuntManager>();
            hunt.BeginPrototypeRun();
            hunt.BeginBossRun();
            Assert.That(app.CurrentScreen, Is.EqualTo(UiScreenId.ProfileLoadError));
            Assert.That(Find<CombatRunController>().IsRunActive, Is.False);
            Assert.That(progression.PrepareTraitCards(), Is.False);
            Assert.That(progression.TryChangeCurrenciesForTesting(1, false), Is.False);
            Assert.That(PlayerPrefs.GetString(SaveKey), Is.EqualTo(source));
            Assert.That(PlayerPrefs.GetString(BackupKey), Is.EqualTo("migration backup fixture"));
        }

        private Button RetryButton() => canvas.transform.Find("ProfileLoadError_Canvas/Retry").GetComponent<Button>();

        private IEnumerator LoadMain()
        {
            yield return SceneManager.LoadSceneAsync(MainPath, LoadSceneMode.Additive);
            yield return BindMain();
        }

        private IEnumerator RestartWith(Button button)
        {
            Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True);
            Scene before = scene;
            button.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (before.isLoaded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(before.isLoaded, Is.False);
            yield return BindMain();
        }

        private IEnumerator BindMain()
        {
            scene = SceneManager.GetSceneByPath(MainPath);
            Assert.That(scene.isLoaded, Is.True);
            yield return null;
            yield return null;
            canvas = scene.GetRootGameObjects().Single(go => go.name == "UICanvas");
            progression = Find<TraitScreenController>();
        }

        private T Find<T>() where T : Component => scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<T>(true)).Single();
    }
}
