using UnityEngine;
using UnityEngine.InputSystem;

namespace CursorHunter.App
{
    /// <summary>
    /// Toggles the prototype test panel with the Space key. It lives on an
    /// always-active object such as UICanvas because TestPanel can be inactive.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TestPanelToggleController : MonoBehaviour
    {
        [SerializeField] private GameObject testPanel;

        private void Awake()
        {
            if (testPanel == null)
            {
                testPanel = FindSceneTestPanel();
            }
        }

        private void Update()
        {
            if (Keyboard.current != null &&
                Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                ToggleTestPanel();
            }
        }

        /// <summary>
        /// Toggles TestPanel visibility. This is also available to UI buttons.
        /// </summary>
        public void ToggleTestPanel()
        {
            if (testPanel != null)
            {
                testPanel.SetActive(!testPanel.activeSelf);
            }
        }

        public void ShowTestPanel()
        {
            if (testPanel != null)
            {
                testPanel.SetActive(true);
            }
        }

        public void HideTestPanel()
        {
            if (testPanel != null)
            {
                testPanel.SetActive(false);
            }
        }

        private GameObject FindSceneTestPanel()
        {
            Transform[] sceneTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (Transform sceneTransform in sceneTransforms)
            {
                if (sceneTransform == null ||
                    sceneTransform.name != "TestPanel" ||
                    !sceneTransform.gameObject.scene.IsValid())
                {
                    continue;
                }

                return sceneTransform.gameObject;
            }

            return null;
        }
    }
}
