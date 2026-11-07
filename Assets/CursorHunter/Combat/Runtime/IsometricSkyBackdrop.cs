using System;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>Camera-fitted scenery behind the arena. Never participates in combat or input.</summary>
    [DisallowMultipleComponent]
    public sealed class IsometricSkyBackdrop : MonoBehaviour
    {
        [Serializable]
        private sealed class DriftingCloud
        {
            public SpriteRenderer Visual;
            public Vector2 ViewportCenter;
            [Range(0.05f, 0.35f)] public float ViewportWidth = 0.15f;
            [Tooltip("Fraction of screen width travelled per second. Negative moves left.")]
            public float DriftSpeed = 0.0025f;
            [Range(0f, 0.02f)] public float BobHeight = 0.004f;
            [Min(1f)] public float BobPeriod = 24f;
        }

        [SerializeField] private Camera worldCamera;
        [SerializeField] private CombatRunController combat;
        [SerializeField] private SpriteRenderer sky;
        [SerializeField] private Color skyTint = new Color(0.13f, 0.28f, 0.32f, 1f);
        [SerializeField] private Color cloudTint = new Color(0.4f, 0.62f, 0.66f, 0.18f);
        [SerializeField, Range(1f, 1.15f)] private float overscan = 1.03f;
        [SerializeField] private DriftingCloud[] clouds = Array.Empty<DriftingCloud>();

        private float _motionTime;

        private void OnEnable()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            UpdateLayout();
        }

        private void LateUpdate()
        {
            if (worldCamera == null) return;
            // Settings/focus/entrance pauses freeze scenery along with the battlefield.
            if (combat == null || !combat.IsPaused) _motionTime += Time.deltaTime;
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            if (worldCamera == null || !worldCamera.orthographic || sky == null || sky.sprite == null)
                return;
            float distance = Vector3.Dot(transform.position - worldCamera.transform.position,
                worldCamera.transform.forward);
            float height = worldCamera.orthographicSize * 2f;
            float width = height * worldCamera.aspect;
            Vector2 sourceSize = sky.sprite.bounds.size;
            float scale = Mathf.Max(width / sourceSize.x, height / sourceSize.y) * overscan;
            Place(sky, new Vector2(0.5f, 0.5f), distance, scale);
            sky.color = skyTint;

            for (int i = 0; i < clouds.Length; i++)
            {
                DriftingCloud cloud = clouds[i];
                if (cloud == null || cloud.Visual == null || cloud.Visual.sprite == null) continue;
                // Wrap only once the entire sprite is offscreen; no visible edge teleport.
                float margin = cloud.ViewportWidth * 0.5f + 0.03f;
                float x = Mathf.Repeat(cloud.ViewportCenter.x + _motionTime * cloud.DriftSpeed + margin,
                    1f + margin * 2f) - margin;
                float bob = Mathf.Sin(_motionTime * Mathf.PI * 2f / Mathf.Max(1f, cloud.BobPeriod)) * cloud.BobHeight;
                Place(cloud.Visual, new Vector2(x, cloud.ViewportCenter.y + bob), distance,
                    width * cloud.ViewportWidth / cloud.Visual.sprite.bounds.size.x);
                cloud.Visual.color = cloudTint;
            }
        }

        private void Place(SpriteRenderer visual, Vector2 viewport, float distance, float scale)
        {
            Transform target = visual.transform;
            Vector3 parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
            target.localScale = new Vector3(scale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                scale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1f);
            Vector3 center = worldCamera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, distance));
            target.SetPositionAndRotation(center, worldCamera.transform.rotation);
            target.position -= target.TransformVector(visual.sprite.bounds.center);
        }
    }
}
