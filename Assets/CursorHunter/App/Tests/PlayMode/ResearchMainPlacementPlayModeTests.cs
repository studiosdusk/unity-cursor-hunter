using System.Collections;
using System.Linq;
using CursorHunter.App;
using CursorHunter.Progression.ResearchPrototype;
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
    public sealed class ResearchMainPlacementPlayModeTests
    {
        [UnityTest]
        public IEnumerator ScaledTooltipStaysVisibleAndClearOfNodeNearViewportEdges()
        {
#if UNITY_EDITOR
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            try
            {
                yield return null;
                yield return null;
                var canvas = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                var map = canvas.transform.Find("ResearchPrototype_Canvas").GetComponent<ResearchTestMap>();
                map.gameObject.SetActive(true);
                yield return null;
                var node = map.nodes[0];
                var viewport = (RectTransform)map.transform;
                var board = map.navigation.board;
                Assert.That(map.tooltipPanel.localScale.x, Is.GreaterThan(1f), "The authored tooltip scale is preserved.");
                Rect Bounds(RectTransform rect)
                {
                    var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                    Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                    Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                    foreach (var corner in corners)
                    {
                        Vector2 point = viewport.InverseTransformPoint(corner);
                        min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                    }
                    return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                }
                var positions = new[] { new Vector2(0f, 0f), new Vector2(.40f, .32f),
                    new Vector2(-.40f, .32f), new Vector2(.40f, -.32f), new Vector2(-.40f, -.32f) };
                foreach (float zoom in new[] { 1f, 1.8f })
                {
                    board.localScale = new Vector3(zoom, zoom, 1f);
                    foreach (var fraction in positions)
                    {
                        Vector2 target = new Vector2(viewport.rect.width * fraction.x, viewport.rect.height * fraction.y);
                        Vector2 current = viewport.InverseTransformPoint(node.transform.position);
                        board.anchoredPosition += target - current;
                        Canvas.ForceUpdateCanvases();
                        map.HideTooltip(node);
                        map.Select(node);
                        yield return new WaitForSecondsRealtime(.08f);
                        var panel = Bounds(map.tooltipPanel);
                        var nodeBounds = Bounds((RectTransform)node.transform);
                        Assert.That(panel.xMin, Is.GreaterThanOrEqualTo(viewport.rect.xMin - .5f));
                        Assert.That(panel.xMax, Is.LessThanOrEqualTo(viewport.rect.xMax + .5f));
                        Assert.That(panel.yMin, Is.GreaterThanOrEqualTo(viewport.rect.yMin - .5f));
                        Assert.That(panel.yMax, Is.LessThanOrEqualTo(viewport.rect.yMax + .5f));
                        Assert.That(panel.Overlaps(nodeBounds), Is.False, "Tooltip covers its node at " + target + " / " + zoom);
                    }
                }
            }
            finally { if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene); }
#else
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator PrototypeIsEditableUnderMainCanvasAndUsesExistingInput()
        {
#if UNITY_EDITOR
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return null;
                yield return null;
                var canvas = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                var prototype = canvas.transform.Find("ResearchPrototype_Canvas");
                Assert.That(prototype, Is.Not.Null);
                var artwork = prototype.Find("TraitBackdropArtwork").GetComponent<Image>();
                Assert.That(artwork.sprite.name, Is.EqualTo("Background_00"));
                Assert.That(artwork.raycastTarget, Is.False);
                Assert.That(artwork.transform.GetSiblingIndex(), Is.GreaterThan(prototype.Find("Background").GetSiblingIndex()));
                Assert.That(artwork.transform.GetSiblingIndex(), Is.LessThan(prototype.Find("ResearchBoard").GetSiblingIndex()));
                Assert.That(artwork.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(artwork.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(prototype.GetComponent<Canvas>(), Is.Null);
                Assert.That(prototype.GetComponent<GraphicRaycaster>(), Is.Null);
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Count(), Is.EqualTo(1));
                var app = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AppUiRootController>(true)).Single();
                Assert.That(app.CurrentScreen, Is.EqualTo(UiScreenId.Trait));
                Assert.That(app.IsVisible(UiScreenId.Trait), Is.True);
                var map = prototype.GetComponent<ResearchTestMap>();
                Assert.That(map, Is.Not.Null);
                Assert.That(map.nodes.Length, Is.EqualTo(12));
                Assert.That(map.nodes.All(node => node != null && node.map == map && node.definition != null), Is.True);
                Assert.That(map.nodes.All(node => node.icon != null), Is.True);
                Assert.That(map.nodes.All(node => node.border != null && !node.border.raycastTarget && node.upgradeIndicator != null &&
                    !node.upgradeIndicator.GetComponent<Image>().raycastTarget), Is.True);
                Assert.That(map.navigation.viewport, Is.EqualTo(prototype));
                prototype.gameObject.SetActive(true);
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(map.nodes.Count(node => node.gameObject.activeSelf), Is.EqualTo(1));
                Vector2 position = RectTransformUtility.WorldToScreenPoint(null, map.nodes[0].transform.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
                InputSystem.Update();
                yield return null;
                yield return null;
                Assert.That(map.tooltipRoot.activeSelf, Is.True);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 });
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
                InputSystem.Update();
                yield return null;
                yield return null;
                Assert.That(map.nodes[0].rank, Is.EqualTo(1));
                Assert.That(map.nodes.Count(node => node.gameObject.activeSelf), Is.EqualTo(7));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 });
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position + new Vector2(80, 35), buttons = 1 });
                InputSystem.Update();
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position + new Vector2(120, 60), buttons = 1 });
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position + new Vector2(120, 60) });
                InputSystem.Update();
                yield return null;
                yield return null;
                Assert.That(map.nodes[0].rank, Is.EqualTo(1), "Dragging must not purchase another rank.");
                Assert.That(map.navigation.board.localPosition.magnitude, Is.GreaterThan(10f));
                float zoomBefore = map.navigation.Zoom;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position + new Vector2(120, 60), scroll = new Vector2(0, 120) });
                yield return null;
                yield return null;
                Assert.That(map.navigation.Zoom, Is.GreaterThan(zoomBefore));
                prototype.gameObject.SetActive(false);
                Assert.That(app.IsVisible(UiScreenId.Trait), Is.True);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            finally
            {
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
#else
            yield return null;
#endif
        }
    }
}
