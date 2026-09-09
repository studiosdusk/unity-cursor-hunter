using UnityEngine;
using UnityEngine.InputSystem;

namespace CursorHunter.App
{
    /// <summary>
    /// Controls the world-space cursor used by Main.unity's test panel.
    ///
    /// The cursor follows the mouse on the gameplay XY plane. Its visual
    /// SpriteRenderer and attack Collider2D therefore share the same world
    /// position and scale.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainCursorController : MonoBehaviour
    {
        private const string CursorObjectName = "cursor_image";
        private const string LegacyCursorObjectName = "cursor_Image";

        [SerializeField] private Transform cursorImage;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float cursorPlaneZ;

        private Transform _cursorTransform;
        private GameObject _cursorObject;
        private bool _customCursorActive;

        public bool IsCustomCursorActive => _customCursorActive;

        public Vector3 CursorWorldPosition =>
            _cursorTransform != null ? _cursorTransform.position : Vector3.zero;

        public Transform CursorTransform => _cursorTransform;

        private void Awake()
        {
            _cursorTransform = cursorImage != null ? cursorImage : FindCursorTransform();
            _cursorObject = _cursorTransform != null ? _cursorTransform.gameObject : null;

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (_cursorTransform == null)
            {
                Debug.LogWarning(
                    "MainCursorController could not find the child cursor_image Transform.",
                    this);
            }

            // The scene starts with the native cursor. Button_CursorOn calls
            // ShowCursorImage() to enter world-cursor mode.
            HideCursorImage();
        }

        private void Update()
        {
            if (!_customCursorActive)
            {
                return;
            }

            RefreshCursorPosition();
        }

        /// <summary>
        /// Activates the world cursor and hides the native OS cursor.
        /// Assign this parameterless method to Button_CursorOn.
        /// </summary>
        public void ShowCursorImage()
        {
            _customCursorActive = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;

            if (_cursorObject != null)
            {
                _cursorObject.SetActive(true);
            }

            RefreshCursorPosition();
        }

        /// <summary>
        /// Deactivates the world cursor and restores the native OS cursor.
        /// </summary>
        public void HideCursorImage()
        {
            _customCursorActive = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_cursorObject != null)
            {
                _cursorObject.SetActive(false);
            }
        }

        /// <summary>
        /// Updates the cursor immediately from the current mouse position.
        /// CursorAttackController calls this before an attack so script
        /// execution order cannot cause a one-frame-old attack position.
        /// </summary>
        public bool RefreshCursorPosition()
        {
            if (!_customCursorActive || _cursorTransform == null || worldCamera == null)
            {
                return false;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            if (!TryGetWorldPosition(mouse.position.ReadValue(), out Vector3 worldPosition))
            {
                return false;
            }

            _cursorTransform.position = worldPosition;
            return true;
        }

        private bool TryGetWorldPosition(Vector2 screenPosition, out Vector3 worldPosition)
        {
            Ray ray = worldCamera.ScreenPointToRay(screenPosition);
            Plane cursorPlane = new Plane(
                Vector3.forward,
                new Vector3(0f, 0f, cursorPlaneZ));

            if (!cursorPlane.Raycast(ray, out float distance))
            {
                worldPosition = default;
                return false;
            }

            worldPosition = ray.GetPoint(distance);
            worldPosition.z = cursorPlaneZ;
            return true;
        }

        private Transform FindCursorTransform()
        {
            Transform found = transform.Find(CursorObjectName);
            return found != null ? found : transform.Find(LegacyCursorObjectName);
        }

        private void OnDestroy()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDisable()
        {
            _customCursorActive = false;

            if (_cursorObject != null)
            {
                _cursorObject.SetActive(false);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
