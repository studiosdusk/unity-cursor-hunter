using UnityEngine;
using UnityEngine.Rendering;

namespace CursorHunter.Combat
{
    /// <summary>Pooled, clock-driven beam ribbons and impact pulses. No per-hit objects.</summary>
    internal sealed class LaserBeamVisual
    {
        internal const float HaloWidthMultiplier = 1.8f;
        private const int Points = 17;
        private const int PulseCount = 16;
        private const int PulsePoints = 12;
        private const float PulseLifetime = 0.16f;
        private static readonly int FeatherId = Shader.PropertyToID("_Feather");
        private readonly LineRenderer _halo;
        private readonly LineRenderer _body;
        private readonly LineRenderer _core;
        private readonly LineRenderer[] _pulses = new LineRenderer[PulseCount];
        private readonly Vector3[] _pulseCenters = new Vector3[PulseCount];
        private readonly float[] _pulseStarted = new float[PulseCount];
        private readonly float[] _pulseRadii = new float[PulseCount];
        private Vector3 _start;
        private Vector3 _end;
        private Vector3 _perpendicular;
        private float _width;
        private int _nextPulse;

        internal LaserBeamVisual(Transform parent, Material material, int sortingLayer)
        {
            _halo = Create("Beam glow", parent, material, sortingLayer, 0, Points, 0.5f);
            _body = Create("Beam body", parent, material, sortingLayer, 1, Points);
            _core = Create("Beam core", parent, material, sortingLayer, 2, Points, 0.25f);
            for (int i = 0; i < PulseCount; i++)
            {
                _pulses[i] = Create("Beam impact " + i, parent, material,
                    sortingLayer, 3, PulsePoints);
                _pulses[i].loop = true;
            }
            Hide();
        }

        private static LineRenderer Create(string name, Transform parent, Material material,
            int sortingLayer, int order, int points, float feather = 0.16f)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = material;
            var properties = new MaterialPropertyBlock();
            properties.SetFloat(FeatherId, feather);
            line.SetPropertyBlock(properties);
            line.sortingLayerID = sortingLayer;
            line.sortingOrder = order;
            line.positionCount = points;
            line.numCapVertices = 0;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return line;
        }

        internal void SetPath(Vector2 start, Vector2 end, float z, float width)
        {
            _start = new Vector3(start.x, start.y, z);
            _end = new Vector3(end.x, end.y, z);
            Vector3 direction = (_end - _start).normalized;
            _perpendicular = new Vector3(-direction.y, direction.x);
            _width = width;
            for (int i = 0; i < Points; i++)
            {
                Vector3 point = Vector3.Lerp(_start, _end, (float)i / (Points - 1));
                _halo.SetPosition(i, point);
                _body.SetPosition(i, point);
                _core.SetPosition(i, point);
            }
        }

        internal void Show(LaserEffectPhase phase, float progress, float elapsed, float now)
        {
            if (phase == LaserEffectPhase.Telegraph)
            {
                float pulse = 0.65f + 0.35f * Mathf.Sin(progress * Mathf.PI);
                Set(_halo, _width, new Color(1f, 0.35f, 0.03f, 0.13f));
                Set(_body, _width * 0.085f, new Color(1f, 0.45f, 0.06f, pulse));
                Set(_core, _width * 0.025f, new Color(1f, 0.91f, 0.55f, pulse));
            }
            else if (phase == LaserEffectPhase.Firing)
            {
                float flash = Mathf.Exp(-progress * 12f);
                Set(_halo, _width * HaloWidthMultiplier,
                    new Color(1f, 0.26f, 0.015f, 0.3f + flash * 0.2f));
                Set(_body, _width, new Color(1f, 0.48f + flash * 0.2f, 0.025f, 0.96f));
                Set(_core, _width * (0.25f + flash * 0.3f), new Color(1f, 1f, 0.8f, 1f));
            }
            else
            {
                float fade = (1f - progress) * (1f - progress);
                Set(_halo, _width * Mathf.Lerp(1.1f, 0.4f, progress),
                    new Color(1f, 0.3f, 0.025f, fade * 0.2f));
                Set(_body, _width * Mathf.Lerp(0.4f, 0.06f, progress),
                    new Color(1f, 0.5f, 0.05f, fade * 0.65f));
                Set(_core, _width * Mathf.Lerp(0.11f, 0.015f, progress),
                    new Color(1f, 0.95f, 0.65f, fade * 0.85f));
            }

            // The slim white filament carries the old laser's flowing energy shape.
            float wave = phase == LaserEffectPhase.Firing ? _width * 0.16f : 0f;
            for (int i = 0; i < Points; i++)
            {
                float t = (float)i / (Points - 1);
                float offset = Mathf.Sin(t * Mathf.PI * 7f + elapsed * 24f) *
                    Mathf.Sin(t * Mathf.PI) * wave;
                _core.SetPosition(i, Vector3.Lerp(_start, _end, t) + _perpendicular * offset);
            }
            UpdatePulses(now);
        }

        internal void Pulse(Vector2 point, float now, float width)
        {
            int index = _nextPulse++ % PulseCount;
            _pulseCenters[index] = new Vector3(point.x, point.y, _start.z);
            _pulseStarted[index] = now;
            _pulseRadii[index] = width * 0.3f;
            _pulses[index].enabled = true;
        }

        internal void FirePulse(float now)
        {
            Pulse(Vector3.Lerp(_start, _end, 0.02f), now, _width);
            Pulse(Vector3.Lerp(_start, _end, 0.98f), now, _width);
        }

        private void UpdatePulses(float now)
        {
            for (int i = 0; i < PulseCount; i++)
            {
                LineRenderer pulse = _pulses[i];
                if (!pulse.enabled) continue;
                float progress = Mathf.Clamp01((now - _pulseStarted[i]) / PulseLifetime);
                if (progress >= 1f) { pulse.enabled = false; continue; }
                float radius = _pulseRadii[i] * Mathf.Lerp(0.4f, 1.3f, progress);
                Set(pulse, _pulseRadii[i] * 0.14f,
                    new Color(1f, 0.88f, 0.45f, (1f - progress) * 0.9f));
                for (int j = 0; j < PulsePoints; j++)
                {
                    float angle = j * Mathf.PI * 2f / PulsePoints;
                    float r = radius * (j % 2 == 0 ? 1f : 0.5f);
                    pulse.SetPosition(j, _pulseCenters[i] +
                        new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
                }
            }
        }

        private static void Set(LineRenderer line, float width, Color color)
        {
            line.enabled = true;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
        }

        internal void Hide()
        {
            _halo.enabled = _body.enabled = _core.enabled = false;
            for (int i = 0; i < PulseCount; i++) _pulses[i].enabled = false;
            _nextPulse = 0;
        }
    }
}
