using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.Combat.Tests
{
    public sealed class BossSkillCompletionPlayModeTests
    {
        private Scene _scene;
        private CombatRunController _combat;
        private MonsterCombatTarget _boss;
        private GameObject _cursor;
        private Sprite _sprite;
        private RunResult _result;
        private int _completions;

        [UnitySetUp]
        public IEnumerator Setup()
        {
#if UNITY_EDITOR
            _scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Combat/Scenes/CombatSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            sandbox.enabled = false;
            _combat = sandbox.GetComponent<CombatRunController>();
            _combat.enabled = false;
            _combat.TryAbortRun(RunEndReason.UserExit, RunSettlementPolicy.Discard, out _);
            foreach (MonoBehaviour skill in _combat.GetComponentsInChildren<MonoBehaviour>(true)
                         .Where(x => x is IRunSkillController))
            {
                skill.enabled = false; // Drive actual LateUpdate deterministically below.
                Set(skill, "editorSkillAcquired", false);
            }
            var targets = sandbox.GetComponentsInChildren<MonsterCombatTarget>();
            _boss = targets[0];
            foreach (MonsterCombatTarget target in targets)
            {
                target.enabled = false;
                var behavior = target.GetComponent<MonsterBehaviorController>();
                if (behavior != null) behavior.enabled = false;
                target.gameObject.SetActive(target == _boss);
            }
            _boss.transform.position = new Vector3(1000, 1000, 0);
            _cursor = new GameObject("Boss skill test cursor");
            var circle = _cursor.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 1;
            var renderer = _cursor.AddComponent<SpriteRenderer>();
            _sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
                new Vector2(.5f, .5f), 1);
            renderer.sprite = _sprite;
            var flame = _combat.GetComponentInChildren<FlameExhaustSkillController>(true);
            Set(flame, "sourceCursorHitArea", circle);
            Set(flame, "sourceCursorVisual", renderer);
            _completions = 0;
            _result = default;
            _combat.CompletionRequested += reason =>
            {
                _completions++;
                Assert.That(_combat.TryCompleteRun(reason, RunSettlementPolicy.Eligible,
                    out _result), Is.True);
                _boss.gameObject.SetActive(false); // Same synchronous cleanup as App.
            };
#else
            Assert.Ignore("Requires the Editor PlayMode scene loader.");
            yield break;
#endif
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_combat != null)
                _combat.TryAbortRun(RunEndReason.UserExit, RunSettlementPolicy.Discard, out _);
            if (_cursor != null) Object.Destroy(_cursor);
            if (_sprite != null) Object.Destroy(_sprite);
            if (_scene.IsValid() && _scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_scene);
        }

        [TestCase("initial")]
        [TestCase("overlap")]
        [TestCase("sweep")]
        public void FlameLethalBossHitStopsWithoutReusingReturnedProjectiles(string path)
        {
            var skill = _combat.GetComponentInChildren<FlameExhaustSkillController>(true);
            SetStat(skill, "CooldownSeconds", .1f);
            SetStat(skill, "ProjectileDurationSeconds", 5f);
            SetStat(skill, "SpeedWorldUnitsPerSecond", path == "overlap" ? 0f : 8f);
            StartBoss(FlameExhaustSkillController.SkillId);
            int hitPoolSize = skill.PooledHitEffectCount;
            if (path == "initial") PlaceBoss(_cursor.transform.position);
            Tick(skill);
            if (path != "initial")
            {
                _combat.AdvanceTime(.11f);
                Tick(skill);
                Assert.That(skill.ActiveProjectileCount, Is.EqualTo(2));
                var projectiles = Get<List<FlameExhaustProjectile>>(skill, "_activeProjectiles");
                var projectile = projectiles[projectiles.Count - 1];
                Vector2 point = projectile.GetComponent<CircleCollider2D>().bounds.center;
                if (path == "sweep") point += Get<Vector2>(projectile, "_direction") * .5f;
                PlaceBoss(point);
                _combat.AdvanceTime(.1f);
                Tick(skill);
            }
            AssertCompleted();
            Assert.That(skill.ActiveProjectileCount, Is.Zero);
            Assert.That(skill.ActiveHitEffectCount, Is.Zero);
            Assert.That(skill.PooledHitEffectCount, Is.EqualTo(hitPoolSize));
            var free = Get<Stack<FlameExhaustProjectile>>(skill, "_freeProjectiles");
            Assert.That(free.Count, Is.EqualTo(free.Distinct().Count()), "No duplicate pool returns.");
            Assert.That(free.Count, Is.EqualTo(skill.GetComponentsInChildren<FlameExhaustProjectile>(true).Length));
            foreach (var projectile in free)
                Assert.That(Get<HashSet<MonsterCombatTarget>>(projectile, "_hitTargets"), Is.Empty);

            // A subsequent run must still be able to hit and show its own VFX.
            StartBoss(FlameExhaustSkillController.SkillId, 10000);
            PlaceBoss(_cursor.transform.position);
            Tick(skill);
            Assert.That(_combat.IsRunning, Is.True);
            Assert.That(_boss.CurrentHealth, Is.LessThan(10000));
            Assert.That(skill.ActiveHitEffectCount, Is.EqualTo(1));
            Assert.That(_completions, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(HeadshotSkillController.SkillId)]
        [TestCase(ThunderSkillController.SkillId)]
        public void DelayedLethalHitReturnsAllConcurrentEffectsOnce(string id)
        {
            MonoBehaviour skill = Skill(id);
            SetStat(skill, "CooldownSeconds", .1f);
            SetStat(skill, "EffectDurationSeconds", 5f);
            string delay = id == HeadshotSkillController.SkillId
                ? "ExplosionDelaySeconds" : "ImpactDelaySeconds";
            SetStat(skill, delay, 1f);
            StartBoss(id);
            Tick(skill);
            _combat.AdvanceTime(.11f);
            Tick(skill);
            Assert.That(((ICollection)Get<object>(skill, "_active")).Count, Is.EqualTo(2));
            _combat.AdvanceTime(1.1f);
            Tick(skill);
            AssertCompleted();
            Assert.That(((ICollection)Get<object>(skill, "_active")).Count, Is.Zero);
            var free = ((IEnumerable)Get<object>(skill, "_free")).Cast<object>().ToArray();
            Assert.That(free.Length, Is.EqualTo(free.Distinct().Count()));
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LaserLethalHitStopsBeforeTouchingReturnedBeam(bool initialHit)
        {
            var skill = _combat.GetComponentInChildren<LaserSkillController>(true);
            SetStat(skill, "CooldownSeconds", .1f);
            SetStat(skill, "EffectDurationSeconds", 5f);
            StartBoss(LaserSkillController.SkillId);
            if (initialHit)
            {
                PlaceBoss(Vector2.zero);
                _boss.transform.localScale = Vector3.one * 100;
                Physics2D.SyncTransforms();
            }
            Tick(skill);
            if (!initialHit)
            {
                _combat.AdvanceTime(.11f);
                Tick(skill);
                Assert.That(skill.ActiveEffectCount, Is.EqualTo(2));
                var beams = Get<List<LaserEffect>>(skill, "_active");
                PlaceBoss(Get<BoxCollider2D>(beams[beams.Count - 1], "hitbox").bounds.center);
                Tick(skill);
            }
            AssertCompleted();
            Assert.That(skill.ActiveEffectCount, Is.Zero);
            var free = Get<Stack<LaserEffect>>(skill, "_free");
            Assert.That(free.Count, Is.EqualTo(free.Distinct().Count()));
            foreach (var beam in free)
                Assert.That(Get<HashSet<MonsterCombatTarget>>(beam, "_hitTargets"), Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        private void StartBoss(string id, long health = 1)
        {
            Skill(id).enabled = true;
            SetStat(Skill(id), "FirstCastDelaySeconds", 0f);
            var request = new RunRequest(RunId.Create(), 4, 1, RunMode.Boss, "boss.02", 2, 30, 2);
            Assert.That(_combat.StartRun(request, new CombatSnapshot(1000, 1, .5f, 1)), Is.True);
            _boss.gameObject.SetActive(true);
            _boss.Initialize(new SpawnSnapshot("boss.02", "test", health, 31, 1, 1, 1300), request.RunId);
            Assert.That(_combat.ConfigureSkills(new[] { new SkillCombatSnapshot(id, true) }), Is.True);
            Physics2D.SyncTransforms();
        }

        private void PlaceBoss(Vector2 point)
        {
            Physics2D.SyncTransforms();
            Assert.That(_boss.TryGetHitEffectBounds(out Bounds bounds), Is.True);
            _boss.transform.position += (Vector3)point - bounds.center;
            Physics2D.SyncTransforms();
        }

        private void AssertCompleted()
        {
            Assert.That(_completions, Is.EqualTo(1));
            Assert.That(_result.BossDefeated, Is.True);
            Assert.That(_result.BossStage, Is.EqualTo(2));
            Assert.That(_result.DefeatedCount, Is.EqualTo(1));
            Assert.That(_result.GarnetEarned, Is.EqualTo(1300));
            Assert.That(_combat.IsRunActive, Is.False);
        }

        private MonoBehaviour Skill(string id) => _combat.GetComponentsInChildren<MonoBehaviour>(true)
            .Single(x => x is IRunSkillController skill && skill.Id == id);
        private static void Tick(object skill) => skill.GetType().GetMethod("LateUpdate",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(skill, null);
        private static T Get<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void SetStat(object target, string name, float value)
        {
            object stat = Get<object>(target, "baseStat");
            stat.GetType().GetField(name).SetValue(stat, value);
            Set(target, "baseStat", stat);
        }
    }
}
