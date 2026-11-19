using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Progression;
using CursorHunter.Progression.ResearchPrototype;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CursorHunter.App.Tests
{
    public sealed class CheatMenuPlayModeTests
    {
        private const string Key = "cursor_hunter.progression.v1";
        private readonly string[] keys = { Key, Key + ".before-v6" };
        private string[] previous;
        private Scene scene;
        private CheatMenuController menu;
        private TraitScreenController progression;
        private ResearchTestMap map;
        private GameObject root;
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorBehavior;
        private float timeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous = keys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
            foreach (var key in keys) PlayerPrefs.DeleteKey(key);
            Assert.That(TraitProgressionStore.Save(25, new long[] { 25, 2, 3, 4, 5, 6 },
                new string[0], null, null, ownedRelicIds: new[] { "relic.red_potion", "relic.blue_potion" },
                unseenRelicIds: new[] { "relic.red_potion" }, highestBossStageCleared: 2,
                settledBossRunIds: new[] { "old-run" }, experiencePoints: ProgressionLevelCurve.TotalExperienceForLevel(5),
                ownedTraitCardIds: new[] { "talent.might" },
                researchRanks: new Dictionary<string, int> { ["research-test.cursor.attack"] = 1, ["other-map.node"] = 3 },
                newTraitCards: new TraitCardProgressData { points = 4 }), Is.True);
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            timeScale = Time.timeScale;
            yield return LoadMain();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = timeScale;
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
            for (int i = 0; i < keys.Length; i++)
                if (previous[i] == null) PlayerPrefs.DeleteKey(keys[i]); else PlayerPrefs.SetString(keys[i], previous[i]);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator EqualsKeyOpensButtonsAndAllCheatsPersistTheirIntendedChanges()
        {
            Assert.That(root.activeSelf, Is.False);
            var authoredButtons = root.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("Cheat_")).ToArray();
            Assert.That(authoredButtons.Length, Is.GreaterThanOrEqualTo(6), "Cheat buttons exist in the scene before opening.");
            var requiredLabels = new Dictionary<string, string> {
                ["reset_trait_cards"] = "특성 카드 초기화", ["reset_research"] = "연구 진행 상황 초기화",
                ["reset_relics"] = "유물 진행 상황 초기화", ["grant_currencies"] = "모든 재화 999,999 지급",
                ["clear_currencies"] = "모든 재화 0으로", ["increase_user_level"] = "유저 레벨 1 올리기" };
            foreach (var entry in requiredLabels)
                Assert.That(authoredButtons.Single(b => b.name == "Cheat_" + entry.Key)
                    .GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo(entry.Value));
            int authoredObjectCount = root.GetComponentsInChildren<Transform>(true).Length;
            var authoredParents = authoredButtons.Select(button => button.transform.parent).ToArray();
            Assert.That(progression.PrepareTraitCards(), Is.True);
            Assert.That(progression.RevealTraitCards(), Is.True);
            Assert.That(progression.SelectTraitCard(progression.TraitCards.CandidateIds[0]), Is.True);
            Assert.That(progression.TraitCards.Level, Is.EqualTo(1));
            Assert.That(map.nodes.Single(n => n.placementId == "research-test.cursor.attack").rank, Is.EqualTo(1));
            Assert.That(progression.TryGetRelicView("relic.red_potion", out var relic), Is.True);
            Assert.That(relic.IsOwned, Is.True);

            Time.timeScale = 0;
            yield return PressEquals();
            Assert.That(root.activeSelf, Is.True);
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(authoredObjectCount),
                "Opening the cheat menu must not create UI objects.");
            var buttons = root.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Cheat_")).ToArray();
            Assert.That(buttons, Is.EqualTo(authoredButtons.Where(button => button.gameObject.activeSelf)));
            Assert.That(authoredButtons.Select(button => button.transform.parent), Is.EqualTo(authoredParents));
            Canvas.ForceUpdateCanvases();
            var eventSystem = Find<EventSystem>();
            foreach (Button button in buttons.Take(6))
            {
                var pointer = new PointerEventData(eventSystem) {
                    position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position),
                    button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>();
                eventSystem.RaycastAll(pointer, hits);
                Assert.That(hits, Is.Not.Empty, button.name);
                Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject), button.name);
            }

            Click("grant_currencies");
            for (int i = 0; i < 6; i++)
                Assert.That(progression.GetResearchGemstoneBalance(i), Is.EqualTo(new long[] { 25, 2, 3, 4, 5, 6 }[i] + 999999));
            Assert.That(progression.TraitCards.Points, Is.EqualTo(1000002));
            Click("grant_currencies");
            Assert.That(progression.GetResearchGemstoneBalance(0), Is.EqualTo(2000023));
            Assert.That(progression.TraitCards.Points, Is.EqualTo(2000001));

            Click("reset_research");
            Assert.That(map.nodes.All(n => n == null || n.rank == 0), Is.True);
            Assert.That(progression.GetResearchRank("research-test.cursor.attack"), Is.Zero);
            Assert.That(progression.GetResearchRank("other-map.node"), Is.EqualTo(3));
            Assert.That(progression.GetResearchGemstoneBalance(0), Is.EqualTo(2000023));
            Assert.That(progression.TryGetRelicView("relic.red_potion", out relic) && relic.IsOwned, Is.True);

            Find<ProgressionTopMenu>().ShowRelics();
            Click("reset_relics");
            Assert.That(progression.TryGetRelicView("relic.red_potion", out relic), Is.True);
            Assert.That(relic.IsOwned || relic.IsNew, Is.False);
            var relicCanvas = Find<ArtifactCollectionCanvas>();
            var relicCount = (TMP_Text)typeof(ArtifactCollectionCanvas).GetField("collectionCount",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(relicCanvas);
            Assert.That(relicCount.text, Does.Contain("00 /"));
            Assert.That(TraitProgressionStore.TryLoad(out var saved), Is.True);
            Assert.That(saved.ownedRelicIds, Is.Empty);
            Assert.That(saved.unseenRelicIds, Is.Empty);
            Assert.That(saved.highestBossStageCleared, Is.EqualTo(2));
            Assert.That(saved.settledBossRunIds, Does.Contain("old-run"));
            Assert.That(saved.ownedTraitCardIds, Does.Contain("talent.might"));

            Click("clear_currencies");
            Assert.That(Enumerable.Range(0, 6).Select(progression.GetResearchGemstoneBalance), Is.All.EqualTo(0));
            Assert.That(progression.TraitCards.Points, Is.Zero);
            Click("grant_currencies");
            Click("reset_trait_cards");
            Assert.That(progression.TraitCards.Level, Is.Zero);
            Assert.That(progression.TraitCards.Points, Is.Zero);
            Assert.That(progression.ExperiencePoints, Is.Zero);
            Assert.That(progression.TraitCards.CandidateIds.Count, Is.EqualTo(3));
            Assert.That(progression.GetResearchGemstoneBalance(0), Is.EqualTo(999999));
            Click("clear_currencies");
            Assert.That(Enumerable.Range(0, 6).Select(progression.GetResearchGemstoneBalance), Is.All.EqualTo(0));
            Assert.That(progression.TraitCards.Points, Is.Zero);

            yield return PressEquals();
            Assert.That(root.activeSelf, Is.False);
            yield return PressEquals();
            Assert.That(root.activeSelf, Is.True);
            Assert.That(root.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Cheat_")), Is.EqualTo(buttons.Length));
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return LoadMain();
            Assert.That(progression.ExperiencePoints, Is.Zero);
            Assert.That(progression.TraitCards.Points, Is.Zero);
            Assert.That(progression.GetResearchRank("other-map.node"), Is.EqualTo(3));
            Assert.That(Enumerable.Range(0, 6).Select(progression.GetResearchGemstoneBalance), Is.All.EqualTo(0));
            Assert.That(progression.TryGetRelicView("relic.red_potion", out relic) && !relic.IsOwned, Is.True);
        }

        [UnityTest]
        public IEnumerator FailedSavesRollBackCheatsAndShowFailureUntilRetry()
        {
            menu.Toggle();
            string saved = PlayerPrefs.GetString(Key);
            string beforeCards = JsonUtility.ToJson(progression.TraitCards.Capture());
            long experience = progression.ExperiencePoints;
            PlayerPrefs.SetString(Key, "{\"version\":999}");
            foreach (string id in new[] { "grant_currencies", "clear_currencies", "reset_research", "reset_relics", "reset_trait_cards", "increase_user_level" })
            {
                LogAssert.Expect(LogType.Warning, "Trait progression changed in memory but could not be saved.");
                if (id == "reset_trait_cards")
                    LogAssert.Expect(LogType.Warning, "특성 카드와 유저 경험치 초기화를 저장하지 못했습니다. 다시 시도해 주세요.");
                Click(id);
                Assert.That(root.transform.Find("CheatStatusFailure").gameObject.activeSelf, Is.True);
                Assert.That(progression.GetResearchGemstoneBalance(0), Is.EqualTo(25));
                Assert.That(progression.GetResearchRank("research-test.cursor.attack"), Is.EqualTo(1));
                Assert.That(progression.TryGetRelicView("relic.red_potion", out var relic) && relic.IsOwned && relic.IsNew, Is.True);
                Assert.That(progression.ExperiencePoints, Is.EqualTo(experience));
                Assert.That(JsonUtility.ToJson(progression.TraitCards.Capture()), Is.EqualTo(beforeCards));
            }
            PlayerPrefs.SetString(Key, saved);
            Click("grant_currencies");
            Assert.That(progression.GetResearchGemstoneBalance(0), Is.EqualTo(1000024));
            Assert.That(progression.TraitCards.Points, Is.EqualTo(1000003));
            Assert.That(root.transform.Find("CheatStatusSuccess").gameObject.activeSelf, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LevelCheatAdvancesOneLevelAwardsPointAndStopsAtTheMaximum()
        {
            Assert.That(progression.GrantRunRewards(null, null, 37), Is.True);
            var presenter = Find<PlayerExperiencePresenter>();
            presenter.gameObject.SetActive(true);
            long initialPoints = progression.TraitCards.Points;
            Assert.That(ProgressionLevelCurve.GetLevel(progression.ExperiencePoints), Is.EqualTo(5));
            menu.Toggle();
            Click("increase_user_level");
            Assert.That(progression.ExperiencePoints, Is.EqualTo(ProgressionLevelCurve.TotalExperienceForLevel(6)));
            Assert.That(progression.TraitCards.Points, Is.EqualTo(initialPoints + 1));
            Assert.That(presenter.DisplayedLevel, Is.EqualTo(6));
            var slider = (Slider)typeof(PlayerExperiencePresenter).GetField("experienceSlider",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter);
            Assert.That(slider.value, Is.Zero);
            Assert.That(root.transform.Find("CheatStatusSuccess").gameObject.activeSelf, Is.True);
            Click("increase_user_level");
            Assert.That(progression.ExperiencePoints, Is.EqualTo(ProgressionLevelCurve.TotalExperienceForLevel(7)));
            Assert.That(progression.TraitCards.Points, Is.EqualTo(initialPoints + 2));
            Assert.That(TraitProgressionStore.TryLoad(out var saved), Is.True);
            Assert.That(saved.experiencePoints, Is.EqualTo(progression.ExperiencePoints));
            Assert.That(saved.newTraitCards.points, Is.EqualTo(initialPoints + 2));

            yield return SceneManager.UnloadSceneAsync(scene);
            yield return LoadMain();
            Assert.That(ProgressionLevelCurve.GetLevel(progression.ExperiencePoints), Is.EqualTo(7));
            Assert.That(progression.TraitCards.Points, Is.EqualTo(initialPoints + 2));
            long maxXp = ProgressionLevelCurve.TotalExperienceForLevel(ProgressionLevelCurve.MaximumLevel);
            long nearMaximum = ProgressionLevelCurve.TotalExperienceForLevel(24) + 13;
            Assert.That(progression.GrantRunRewards(null, null, nearMaximum - progression.ExperiencePoints), Is.True);
            menu.Toggle();
            long beforeLastLevel = progression.TraitCards.Points;
            Click("increase_user_level");
            Assert.That(progression.ExperiencePoints, Is.EqualTo(maxXp));
            Assert.That(progression.TraitCards.Points, Is.EqualTo(beforeLastLevel + 1));
            string previousSave = PlayerPrefs.GetString(Key);
            long pointsAtMaximum = progression.TraitCards.Points;
            Click("increase_user_level");
            Assert.That(progression.ExperiencePoints, Is.EqualTo(maxXp));
            Assert.That(progression.TraitCards.Points, Is.EqualTo(pointsAtMaximum));
            Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo(previousSave));
            Assert.That(root.transform.Find("CheatStatusMaxLevel").gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator LevelCheatKeepsLiveRunExperienceAndSettlementFromBeingCountedTwice()
        {
            GameObject combatObject = new GameObject("LevelCheatTestCombat");
            GameObject targetObject = new GameObject("LevelCheatTestTarget", typeof(BoxCollider2D));
            try
            {
                var presenter = Find<PlayerExperiencePresenter>();
                var hud = Find<PrototypeRunHud>();
                presenter.gameObject.SetActive(true);
                var combat = combatObject.AddComponent<CombatRunController>();
                var request = new RunRequest("level-cheat-live-" + System.Guid.NewGuid(), 60);
                Assert.That(combat.StartRun(request, new CombatSnapshot(1, 1, 1, 1, 0, 1, true, 1)), Is.True);
                hud.ShowRunning(combat, null);
                var target = targetObject.AddComponent<MonsterCombatTarget>();
                target.Initialize(new SpawnSnapshot("level-cheat-monster", "test-prefab", 2, 1, 1, 1, 0,
                    string.Empty, 0, 0, experienceReward: 50), request.RunId);
                Assert.That(combat.TryApplySkillDamage(request.RunId, target, 2), Is.True);
                long startExperience = progression.ExperiencePoints;
                Assert.That(presenter.DisplayedExperience, Is.EqualTo(startExperience + 50));
                menu.Toggle();
                Click("increase_user_level");
                long leveledExperience = ProgressionLevelCurve.TotalExperienceForLevel(6);
                Assert.That(progression.ExperiencePoints, Is.EqualTo(leveledExperience));
                Assert.That(presenter.DisplayedExperience, Is.EqualTo(leveledExperience + 50));
                Assert.That(progression.TraitCards.Points, Is.EqualTo(5));
                Assert.That(combat.TryCompleteRun(RunEndReason.TimeExpired, RunSettlementPolicy.Eligible, out var result), Is.True);
                Assert.That(progression.GrantSettledRun(result), Is.True);
                hud.ShowResult(result);
                Assert.That(progression.ExperiencePoints, Is.EqualTo(leveledExperience + 50));
                Assert.That(presenter.DisplayedExperience, Is.EqualTo(leveledExperience + 50));
                Assert.That(progression.TraitCards.Points, Is.EqualTo(5));
                hud.Reset();
                yield return null;
            }
            finally { Object.Destroy(combatObject); Object.Destroy(targetObject); }
        }

        [UnityTest]
        public IEnumerator TableControlsActionsAndAmountsWithoutGeneratingOrReplacingSceneLabels()
        {
            var asset = Object.Instantiate(Resources.Load<CheatTable>("CheatTable"));
            try
            {
                var entries = (List<CheatRow>)typeof(CheatTable).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(asset);
                entries.Reverse();
                entries.Single(r => r.action == CheatAction.ClearAllCurrencies).enabled = false;
                entries.Single(r => r.action == CheatAction.AddAllCurrencies).amount = 123;
                entries.Add(new CheatRow { id = "unplaced", action = CheatAction.AddAllCurrencies, amount = 1, enabled = true });
                typeof(CheatMenuController).GetField("table", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu, asset);
                var authored = root.GetComponentsInChildren<Button>(true).Single(b => b.name == "Cheat_grant_currencies");
                var label = authored.GetComponentInChildren<TMP_Text>(true);
                label.text = "씬에서 직접 지정한 문구";
                var bindings = (List<CheatButtonBinding>)typeof(CheatMenuController)
                    .GetField("buttonBindings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
                var group = root.transform.Find("CheatViewport/Group");
                // Simulate extra authored scene buttons before the menu is opened.
                for (int i = 0; i < 21; i++)
                {
                    string id = "extra_" + i;
                    var extra = Object.Instantiate(authored, group, false);
                    extra.name = "Cheat_" + id;
                    entries.Add(new CheatRow { id = id, action = CheatAction.AddAllCurrencies, amount = 1, enabled = true });
                    bindings.Add(new CheatButtonBinding { id = id, button = extra });
                }
                int count = root.GetComponentsInChildren<Transform>(true).Length;
                LogAssert.Expect(LogType.Warning, "Cheat table row has no scene button: unplaced");
                menu.Toggle();
                var buttons = root.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Cheat_")).ToArray();
                Assert.That(buttons.Length, Is.EqualTo(entries.Count(row => row.enabled) - 1));
                Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                Assert.That(label.text, Is.EqualTo("씬에서 직접 지정한 문구"));
                Assert.That(buttons.Any(b => b.name == "Cheat_clear_currencies"), Is.False);
                Click("grant_currencies");
                Assert.That(progression.GetResearchGemstoneBalance(0), Is.EqualTo(148));
                Assert.That(progression.TraitCards.Points, Is.EqualTo(127));
                Assert.That(progression.TryChangeCurrenciesForTesting(long.MaxValue, false), Is.True);
                Assert.That(progression.TryChangeCurrenciesForTesting(1, false), Is.True);
                Assert.That(progression.TraitCards.Points, Is.EqualTo(long.MaxValue));
                Assert.That(Enumerable.Range(0, 6).Select(progression.GetResearchGemstoneBalance), Is.All.EqualTo(long.MaxValue));
                var scroll = root.GetComponentInChildren<ScrollRect>();
                Assert.That(scroll, Is.Not.Null);
                Canvas.ForceUpdateCanvases();
                yield return null;
                Assert.That(scroll.content.GetComponent<ContentSizeFitter>(), Is.Not.Null);
                Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                scroll.verticalNormalizedPosition = 0;
                yield return null;
                Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(.01f));
            }
            finally { Object.Destroy(asset); }
        }

        private void Click(string id)
        {
            var button = root.GetComponentsInChildren<Button>().Single(b => b.name == "Cheat_" + id);
            button.onClick.Invoke();
        }

        private IEnumerator PressEquals()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Equals));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private IEnumerator LoadMain()
        {
#if UNITY_EDITOR
            scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            yield return null;
            menu = Find<CheatMenuController>();
            progression = Find<TraitScreenController>();
            map = Find<ResearchTestMap>();
            root = Find<ProgressionTopMenu>().transform.Find("Cheat_Canvas").gameObject;
            yield return null;
        }

        private T Find<T>() where T : Component => scene.GetRootGameObjects()
            .SelectMany(obj => obj.GetComponentsInChildren<T>(true)).Single();
    }
}
