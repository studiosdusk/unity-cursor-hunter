using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.Progression.ResearchPrototype
{
    /// <summary>Creates a light, reusable field of drifting sparkles behind the research UI.</summary>
    [DisallowMultipleComponent]
    public sealed class ProgressionUiBackgroundAtmosphere : MonoBehaviour
    {
        [SerializeField] private Sprite sparkleSprite;
        [SerializeField, Min(1)] private int particleCount = 26;
        [SerializeField] private Vector2 sizeRange = new Vector2(12f, 32f);
        [SerializeField] private Vector2 lifetimeRange = new Vector2(12f, 24f);
        [SerializeField] private Vector2 driftSpeedRange = new Vector2(10f, 30f);
        [SerializeField, Range(0f, 1f)] private float peakAlpha = 0.24f;
        [SerializeField] private Color tint = new Color(0.58f, 0.84f, 1f, 1f);

        private Particle[] particles;
        private RectTransform root;
        private bool initialized;

        private sealed class Particle
        {
            public RectTransform rect;
            public Image image;
            public Vector2 origin;
            public Vector2 velocity;
            public float age;
            public float lifetime;
            public float size;
            public float phase;
            public float wobble;
            public float rotationSpeed;
        }

        private void Awake()
        {
            root = (RectTransform)transform;
            if (sparkleSprite == null) return;

            particles = new Particle[Mathf.Max(1, particleCount)];
            for (var i = 0; i < particles.Length; i++)
            {
                var go = new GameObject("Sparkle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = gameObject.layer;
                var rect = (RectTransform)go.transform;
                rect.SetParent(transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                var image = go.GetComponent<Image>();
                image.sprite = sparkleSprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = tint;

                particles[i] = new Particle { rect = rect, image = image };
            }
        }

        private void Update()
        {
            if (particles == null) return;
            var size = root.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            if (!initialized)
            {
                initialized = true;
                for (var i = 0; i < particles.Length; i++) ResetParticle(particles[i], true);
            }

            var dt = Time.unscaledDeltaTime;
            for (var i = 0; i < particles.Length; i++)
            {
                var p = particles[i];
                p.age += dt;
                if (p.age >= p.lifetime)
                {
                    ResetParticle(p, false);
                    continue;
                }

                var progress = p.age / p.lifetime;
                var wobble = Mathf.Sin(p.age * p.wobble + p.phase) * 12f;
                var position = p.origin + p.velocity * p.age + new Vector2(wobble, Mathf.Sin(p.age * 0.65f + p.phase) * 8f);
                p.rect.anchoredPosition = position;
                p.rect.localRotation = Quaternion.Euler(0f, 0f, p.phase * 57.2958f + p.age * p.rotationSpeed);

                var fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.18f));
                var fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.66f) / 0.34f));
                var twinkle = 0.72f + Mathf.Sin(p.age * 2.1f + p.phase) * 0.28f;
                var color = tint;
                color.a = peakAlpha * fadeIn * fadeOut * twinkle;
                p.image.color = color;

                if (position.x > size.x * 0.65f || position.y > size.y * 0.65f)
                    ResetParticle(p, false);
            }
        }

        private void ResetParticle(Particle particle, bool distributeAcrossScreen)
        {
            var size = root.rect.size;
            particle.age = distributeAcrossScreen ? UnityEngine.Random.Range(0f, lifetimeRange.y) : 0f;
            particle.lifetime = UnityEngine.Random.Range(lifetimeRange.x, lifetimeRange.y);
            particle.size = UnityEngine.Random.Range(sizeRange.x, sizeRange.y);
            particle.phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            particle.wobble = UnityEngine.Random.Range(0.35f, 0.9f);
            particle.rotationSpeed = UnityEngine.Random.Range(-12f, 12f);
            var speed = UnityEngine.Random.Range(driftSpeedRange.x, driftSpeedRange.y);
            var angle = UnityEngine.Random.Range(24f, 64f) * Mathf.Deg2Rad;
            particle.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            var margin = distributeAcrossScreen ? 0f : particle.size;
            particle.origin = new Vector2(
                UnityEngine.Random.Range(-size.x * 0.5f - margin, size.x * 0.5f),
                distributeAcrossScreen
                    ? UnityEngine.Random.Range(-size.y * 0.5f, size.y * 0.5f)
                    : -size.y * 0.5f - margin);
            particle.rect.sizeDelta = Vector2.one * particle.size;
            particle.rect.anchoredPosition = particle.origin + particle.velocity * particle.age;
            particle.image.color = new Color(tint.r, tint.g, tint.b, 0f);
        }
    }
}
