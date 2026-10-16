using System.Collections;
using System.Reflection;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.Combat.Tests
{
    public sealed class LaserSkillPlayModeTests
    {
        private Scene _sandboxScene;

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            if (_sandboxScene.IsValid() && _sandboxScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_sandboxScene);
        }

        [UnityTest]
        public IEnumerator BeamHitsEveryEntrantOnceAndReturnsToPool()
        {
#if UNITY_EDITOR
            _sandboxScene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Combat/Scenes/CombatSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;

            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            LaserSkillController controller =
                sandbox.GetComponentInChildren<LaserSkillController>(true);
            Assert.That(controller, Is.Not.Null);
            SetField(sandbox, "_autoHit", false);
            MonsterCombatTarget[] targets =
                sandbox.GetComponentsInChildren<MonsterCombatTarget>();
            Assert.That(targets.Length, Is.GreaterThanOrEqualTo(3));
            for (int i = 3; i < targets.Length; i++)
                targets[i].gameObject.SetActive(false);
            for (int i = 0; i < 3; i++)
            {
                targets[i].enabled = false;
                targets[i].transform.position = new Vector3(-100f - i, 0f, 0f);
            }
            Assert.That(combat.ConfigureSkills(new[]
            {
                new SkillCombatSnapshot(LaserSkillController.SkillId, true,
                    new[] { new SkillStatUpgrade("sizeMultiplier", 0.5f) })
            }), Is.True);

            var hits = new int[3];
            combat.DamageApplied += applied =>
            {
                if (applied.Source != CombatDamageSource.Skill) return;
                for (int i = 0; i < hits.Length; i++)
                    if (ReferenceEquals(applied.Target, targets[i])) hits[i]++;
            };
            combat.AdvanceTime(0.01f);
            yield return null;
            Assert.That(controller.ActiveEffectCount, Is.EqualTo(1));
            LaserEffect beam = controller.GetComponentInChildren<LaserEffect>(false);
            Assert.That(beam, Is.Not.Null);
            ParticleSystem visual = beam.GetComponentInChildren<ParticleSystem>();
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.isPlaying, Is.True);
            BoxCollider2D hitbox = beam.GetComponentInChildren<BoxCollider2D>();
            Assert.That(hitbox.isTrigger, Is.True);
            Camera camera = Camera.main;
            float distance = Mathf.Abs(camera.transform.position.z);
            Vector3 bottom = camera.ViewportToWorldPoint(new Vector3(0.5f, 0f, distance));
            Vector3 top = camera.ViewportToWorldPoint(new Vector3(0.5f, 1f, distance));
            Assert.That(hitbox.bounds.size.x, Is.EqualTo(7.5f * 1.5f *
                (top.y - bottom.y) / 24f).Within(0.01f));
            Assert.That(hitbox.bounds.size.y, Is.EqualTo(top.y - bottom.y).Within(0.01f));
            Transform visualTransform = beam.transform.Find("VisualAnchor/Lazer_normal_Visual");
            Assert.That(visualTransform, Is.Not.Null);
            Assert.That(beam.transform.Find("VisualAnchor").localScale,
                Is.EqualTo(Vector3.one));
            Assert.That(visualTransform.localScale.y,
                Is.EqualTo((top.y - bottom.y) / 24f * 3f).Within(0.01f));
            Assert.That(visualTransform.localScale.x,
                Is.EqualTo((top.y - bottom.y) / 24f * 3f * 1.5f).Within(0.01f));
            ParticleSystemRenderer visualRenderer = visual.GetComponent<ParticleSystemRenderer>();
            Assert.That(visualRenderer.bounds.min.y, Is.LessThanOrEqualTo(bottom.y));
            Assert.That(visualRenderer.bounds.max.y, Is.GreaterThanOrEqualTo(top.y));

            targets[0].transform.position = new Vector3(beam.transform.position.x, -1f, 0f);
            targets[1].transform.position = new Vector3(beam.transform.position.x, 1f, 0f);
            targets[2].transform.position = new Vector3(beam.transform.position.x + 4f, 0f, 0f);
            GameObject extraHitbox = new GameObject("ExtraHitBox");
            extraHitbox.transform.SetParent(targets[0].transform, false);
            extraHitbox.AddComponent<BoxCollider2D>().isTrigger = true;
            combat.AdvanceTime(0.1f);
            yield return null;
            Assert.That(hits, Is.EqualTo(new[] { 1, 1, 0 }));

            targets[2].transform.position = new Vector3(beam.transform.position.x, 0f, 0f);
            combat.AdvanceTime(0.1f);
            yield return null;
            Assert.That(hits, Is.EqualTo(new[] { 1, 1, 1 }));
            targets[0].transform.position = new Vector3(-100f, 0f, 0f);
            combat.AdvanceTime(0.1f);
            yield return null;
            targets[0].transform.position = new Vector3(beam.transform.position.x, -1f, 0f);
            combat.AdvanceTime(0.1f);
            yield return null;
            Assert.That(hits[0], Is.EqualTo(1));

            Assert.That(combat.PauseRun(), Is.True);
            combat.AdvanceTime(5f);
            yield return null;
            Assert.That(beam.IsActive, Is.True);
            Assert.That(combat.ResumeRun(), Is.True);
            combat.AdvanceTime(1.1f);
            yield return null;
            Assert.That(beam.IsActive, Is.False);
            Assert.That(controller.ActiveEffectCount, Is.Zero);

            combat.AdvanceTime(4f);
            yield return null;
            Assert.That(beam.IsActive, Is.True);
            targets[0].transform.position = new Vector3(beam.transform.position.x, -1f, 0f);
            combat.AdvanceTime(0.1f);
            yield return null;
            Assert.That(hits[0], Is.EqualTo(2));

            combat.TryAbortRun(RunEndReason.UserExit, default, out _);
            Assert.That(beam.IsActive, Is.False);
#else
            Assert.Ignore("The sandbox scene is loaded through the Editor PlayMode runner.");
            yield break;
#endif
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
