using System.Collections.Generic;
using System.Reflection;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CursorHunter.Combat.Tests
{
    public sealed class MonsterBalanceTests
    {
        private GameObject _root;
        private GameObject _monsterPrefab;
        private GameObject _visualPrefab;
        private CombatRunController _combat;
        private MonsterSpawner _spawner;
        private readonly RunRequest _request = new RunRequest("monster-balance", 30f);

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("MonsterBalanceTests");
            _combat = _root.AddComponent<CombatRunController>();
            Assert.That(_combat.StartRun(_request,
                new CombatSnapshot(10L, 1f, 0.1f, 1, 0f, 1f, true, 0.1f)), Is.True);
            Camera camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(_root.transform);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            camera.aspect = 1f;
            _spawner = _root.AddComponent<MonsterSpawner>();
            _monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CursorHunter/Combat/Prefabs/Monsters/MonsterRoot.prefab");
            _visualPrefab = new GameObject("TestSpeciesVisual");
            _visualPrefab.transform.SetParent(_root.transform);
            Set(_spawner, "worldCamera", camera);
            Set(_spawner, "combatRunController", _combat);
            Set(_spawner, "monsterRootPrefab", _monsterPrefab);
            // EditMode AddComponent does not run this MonoBehaviour's Awake.
            // Bind the owned hierarchy explicitly so spawned bodies are found
            // by the test and destroyed by TearDown instead of leaking at scene root.
            Set(_spawner, "spawnedEnemyRoot", _root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            // Destroy bodies immediately before MonsterSpawner.OnDisable in EditMode.
            foreach (MonsterCombatTarget target in _root.GetComponentsInChildren<MonsterCombatTarget>(true))
                if (target != null) Object.DestroyImmediate(target.gameObject);
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void FinalValuesPreserveBehaviorWithoutApplyingProfileMultipliersAgain()
        {
            var reference = new SpawnSnapshot("monster.a", "visual.a", 250L, 4f, 1, 80, 3L,
                "gem.red", 2L, 50f, MonsterBehaviorType.None, MonsterMovementMode.Circular,
                1.8f, 2f, 1.8f, 2.16f, 0.9f, 80f,
                MonsterSpecialAbility.Shield, 4.5f, 1.7f, 3f, 0.65f);
            MonsterBalanceSettings settings = MonsterBalanceSettings.FromSnapshot(reference);
            settings.MaxHealth = 100L;
            settings.MoveSpeed = 1f;
            Assert.That(settings.TryCreateSnapshot(reference, 12, out SpawnSnapshot actual, out string error),
                Is.True, error);
            Assert.That(actual.MaxHealth, Is.EqualTo(100L));
            Assert.That(actual.MoveSpeed, Is.EqualTo(1f));
            Assert.That(actual.AliveLimit, Is.EqualTo(12));
            Assert.That(actual.SpecialAbility, Is.EqualTo(MonsterSpecialAbility.Shield));
            Assert.That(actual.SpecialAbilityStrength, Is.EqualTo(0.65f));
            Assert.That(actual.OrbitRadius, Is.EqualTo(0.9f));
            settings.SpawnIntervalSeconds = float.NaN;
            Assert.That(settings.TryCreateSnapshot(reference, 12, out _, out _), Is.False);
        }

        [TestCase(100L, 40L, 200L, 80L)]
        [TestCase(100L, 1L, 1L, 1L)]
        [TestCase(long.MaxValue, 1L, long.MaxValue - 1L, 1L)]
        [TestCase(long.MaxValue, 4611686018427387903L, 2L, 1L)]
        [TestCase(long.MaxValue, 4611686018427387904L, 2L, 2L)]
        public void LiveHealthKeepsRatioWithoutOverflowOrDeath(long oldMax, long health,
            long newMax, long expectedHealth)
        {
            SpawnSnapshot original = Snapshot("monster.a", oldMax);
            GameObject instance = Object.Instantiate(_monsterPrefab, _root.transform);
            var target = instance.GetComponent<MonsterCombatTarget>();
            target.Initialize(original, _request.RunId);
            if (oldMax > health)
                Assert.That(target.ApplyDamage(_request.RunId, oldMax - health, out _, out _), Is.True);
            SpawnSnapshot changed = Snapshot("monster.a", newMax);
            Vector3 position = target.transform.position;
            Assert.That(target.TryApplyBalance(changed, new Bounds(Vector3.zero, Vector3.one * 20f),
                out string error), Is.True, error);
            Assert.That(target.CurrentHealth, Is.EqualTo(expectedHealth));
            Assert.That(target.MaxHealth, Is.EqualTo(newMax));
            Assert.That(target.IsDead, Is.False);
            Assert.That(target.transform.position, Is.EqualTo(position));
        }

        [Test]
        public void LiveSpawnChangesKeepBodiesActionsAndRemainingTimerFraction()
        {
            SpawnPlan initial = Plan(Snapshot("monster.a", 100L), Snapshot("monster.b", 50L));
            Assert.That(_spawner.StartRun(_request, initial, new[] { "monster.a" }).Succeeded, Is.True);
            Assert.That(_spawner.ActiveSpawnedCount, Is.EqualTo(1));
            MonsterCombatTarget target = FindSpawnedTarget("monster.a");
            var behavior = target.GetComponent<MonsterBehaviorController>();
            target.ApplyDamage(_request.RunId, 60L, out _, out _);
            Vector3 position = target.transform.position;
            float actionAt = Get<float>(behavior, "_nextActionAt");
            float abilityAt = Get<float>(behavior, "_nextSpecialAbilityAt");
            _combat.AdvanceTime(2f);
            SpawnSnapshot changedA = Snapshot("monster.a", 200L, 8f, 0.9f, 9L);
            SpawnPlan changed = Plan(changedA, Snapshot("monster.b", 50L));
            Assert.That(_spawner.TryApplyBalance(changed, new[] { "monster.a", "monster.b" },
                out string error), Is.True, error);
            Assert.That(target.CurrentHealth, Is.EqualTo(80L));
            Assert.That(target.GarnetReward, Is.EqualTo(9L));
            Assert.That(target.transform.position, Is.EqualTo(position));
            Assert.That(Get<float>(behavior, "_moveSpeed"), Is.EqualTo(0.9f));
            Assert.That(Get<float>(behavior, "_nextActionAt"), Is.EqualTo(actionAt));
            Assert.That(Get<float>(behavior, "_nextSpecialAbilityAt"), Is.EqualTo(abilityAt));
            float[] nextTimes = Get<float[]>(_spawner, "_nextSpawnTimes");
            Assert.That(nextTimes[0], Is.EqualTo(6f).Within(0.001f));
            Assert.That(nextTimes[1], Is.EqualTo(6f).Within(0.001f));
            Assert.That(_spawner.TryApplyBalance(changed, new string[0], out error), Is.True, error);
            Assert.That(_spawner.ActiveSpawnedCount, Is.EqualTo(1));
            Assert.That(_spawner.IsSpeciesEnabled("monster.a"), Is.False);
            Assert.That(_spawner.IsSpeciesEnabled("monster.b"), Is.False);
        }

        [Test]
        public void InvalidLaterSpeciesDoesNotPartiallyChangeLivingMonsterOrPlan()
        {
            SpawnPlan initial = Plan(Snapshot("monster.a", 100L), Snapshot("monster.b", 50L));
            Assert.That(_spawner.StartRun(_request, initial).Succeeded, Is.True);
            MonsterCombatTarget target = FindSpawnedTarget("monster.a");
            SpawnSnapshot invalidBounds = new SpawnSnapshot("monster.b", "visual", 50L,
                4f, 1, 80, 0L, "", 0L, 0f, hitAreaWidth: 10000f);
            Assert.That(_spawner.TryApplyBalance(
                Plan(Snapshot("monster.a", 500L), invalidBounds), null, out _), Is.False);
            Assert.That(target.MaxHealth, Is.EqualTo(100L));
            Assert.That(_spawner.CurrentSpawnPlan, Is.SameAs(initial));
            Assert.That(_spawner.TryApplyBalance(initial, new[] { "unknown" }, out _), Is.False);
        }

        [Test]
        public void EditingDeadMonsterDoesNotResurrectIt()
        {
            GameObject instance = Object.Instantiate(_monsterPrefab, _root.transform);
            var target = instance.GetComponent<MonsterCombatTarget>();
            target.Initialize(Snapshot("monster.a", 100L), _request.RunId);
            target.ApplyDamage(_request.RunId, 100L, out _, out bool killed);
            Assert.That(killed, Is.True);
            Assert.That(target.TryApplyBalance(Snapshot("monster.a", 1000L),
                new Bounds(Vector3.zero, Vector3.one * 20f), out _), Is.True);
            Assert.That(target.IsDead, Is.True);
            Assert.That(target.CurrentHealth, Is.Zero);
        }

        private SpawnPlan Plan(params SpawnSnapshot[] snapshots)
        {
            var entries = new List<SpawnPlanEntry>();
            foreach (SpawnSnapshot snapshot in snapshots)
                entries.Add(new SpawnPlanEntry(snapshot, _visualPrefab));
            return new SpawnPlan(entries);
        }

        private MonsterCombatTarget FindSpawnedTarget(string monsterId)
        {
            foreach (MonsterCombatTarget target in _root.GetComponentsInChildren<MonsterCombatTarget>(true))
                if (target.MonsterId == monsterId) return target;
            Assert.Fail("Expected an owned spawned body for " + monsterId);
            return null;
        }

        private static SpawnSnapshot Snapshot(string id, long hp, float interval = 4f,
            float speed = 0.45f, long reward = 0L) => new SpawnSnapshot(id, "visual", hp,
                interval, 1, 80, reward, "", 0L, 0f, moveSpeed: speed);

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        private static T Get<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
