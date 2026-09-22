using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Small world-space health bar for a spawned normal monster. The view is
    /// only enabled at spawn and after damage, which keeps a crowded field
    /// readable while still making the target's current HP inspectable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterHealthBarView : MonoBehaviour
    {
        private static Sprite _whiteSprite;

        [SerializeField, Min(0.1f)] private float barWidth = 0.9f;
        [SerializeField, Min(0.02f)] private float barHeight = 0.08f;
        [SerializeField, Min(0.1f)] private float verticalOffset = 0.65f;
        [SerializeField, Min(0f)] private float visibleSeconds = 1.25f;
        [SerializeField] private int sortingOrder = 250;

        private Transform _barRoot;
        private Transform _fillTransform;
        private SpriteRenderer _background;
        private SpriteRenderer _fill;
        private long _maxHealth;
        private long _currentHealth;
        private float _visibleUntil;

        public long MaxHealth => _maxHealth;
        public long CurrentHealth => _currentHealth;

        /// <summary>
        /// Adjusts the bar to the host sprite bounds without coupling this
        /// view to a particular monster prefab size.
        /// </summary>
        public void Configure(Bounds targetBounds)
        {
            barWidth = Mathf.Clamp(targetBounds.size.x * 1.15f, 0.45f, 3.5f);
            verticalOffset = Mathf.Max(0.45f, targetBounds.extents.y + 0.16f);
            if (_barRoot != null)
            {
                _barRoot.localPosition = new Vector3(0f, verticalOffset, 0f);
            }

            ApplyLayout();
        }

        public void Initialize(long maxHealth)
        {
            _maxHealth = maxHealth > 0L ? maxHealth : 1L;
            _currentHealth = _maxHealth;
            EnsureVisuals();
            ApplyLayout();
            UpdateFill();
            ShowTemporarily();
        }

        public void SetHealth(long currentHealth)
        {
            if (_maxHealth <= 0L)
            {
                return;
            }

            _currentHealth = currentHealth < 0L
                ? 0L
                : currentHealth > _maxHealth ? _maxHealth : currentHealth;
            EnsureVisuals();
            UpdateFill();
            ShowTemporarily();
        }

        public void Hide()
        {
            if (_barRoot != null)
            {
                _barRoot.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (_barRoot == null || !_barRoot.gameObject.activeSelf)
            {
                return;
            }

            if (Time.unscaledTime >= _visibleUntil)
            {
                _barRoot.gameObject.SetActive(false);
            }
        }

        private void EnsureVisuals()
        {
            if (_barRoot != null && _background != null && _fill != null)
            {
                return;
            }

            if (_whiteSprite == null)
            {
                _whiteSprite = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    1f);
                _whiteSprite.name = "CursorHunter_RuntimeHealthBarPixel";
                _whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            }

            GameObject root = new GameObject("MonsterHealthBar");
            root.transform.SetParent(transform, false);
            _barRoot = root.transform;
            _barRoot.localPosition = new Vector3(0f, verticalOffset, 0f);

            GameObject background = new GameObject(
                "Background",
                typeof(Transform),
                typeof(SpriteRenderer));
            background.transform.SetParent(_barRoot, false);
            _background = background.GetComponent<SpriteRenderer>();
            _background.sprite = _whiteSprite;
            _background.color = new Color(0.02f, 0.025f, 0.04f, 0.92f);
            _background.sortingOrder = sortingOrder;

            GameObject fill = new GameObject(
                "Fill",
                typeof(Transform),
                typeof(SpriteRenderer));
            fill.transform.SetParent(_barRoot, false);
            _fillTransform = fill.transform;
            _fill = fill.GetComponent<SpriteRenderer>();
            _fill.sprite = _whiteSprite;
            _fill.sortingOrder = sortingOrder + 1;
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            if (_background == null || _fill == null || _fillTransform == null)
            {
                return;
            }

            float safeWidth = Mathf.Max(0.1f, barWidth);
            float safeHeight = Mathf.Max(0.02f, barHeight);
            _barRoot.localPosition = new Vector3(0f, verticalOffset, 0f);
            _background.transform.localPosition = Vector3.zero;
            _background.transform.localScale = new Vector3(
                safeWidth,
                safeHeight,
                1f);
            _fillTransform.localPosition = new Vector3(
                -safeWidth * 0.5f,
                0f,
                -0.01f);
            _fillTransform.localScale = new Vector3(
                safeWidth,
                safeHeight * 0.72f,
                1f);
        }

        private void UpdateFill()
        {
            if (_fill == null || _fillTransform == null || _maxHealth <= 0L)
            {
                return;
            }

            float ratio = Mathf.Clamp01((float)_currentHealth / _maxHealth);
            float safeWidth = Mathf.Max(0.1f, barWidth);
            float safeHeight = Mathf.Max(0.02f, barHeight);
            _fillTransform.localPosition = new Vector3(
                -safeWidth * 0.5f + safeWidth * ratio * 0.5f,
                0f,
                -0.01f);
            _fillTransform.localScale = new Vector3(
                safeWidth * ratio,
                safeHeight * 0.72f,
                1f);
            _fill.color = ratio > 0.5f
                ? new Color(0.27f, 0.92f, 0.45f, 1f)
                : ratio > 0.25f
                    ? new Color(1f, 0.78f, 0.20f, 1f)
                    : new Color(1f, 0.24f, 0.25f, 1f);
        }

        private void ShowTemporarily()
        {
            if (_barRoot == null)
            {
                return;
            }

            _barRoot.gameObject.SetActive(true);
            _visibleUntil = Time.unscaledTime + Mathf.Max(0f, visibleSeconds);
        }
    }
}
