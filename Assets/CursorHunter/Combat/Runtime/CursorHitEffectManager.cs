using System;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>One shared emitter per configured variant; no per-hit GameObjects or managed allocations.</summary>
    [DisallowMultipleComponent]
    public sealed class CursorHitEffectManager : MonoBehaviour, ISerializationCallbackReceiver
    {
        [Serializable]
        private struct EffectVariant
        {
            public ParticleSystem prefab;
            [Tooltip("Optional. When empty, uses the prefab renderer's shared material.")]
            public Material materialOverride;
        }

        [SerializeField] private CombatRunController combatRunController;
        [Tooltip("Add, remove or reorder complete variants. Empty entries are skipped; Play Mode edits apply on the next update.")]
        [SerializeField] private EffectVariant[] effectVariants = Array.Empty<EffectVariant>();
        // Retain old field names so already-open scenes survive the script reload as well.
        [SerializeField, HideInInspector] private ParticleSystem[] effectPrefabs;
        [SerializeField, HideInInspector] private Material[] effectMaterials;
        [Tooltip("Total visible particles across all configured variants.")]
        [SerializeField, Range(1, 4096)] private int maximumVisibleEffects = 256;
        [SerializeField, Range(0.05f, 1f)] private float lifetimeSeconds = 0.5f;
        [SerializeField, Min(0.01f)] private float sizeMultiplier = 1.2f;
        [SerializeField, Min(0.01f)] private float minimumSize = 0.5f;
        [SerializeField, Min(0.01f)] private float maximumSize = 2.5f;
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0f, -0.02f);
        [SerializeField] private int sortingOrder = 200;
        [Tooltip("Presentation-only seed; never consumes combat or UnityEngine.Random state.")]
        [SerializeField] private int visualRandomSeed = 19770419;

        private ParticleSystem[] _emitters;
        private Transform _emitterRoot;
        private CombatRunController _subscribedController;
        private SeededRandom _random;
        private RunId _runId;
        private int _capacity;
        private bool _simulating;
        private bool _paused;
        private bool _configurationDirty;

        public int EmittedCount { get; private set; }
        public int SuppressedCount { get; private set; }
        public int PeakActiveCount { get; private set; }
        public int EmitterCount => _emitters == null ? 0 : _emitters.Length;
        public int ConfiguredVariantCount
        {
            get
            {
                int count = 0;
                if (effectVariants != null)
                    for (int i = 0; i < effectVariants.Length; i++)
                        if (TryGetVariantMaterial(effectVariants[i], out _)) count++;
                return count;
            }
        }
        public int ActiveCount
        {
            get
            {
                int count = 0;
                if (_emitters != null)
                    for (int i = 0; i < _emitters.Length; i++) count += _emitters[i].particleCount;
                return count;
            }
        }

        private void OnEnable()
        {
            if (combatRunController == null)
                combatRunController = GetComponentInParent<CombatRunController>();
            // Allows references to be supplied after AddComponent in tests and authoring tools.
            if (combatRunController != null) Initialize();
        }

        public bool Initialize()
        {
            if (combatRunController == null) return false;
            if (_configurationDirty || _emitters == null)
            {
                _configurationDirty = false;
                ReleaseEmitters();
                int variantCount = ConfiguredVariantCount;
                if (variantCount == 0)
                {
                    Unsubscribe();
                    ResetPresentation();
                    return false;
                }
                _capacity = Mathf.Clamp(maximumVisibleEffects, 1, 4096);
                _emitters = new ParticleSystem[variantCount];
                _emitterRoot = new GameObject("CursorHitEmitters").transform;
                _emitterRoot.SetParent(transform, false);
                _emitterRoot.gameObject.SetActive(false);
                int emitterIndex = 0;
                for (int i = 0; i < effectVariants.Length; i++)
                {
                    if (!TryGetVariantMaterial(effectVariants[i], out Material material)) continue;
                    ParticleSystem emitter = Instantiate(effectVariants[i].prefab, _emitterRoot, false);
                    _emitters[emitterIndex++] = emitter;
                    emitter.name = "CursorHit_" + i;
                    emitter.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    emitter.transform.localPosition = Vector3.zero;
                    emitter.transform.localRotation = Quaternion.identity;
                    emitter.transform.localScale = Vector3.one;
                    var main = emitter.main;
                    main.playOnAwake = false;
                    // The shared simulator stays alive. Only explicit Emit calls create particles.
                    main.loop = true;
                    main.prewarm = false;
                    main.stopAction = ParticleSystemStopAction.None;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.scalingMode = ParticleSystemScalingMode.Shape;
                    main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                    main.useUnscaledTime = false;
                    main.simulationSpeed = 1f;
                    main.startSpeed = 0f;
                    // Each variant can absorb the full global budget without biasing random selection.
                    main.maxParticles = _capacity;
                    var emission = emitter.emission;
                    emission.enabled = false;
                    var shape = emitter.shape;
                    shape.enabled = false;
                    emitter.useAutoRandomSeed = false;
                    emitter.randomSeed = (uint)(i + 1);
                    var renderer = emitter.GetComponent<ParticleSystemRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.sortingOrder = sortingOrder;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                _emitterRoot.gameObject.SetActive(true);
                // Reserve native particle storage once, then clear before any frame can render it.
                for (int i = 0; i < _emitters.Length; i++)
                {
                    _emitters[i].Emit(_capacity);
                    _emitters[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                ResetPresentation();
            }
            if (_subscribedController != combatRunController)
            {
                Unsubscribe();
                _subscribedController = combatRunController;
                _subscribedController.DamageApplied += HandleDamageApplied;
                _subscribedController.PresentationReset += ResetPresentation;
                ResetPresentation();
            }
            return true;
        }

        private static bool TryGetVariantMaterial(EffectVariant variant, out Material material)
        {
            material = null;
            if (variant.prefab == null || variant.prefab.transform.childCount != 0 ||
                !variant.prefab.TryGetComponent(out ParticleSystemRenderer renderer)) return false;
            material = variant.materialOverride != null ? variant.materialOverride : renderer.sharedMaterial;
            return material != null;
        }

        private void HandleDamageApplied(CombatDamageApplied hit)
        {
            if (_configurationDirty || _emitters == null || !isActiveAndEnabled || combatRunController == null ||
                !combatRunController.IsRunning || hit.RunId != combatRunController.CurrentRunId ||
                hit.Source != CombatDamageSource.CursorAttack || hit.EffectiveDamage <= 0L ||
                !hit.HasHitEffectPosition || !IsFinite(hit.HitEffectPosition) ||
                !IsFinite(hit.HitEffectSizeWorldUnits) || hit.HitEffectSizeWorldUnits <= 0f) return;

            SynchronizePresentation();
            int count = ActiveCount;
            if (count >= _capacity)
            {
                SuppressedCount++;
                return;
            }
            // Select only initialized variants, including consecutive repeats, for any list size.
            int variant = (int)(_random.NextUInt64() % (ulong)_emitters.Length);
            float size = Mathf.Clamp(hit.HitEffectSizeWorldUnits * sizeMultiplier,
                minimumSize, maximumSize);
            var parameters = new ParticleSystem.EmitParams
            {
                position = hit.HitEffectPosition + spawnOffset,
                velocity = Vector3.zero,
                startLifetime = lifetimeSeconds,
                startSize = size,
                applyShapeToPosition = false
            };
            _emitters[variant].Emit(parameters, 1);
            EmittedCount++;
            PeakActiveCount = Mathf.Max(PeakActiveCount, count + 1);
        }

        private void Update()
        {
            // Inspector edits rebuild outside the per-hit path, at most once per update.
            if (_configurationDirty) Initialize();
            SynchronizePresentation();
        }

        // Run pause is independent of Time.timeScale. Pause the native simulators as well.
        public void SynchronizePresentation()
        {
            if (_emitters == null) return;
            if (combatRunController == null || !combatRunController.IsRunActive)
            {
                if (_simulating || ActiveCount > 0) StopAndClear();
                return;
            }
            if (_runId != combatRunController.CurrentRunId) ResetPresentation();
            bool pause = combatRunController.IsPaused;
            if (!_simulating || _paused != pause)
            {
                for (int i = 0; i < _emitters.Length; i++)
                {
                    if (pause) _emitters[i].Pause(false);
                    else _emitters[i].Play(false);
                }
                _simulating = true;
                _paused = pause;
            }
        }

        private void StopAndClear()
        {
            if (_emitters != null)
                for (int i = 0; i < _emitters.Length; i++)
                    if (_emitters[i] != null)
                        _emitters[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            _simulating = _paused = false;
        }

        private void ResetPresentation()
        {
            StopAndClear();
            _runId = combatRunController != null ? combatRunController.CurrentRunId : default;
            _random = new SeededRandom((ulong)(uint)visualRandomSeed);
            EmittedCount = SuppressedCount = PeakActiveCount = 0;
        }

        private void Unsubscribe()
        {
            if (_subscribedController != null)
            {
                _subscribedController.DamageApplied -= HandleDamageApplied;
                _subscribedController.PresentationReset -= ResetPresentation;
            }
            _subscribedController = null;
        }

        private void OnDisable() { Unsubscribe(); StopAndClear(); }

        private void OnDestroy()
        {
            Unsubscribe();
            ReleaseEmitters();
        }

        private void ReleaseEmitters()
        {
            StopAndClear();
            _emitters = null;
            if (_emitterRoot == null) return;
            _emitterRoot.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(_emitterRoot.gameObject);
            else DestroyImmediate(_emitterRoot.gameObject);
            _emitterRoot = null;
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            // Serialization can run off the main thread: copy references only, with no Unity API calls.
            if (effectPrefabs != null)
            {
                if (effectVariants == null || effectVariants.Length == 0)
                {
                    effectVariants = new EffectVariant[effectPrefabs.Length];
                    for (int i = 0; i < effectPrefabs.Length; i++)
                        effectVariants[i] = new EffectVariant
                        {
                            prefab = effectPrefabs[i],
                            materialOverride = effectMaterials != null && i < effectMaterials.Length
                                ? effectMaterials[i] : null
                        };
                }
                effectPrefabs = null;
                effectMaterials = null;
            }
            _configurationDirty = true;
        }

        private void OnValidate()
        {
            lifetimeSeconds = Mathf.Clamp(IsFinite(lifetimeSeconds) ? lifetimeSeconds : 0.5f, 0.05f, 1f);
            sizeMultiplier = Mathf.Clamp(IsFinite(sizeMultiplier) ? sizeMultiplier : 1.2f, 0.01f, 10f);
            minimumSize = Mathf.Clamp(IsFinite(minimumSize) ? minimumSize : 0.5f, 0.01f, 10f);
            maximumSize = Mathf.Clamp(IsFinite(maximumSize) ? maximumSize : 2.5f, minimumSize, 10f);
            if (!IsFinite(spawnOffset)) spawnOffset = Vector3.zero;
            _configurationDirty = true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
