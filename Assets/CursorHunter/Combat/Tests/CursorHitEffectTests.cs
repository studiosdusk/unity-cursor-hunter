using System;
using System.Reflection;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CursorHunter.Combat.Tests
{
    public sealed class CursorHitEffectTests
    {
        private const string PrefabPath = "Assets/CursorHunter/Combat/Prefabs/Vfx/CursorHitEffects.prefab";
        private const string MonsterPath = "Assets/CursorHunter/Combat/Prefabs/Monsters/MonsterRoot.prefab";
        private static readonly MethodInfo ApplyHit = typeof(CombatRunController).GetMethod(
            "ApplyBundle", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo HandleHit = typeof(CursorHitEffectManager).GetMethod(
            "HandleDamageApplied", BindingFlags.NonPublic | BindingFlags.Instance);
        private GameObject _root;
        private CombatRunController _combat;
        private CursorHitEffectManager _manager;
        private ParticleSystem[] _emitters;
        private Action<CombatDamageApplied> _show;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("HitEffectTests");
            _combat = _root.AddComponent<CombatRunController>();
            StartRun("hit-effects");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            _root.SetActive(false);
            GameObject view = Object.Instantiate(prefab, _root.transform);
            _manager = view.GetComponent<CursorHitEffectManager>();
            var settings = new SerializedObject(_manager);
            settings.FindProperty("combatRunController").objectReferenceValue = _combat;
            settings.FindProperty("maximumVisibleEffects").intValue = 256;
            settings.ApplyModifiedPropertiesWithoutUndo();
            _root.SetActive(true);
            Assert.That(_manager.Initialize(), Is.True);
            _emitters = _manager.GetComponentsInChildren<ParticleSystem>();
            _show = (Action<CombatDamageApplied>)Delegate.CreateDelegate(
                typeof(Action<CombatDamageApplied>), _manager, HandleHit);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        private void StartRun(string id) => Assert.That(_combat.StartRun(new RunRequest(id, 30f),
            new CombatSnapshot(10L, 1f, 0.1f, 1, 50f, 1f, true, 0.1f)), Is.True);

        private CombatDamageApplied Hit(CombatDamageSource source = CombatDamageSource.CursorAttack,
            RunId? run = null, Vector3? position = null, bool hasPosition = true, float size = 1.2f)
            => new CombatDamageApplied(run ?? _combat.CurrentRunId, null, 10L, false,
                new Vector3(0f, 9f, 0f), true, source, position ?? new Vector3(2f, 3f, 0f),
                hasPosition, size);

        private void ExpireParticles()
        {
            foreach (ParticleSystem emitter in _emitters)
                emitter.Simulate(0.6f, false, false, false);
        }

        [Test]
        public void WrapperUsesFourSharedWorldEmittersAndUrpMaterials()
        {
            Assert.That(_emitters.Length, Is.EqualTo(4));
            foreach (ParticleSystem emitter in _emitters)
            {
                Assert.That(emitter.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
                Assert.That(emitter.main.maxParticles, Is.EqualTo(256));
                Assert.That(emitter.emission.enabled, Is.False);
                Assert.That(emitter.textureSheetAnimation.numTilesX, Is.EqualTo(4));
                Assert.That(emitter.textureSheetAnimation.numTilesY, Is.EqualTo(2));
                var renderer = emitter.GetComponent<ParticleSystemRenderer>();
                Assert.That(renderer.sharedMaterial.shader.name,
                    Is.EqualTo("Universal Render Pipeline/Particles/Unlit"));
                Assert.That(renderer.sharedMaterial.GetFloat("_ZWrite"), Is.Zero);
                Assert.That(renderer.sortingOrder, Is.EqualTo(200));
            }
            _show(Hit());
            var particles = new ParticleSystem.Particle[1];
            foreach (ParticleSystem emitter in _emitters)
                if (emitter.GetParticles(particles) > 0)
                {
                    Assert.That(particles[0].position, Is.EqualTo(new Vector3(2f, 3f, -0.02f)));
                    Assert.That(particles[0].startSize, Is.EqualTo(1.44f).Within(0.001f));
                    Assert.That(particles[0].startLifetime, Is.EqualTo(0.5f).Within(0.001f));
                }
            _manager.transform.position = new Vector3(10f, 10f, 0f);
            foreach (ParticleSystem emitter in _emitters)
                if (emitter.GetParticles(particles) > 0)
                    Assert.That(particles[0].position, Is.EqualTo(new Vector3(2f, 3f, -0.02f)));
        }

        [Test]
        public void RejectsSkillsOldRunsAndInvalidPositions()
        {
            _show(Hit(CombatDamageSource.Skill));
            _show(Hit(CombatDamageSource.Unknown));
            _show(Hit(run: new RunId("old-run")));
            _show(Hit(hasPosition: false));
            _show(Hit(position: new Vector3(float.NaN, 0f, 0f)));
            _show(Hit(size: float.PositiveInfinity));
            Assert.That(_manager.EmittedCount, Is.Zero);
            Assert.That(_manager.ActiveCount, Is.Zero);
        }

        [Test]
        public void TenThousandHitsRespectGlobalCapacityAndReuseEmitters()
        {
            CombatDamageApplied hit = Hit();
            for (int i = 0; i < 10000; i++) _show(hit);
            Assert.That(_manager.ActiveCount, Is.EqualTo(256));
            Assert.That(_manager.EmittedCount, Is.EqualTo(256));
            Assert.That(_manager.SuppressedCount, Is.EqualTo(9744));
            Assert.That(_manager.PeakActiveCount, Is.EqualTo(256));
            ExpireParticles();
            Assert.That(_manager.ActiveCount, Is.Zero);
            _show(hit);
            Assert.That(_manager.ActiveCount, Is.EqualTo(1));
            Assert.That(_manager.GetComponentsInChildren<ParticleSystem>(), Is.EqualTo(_emitters));
        }

        [Test]
        public void RandomSelectionUsesAllVariantsAndAllowsConsecutiveRepeats()
        {
            var counts = new int[4];
            int last = -1;
            bool repeated = false;
            for (int i = 0; i < 256; i++)
            {
                _show(Hit());
                for (int j = 0; j < 4; j++)
                    if (_emitters[j].particleCount != 0)
                    {
                        counts[j]++;
                        repeated |= last == j;
                        last = j;
                    }
                ExpireParticles();
            }
            foreach (int count in counts) Assert.That(count, Is.InRange(30, 100));
            Assert.That(repeated, Is.True);
        }

        [TestCase(new int[] { 1, 2, 3 })]
        [TestCase(new int[] { 2 })]
        [TestCase(new int[] { -1, 1, 2, 3 })]
        [TestCase(new int[] { 3, 2, 1, 0, 3, 1 })]
        public void RemovedOrEmptyVariantsKeepRemainingEffectsAndLifecycleWorking(int[] indices)
        {
            var manager = CreateConfiguredManager(indices);
            Assert.That(manager.Initialize(), Is.True);
            var emitters = manager.GetComponentsInChildren<ParticleSystem>();
            int expectedCount = 0;
            foreach (int index in indices)
                if (index >= 0) expectedCount++;
            Assert.That(manager.ConfiguredVariantCount, Is.EqualTo(expectedCount));
            Assert.That(manager.EmitterCount, Is.EqualTo(expectedCount));
            int emitterIndex = 0;
            foreach (int index in indices)
                if (index >= 0)
                    Assert.That(emitters[emitterIndex++].GetComponent<ParticleSystemRenderer>().sharedMaterial,
                        Is.SameAs(_emitters[index].GetComponent<ParticleSystemRenderer>().sharedMaterial));

            var show = (Action<CombatDamageApplied>)Delegate.CreateDelegate(
                typeof(Action<CombatDamageApplied>), manager, HandleHit);
            CombatDamageApplied hit = Hit();
            show(hit);
            foreach (var emitter in emitters) emitter.Simulate(0.6f, false, false, false);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) show(hit);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(manager.ActiveCount, Is.EqualTo(128));
            foreach (var emitter in emitters) Assert.That(emitter.particleCount, Is.GreaterThan(0));

            Assert.That(_combat.PauseRun(), Is.True);
            manager.SynchronizePresentation();
            foreach (var emitter in emitters) Assert.That(emitter.isPaused, Is.True);
            Assert.That(_combat.ResumeRun(), Is.True);
            manager.SynchronizePresentation();
            foreach (var emitter in emitters) Assert.That(emitter.isPlaying, Is.True);
            _combat.TryAbortRun(RunEndReason.UserExit, default, out _);
            Assert.That(manager.ActiveCount, Is.Zero);
        }

        [Test]
        public void EmptyMaterialOverrideUsesPrefabSharedMaterial()
        {
            var manager = CreateConfiguredManager(new[] { 2 }, true);
            Assert.That(manager.Initialize(), Is.True);
            Assert.That(manager.EmitterCount, Is.EqualTo(1));
            var settings = new SerializedObject(manager);
            var source = (ParticleSystem)settings.FindProperty("effectVariants")
                .GetArrayElementAtIndex(0).FindPropertyRelative("prefab").objectReferenceValue;
            Assert.That(manager.GetComponentInChildren<ParticleSystemRenderer>().sharedMaterial,
                Is.SameAs(source.GetComponent<ParticleSystemRenderer>().sharedMaterial));
            HandleHit.Invoke(manager, new object[] { Hit() });
            Assert.That(manager.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void LegacyArraysMigrateOnceAndClearingVariantsDoesNotRestoreThem()
        {
            var settings = new SerializedObject(_manager);
            var variants = settings.FindProperty("effectVariants");
            var oldPrefabs = new ParticleSystem[3];
            var oldMaterials = new Material[3];
            for (int i = 0; i < 3; i++)
            {
                var entry = variants.GetArrayElementAtIndex(i + 1);
                oldPrefabs[i] = (ParticleSystem)entry.FindPropertyRelative("prefab").objectReferenceValue;
                oldMaterials[i] = (Material)entry.FindPropertyRelative("materialOverride").objectReferenceValue;
            }
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(CursorHitEffectManager).GetField("effectVariants", flags).SetValue(_manager, null);
            typeof(CursorHitEffectManager).GetField("effectPrefabs", flags).SetValue(_manager, oldPrefabs);
            typeof(CursorHitEffectManager).GetField("effectMaterials", flags).SetValue(_manager, oldMaterials);
            _manager.OnAfterDeserialize();
            Assert.That(_manager.Initialize(), Is.True);
            Assert.That(_manager.EmitterCount, Is.EqualTo(3));
            var emitters = _manager.GetComponentsInChildren<ParticleSystem>();
            for (int i = 0; i < 3; i++)
                Assert.That(emitters[i].GetComponent<ParticleSystemRenderer>().sharedMaterial, Is.SameAs(oldMaterials[i]));
            settings.Update();
            settings.FindProperty("effectVariants").ClearArray();
            settings.ApplyModifiedPropertiesWithoutUndo();
            _manager.OnAfterDeserialize();
            Assert.That(_manager.Initialize(), Is.False);
            Assert.That(_manager.ConfiguredVariantCount, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(3)]
        public void NoConfiguredVariantsDisablePresentationWithoutCreatingEmitters(int count)
        {
            var indices = new int[count];
            for (int i = 0; i < count; i++) indices[i] = -1;
            var manager = CreateConfiguredManager(indices);
            Assert.That(manager.Initialize(), Is.False);
            Assert.That(manager.ConfiguredVariantCount, Is.Zero);
            Assert.That(manager.EmitterCount, Is.Zero);
            HandleHit.Invoke(manager, new object[] { Hit() });
            manager.SynchronizePresentation();
            Assert.That(manager.ActiveCount, Is.Zero);
            Assert.That(manager.transform.childCount, Is.Zero);
        }

        private CursorHitEffectManager CreateConfiguredManager(int[] indices, bool usePrefabMaterial = false)
        {
            var container = new GameObject("ConfiguredEffects");
            container.transform.SetParent(_root.transform);
            container.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var manager = Object.Instantiate(prefab, container.transform).GetComponent<CursorHitEffectManager>();
            var settings = new SerializedObject(manager);
            var variants = settings.FindProperty("effectVariants");
            var selectedPrefabs = new Object[indices.Length];
            var selectedMaterials = new Object[indices.Length];
            for (int i = 0; i < indices.Length; i++)
                if (indices[i] >= 0)
                {
                    var entry = variants.GetArrayElementAtIndex(indices[i]);
                    selectedPrefabs[i] = entry.FindPropertyRelative("prefab").objectReferenceValue;
                    selectedMaterials[i] = entry.FindPropertyRelative("materialOverride").objectReferenceValue;
                }
            variants.arraySize = indices.Length;
            for (int i = 0; i < indices.Length; i++)
            {
                var entry = variants.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("prefab").objectReferenceValue = selectedPrefabs[i];
                entry.FindPropertyRelative("materialOverride").objectReferenceValue =
                    usePrefabMaterial ? null : selectedMaterials[i];
            }
            settings.FindProperty("combatRunController").objectReferenceValue = _combat;
            settings.ApplyModifiedPropertiesWithoutUndo();
            container.SetActive(true);
            return manager;
        }

        [Test]
        public void EmittingAfterWarmupAllocatesNoManagedMemory()
        {
            CombatDamageApplied hit = Hit();
            _show(hit);
            ExpireParticles();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) _show(hit);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(_manager.ActiveCount, Is.EqualTo(128));
        }

        [Test]
        public void PauseResumeAndAbortRejectOldRun()
        {
            _show(Hit());
            Assert.That(_combat.PauseRun(), Is.True);
            _manager.SynchronizePresentation();
            foreach (var emitter in _emitters) Assert.That(emitter.isPaused, Is.True);
            _show(Hit());
            Assert.That(_manager.EmittedCount, Is.EqualTo(1));
            Assert.That(_combat.ResumeRun(), Is.True);
            _manager.SynchronizePresentation();
            foreach (var emitter in _emitters) Assert.That(emitter.isPlaying, Is.True);
            RunId oldRun = _combat.CurrentRunId;
            _combat.TryAbortRun(RunEndReason.UserExit, default, out _);
            Assert.That(_manager.ActiveCount, Is.Zero);
            StartRun("next-run");
            _show(Hit(run: oldRun));
            Assert.That(_manager.EmittedCount, Is.Zero);
            _show(Hit());
            Assert.That(_manager.EmittedCount, Is.EqualTo(1));
        }

        [Test]
        public void LethalHitCapturesBodyBeforeDeathAndPreservesTextAnchor()
        {
            var target = new FakeTarget { RunId = _combat.CurrentRunId, Health = 3L };
            CombatDamageApplied captured = default;
            _combat.DamageApplied += hit => captured = hit;
            ApplyHit.Invoke(_combat, new object[] { target, 0L, CombatDamageSource.CursorAttack });
            Assert.That(captured.Source, Is.EqualTo(CombatDamageSource.CursorAttack));
            Assert.That(captured.EffectiveDamage, Is.EqualTo(3L));
            Assert.That(captured.WorldPosition, Is.EqualTo(new Vector3(1f, 9f, 0f)));
            Assert.That(captured.HitEffectPosition, Is.EqualTo(new Vector3(1f, 2f, 0f)));
            Assert.That(captured.HitEffectSizeWorldUnits, Is.EqualTo(2f));
            Assert.That(_manager.EmittedCount, Is.EqualTo(1));
            ApplyHit.Invoke(_combat, new object[] { target, 0L, CombatDamageSource.CursorAttack });
            Assert.That(_manager.EmittedCount, Is.EqualTo(1));
        }

        [Test]
        public void SweepAndDuplicateCollidersEmitOncePerTargetCooldown()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPath);
            var target = Object.Instantiate(prefab, _root.transform).GetComponent<MonsterCombatTarget>();
            target.Initialize(new SpawnSnapshot("test", "test", 10000L, 1f, 1, 80, 0L), _combat.CurrentRunId);
            var extra = target.gameObject.AddComponent<BoxCollider2D>();
            extra.offset = new Vector2(0f, 0.5f);
            var cursorObject = new GameObject("TestCursor");
            cursorObject.transform.SetParent(_root.transform);
            var cursor = cursorObject.AddComponent<CircleCollider2D>();
            cursor.radius = 0.25f;
            cursor.transform.position = new Vector3(-5f, 0.5f, 0f);
            _combat.TryAttack(cursor);
            cursor.transform.position = new Vector3(5f, 0.5f, 0f);
            _combat.TryAttack(cursor);
            Assert.That(_manager.EmittedCount, Is.EqualTo(1), "Sweep hits the target between endpoints.");
            cursor.transform.position = new Vector3(0f, 0.5f, 0f);
            _combat.TryAttack(cursor);
            Assert.That(_manager.EmittedCount, Is.EqualTo(1));
            _combat.AdvanceTime(0.11f);
            _combat.TryAttack(cursor);
            Assert.That(_manager.EmittedCount, Is.EqualTo(2));
            _combat.AdvanceTime(30f);
            _combat.TryAttack(cursor);
            Assert.That(_manager.EmittedCount, Is.EqualTo(2), "No hit at the run timeout boundary.");
        }

        [Test]
        public void VisualRandomDoesNotChangeUnityOrCombatRandom()
        {
            var state = UnityEngine.Random.state;
            for (int i = 0; i < 64; i++) _show(Hit());
            Assert.That(UnityEngine.Random.state, Is.EqualTo(state));
            var otherRoot = new GameObject("ControlRun");
            try
            {
                var other = otherRoot.AddComponent<CombatRunController>();
                Assert.That(other.StartRun(new RunRequest("hit-effects", 30f),
                    new CombatSnapshot(10L, 1f, 0.1f, 1, 50f, 1f, true, 0.1f)), Is.True);
                var a = new FakeTarget { RunId = _combat.CurrentRunId };
                var b = new FakeTarget { RunId = other.CurrentRunId };
                for (int i = 0; i < 50; i++)
                {
                    ApplyHit.Invoke(_combat, new object[] { a, 0L, CombatDamageSource.CursorAttack });
                    ApplyHit.Invoke(other, new object[] { b, 0L, CombatDamageSource.CursorAttack });
                    Assert.That(a.Health, Is.EqualTo(b.Health));
                }
            }
            finally { Object.DestroyImmediate(otherRoot); }
        }

        private sealed class FakeTarget : ICombatTarget, IDamageTextAnchor, IHitEffectAnchor
        {
            public RunId RunId { get; set; }
            public long Health = 100000L;
            public bool IsActive => Health > 0L;
            public bool IsRegistered => IsActive;
            public long GarnetReward => 0L;
            public string BonusDropCurrencyId => string.Empty;
            public long BonusDropAmount => 0L;
            public float BonusDropChancePercent => 0f;
            public Vector3 DamageTextPosition => IsActive ? new Vector3(1f, 9f, 0f) : Vector3.zero;
            public bool TryGetHitEffectBounds(out Bounds bounds)
            {
                bounds = IsActive ? new Bounds(new Vector3(1f, 2f, 0f), new Vector3(1f, 2f, 0f)) : default;
                return IsActive;
            }
            public bool ApplyDamage(RunId runId, long damage, out long effectiveDamage, out bool killed)
            {
                effectiveDamage = 0L; killed = false;
                if (!IsActive || runId != RunId || damage <= 0L) return false;
                effectiveDamage = Math.Min(Health, damage);
                Health -= effectiveDamage;
                killed = Health == 0L;
                return true;
            }
        }
    }
}
