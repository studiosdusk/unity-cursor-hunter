using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CursorHunter.App;
using CursorHunter.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CursorHunter.App.Tests
{
    public sealed class WorldCanvasPlayModeTests
    {
        [UnityTest]
        public IEnumerator WorldButtonOpensSharedBackdropAndPreservesNavigation()
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
                var menu = canvas.GetComponent<ProgressionTopMenu>();
                var nav = canvas.transform.Find("TopMenu/Menus/Button_MainMenu");
                var worldButton = nav.Find("Button_World").GetComponent<Button>();
                var researchButton = nav.Find("Button_Research").GetComponent<Button>();
                var relicButton = nav.Find("Button_Artifact").GetComponent<Button>();
                var talentButton = nav.Find("Button_TraitCard").GetComponent<Button>();
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
                Assert.That(world.childCount, Is.EqualTo(1));
                var backdrop = world.Find("ProgressionUiBackdrop");
                Assert.That(backdrop, Is.Not.Null);
                Assert.That(backdrop.Find("BackgroundArtwork").GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(backdrop.Find("BackgroundArtwork").GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(backdrop.Find("DesaturationOverlay").GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(backdrop.Find("FloatingSparkles"), Is.Not.Null);

                Canvas.ForceUpdateCanvases();
                var events = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Single();
                var pointer = new PointerEventData(events)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, worldButton.transform.position),
                    button = PointerEventData.InputButton.Left
                };
                var hits = new List<RaycastResult>();
                events.RaycastAll(pointer, hits);
                Assert.That(hits.Count, Is.GreaterThan(0));
                Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(worldButton.gameObject));
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
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
                foreach (var button in new[] { worldButton, researchButton, relicButton, talentButton,
                    bottom.Find("Button_Boss").GetComponent<Button>(), bottom.Find("Button_Battle").GetComponent<Button>() })
                {
                    pointer.position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                    hits.Clear();
                    events.RaycastAll(pointer, hits);
                    Assert.That(hits.Count, Is.GreaterThan(0), button.name);
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),
                        Is.EqualTo(button.gameObject), button.name);
                }

                app.ShowSettings();
                yield return null;
                Assert.That(world.gameObject.activeSelf, Is.False);
                app.ShowTrait();
                yield return null;
                Assert.That(world.gameObject.activeInHierarchy, Is.True);
                Assert.That(worldButton.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));

                relicButton.onClick.Invoke();
                yield return null;
                Assert.That(world.gameObject.activeSelf, Is.False);
                Assert.That(artifact.gameObject.activeInHierarchy, Is.True);
                worldButton.onClick.Invoke();
                yield return null;
                talentButton.onClick.Invoke();
                yield return null;
                Assert.That(world.gameObject.activeSelf, Is.False);
                Assert.That(trait.gameObject.activeInHierarchy, Is.True);
                worldButton.onClick.Invoke();
                yield return null;
                menu.ShowFields();
                yield return null;
                Assert.That(world.gameObject.activeSelf, Is.False);
                Assert.That(runtime.gameObject.activeInHierarchy, Is.True);
                Assert.That(runtime.GetComponentsInChildren<Transform>(false).Any(t => t.name == "FieldCard_1"), Is.True);
                worldButton.onClick.Invoke();
                yield return null;
                researchButton.onClick.Invoke();
                yield return null;
                Assert.That(world.gameObject.activeSelf, Is.False);
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
