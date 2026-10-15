using System.Collections;
using System.Reflection;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.Combat.Tests
{
    public sealed class TornadoSkillPlayModeTests
    {
        private Scene _sandboxScene;

        [UnityTearDown]
        public IEnumerator UnloadSandbox()
        {
            if (_sandboxScene.IsValid() && _sandboxScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_sandboxScene);
        }

        [UnityTest]
        public IEnumerator SceneInspectorAcquisitionAndBaseBalanceRunLockedSkill()
        {
#if UNITY_EDITOR
            _sandboxScene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Combat/Scenes/CombatSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;

            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            TornadoSkillController controller =
                sandbox.GetComponentInChildren<TornadoSkillController>(true);
            Assert.That(controller, Is.Not.Null);
            typeof(TornadoSkillController).GetField("editorSkillAcquired",
                BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, true);
            SetBaseStat(controller, "Count", 3);
            combat.ConfigureSkills(
                new[] { new SkillCombatSnapshot(TornadoSkillController.SkillId, false) });

            combat.AdvanceTime(1.05f);
            yield return null;
            Assert.That(controller.ActiveObjectCount, Is.EqualTo(3));
            combat.ConfigureSkills(null);
            combat.AdvanceTime(1.05f);
            yield return null;
            Assert.That(controller.ActiveObjectCount, Is.EqualTo(3),
                "The Inspector acquisition should work without a progression skill list.");
            TornadoObject tornado = null;
            foreach (TornadoObject candidate in controller.GetComponentsInChildren<TornadoObject>(true))
            {
                if (!candidate.IsActive) continue;
                tornado = candidate;
                break;
            }
            Assert.That(tornado, Is.Not.Null);
            float capturedMoveSpeed = CapturedMoveSpeed(controller);
            SetBaseStat(controller, "MoveSpeedWorldUnitsPerSecond", 0f);
            Vector3 before = tornado.transform.position;
            combat.AdvanceTime(0.2f);
            yield return null;
            Assert.That(CapturedMoveSpeed(controller),
                Is.EqualTo(capturedMoveSpeed));
            Assert.That(Vector3.Distance(tornado.transform.position, before),
                Is.GreaterThan(0.01f),
                "An Inspector edit must not change the active run snapshot.");
            SetBaseStat(controller, "MoveSpeedWorldUnitsPerSecond", 2f);
            combat.AdvanceTime(0.2f);
            yield return null;
            Assert.That(CapturedMoveSpeed(controller),
                Is.EqualTo(capturedMoveSpeed));
            combat.AdvanceTime(4.05f);
            yield return null;
            Assert.That(tornado.transform.localScale.x, Is.InRange(0.3f, 0.8f),
                "The tornado should shrink during its final second.");
            typeof(TornadoSkillController).GetField("editorSkillAcquired",
                BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, false);
            combat.ConfigureSkills(
                new[] { new SkillCombatSnapshot(TornadoSkillController.SkillId, false) });
            Assert.That(controller.ActiveObjectCount, Is.Zero,
                "A locked skill must stop once the Editor acquisition toggle is off.");
            Assert.That(tornado.transform.localScale, Is.EqualTo(Vector3.one),
                "Returning to the pool should restore the prefab scale.");
            combat.TryAbortRun(RunEndReason.UserExit, default, out _);
#else
            Assert.Ignore("Inspector acquisition is available only in the Unity Editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator IndependentTornadoesStackHitsPauseAndReturnToPool()
        {
#if UNITY_EDITOR
            _sandboxScene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Combat/Scenes/CombatSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;

            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            Assert.That(sandbox, Is.Not.Null);
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            typeof(CursorHitEffectSandbox).GetField("_autoHit",
                BindingFlags.NonPublic | BindingFlags.Instance).SetValue(sandbox, false);
            TornadoSkillController controller =
                sandbox.GetComponentInChildren<TornadoSkillController>(true);
            Assert.That(controller, Is.Not.Null,
                "The controller must already be placed in the scene.");
            SetBaseStat(controller, "Count", 2);
            SetBaseStat(controller, "MoveSpeedWorldUnitsPerSecond", 0f);
            combat.ConfigureSkills(
                new[] { new SkillCombatSnapshot(TornadoSkillController.SkillId,
                    true, new[] { new SkillStatUpgrade("count", 1f) }) });
            Assert.That(sandbox.GetComponentsInChildren<TornadoSkillController>(true),
                Has.Length.EqualTo(1), "Starting a run must reuse the scene controller.");

            combat.AdvanceTime(1.05f);
            yield return null;
            Assert.That(controller.ActiveObjectCount, Is.EqualTo(3));
            TornadoObject[] objects = controller.GetComponentsInChildren<TornadoObject>(true);
            MonsterCombatTarget target = sandbox.GetComponentInChildren<MonsterCombatTarget>();
            Assert.That(target, Is.Not.Null);
            target.enabled = false;
            BoxCollider2D targetHitbox = target.transform.Find("HitArea").GetComponent<BoxCollider2D>();
            targetHitbox.size = new Vector2(0.1f, 0.1f);
            Physics2D.SyncTransforms();
            int activeCount = 0;
            foreach (TornadoObject tornado in objects)
            {
                if (!tornado.IsActive) continue;
                BoxCollider2D hitbox = tornado.transform.Find("HitArea").GetComponent<BoxCollider2D>();
                Assert.That(hitbox.isTrigger, Is.True);
                Assert.That(hitbox.size, Is.EqualTo(new Vector2(3f, 3f)));
                // Inside the square corner, outside the former radius 1.5 circle.
                tornado.transform.position = targetHitbox.bounds.center -
                    new Vector3(1.3f, 1.3f, 0f);
                activeCount++;
            }
            Assert.That(activeCount, Is.EqualTo(3));
            Physics2D.SyncTransforms();

            int skillHits = 0;
            combat.DamageApplied += hit =>
            {
                if (hit.Source == CombatDamageSource.Skill && hit.Target == target)
                    skillHits++;
            };
            combat.AdvanceTime(0.25f);
            yield return null;
            Assert.That(skillHits, Is.Zero, "The next hit should wait for the 0.5 second interval.");
            combat.AdvanceTime(0.3f);
            yield return null;
            Assert.That(skillHits, Is.EqualTo(3),
                "Each overlapping tornado should hit after 0.5 seconds.");
            combat.AdvanceTime(0.25f);
            yield return null;
            Assert.That(skillHits, Is.EqualTo(3));
            combat.AdvanceTime(0.3f);
            yield return null;
            Assert.That(skillHits, Is.EqualTo(6));

            Assert.That(combat.PauseRun(), Is.True);
            combat.AdvanceTime(2f);
            yield return null;
            Assert.That(skillHits, Is.EqualTo(6));
            Assert.That(combat.ResumeRun(), Is.True);
            combat.AdvanceTime(0.55f);
            yield return null;
            Assert.That(skillHits, Is.EqualTo(9));

            Assert.That(combat.TryAbortRun(RunEndReason.UserExit, default, out _), Is.True);
            yield return null;
            Assert.That(controller.ActiveObjectCount, Is.Zero);
            Assert.That(controller.PooledObjectCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(skillHits, Is.EqualTo(9));
#else
            Assert.Ignore("The sandbox scene is loaded through the Editor PlayMode runner.");
            yield break;
#endif
        }

        private static float CapturedMoveSpeed(TornadoSkillController controller)
        {
            PropertyInfo property = typeof(TornadoSkillController).GetProperty(
                "MoveSpeedWorldUnitsPerSecond",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null);
            return (float)property.GetValue(controller);
        }

        private static void SetBaseStat(TornadoSkillController controller,
            string name, object value)
        {
            FieldInfo field = typeof(TornadoSkillController).GetField("baseStat",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            object basis = field.GetValue(controller);
            FieldInfo statField = basis.GetType().GetField(name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(statField, Is.Not.Null, name);
            statField.SetValue(basis, value);
            field.SetValue(controller, basis);
        }
    }
}
