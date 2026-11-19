using System.Collections;
using System.Linq;
using System.Reflection;
using CursorHunter.Contracts;
using CursorHunter.Progression;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CursorHunter.App.Tests
{
    public sealed class TraitCardResetPlayModeTests
    {
        private const string Key = "cursor_hunter.progression.v1";
        private readonly string[] keys = { Key, Key + ".before-v6" };
        private string[] previous;
        private Scene scene;
        private HuntManager hunt;
        private TraitScreenController progression;
        private TraitCardCanvas canvas;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            foreach (string key in keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetString(Key, "{\"version\":11,\"garnetBalance\":25," +
                "\"experiencePoints\":" + ProgressionLevelCurve.TotalExperienceForLevel(11) +
                ",\"availableTraitCards\":6,\"ownedTraitCardIds\":[\"talent.might\"]," +
                "\"highestBossStageCleared\":2,\"settledBossRunIds\":[\"previous-run\"]}");
            yield return LoadMain();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (hunt != null) hunt.ResetPrototypeRun();
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            for (int i = 0; i < keys.Length; i++)
                if (previous[i] == null) PlayerPrefs.DeleteKey(keys[i]); else PlayerPrefs.SetString(keys[i], previous[i]);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator ButtonCallbackCancelsBothAnimationsAndPersistsLevelOneWithoutResettingOtherProgress()
        {
            Assert.That(progression.TraitCards.Points, Is.EqualTo(10));
            var slots = canvas.GetComponentsInChildren<TraitCardSlot>(true);
            canvas.RevealCards();
            Assert.That(canvas.IsAnimating, Is.True);
            Assert.That(TraitProgressionStore.TryLoad(out var before), Is.True);
            hunt.ResetTraitCardProgress();
            AssertFreshProgress();
            Assert.That(TraitProgressionStore.TryLoad(out var after), Is.True);
            before.experiencePoints = after.experiencePoints = 0;
            before.newTraitCards = after.newTraitCards = null;
            Assert.That(JsonUtility.ToJson(after), Is.EqualTo(JsonUtility.ToJson(before)),
                "Wallet, boss progress, legacy cards, and settlement receipts must survive the reset.");

            Assert.That(progression.GrantRunRewards(null, null, 250), Is.True);
            Assert.That(progression.TraitCards.Points, Is.EqualTo(1), "Level two awards one fresh point.");
            canvas.RevealCards();
            yield return new WaitForSecondsRealtime(.8f);
            var selected = slots.First(slot => slot.IsSelectable);
            selected.OnPointerClick(new PointerEventData(Find<EventSystem>()) { button = PointerEventData.InputButton.Left });
            Assert.That(progression.TraitCards.Level, Is.EqualTo(1));
            Assert.That(canvas.IsAnimating, Is.True);
            Assert.That(canvas.transform.Find("TraitIconFlight").gameObject.activeSelf, Is.True);
            hunt.ResetTraitCardProgress();
            AssertFreshProgress();
            yield return new WaitForSecondsRealtime(.8f);
            AssertFreshProgress();
            hunt.ResetTraitCardProgress();
            AssertFreshProgress();

            var oldResult = new RunResult(new RunRequest("previous-run", 20), RunEndReason.TimeExpired,
                RunSettlementPolicy.Eligible, 20, 1, 0, 0, null, null, 1000);
            Assert.That(progression.GrantSettledRun(oldResult), Is.True);
            AssertFreshProgress();
            string[] draft = progression.TraitCards.CandidateIds.ToArray();
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return LoadMain();
            AssertFreshProgress();
            Assert.That(progression.TraitCards.CandidateIds, Is.EqualTo(draft));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SaveFailureKeepsUiProgressAndExperienceUntilSuccessfulRetry()
        {
            var presenter = Find<PlayerExperiencePresenter>();
            presenter.gameObject.SetActive(true);
            long experience = progression.ExperiencePoints;
            Assert.That(presenter.DisplayedExperience, Is.EqualTo(experience));
            canvas.RevealCards();
            Assert.That(canvas.IsAnimating, Is.True);
            string cards = JsonUtility.ToJson(progression.TraitCards.Capture());
            string saved = PlayerPrefs.GetString(Key);
            PlayerPrefs.SetString(Key, "{\"version\":999}");
            LogAssert.Expect(LogType.Warning, "Trait progression changed in memory but could not be saved.");
            LogAssert.Expect(LogType.Warning, "특성 카드와 유저 경험치 초기화를 저장하지 못했습니다. 다시 시도해 주세요.");
            hunt.ResetTraitCardProgress();
            Assert.That(canvas.IsAnimating, Is.True);
            Assert.That(progression.ExperiencePoints, Is.EqualTo(experience));
            Assert.That(JsonUtility.ToJson(progression.TraitCards.Capture()), Is.EqualTo(cards));
            Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo("{\"version\":999}"));

            PlayerPrefs.SetString(Key, saved);
            hunt.ResetTraitCardProgress();
            Assert.That(presenter.DisplayedExperience, Is.Zero);
            Assert.That(presenter.DisplayedLevel, Is.EqualTo(1));
            var slider = (Slider)typeof(PlayerExperiencePresenter).GetField("experienceSlider",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter);
            Assert.That(slider.value, Is.Zero);
            AssertFreshProgress();
            yield return null;
            Assert.That(progression.ExperiencePoints, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        private void AssertFreshProgress()
        {
            Assert.That(progression.ExperiencePoints, Is.Zero);
            Assert.That(ProgressionLevelCurve.GetLevel(progression.ExperiencePoints), Is.EqualTo(1));
            Assert.That(progression.TraitCards.Points, Is.Zero);
            Assert.That(progression.TraitCards.Level, Is.Zero);
            Assert.That(progression.TraitCards.Revealed, Is.False);
            Assert.That(progression.TraitCards.RevealCost, Is.EqualTo(1));
            Assert.That(progression.TraitCards.OwnedIds, Is.Empty);
            Assert.That(progression.TraitCards.CandidateIds.Count, Is.EqualTo(3));
            Assert.That(canvas.IsAnimating, Is.False);
            Assert.That(canvas.GetComponentsInChildren<TraitCardSlot>(true).All(slot => slot.IsLocked && !slot.IsSelectable), Is.True);
            Assert.That(canvas.transform.Find("TraitIconFlight").gameObject.activeSelf, Is.False);
            Assert.That(canvas.transform.Find("IconLayout").Cast<Transform>().All(t => !t.gameObject.activeSelf), Is.True);
            Assert.That(canvas.transform.Find("TraitPointView").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("0"));
            Assert.That(canvas.transform.Find("TraitLevel_Text").GetComponent<TMP_Text>().text, Is.EqualTo("특성 레벨 : 0"));
            var button = canvas.transform.Find("TraitCard_OpenButton").GetComponent<Button>();
            Assert.That(button.interactable, Is.False);
            var costView = button.transform.Find("RevealCostView");
            Assert.That(costView.gameObject.activeSelf, Is.True);
            Assert.That(costView.Find("RevealCostRow/RevealCostValue").GetComponent<TMP_Text>().text, Is.EqualTo("1"));
            Assert.That((Color32)costView.Find("RevealCostHeading").GetComponent<TMP_Text>().color,
                Is.EqualTo(new Color32(255, 120, 100, 255)));
            Assert.That(TraitProgressionStore.TryLoad(out var saved), Is.True);
            Assert.That(saved.hasNewTraitCardProgress, Is.True);
            Assert.That(saved.experiencePoints, Is.Zero);
            Assert.That(JsonUtility.ToJson(saved.newTraitCards), Is.EqualTo(JsonUtility.ToJson(progression.TraitCards.Capture())));
        }

        private IEnumerator LoadMain()
        {
#if UNITY_EDITOR
            scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            yield return null;
            hunt = Find<HuntManager>();
            progression = Find<TraitScreenController>();
            canvas = Find<TraitCardCanvas>();
            Find<ProgressionTopMenu>().ShowTalents();
            yield return null;
        }

        private T Find<T>() where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
    }
}
