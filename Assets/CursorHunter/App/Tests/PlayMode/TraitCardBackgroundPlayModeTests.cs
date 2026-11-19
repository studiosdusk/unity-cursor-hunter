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
    public sealed class TraitCardBackgroundPlayModeTests
    {
        [UnityTest]
        public IEnumerator TraitMenuShowsAuthoredCardCanvasAndPreservesNavigation()
        {
#if UNITY_EDITOR
            const string key = "cursor_hunter.progression.v1";
            string backupKey = key + ".before-v6";
            bool existed = PlayerPrefs.HasKey(key), backupExisted = PlayerPrefs.HasKey(backupKey);
            string previous = PlayerPrefs.GetString(key), backup = PlayerPrefs.GetString(backupKey);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            try
            {
                yield return null;
                yield return null;
                var canvas = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                var app = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AppUiRootController>(true)).Single();
                var progression = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TraitScreenController>(true)).Single();
                var runtime = progression.transform.Find("TraitScreen_Runtime");
                var research = canvas.transform.Find("ResearchPrototype_Canvas");
                var artifact = canvas.transform.Find("Artifact_Canvas");
                var trait = canvas.transform.Find("TraitCard_Canvas");
                Assert.That(trait, Is.Not.Null);
                Assert.That(trait.gameObject.activeSelf, Is.False);
                Assert.That(trait.Find("TraitListLayout"), Is.Not.Null);
                Assert.That(trait.Find("IconLayout"), Is.Not.Null);
                Assert.That(trait.GetComponentsInChildren<TraitCardSlot>(true).Length, Is.EqualTo(3));
                var backdrop = trait.Find("ProgressionUiBackdrop");
                Assert.That(backdrop, Is.Not.Null);
                Assert.That(backdrop.Find("BackgroundArtwork").GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(backdrop.Find("BackgroundArtwork").GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(backdrop.Find("DesaturationOverlay").GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(backdrop.Find("FloatingSparkles"), Is.Not.Null);
                Assert.That(trait.GetComponentsInChildren<Button>(true).Select(button => button.name),
                    Is.EqualTo(new[] { "TraitCard_OpenButton" }));
                var rect = (RectTransform)trait;
                Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));

                var nav = canvas.transform.Find("TopMenu/Menus/Button_MainMenu");
                var researchButton = nav.Find("Button_Research").GetComponent<Button>();
                var relicButton = nav.Find("Button_Artifact").GetComponent<Button>();
                var traitButton = nav.Find("Button_TraitCard").GetComponent<Button>();
                var fieldButton = nav.Find("Button_Shop (3)").GetComponent<Button>();
                Canvas.ForceUpdateCanvases();
                var events = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Single();
                var fieldPointer = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(null, fieldButton.transform.position) };
                var baselineHits = new List<RaycastResult>();
                events.RaycastAll(fieldPointer, baselineHits);
                var baselineFieldHandler = baselineHits.Count == 0 ? null :
                    ExecuteEvents.GetEventHandler<IPointerClickHandler>(baselineHits[0].gameObject);
                traitButton.onClick.Invoke();
                yield return null;
                Assert.That(trait.gameObject.activeInHierarchy, Is.True);
                Assert.That(research.gameObject.activeSelf, Is.False);
                Assert.That(artifact.gameObject.activeSelf, Is.False);
                Assert.That(runtime.gameObject.activeSelf, Is.False);
                Assert.That(traitButton.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));

                Canvas.ForceUpdateCanvases();
                foreach (Button button in new[] { researchButton, relicButton, traitButton, fieldButton })
                {
                    var pointer = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position) };
                    var hits = new List<RaycastResult>();
                    events.RaycastAll(pointer, hits);
                    Assert.That(hits.Count, Is.GreaterThan(0), button.name);
                    string details = string.Join(", ", hits.Select(hit => hit.gameObject.name));
                    if (button == fieldButton && baselineFieldHandler != fieldButton.gameObject)
                        Assert.That(hits.Any(hit => hit.gameObject.transform.IsChildOf(trait)), Is.False,
                            "The new background must not block an already-obstructed field button: " + details);
                    else
                        Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),
                            Is.EqualTo(button.gameObject), button.name + " hits: " + details);
                }

                relicButton.onClick.Invoke();
                yield return null;
                Assert.That(trait.gameObject.activeSelf, Is.False);
                Assert.That(artifact.gameObject.activeInHierarchy, Is.True);
                researchButton.onClick.Invoke();
                yield return null;
                Assert.That(research.gameObject.activeInHierarchy, Is.True);
                Assert.That(trait.gameObject.activeSelf, Is.False);
                fieldButton.onClick.Invoke();
                yield return null;
                Assert.That(runtime.gameObject.activeInHierarchy, Is.True);
                Assert.That(trait.gameObject.activeSelf, Is.False);
                traitButton.onClick.Invoke();
                yield return null;
                app.ShowSettings();
                yield return null;
                Assert.That(trait.gameObject.activeSelf, Is.False);
                app.ShowTrait();
                yield return null;
                Assert.That(trait.gameObject.activeInHierarchy, Is.True);
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
