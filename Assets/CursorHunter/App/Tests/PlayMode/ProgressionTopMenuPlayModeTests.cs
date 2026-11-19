using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CursorHunter.App;
using CursorHunter.Progression;
using CursorHunter.Progression.ResearchPrototype;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CursorHunter.App.Tests
{
    public sealed class ProgressionTopMenuPlayModeTests
    {
        [UnityTest]
        public IEnumerator MenuButtonsScaleOnMouseHoverAndResetWhenHidden()
        {
#if UNITY_EDITOR
            const string key = "cursor_hunter.progression.v1";
            string backupKey = key + ".before-v6";
            bool existed = PlayerPrefs.HasKey(key), backupExisted = PlayerPrefs.HasKey(backupKey);
            string previous = PlayerPrefs.GetString(key), backup = PlayerPrefs.GetString(backupKey);
            float originalTimeScale = Time.timeScale;
            var originalBackground = InputSystem.settings.backgroundBehavior;
            var originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            Mouse device = null;
            Scene scene = default;
            try
            {
                PlayerPrefs.DeleteKey(key);
                Assert.That(TraitProgressionStore.Save(1500, new long[] { 1500, 0, 0, 0, 0, 0 },
                    new string[0], new Dictionary<string, int>(), new Dictionary<string, string>()), Is.True);
                scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;
                yield return null;

                var canvas = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                var top = canvas.transform.Find("TopMenu");
                var bottom = canvas.transform.Find("ButtomMenu");
                Button[] topButtons = top.GetComponentsInChildren<Button>(true);
                Button[] bottomButtons = bottom.GetComponentsInChildren<Button>(true);
                Assert.That(topButtons, Is.Not.Empty);
                Assert.That(bottomButtons, Is.Not.Empty);
                Button[] allButtons = topButtons.Concat(bottomButtons).ToArray();
                foreach (Button button in allButtons)
                    Assert.That(button.GetComponent<MenuButtonHoverScale>(), Is.Not.Null, button.name);

                Button[] visibleButtons = allButtons.Where(button => button.gameObject.activeInHierarchy).ToArray();
                Vector3[] originalScales = visibleButtons.Select(button => button.transform.localScale).ToArray();
                var eventSystem = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Single();
                var mouse = new PointerEventData(eventSystem) { pointerId = -1 };
                Canvas.ForceUpdateCanvases();
                foreach (Button button in visibleButtons)
                {
                    mouse.position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                    var hits = new List<RaycastResult>();
                    eventSystem.RaycastAll(mouse, hits);
                    Assert.That(hits.Count, Is.GreaterThan(0), button.name);
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),
                        Is.EqualTo(button.gameObject), button.name);
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerEnterHandler>(hits[0].gameObject),
                        Is.EqualTo(button.gameObject), button.name);
                }

                Time.timeScale = 0f;
                foreach (Button button in visibleButtons)
                    ExecuteEvents.Execute(button.gameObject, mouse, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(0.25f);
                for (int i = 0; i < visibleButtons.Length; i++)
                    Assert.That(Vector3.Distance(visibleButtons[i].transform.localScale, originalScales[i] * 1.08f),
                        Is.LessThan(0.001f), visibleButtons[i].name);

                foreach (Button button in visibleButtons)
                    ExecuteEvents.Execute(button.gameObject, mouse, ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(0.25f);
                for (int i = 0; i < visibleButtons.Length; i++)
                    Assert.That(Vector3.Distance(visibleButtons[i].transform.localScale, originalScales[i]),
                        Is.LessThan(0.001f), visibleButtons[i].name);

                var touch = new PointerEventData(eventSystem) { pointerId = 1 };
                foreach (Button button in visibleButtons)
                    ExecuteEvents.Execute(button.gameObject, touch, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(0.18f);
                for (int i = 0; i < visibleButtons.Length; i++)
                    Assert.That(Vector3.Distance(visibleButtons[i].transform.localScale, originalScales[i]),
                        Is.LessThan(0.001f), visibleButtons[i].name);

                foreach (Button button in visibleButtons)
                    ExecuteEvents.Execute(button.gameObject, mouse, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(0.06f);
                var app = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AppUiRootController>(true)).Single();
                app.ShowSettings();
                for (int i = 0; i < visibleButtons.Length; i++)
                    Assert.That(Vector3.Distance(visibleButtons[i].transform.localScale, originalScales[i]),
                        Is.LessThan(0.001f), visibleButtons[i].name);
                app.ShowTrait();
                yield return null;
                for (int i = 0; i < visibleButtons.Length; i++)
                    Assert.That(Vector3.Distance(visibleButtons[i].transform.localScale, originalScales[i]),
                        Is.LessThan(0.001f), visibleButtons[i].name);

                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                device = InputSystem.AddDevice<Mouse>();
                yield return null;
                var world = topButtons.Single(button => button.name == "Button_World");
                var battle = bottomButtons.Single(button => button.name == "Button_Battle" && button.gameObject.activeInHierarchy);
                int worldIndex = System.Array.IndexOf(visibleButtons, world);
                int battleIndex = System.Array.IndexOf(visibleButtons, battle);
                InputSystem.QueueStateEvent(device, new MouseState
                    { position = RectTransformUtility.WorldToScreenPoint(null, world.transform.position) });
                yield return new WaitForSecondsRealtime(0.25f);
                Assert.That(Vector3.Distance(world.transform.localScale, originalScales[worldIndex] * 1.08f),
                    Is.LessThan(0.001f), "The input system must deliver the mouse hover to the world tab.");
                InputSystem.QueueStateEvent(device, new MouseState
                    { position = RectTransformUtility.WorldToScreenPoint(null, battle.transform.position) });
                yield return new WaitForSecondsRealtime(0.25f);
                Assert.That(Vector3.Distance(world.transform.localScale, originalScales[worldIndex]),
                    Is.LessThan(0.001f), "Moving away must restore the world tab.");
                Assert.That(Vector3.Distance(battle.transform.localScale, originalScales[battleIndex] * 1.08f),
                    Is.LessThan(0.001f), "The input system must deliver the mouse hover to the battle button.");
            }
            finally
            {
                if (device != null) InputSystem.RemoveDevice(device);
                InputSystem.settings.backgroundBehavior = originalBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
                Time.timeScale = originalTimeScale;
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                if (existed) PlayerPrefs.SetString(key, previous); else PlayerPrefs.DeleteKey(key);
                if (backupExisted) PlayerPrefs.SetString(backupKey, backup); else PlayerPrefs.DeleteKey(backupKey);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator BottomButtonsStartHuntAndBossRuns()
        {
#if UNITY_EDITOR
            const string key = "cursor_hunter.progression.v1";
            string backupKey = key + ".before-v6";
            bool existed = PlayerPrefs.HasKey(key), backupExisted = PlayerPrefs.HasKey(backupKey);
            string previous = PlayerPrefs.GetString(key), backup = PlayerPrefs.GetString(backupKey);
            Assert.That(TraitProgressionStore.Save(1500, new long[] { 1500, 0, 0, 0, 0, 0 },
                new string[0], new Dictionary<string, int>(), new Dictionary<string, string>(),
                highestBossStageCleared: 1, newTraitCards: new TraitCardProgressData { points = 1 }), Is.True);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            try
            {
                yield return null;
                yield return null;
                var canvas = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                var app = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AppUiRootController>(true)).Single();
                var hunt = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HuntManager>(true)).Single();
                var bottom = canvas.transform.Find("ButtomMenu");
                var progression = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TraitScreenController>(true)).Single();
                Assert.That(progression.CanStartPlayerCombat, Is.True);
                var information = JsonUtility.FromJson<CursorHunter.Contracts.GameInformation>(progression.CreateGameInformationJson());
                Assert.That(information.monsters.Single(monster => monster.id == "monster.01").enabled, Is.True);
                Assert.That(TraitProgressionStore.TryLoad(out var saved), Is.True);
                Assert.That(saved.purchasedNodeIds, Is.Empty, "The baseline must not write legacy purchases.");
                var huntButton = bottom.Find("Menus/Button_MainMenu/Button_Battle").GetComponent<Button>();
                huntButton.onClick.Invoke();
                yield return null;
                Assert.That(app.CurrentScreen, Is.EqualTo(UiScreenId.Combat), "Hunt button enters combat.");
                Assert.That(bottom.gameObject.activeSelf, Is.False);
                var spawner = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CursorHunter.Combat.MonsterSpawner>(true)).Single();
                float deadline = Time.realtimeSinceStartup + 4f;
                while (spawner.ActiveSpawnedCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(spawner.ActiveSpawnedCount, Is.GreaterThan(0), "A new profile spawns the basic monster.");
                hunt.ResetPrototypeRun();
                app.ShowTrait();
                yield return null;
                var bossButton = bottom.Find("Menus/Button_MainMenu/Button_Boss").GetComponent<Button>();
                var raidStage = bossButton.transform.Find("Text (1)").GetComponent<TMP_Text>();
                Assert.That(raidStage.text, Is.EqualTo("[2단계]"));
                bossButton.onClick.Invoke();
                yield return null;
                Assert.That(app.CurrentScreen, Is.EqualTo(UiScreenId.Combat), "Boss button enters combat.");
                Assert.That(hunt.IsBossCombatActive, Is.True);
                hunt.ResetPrototypeRun();
                app.ShowTrait();
                hunt.ResetBossProgress();
                yield return null;
                Assert.That(raidStage.text, Is.EqualTo("[1단계]"));
                var fieldButton = canvas.transform.Find("TopMenu/Menus/Button_MainMenu/Button_Shop (3)").GetComponent<Button>();
                fieldButton.onClick.Invoke();
                yield return null;
                var world = canvas.transform.Find("World_Canvas");
                var fieldCard = world.GetComponentsInChildren<WorldStageCard>()[0].transform.Find("Map").GetComponent<Button>();
                fieldCard.onClick.Invoke();
                yield return null;
                Assert.That(app.CurrentScreen, Is.EqualTo(UiScreenId.Trait));
                var worldName = huntButton.transform.Find("Text (1)").GetComponent<TMP_Text>();
                Assert.That(worldName.text, Is.EqualTo("[숲]"));
                Assert.That(progression.TryChooseStage(progression.WorldStages[1], true), Is.True);
                yield return null;
                Assert.That(worldName.text, Is.EqualTo("[사막]"));
                huntButton.onClick.Invoke();
                Assert.That(app.CurrentScreen, Is.EqualTo(UiScreenId.Combat));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                if (existed) PlayerPrefs.SetString(key, previous); else PlayerPrefs.DeleteKey(key);
                if (backupExisted) PlayerPrefs.SetString(backupKey, backup); else PlayerPrefs.DeleteKey(backupKey);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }
        [UnityTest]
        public IEnumerator MenusSwitchCollectionsAndFollowAppScreenWithoutLosingResearch()
        {
#if UNITY_EDITOR
            const string key = "cursor_hunter.progression.v1";
            string backupKey = key + ".before-v6";
            bool existed = PlayerPrefs.HasKey(key), backupExisted = PlayerPrefs.HasKey(backupKey);
            string previous = PlayerPrefs.GetString(key), backup = PlayerPrefs.GetString(backupKey);
            Assert.That(TraitProgressionStore.Save(1500, new long[] { 1500, 0, 0, 0, 0, 0 },
                new string[0], new Dictionary<string, int>(), new Dictionary<string, string>()), Is.True);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            try
            {
                yield return null;
                yield return null;
                var canvas = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                var top = canvas.transform.Find("TopMenu");
                var nav = top.Find("Menus/Button_MainMenu");
                var research = nav.Find("Button_Research").GetComponent<Button>();
                var relic = nav.Find("Button_Artifact").GetComponent<Button>();
                var talent = nav.Find("Button_TraitCard").GetComponent<Button>();
                var field = nav.Find("Button_Shop (3)").GetComponent<Button>();
                var bottom = canvas.transform.Find("ButtomMenu");
                var bottomNav = bottom.Find("Menus/Button_MainMenu");
                var boss = bottomNav.Find("Button_Boss").GetComponent<Button>();
                var huntButton = bottomNav.Find("Button_Battle").GetComponent<Button>();
                Assert.That(research.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text, Is.EqualTo("연구"));
                Assert.That(relic.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text, Is.EqualTo("유물"));
                Assert.That(talent.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text, Is.EqualTo("특성"));
                Assert.That(field.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text, Is.EqualTo("스테이지"));
                Assert.That(boss.transform.Find("Text").GetComponent<TMP_Text>().text, Is.EqualTo("레이드"));
                Assert.That(huntButton.transform.Find("Text").GetComponent<TMP_Text>().text, Is.EqualTo("전투 시작"));
                var map = canvas.transform.Find("ResearchPrototype_Canvas").GetComponent<ResearchTestMap>();
                var artifactCanvas = canvas.transform.Find("Artifact_Canvas");
                var traitCardCanvas = canvas.transform.Find("TraitCard_Canvas");
                var progression = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TraitScreenController>(true)).Single();
                var app = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AppUiRootController>(true)).Single();
                var runtime = progression.transform.Find("TraitScreen_Runtime");
                var backdrop = runtime.Find("ProgressionUiBackdrop");
                var artwork = backdrop.Find("BackgroundArtwork").GetComponent<Image>();
                var veil = backdrop.Find("DesaturationOverlay").GetComponent<Image>();
                var atmosphere = backdrop.Find("FloatingSparkles");
                var authoredColor = new Color(0.3f, 0.4f, 0.5f, 0.7f);
                artwork.color = authoredColor;
                var authoredPosition = new Vector2(13f, 21f);
                veil.rectTransform.anchoredPosition = authoredPosition;
                Assert.That(app.CurrentScreen, Is.EqualTo(UiScreenId.Trait));
                Assert.That(top.gameObject.activeInHierarchy, Is.True);
                Assert.That(map.gameObject.activeInHierarchy, Is.True);
                Assert.That(runtime.gameObject.activeSelf, Is.False);
                Assert.That(research.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));
                Assert.That(relic.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_n"));
                var titleFont = map.title.font;
                var costFont = map.costValue.font;
                var gemstoneFont = map.gemstoneValues[0].font;
                Assert.That(titleFont, Is.Not.Null);
                Assert.That(costFont, Is.Not.Null);
                Assert.That(gemstoneFont, Is.Not.Null);
                foreach (Button button in new[] { research, relic, talent })
                    Assert.That(button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).font, Is.Not.Null, button.name);
                Canvas.ForceUpdateCanvases();
                var eventSystem = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Single();
                foreach (Button button in new[] { research, relic, talent, boss, huntButton })
                {
                    var pointer = new PointerEventData(eventSystem) { position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position) };
                    var hits = new List<RaycastResult>();
                    eventSystem.RaycastAll(pointer, hits);
                    Assert.That(hits.Count, Is.GreaterThan(0), button.name);
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject), button.name);
                }
                map.Purchase(map.nodes[0]);
                Assert.That(map.nodes[0].rank, Is.EqualTo(1));
                long balance = progression.GetResearchGemstoneBalance(0);
                relic.onClick.Invoke();
                yield return null;
                Assert.That(map.gameObject.activeSelf, Is.False);
                Assert.That(runtime.gameObject.activeSelf, Is.False);
                Assert.That(artifactCanvas.gameObject.activeInHierarchy, Is.True);
                Assert.That(relic.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));
                Assert.That(research.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_n"));
                Assert.That(artifactCanvas.GetComponentsInChildren<Transform>(false).Any(t => t.name == "RelicSlot_01"), Is.True);
                Assert.That(runtime.Find("ResearchCategoryTabs").gameObject.activeInHierarchy, Is.False);
                talent.onClick.Invoke();
                yield return null;
                Assert.That(talent.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));
                Assert.That(relic.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_n"));
                Assert.That(artifactCanvas.gameObject.activeSelf, Is.False);
                Assert.That(traitCardCanvas.gameObject.activeInHierarchy, Is.True);
                Assert.That(runtime.gameObject.activeSelf, Is.False);
                field.onClick.Invoke();
                yield return null;
                Assert.That(canvas.transform.Find("World_Canvas").gameObject.activeInHierarchy, Is.True);
                Assert.That(runtime.gameObject.activeSelf, Is.False);
                Assert.That(backdrop.Find("BackgroundArtwork").GetComponent<Image>(), Is.SameAs(artwork));
                Assert.That(artwork.color, Is.EqualTo(authoredColor));
                Assert.That(backdrop.Find("DesaturationOverlay").GetComponent<Image>(), Is.SameAs(veil));
                Assert.That(veil.rectTransform.anchoredPosition, Is.EqualTo(authoredPosition));
                Assert.That(backdrop.Find("FloatingSparkles"), Is.SameAs(atmosphere));
                talent.onClick.Invoke();
                yield return null;
                app.ShowSettings();
                yield return null;
                Assert.That(top.gameObject.activeSelf, Is.False);
                Assert.That(bottom.gameObject.activeSelf, Is.False);
                Assert.That(map.gameObject.activeSelf, Is.False);
                app.ShowTrait();
                yield return null;
                Assert.That(top.gameObject.activeInHierarchy, Is.True);
                Assert.That(bottom.gameObject.activeInHierarchy, Is.True);
                Assert.That(talent.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));
                Assert.That(traitCardCanvas.gameObject.activeInHierarchy, Is.True);
                Assert.That(runtime.gameObject.activeSelf, Is.False);
                research.onClick.Invoke();
                yield return null;
                Assert.That(map.gameObject.activeInHierarchy, Is.True);
                Assert.That(runtime.gameObject.activeSelf, Is.False);
                Assert.That(research.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));
                Assert.That(map.title.font, Is.SameAs(titleFont));
                Assert.That(map.costValue.font, Is.SameAs(costFont));
                Assert.That(map.gemstoneValues[0].font, Is.SameAs(gemstoneFont));
                Assert.That(map.nodes[0].rank, Is.EqualTo(1));
                Assert.That(progression.GetResearchGemstoneBalance(0), Is.EqualTo(balance));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                if (existed) PlayerPrefs.SetString(key, previous); else PlayerPrefs.DeleteKey(key);
                if (backupExisted) PlayerPrefs.SetString(backupKey, backup); else PlayerPrefs.DeleteKey(backupKey);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }
    }
}
