using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CursorHunter.App;
using TMPro;
using CursorHunter.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CursorHunter.App.Tests
{
    public sealed class WorldCanvasPlayModeTests
    {
        [UnityTest]
        public IEnumerator TouchSwipesCancelPurchasesAndInfoTapsToggleTheTooltip()
        {
#if UNITY_EDITOR
            const string key = "cursor_hunter.progression.v1";
            string previous = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            string backup = PlayerPrefs.HasKey(key + ".before-v6") ? PlayerPrefs.GetString(key + ".before-v6") : null;
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var touch = InputSystem.AddDevice<Touchscreen>();
            Scene scene = default;
            try
            {
                PlayerPrefs.DeleteKey(key);
                Assert.That(TraitProgressionStore.Save(0, null, null, null, null,
                    newTraitCards: new TraitCardProgressData { points = 1 }), Is.True);
                scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null; yield return null;
                var canvas = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                canvas.GetComponent<ProgressionTopMenu>().ShowWorld();
                yield return null; yield return null;
                var screen = canvas.GetComponentInChildren<WorldStageScreen>();
                var cards = screen.GetComponentsInChildren<WorldStageCard>();
                var tooltip = screen.transform.Find("StageTooltip").gameObject;
                var scroll = screen.GetComponentInChildren<WorldStageScroll>();
                var state = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<TraitScreenController>(true)).Single();
                Vector2 At(Transform t) => RectTransformUtility.WorldToScreenPoint(null, t.position);
                IEnumerator Touch(Vector2 point, UnityEngine.InputSystem.TouchPhase phase)
                {
                    InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, position = point, phase = phase });
                    InputSystem.Update();
                    yield return null; yield return null;
                }
                IEnumerator Tap(Vector2 p)
                {
                    yield return Touch(p, UnityEngine.InputSystem.TouchPhase.Began);
                    yield return Touch(p, UnityEngine.InputSystem.TouchPhase.Ended);
                }
                var info = At(cards[0].transform.Find("Info"));
                yield return Tap(info);
                Assert.That(tooltip.activeSelf, Is.True);
                yield return Tap(info);
                Assert.That(tooltip.activeSelf, Is.False);
                var desert = cards[1];
                var lockedInfo = desert.transform.Find("Info").GetComponent<Button>();
                Assert.That(lockedInfo.image.sprite, Is.Not.SameAs(cards[0].transform.Find("Info").GetComponent<Button>().image.sprite));
                yield return Tap(At(lockedInfo.transform));
                Assert.That(tooltip.activeSelf, Is.False);
                var buy = desert.transform.Find("Unlock");
                Assert.That(buy.gameObject.activeInHierarchy, Is.True);
                Assert.That(state.TraitCards.Points, Is.EqualTo(1));
                var start = At(buy);
                yield return Touch(start, UnityEngine.InputSystem.TouchPhase.Began);
                yield return Touch(start + Vector2.left * 80, UnityEngine.InputSystem.TouchPhase.Moved);
                yield return Touch(start + Vector2.left * 145, UnityEngine.InputSystem.TouchPhase.Moved);
                yield return Touch(start + Vector2.left * 145, UnityEngine.InputSystem.TouchPhase.Ended);
                Assert.That(scroll.horizontalNormalizedPosition, Is.GreaterThan(0));
                Assert.That(tooltip.activeSelf || buy.gameObject.activeSelf, Is.False);
                Assert.That(state.TraitCards.Points, Is.EqualTo(1));
                scroll.StopMovement(); scroll.horizontalNormalizedPosition = 0;
                yield return null; yield return null;
                yield return Tap(At(desert.transform.Find("Map")));
                yield return Tap(At(buy));
                Assert.That(state.SelectedWorldStage, Is.EqualTo(desert.Stage));
                Assert.That(lockedInfo.image.sprite, Is.SameAs(cards[0].transform.Find("Info").GetComponent<Button>().image.sprite));
                Assert.That(tooltip.activeSelf, Is.False);
                yield return Tap(At(lockedInfo.transform));
                Assert.That(tooltip.activeSelf, Is.True);
                Assert.That(state.TraitCards.Points, Is.Zero);
            }
            finally
            {
                InputSystem.RemoveDevice(touch);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                if (previous == null) PlayerPrefs.DeleteKey(key); else PlayerPrefs.SetString(key, previous);
                if (backup == null) PlayerPrefs.DeleteKey(key + ".before-v6"); else PlayerPrefs.SetString(key + ".before-v6", backup);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator MouseHoverScalesMapsAndNodesAndRestoresInfoAfterScrolling()
        {
#if UNITY_EDITOR
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>();
            Scene scene = default;
            try
            {
                scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null; yield return null;
                var canvas = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                var menu = canvas.GetComponent<ProgressionTopMenu>();
                menu.ShowWorld();
                yield return null; yield return null;
                var screen = canvas.GetComponentInChildren<WorldStageScreen>();
                var scroll = screen.GetComponentInChildren<WorldStageScroll>();
                scroll.horizontalNormalizedPosition = 0;
                yield return null;
                var card = screen.GetComponentsInChildren<WorldStageCard>()[0].transform;
                var map = card.Find("Map");
                var node = card.Find("SelectNode");
                var info = card.Find("Info");
                var tooltip = screen.transform.Find("StageTooltip");
                var restRotation = tooltip.localRotation;
                var nodeScale = node.localScale;
                var mapScale = map.localScale;
                void Move(Transform target) => InputSystem.QueueStateEvent(mouse, new MouseState
                    { position = RectTransformUtility.WorldToScreenPoint(null, target.position) });

                Assert.That(card.Find("CardFace"), Is.Null);
                Move(map);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
                Assert.That(map.localScale.x, Is.EqualTo(mapScale.x * 1.08f).Within(.001f));
                Move(info);
                bool swayed = false;
                float deadline = Time.realtimeSinceStartup + .5f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    swayed |= Quaternion.Angle(restRotation, tooltip.localRotation) > .1f;
                }
                Assert.That(tooltip.gameObject.activeSelf, Is.True);
                Assert.That(swayed, Is.True);
                Assert.That(Quaternion.Angle(restRotation, tooltip.localRotation), Is.LessThan(.05f));
                Assert.That(map.localScale, Is.EqualTo(mapScale));
                Assert.That(node.localScale, Is.EqualTo(nodeScale));
                // A small wheel tick moves the list while the cursor remains inside the same Info button.
                var mousePosition = mouse.position.ReadValue();
                var beforeWheel = scroll.content.anchoredPosition.x;
                InputSystem.QueueStateEvent(mouse, new MouseState
                    { position = mousePosition, scroll = new Vector2(0, .025f) });
                yield return null; yield return null;
                Assert.That(scroll.content.anchoredPosition.x, Is.LessThan(beforeWheel));
                yield return new WaitForSecondsRealtime(.22f);
                Assert.That(tooltip.gameObject.activeSelf, Is.True, "Wheel scrolling must not consume the only hover-enter event.");
                Move(node);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
                Assert.That(node.localScale.x, Is.EqualTo(nodeScale.x * 1.08f).Within(.001f));
                Move(map);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(node.localScale, Is.EqualTo(nodeScale));
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
                // Enter Info during a coasting drag, then keep the mouse perfectly still.
                scroll.velocity = Vector2.left * 56f;
                Move(info);
                yield return null;
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
                yield return new WaitForSecondsRealtime(.7f);
                Assert.That(scroll.velocity, Is.EqualTo(Vector2.zero));
                Assert.That(tooltip.gameObject.activeSelf, Is.True, "Info must reopen when inertia settles under the cursor.");
                // A fling into the beginning can leave velocity even when the content no longer moves.
                scroll.horizontalNormalizedPosition = 0;
                yield return new WaitForSecondsRealtime(.16f);
                Move(map);
                yield return new WaitForSecondsRealtime(.2f);
                scroll.velocity = Vector2.right * 200f;
                Move(info);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(scroll.velocity, Is.EqualTo(Vector2.zero));
                Assert.That(tooltip.gameObject.activeSelf, Is.True);
                Move(node);
                yield return new WaitForSecondsRealtime(.2f);
                menu.ShowResearch();
                Assert.That(node.localScale, Is.EqualTo(nodeScale));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
#else
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator WorldButtonOpensSharedBackdropAndPreservesNavigation()
        {
#if UNITY_EDITOR
            const string key = "cursor_hunter.progression.v1";
            string backupKey = key + ".before-v6";
            bool existed = PlayerPrefs.HasKey(key), backupExisted = PlayerPrefs.HasKey(backupKey);
            string previous = PlayerPrefs.GetString(key), backup = PlayerPrefs.GetString(backupKey);
            PlayerPrefs.DeleteKey(key);
            Assert.That(TraitProgressionStore.Save(1500, new long[] { 1500, 0, 0, 0, 0, 0 },
                new string[0], new Dictionary<string, int>(), new Dictionary<string, string>()), Is.True);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            try
            {
                yield return null;
                yield return null;
                var canvas = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                var menu = canvas.GetComponent<ProgressionTopMenu>();
                var nav = canvas.transform.Find("TopMenu/Menus/Button_MainMenu");
                var worldButton = nav.Find("Button_World").GetComponent<Button>();
                var researchButton = nav.Find("Button_Research").GetComponent<Button>();
                var relicButton = nav.Find("Button_Artifact").GetComponent<Button>();
                var world = canvas.transform.Find("World_Canvas");
                var research = canvas.transform.Find("ResearchPrototype_Canvas");
                var artifact = canvas.transform.Find("Artifact_Canvas");
                var trait = canvas.transform.Find("TraitCard_Canvas");
                var app = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AppUiRootController>(true)).Single();
                var progression = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TraitScreenController>(true)).Single();
                var runtime = progression.transform.Find("TraitScreen_Runtime");
                Assert.That(world, Is.Not.Null);
                Assert.That(world.gameObject.activeSelf, Is.False);
                var rect = (RectTransform)world;
                Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(rect.sizeDelta, Is.EqualTo(Vector2.zero));
                Assert.That(world.GetComponent<Canvas>(), Is.Null);
                Assert.That(world.GetComponent<GraphicRaycaster>(), Is.Null);
                Assert.That(world.GetComponentInChildren<WorldStageScreen>(true), Is.Not.Null);
                var backdrop = world.Find("ProgressionUiBackdrop");
                Assert.That(backdrop, Is.Not.Null);
                Assert.That(backdrop.Find("BackgroundArtwork").GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(backdrop.Find("BackgroundArtwork").GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(backdrop.Find("DesaturationOverlay").GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(backdrop.Find("FloatingSparkles"), Is.Not.Null);

                Canvas.ForceUpdateCanvases();
                var events = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Single();
                var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>();
                GameObject Hit(Button button)
                {
                    Canvas.ForceUpdateCanvases();
                    pointer.position = RectTransformUtility.WorldToScreenPoint(null,
                        button.transform.TransformPoint(((RectTransform)button.transform).rect.center));
                    hits.Clear();
                    events.RaycastAll(pointer, hits);
                    Assert.That(hits, Is.Not.Empty, button.name);
                    var target = hits[0].gameObject;
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(target), Is.EqualTo(button.gameObject), button.name);
                    return target;
                }
                void Click(Button button) => ExecuteEvents.ExecuteHierarchy(Hit(button), pointer, ExecuteEvents.pointerClickHandler);
                Click(worldButton);
                yield return null;
                Assert.That(world.gameObject.activeInHierarchy, Is.True);
                Assert.That(research.gameObject.activeSelf, Is.False);
                Assert.That(artifact.gameObject.activeSelf, Is.False);
                Assert.That(trait.gameObject.activeSelf, Is.False);
                Assert.That(runtime.gameObject.activeSelf, Is.False);
                Assert.That(worldButton.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));
                Assert.That(researchButton.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_n"));
                Assert.That(canvas.transform.Find("TopMenu").gameObject.activeInHierarchy, Is.True);
                Assert.That(canvas.transform.Find("ButtomMenu").gameObject.activeInHierarchy, Is.True);
                Canvas.ForceUpdateCanvases();
                var bottom = canvas.transform.Find("ButtomMenu/Menus/Button_MainMenu");
                var screen = world.Find("WorldStageScreen");
                var worldBalance = screen.Find("TraitPointView");
                var traitBalance = trait.Find("TraitPointView");
                Assert.That(worldBalance.Find("Icon").GetComponent<Image>().sprite, Is.SameAs(traitBalance.Find("Icon").GetComponent<Image>().sprite));
                var amount = worldBalance.GetComponentInChildren<TMP_Text>();
                Assert.That(amount.font, Is.SameAs(traitBalance.GetComponentInChildren<TMP_Text>(true).font));
                Assert.That(screen.Find("StageTooltip").GetComponent<Image>().sprite,
                    Is.SameAs(artifact.Find("Tooltip_Dark_Prefab/Popup").GetComponent<Image>().sprite));
                var talentButton = nav.Find("Button_TraitCard").GetComponent<Button>();
                var buttons = new[] { researchButton, relicButton, talentButton };
                var pages = new[] { research, artifact, trait };
                foreach (string name in new[] { "Back", "Home", "Help" })
                    Assert.That(screen.Find(name).gameObject.activeInHierarchy, Is.False, name);
                for (int i = 0; i < buttons.Length; i++)
                {
                    var info = screen.GetComponentsInChildren<WorldStageCard>()[0].transform.Find("Info");
                    ExecuteEvents.Execute(info.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                    Assert.That(screen.Find("StageTooltip").gameObject.activeSelf, Is.True);
                    foreach (string name in new[] { "Button_Boss", "Button_Battle" }) Hit(bottom.Find(name).GetComponent<Button>());
                    Click(buttons[i]);
                    Assert.That(pages[i].gameObject.activeInHierarchy, Is.True);
                    Assert.That(world.gameObject.activeSelf, Is.False);
                    Assert.That(screen.Find("StageTooltip").gameObject.activeSelf, Is.False);
                    Assert.That(buttons[i].image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));
                    Click(worldButton);
                    Assert.That(world.gameObject.activeInHierarchy, Is.True);
                    Assert.That(pages[i].gameObject.activeSelf, Is.False);
                    Assert.That(worldButton.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));
                }

                app.ShowSettings();
                yield return null;
                Assert.That(world.gameObject.activeSelf, Is.False);
                app.ShowTrait();
                yield return null;
                Assert.That(world.gameObject.activeInHierarchy, Is.True);
                Assert.That(worldButton.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));

                var cards = screen.GetComponentsInChildren<WorldStageCard>(true);
                Assert.That(cards.Length, Is.EqualTo(6));
                var forest = cards[0];
                var desert = cards[1];
                var winter = cards[2];
                Assert.That(forest.transform.Find("SelectNode/Check").gameObject.activeSelf, Is.True);
                Assert.That(cards.Last().transform.Find("Connector").gameObject.activeSelf, Is.False);
                Assert.That(winter.transform.Find("StageName").GetComponent<TMP_Text>().text, Is.EqualTo("???"));
                var tooltip = screen.Find("StageTooltip");
                forest.OnPointerEnter(pointer);
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
                ExecuteEvents.Execute(forest.transform.Find("Info").gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                Assert.That(tooltip.gameObject.activeSelf, Is.True);
                Assert.That(tooltip.Find("Details/Stats").GetComponent<TMP_Text>().text, Does.Contain("1.00"));
                desert.OnPointerEnter(pointer);
                Assert.That(desert.transform.Find("Unlock").gameObject.activeInHierarchy, Is.True);
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
                desert.OnPointerExit(pointer);
                Assert.That(desert.transform.Find("Unlock").gameObject.activeSelf, Is.False);
                desert.transform.Find("Map").GetComponent<Button>().onClick.Invoke();
                Assert.That(progression.TraitCards.Points, Is.Zero);
                Assert.That(progression.SelectedWorldStage, Is.EqualTo(forest.Stage));
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
                Button buy = desert.transform.Find("Unlock").GetComponent<Button>();
                Assert.That(buy.gameObject.activeInHierarchy, Is.True);
                Assert.That(buy.interactable, Is.False);
                Assert.That(buy.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(desert.Stage.pointCost.ToString()));
                Assert.That(progression.TryIncreaseUserLevelForTesting(), Is.True);
                Assert.That(amount.text, Is.EqualTo("1"));
                Assert.That(buy.interactable, Is.True);
                Assert.That(progression.TryChooseStage(winter.Stage, true), Is.False);
                winter.transform.Find("Map").GetComponent<Button>().onClick.Invoke();
                var lockedPrice = winter.transform.Find("Unlock").GetComponent<Button>();
                Assert.That(lockedPrice.gameObject.activeInHierarchy, Is.True);
                Assert.That(lockedPrice.interactable, Is.False);
                Assert.That(lockedPrice.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(winter.Stage.pointCost.ToString()));
                Assert.That(winter.transform.Find("Requirement").gameObject.activeSelf, Is.False);
                desert.transform.Find("Map").GetComponent<Button>().onClick.Invoke();

                var scroll = screen.GetComponentInChildren<WorldStageScroll>();
                pointer.eligibleForClick = true;
                scroll.OnBeginDrag(pointer);
                Assert.That(pointer.eligibleForClick, Is.False);
                Assert.That(tooltip.gameObject.activeSelf || buy.gameObject.activeSelf, Is.False);
                buy.onClick.Invoke();
                Assert.That(progression.TraitCards.Points, Is.EqualTo(1));
                scroll.OnEndDrag(pointer);
                desert.transform.Find("SelectNode").GetComponent<Button>().onClick.Invoke();
                string goodSave = PlayerPrefs.GetString(key);
                PlayerPrefs.SetString(key, "{\"version\":999}");
                buy.onClick.Invoke();
                Assert.That(progression.TraitCards.Points, Is.EqualTo(1));
                Assert.That(progression.IsStageUnlocked(desert.Stage), Is.False);
                Assert.That(progression.SelectedWorldStage, Is.EqualTo(forest.Stage));
                PlayerPrefs.SetString(key, goodSave);
                desert.transform.Find("Map").GetComponent<Button>().onClick.Invoke();
                buy.onClick.Invoke();
                buy.onClick.Invoke();
                Assert.That(amount.text, Is.EqualTo("0"));
                Assert.That(progression.TraitCards.Points, Is.Zero);
                Assert.That(progression.SelectedWorldStage, Is.EqualTo(desert.Stage));
                Assert.That(app.CurrentScreen, Is.EqualTo(UiScreenId.Trait));
                desert.transform.Find("Map").GetComponent<Button>().onClick.Invoke();
                Assert.That(progression.SelectedWorldStage, Is.EqualTo(desert.Stage));
                Assert.That(TraitProgressionStore.TryLoad(out var saved), Is.True);
                Assert.That(saved.newTraitCards.purchasedStageIds, Is.EqualTo(new[] { desert.Stage.stableId }));
                Assert.That(saved.newTraitCards.selectedStageId, Is.EqualTo(desert.Stage.stableId));
                scroll.horizontalNormalizedPosition = 1;
                Canvas.ForceUpdateCanvases();
                Assert.That(scroll.horizontalNormalizedPosition, Is.EqualTo(1).Within(.01));
                menu.LeaveWorld();
                relicButton.onClick.Invoke();
                worldButton.onClick.Invoke();
                screen.Find("Back").GetComponent<Button>().onClick.Invoke();
                Assert.That(artifact.gameObject.activeInHierarchy, Is.True);
                menu.ShowFields();
                Assert.That(world.gameObject.activeInHierarchy, Is.True);
                Assert.That(progression.TryResetTraitCardProgress(), Is.True);
                Assert.That(progression.SelectedWorldStage, Is.EqualTo(forest.Stage));
                Assert.That(progression.IsStageUnlocked(desert.Stage), Is.False);
                screen.Find("Home").GetComponent<Button>().onClick.Invoke();
                yield return null;
                Assert.That(research.gameObject.activeInHierarchy, Is.True);
                Assert.That(runtime.gameObject.activeSelf, Is.False);
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
