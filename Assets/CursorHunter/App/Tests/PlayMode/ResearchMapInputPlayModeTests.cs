using System.Collections;
using System.Linq;
using CursorHunter.Progression.ResearchPrototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.App.Tests
{
    public sealed class ResearchMapInputPlayModeTests
    {
        [UnityTest]
        public IEnumerator RealMouseDragWheelAndClickUseTheUiEventSystem()
        {
#if UNITY_EDITOR
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Progression/Scenes/ResearchPrototype.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return null;
                yield return null;
                var map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchTestMap>(true)).Single();
                Canvas.ForceUpdateCanvases();
                Vector2 position = RectTransformUtility.WorldToScreenPoint(null, map.nodes[0].transform.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
                InputSystem.Update();
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 });
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position + new Vector2(70, 35), buttons = 1 });
                InputSystem.Update();
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position + new Vector2(120, 65), buttons = 1 });
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position + new Vector2(120, 65) });
                InputSystem.Update();
                yield return null;
                yield return null;
                Assert.That(map.nodes[0].rank, Is.Zero, "Dragging a node must not buy it.");
                Assert.That(map.navigation.board.localPosition.magnitude, Is.GreaterThan(10f));
                float initialZoom = map.navigation.Zoom;
                InputSystem.QueueStateEvent(mouse, new MouseState
                    { position = position + new Vector2(120, 65), scroll = new Vector2(0, 120) });
                yield return null;
                yield return null;
                Assert.That(map.navigation.Zoom, Is.GreaterThan(initialZoom));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
                InputSystem.Update();
                yield return null;
                Canvas.ForceUpdateCanvases();
                Vector2 nodePosition = RectTransformUtility.WorldToScreenPoint(null, map.nodes[0].transform.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = nodePosition });
                InputSystem.Update();
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = nodePosition, buttons = 1 });
                InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = nodePosition });
                InputSystem.Update();
                yield return null;
                yield return null;
                Assert.That(map.nodes[0].rank, Is.EqualTo(1), "An ordinary click must buy the node.");
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            finally
            {
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
#else
            yield return null;
#endif
        }
    }
}
