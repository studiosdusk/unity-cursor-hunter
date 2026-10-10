using System;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>One update loop and bounded storage for all floating damage numbers.</summary>
    [DisallowMultipleComponent]
    public sealed class DamageTextManager : MonoBehaviour
    {
        [SerializeField] private CombatRunController combatRunController;
        [SerializeField] private DamageText_Root damageTextPrefab;
        [Tooltip("Prewarmed instances. Pool sizes are captured at initialization; edit before Play.")]
        [SerializeField, Min(0)] private int initialPoolSize = 128;
        [Tooltip("Hard instance/rendering limit. Edit before Play.")]
        [SerializeField, Min(1)] private int maximumPoolSize = 256;
        [SerializeField, Range(0f, 0.2f)] private float mergeWindowSeconds = 0.1f;
        [SerializeField, Range(0.7f, 0.9f)] private float lifetimeSeconds = 0.8f;
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.22f, -0.05f);
        [SerializeField, Min(0.01f)] private float normalScale = 1f;
        [SerializeField, Min(0.01f)] private float criticalScale = 1.2f;
        [SerializeField, Range(0.3f, 0.9f)] private float fadeStart = 0.65f;
        [SerializeField] private MotionVariant[] motionVariants =
        {
            new MotionVariant(-0.18f, 0.62f, 0.15f),
            new MotionVariant(0.22f, 0.54f, 0.12f),
            new MotionVariant(-0.08f, 0.74f, 0.18f),
            new MotionVariant(0.12f, 0.66f, 0.14f)
        };

        [Serializable]
        private struct MotionVariant
        {
            public float horizontalDrift;
            [Min(0.01f)] public float rise;
            [Range(0f, 0.5f)] public float scaleOvershoot;
            public MotionVariant(float drift, float height, float overshoot)
            { horizontalDrift = drift; rise = height; scaleOvershoot = overshoot; }
        }

        private sealed class Slot
        {
            public DamageText_Root View;
            public ICombatTarget Target;
            public Vector3 Origin;
            public MotionVariant Motion;
            public long Damage;
            public long Order;
            public float Age;
            public bool Critical;
        }

        private Slot[] _slots;
        private int[] _active;
        private int[] _free;
        private int _freeCount;
        private int _createdCount;
        private int _nextVariant;
        private long _spawnSequence;
        private Transform _poolRoot;
        private CombatRunController _subscribedController;
        private RunId _runId;

        // The custom Inspector shows these counters read-only; they are not scene state.
        private int activeCount;
        private int peakActiveCount;
        private int mergedCount;
        private int recycledCount;
        private int suppressedCount;
        public int ActiveCount => activeCount;
        public int PeakActiveCount => peakActiveCount;
        public int CreatedCount => _createdCount;
        public int MergedCount => mergedCount;
        public int RecycledCount => recycledCount;
        public int SuppressedCount => suppressedCount;

        private void OnEnable()
        {
            if (combatRunController == null) combatRunController = GetComponent<CombatRunController>();
            // Tests and prefab authoring can assign references before Initialize is called.
            if (combatRunController == null || damageTextPrefab == null) return;
            Initialize();
        }

        public bool Initialize()
        {
            if (combatRunController == null || damageTextPrefab == null || !damageTextPrefab.IsConfigured)
            {
                Debug.LogWarning("DamageTextManager needs a controller and a prefab with two configured TMP children.", this);
                return false;
            }
            if (_slots == null)
            {
                maximumPoolSize = Mathf.Clamp(maximumPoolSize, 1, 4096);
                initialPoolSize = Mathf.Clamp(initialPoolSize, 0, maximumPoolSize);
                lifetimeSeconds = Mathf.Clamp(lifetimeSeconds, 0.7f, 0.9f);
                mergeWindowSeconds = Mathf.Clamp(mergeWindowSeconds, 0f, 0.2f);
                fadeStart = Mathf.Clamp(fadeStart, 0.3f, 0.9f);
                _slots = new Slot[maximumPoolSize];
                _active = new int[maximumPoolSize];
                _free = new int[maximumPoolSize];
                _poolRoot = new GameObject("DamageTextPool").transform;
                _poolRoot.SetParent(transform, false);
                activeCount = peakActiveCount = mergedCount = recycledCount = suppressedCount = 0;
                for (int i = 0; i < initialPoolSize; i++) CreateSlot();
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

        private void CreateSlot()
        {
            int index = _createdCount++;
            DamageText_Root view = Instantiate(damageTextPrefab, _poolRoot, false);
            view.InitializeForPool();
            _slots[index] = new Slot { View = view };
            _free[_freeCount++] = index;
        }

        private void HandleDamageApplied(CombatDamageApplied hit)
        {
            if (_slots == null || !isActiveAndEnabled || !combatRunController.IsRunning ||
                hit.RunId != combatRunController.CurrentRunId || hit.EffectiveDamage <= 0L ||
                hit.Target == null || !hit.HasWorldPosition || !IsFinite(hit.WorldPosition)) return;
            if (_runId != hit.RunId) ResetPresentation();

            // Bounded scan (default 256); no growing dictionary retaining dead targets.
            for (int i = activeCount - 1; i >= 0; i--)
            {
                Slot existing = _slots[_active[i]];
                if (mergeWindowSeconds > 0f && ReferenceEquals(existing.Target, hit.Target) &&
                    existing.Critical == hit.IsCritical && existing.Age <= mergeWindowSeconds &&
                    long.MaxValue - existing.Damage >= hit.EffectiveDamage)
                {
                    existing.Damage += hit.EffectiveDamage;
                    existing.View.SetDamage(existing.Damage);
                    mergedCount++;
                    // Keep original age/trajectory: continuous hits cannot make a popup immortal.
                    return;
                }
            }

            if (_freeCount == 0 && _createdCount < _slots.Length) CreateSlot();
            if (_freeCount == 0)
            {
                int victim = FindOldest(false);
                if (victim < 0 && !hit.IsCritical)
                {
                    suppressedCount++;
                    return; // A normal hit never displaces a screen full of critical numbers.
                }
                if (victim < 0) victim = FindOldest(true);
                ReleaseAt(victim);
                recycledCount++;
            }

            int slotIndex = _free[--_freeCount];
            Slot slot = _slots[slotIndex];
            slot.Target = hit.Target;
            slot.Origin = hit.WorldPosition + spawnOffset;
            slot.Critical = hit.IsCritical;
            slot.Damage = hit.EffectiveDamage;
            slot.Age = 0f;
            slot.Order = _spawnSequence++;
            slot.Motion = motionVariants != null && motionVariants.Length > 0
                ? motionVariants[_nextVariant++ % motionVariants.Length]
                : new MotionVariant(0f, 0.62f, 0.15f);
            if (_nextVariant == int.MaxValue) _nextVariant = 0;
            _active[activeCount++] = slotIndex;
            peakActiveCount = Mathf.Max(peakActiveCount, activeCount);
            slot.View.Show(slot.Damage, slot.Critical);
            Animate(slot);
        }

        private int FindOldest(bool critical)
        {
            int result = -1;
            long order = long.MaxValue;
            for (int i = 0; i < activeCount; i++)
            {
                Slot slot = _slots[_active[i]];
                if (slot.Critical == critical && slot.Order < order) { order = slot.Order; result = i; }
            }
            return result;
        }

        private void Update()
        {
            AdvancePresentation(Time.deltaTime);
        }

        /// <summary>Uses the combat pause policy; also supports deterministic presentation checks.</summary>
        public void AdvancePresentation(float deltaSeconds)
        {
            if (_slots == null) return;
            if (combatRunController == null || !combatRunController.IsRunActive ||
                _runId != combatRunController.CurrentRunId)
            {
                ResetPresentation();
                return;
            }
            if (combatRunController.IsPaused) return;
            if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;

            for (int i = activeCount - 1; i >= 0; i--)
            {
                Slot slot = _slots[_active[i]];
                slot.Age += deltaSeconds;
                if (slot.Age >= lifetimeSeconds) ReleaseAt(i);
                else Animate(slot);
            }
        }

        private void Animate(Slot slot)
        {
            float t = Mathf.Clamp01(slot.Age / lifetimeSeconds);
            float lift = 1f - (1f - t) * (1f - t) * (1f - t);
            // A small fall after the apex gives the upward impulse a soft landing.
            float settle = Mathf.Max(0f, (t - 0.62f) / 0.38f);
            Vector3 offset = new Vector3(slot.Motion.horizontalDrift * (2f * t - t * t),
                slot.Motion.rise * (lift - 0.12f * settle * settle), 0f);
            float scale;
            if (t < 0.1f)
            {
                float pop = 1f - Mathf.Pow(1f - t / 0.1f, 3f);
                scale = Mathf.Lerp(0.78f, 1f + slot.Motion.scaleOvershoot, pop);
            }
            else scale = Mathf.Lerp(1f + slot.Motion.scaleOvershoot, 1f,
                Mathf.SmoothStep(0f, 1f, (t - 0.1f) / 0.18f));
            float fade = Mathf.SmoothStep(0f, 1f, (t - fadeStart) / (1f - fadeStart));
            scale *= Mathf.Lerp(1f, 0.94f, fade) * (slot.Critical ? criticalScale : normalScale);
            slot.View.SetPose(slot.Origin + offset, scale, 1f - fade);
        }

        private void ReleaseAt(int activeIndex)
        {
            int slotIndex = _active[activeIndex];
            Slot slot = _slots[slotIndex];
            slot.View.Hide();
            slot.Target = null;
            _active[activeIndex] = _active[--activeCount];
            _free[_freeCount++] = slotIndex;
        }

        private void ResetPresentation()
        {
            while (activeCount > 0) ReleaseAt(activeCount - 1);
            _runId = combatRunController != null ? combatRunController.CurrentRunId : default;
            _nextVariant = 0;
            _spawnSequence = 0L;
        }

        private void OnDisable() { Unsubscribe(); ResetPresentation(); }
        private void Unsubscribe()
        {
            if (_subscribedController != null)
            {
                _subscribedController.DamageApplied -= HandleDamageApplied;
                _subscribedController.PresentationReset -= ResetPresentation;
            }
            _subscribedController = null;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_poolRoot == null) return;
            if (Application.isPlaying) Destroy(_poolRoot.gameObject);
            else DestroyImmediate(_poolRoot.gameObject);
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
