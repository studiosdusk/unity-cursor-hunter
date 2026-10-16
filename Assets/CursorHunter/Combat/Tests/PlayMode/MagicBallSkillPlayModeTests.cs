using System.Collections;
using System.Reflection;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.Combat.Tests
{
    public sealed class MagicBallSkillPlayModeTests
    {
        private Scene _sandboxScene;

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            if (_sandboxScene.IsValid() && _sandboxScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_sandboxScene);
        }

        [UnityTest]
        public IEnumerator OneBallSweepsTargetsBouncesAndStopsWithRun()
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
            MagicBallSkillController controller =
                sandbox.GetComponentInChildren<MagicBallSkillController>(true);
            Assert.That(controller, Is.Not.Null);
            SetField(sandbox, "_autoHit", false);
            var directions = new Vector2[8];
            for (int i = 0; i < directions.Length; i++) directions[i] = Vector2.right;
            SetField(controller, "startingDirections", directions);

            MonsterCombatTarget[] targets =
                sandbox.GetComponentsInChildren<MonsterCombatTarget>();
            Assert.That(targets.Length, Is.GreaterThanOrEqualTo(1));
            for (int i = 1; i < targets.Length; i++)
                targets[i].gameObject.SetActive(false);
            targets[0].enabled = false;
            Assert.That(combat.ConfigureSkills(new[]
            {
                new SkillCombatSnapshot(MagicBallSkillController.SkillId, true)
            }), Is.True);

            int hits = 0;
            combat.DamageApplied += applied =>
            {
                if (applied.Source == CombatDamageSource.Skill &&
                    ReferenceEquals(applied.Target, targets[0])) hits++;
            };
            combat.AdvanceTime(0.01f);
            yield return null;
            MagicBallObject ball = controller.GetComponentInChildren<MagicBallObject>(true);
            Assert.That(ball, Is.Not.Null);
            Assert.That(ball.IsActive, Is.True);
            CircleCollider2D boundaryCollider = ball.GetComponent<CircleCollider2D>();
            CircleCollider2D attackHitbox = GetField<CircleCollider2D>(ball, "hitbox");
            Assert.That(boundaryCollider.isTrigger, Is.True);
            Assert.That(attackHitbox.transform.parent, Is.EqualTo(ball.transform));
            Assert.That(attackHitbox, Is.Not.EqualTo(boundaryCollider));
            Assert.That(attackHitbox.bounds.extents.x,
                Is.GreaterThan(boundaryCollider.bounds.extents.x));
            Assert.That(ball.GetComponentInChildren<ParticleSystem>(), Is.Not.Null);
            Camera camera = Camera.main;
            float distance = Mathf.Abs(camera.transform.position.z);
            float horizontalPadding = GetField<float>(controller, "horizontalViewportPadding");
            float bottomPadding = GetField<float>(controller, "bottomViewportPadding");
            float topPadding = GetField<float>(controller, "topViewportPadding");
            float edgePadding = GetField<float>(controller, "fieldEdgePaddingWorldUnits");
            Vector3 bottomLeft = camera.ViewportToWorldPoint(new Vector3(
                horizontalPadding, bottomPadding, distance));
            Vector3 topRight = camera.ViewportToWorldPoint(new Vector3(
                1f - horizontalPadding, 1f - topPadding, distance));
            Rect fieldBounds = Rect.MinMaxRect(
                bottomLeft.x + edgePadding, bottomLeft.y + edgePadding,
                topRight.x - edgePadding, topRight.y - edgePadding);
            Assert.That(Vector2.Distance(ball.transform.position, fieldBounds.center),
                Is.GreaterThan(0.01f), "The ball should spawn at a seeded random position.");
            Assert.That(boundaryCollider.bounds.min.x, Is.GreaterThanOrEqualTo(fieldBounds.xMin - 0.001f));
            Assert.That(boundaryCollider.bounds.max.x, Is.LessThanOrEqualTo(fieldBounds.xMax + 0.001f));
            Assert.That(boundaryCollider.bounds.min.y, Is.GreaterThanOrEqualTo(fieldBounds.yMin - 0.001f));
            Assert.That(boundaryCollider.bounds.max.y, Is.LessThanOrEqualTo(fieldBounds.yMax + 0.001f));

            ball.transform.position = new Vector3(fieldBounds.center.x, fieldBounds.center.y, 0f);
            // The target is outside the root wall circle but inside the child attack circle.
            targets[0].transform.position = ball.transform.position +
                new Vector3(2f, 1.1f, 0f);
            Physics2D.SyncTransforms();

            combat.AdvanceTime(0.35f);
            yield return null;
            Assert.That(hits, Is.EqualTo(1));
            combat.AdvanceTime(0.01f);
            yield return null;
            Assert.That(hits, Is.EqualTo(1));

            Assert.That(combat.PauseRun(), Is.True);
            Vector3 pausedPosition = ball.transform.position;
            combat.AdvanceTime(5f);
            yield return null;
            Assert.That(ball.transform.position, Is.EqualTo(pausedPosition));
            Assert.That(combat.ResumeRun(), Is.True);

            float radius = boundaryCollider.bounds.extents.x;
            ball.transform.position = new Vector3(fieldBounds.xMax - radius - 0.05f, 0f, 0f);
            Vector2 incoming = new Vector2(1f, 0.4f).normalized;
            SetField(ball, "_direction", incoming);
            combat.AdvanceTime(0.1f);
            yield return null;
            Vector2 reflected = GetField<Vector2>(ball, "_direction");
            Vector2 expected = Vector2.Reflect(incoming, Vector2.left);
            Assert.That(reflected.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(reflected.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(boundaryCollider.bounds.max.x, Is.LessThanOrEqualTo(fieldBounds.xMax + 0.001f));
            Assert.That(controller.GetComponentsInChildren<MagicBallObject>(true).Length,
                Is.EqualTo(1));

            combat.TryAbortRun(RunEndReason.UserExit, default, out _);
            Assert.That(ball.IsActive, Is.False);
#else
            Assert.Ignore("The sandbox scene is loaded through the Editor PlayMode runner.");
            yield break;
#endif
        }

        private static T GetField<T>(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(instance);
        }

        private static void SetField(object instance, string name, object value)
        {
            FieldInfo field = instance.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(instance, value);
        }
    }
}
