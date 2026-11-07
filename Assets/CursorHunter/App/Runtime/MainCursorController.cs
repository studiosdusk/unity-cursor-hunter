using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CursorHunter.App
{
    /// <summary>
    /// Controls Main's world-space cursor anchor and its overlay above the HUD.
    ///
    /// The cursor follows the mouse on the gameplay XY plane. The skin visual
    /// is a child of the attack Collider2D transform and inherits its range
    /// scale; CursorSkinController fits each sprite without changing the hit area.
    /// The overlay follows that fitted visual while the collider remains in world space.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainCursorController : MonoBehaviour
    {
        private const string CursorObjectName = "cursor_image";
        private const string LegacyCursorObjectName = "cursor_Image";

        [SerializeField] private Transform cursorImage;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float cursorPlaneZ;
        [SerializeField] private Vector3 baseCursorScale;
        [SerializeField, Min(0.01f)] private float rangeMultiplier = 1f;

        private Transform _cursorTransform;
        private GameObject _cursorObject;
        private Vector3 _baseCursorScale;
        private bool _customCursorActive;
        private float _spatialScale = 1f;
        private SpriteRenderer _cursorVisual;
        private Canvas _cursorCanvas;
        private Image _cursorOverlay;

        public void SetSpatialScale(float value)
        {
            _spatialScale = Mathf.Max(0.01f, value);
            ApplyRangeMultiplier();
        }

        public void SetWindowCursorVisible(bool visible)
        {
            if (!_customCursorActive) return;
            Cursor.visible = !visible;
            if (_cursorObject != null && _cursorObject.activeSelf != visible)
                _cursorObject.SetActive(visible);
            RefreshCursorOverlay();
        }

        public bool IsCustomCursorActive => _customCursorActive;

        public Vector3 CursorWorldPosition =>
            _cursorTransform != null ? _cursorTransform.position : Vector3.zero;

        public Transform CursorTransform => _cursorTransform;

        public float RangeMultiplier => rangeMultiplier;

        private void Awake()
        {
            _cursorTransform = cursorImage != null ? cursorImage : FindCursorTransform();
            _cursorObject = _cursorTransform != null ? _cursorTransform.gameObject : null;

            if (_cursorTransform != null)
            {
                _baseCursorScale = baseCursorScale == Vector3.zero
                    ? _cursorTransform.localScale
                    : baseCursorScale;
                ApplyRangeMultiplier();
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            CreateCursorOverlay();
            Canvas.willRenderCanvases += RefreshCursorOverlay;

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
            // Keep the attack anchor current while the native OS cursor is active.
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

            ApplyRangeMultiplier();

            if (_cursorObject != null)
            {
                _cursorObject.SetActive(true);
            }

            RefreshCursorPosition();
            RefreshCursorOverlay();
        }

        /// <summary>
        /// Changes the logical range multiplier while preserving the authored
        /// cursor scale. A multiplier of 1 uses the current base scale.
        /// </summary>
        public void SetRangeMultiplier(float multiplier)
        {
            rangeMultiplier = Mathf.Max(0.01f, multiplier);
            ApplyRangeMultiplier();
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
            RefreshCursorOverlay();
        }

        private void CreateCursorOverlay()
        {
            _cursorVisual = GetComponent<CursorSkinController>()?.VisualRenderer;
            if (_cursorVisual == null || worldCamera == null) return;
            var canvasObject = new GameObject("CursorOverlay", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            _cursorCanvas = canvasObject.GetComponent<Canvas>();
            _cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _cursorCanvas.sortingOrder = short.MaxValue;
            _cursorCanvas.targetDisplay = worldCamera.targetDisplay;
            var imageObject = new GameObject("CursorVisual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            _cursorOverlay = imageObject.GetComponent<Image>();
            _cursorOverlay.raycastTarget = false;
            _cursorOverlay.maskable = false;
            _cursorOverlay.rectTransform.anchorMin = Vector2.zero;
            _cursorOverlay.rectTransform.anchorMax = Vector2.zero;
            _cursorCanvas.enabled = false;
        }

        // Run after the skin's LateUpdate so its size, pivot and breathing stay in sync.
        // The world renderer remains available to cursor-copy skills and the hit area stays in world space.
        private void RefreshCursorOverlay()
        {
            if (_cursorCanvas == null || _cursorVisual == null) return;
            bool visible = isActiveAndEnabled && _customCursorActive &&
                _cursorObject != null && _cursorObject.activeInHierarchy &&
                _cursorVisual.enabled && _cursorVisual.sprite != null && worldCamera != null;
            _cursorCanvas.enabled = visible;
            _cursorVisual.forceRenderingOff = visible;
            if (!visible) return;

            Sprite sprite = _cursorVisual.sprite;
            Transform visual = _cursorVisual.transform;
            Vector2 size = sprite.rect.size / sprite.pixelsPerUnit;
            Vector2 center = (sprite.rect.size * 0.5f - sprite.pivot) / sprite.pixelsPerUnit;
            Vector3 screenCenter = worldCamera.WorldToScreenPoint(visual.TransformPoint(center));
            Vector3 screenRight = worldCamera.WorldToScreenPoint(
                visual.TransformPoint(center + Vector2.right * size.x * 0.5f));
            Vector3 screenUp = worldCamera.WorldToScreenPoint(
                visual.TransformPoint(center + Vector2.up * size.y * 0.5f));
            Vector2 right = screenRight - screenCenter;
            Vector2 up = screenUp - screenCenter;
            _cursorOverlay.sprite = sprite;
            _cursorOverlay.color = _cursorVisual.color;
            RectTransform rect = _cursorOverlay.rectTransform;
            rect.anchoredPosition = screenCenter;
            rect.sizeDelta = new Vector2(right.magnitude * 2f, up.magnitude * 2f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg);
            rect.localScale = new Vector3(_cursorVisual.flipX ? -1f : 1f, _cursorVisual.flipY ? -1f : 1f, 1f);
        }

        /// <summary>
        /// Updates the cursor immediately from the current mouse position.
        /// CursorAttackController calls this before an attack so script
        /// execution order cannot cause a one-frame-old attack position.
        /// </summary>
        public bool RefreshCursorPosition()
        {
            if (_cursorTransform == null || worldCamera == null)
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

        private void ApplyRangeMultiplier()
        {
            if (_cursorTransform == null)
            {
                return;
            }

            if (_baseCursorScale == Vector3.zero)
            {
                _baseCursorScale = _cursorTransform.localScale;
            }

            _cursorTransform.localScale = _baseCursorScale * (rangeMultiplier * _spatialScale);
        }

        private void OnDestroy()
        {
            Canvas.willRenderCanvases -= RefreshCursorOverlay;
            if (_cursorVisual != null) _cursorVisual.forceRenderingOff = false;
            if (_cursorCanvas != null) Destroy(_cursorCanvas.gameObject);
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

            RefreshCursorOverlay();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
