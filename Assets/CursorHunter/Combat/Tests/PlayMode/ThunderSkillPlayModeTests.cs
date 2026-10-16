using System.Collections;
using System.Reflection;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.Combat.Tests
{
    public sealed class ThunderSkillPlayModeTests
    {
        private Scene _sandboxScene;

        [UnityTearDown]
        public IEnumerator UnloadSandbox()
        {
            if (_sandboxScene.IsValid() && _sandboxScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_sandboxScene);
        }

        [UnityTest]
        public IEnumerator TracksOneTargetAndStrikesOnceAfterPause()
        {
#if UNITY_EDITOR
            yield return LoadSandbox();
            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            ThunderSkillController controller =
                sandbox.GetComponentInChildren<ThunderSkillController>(true);
            Assert.That(controller, Is.Not.Null);
            SetField(sandbox, "_autoHit", false);
            SetField(controller, "editorSkillAcquired", true);
            SetBaseStat(controller, "ImpactDelaySeconds", 0.5f);
            SetBaseStat(controller, "EffectDurationSeconds", 0.8f);
            combat.ConfigureSkills(null);

            combat.AdvanceTime(0.01f);
            yield return null;
            Assert.That(controller.ActiveEffectCount, Is.EqualTo(1));
            ThunderEffect effect = ActiveEffect(controller);
            MonsterCombatTarget primary = TargetOf(effect);
            Assert.That(primary, Is.Not.Null);
            Transform hitPoint = effect.transform.Find(
                "Visual_FX_Cartoon_Thunder/Particle System/HitPoint");
            Assert.That(hitPoint, Is.Not.Null);
            AssertHitPointAtVisualCenter(primary, hitPoint);
            MonsterCombatTarget secondary = null;
            foreach (MonsterCombatTarget candidate in sandbox.GetComponentsInChildren<MonsterCombatTarget>())
                if (candidate != primary) { secondary = candidate; break; }
            Assert.That(secondary, Is.Not.Null);
            primary.enabled = false;
            secondary.enabled = false;
            primary.transform.position = new Vector3(2f, 0f, 0f);
            secondary.transform.position = new Vector3(2f, 0f, 0f);

            int primaryHits = 0;
            int secondaryHits = 0;
            combat.DamageApplied += hit =>
            {
                if (hit.Source != CombatDamageSource.Skill) return;
                if (ReferenceEquals(hit.Target, primary)) primaryHits++;
                if (ReferenceEquals(hit.Target, secondary)) secondaryHits++;
            };
            combat.AdvanceTime(0.2f);
            yield return null;
            Assert.That(effect.transform.position.x, Is.EqualTo(2f).Within(0.01f));
            AssertHitPointAtVisualCenter(primary, hitPoint);
            Assert.That(combat.PauseRun(), Is.True);
            combat.AdvanceTime(5f);
            yield return null;
            Assert.That(primaryHits + secondaryHits, Is.Zero);
            Assert.That(combat.ResumeRun(), Is.True);
            combat.AdvanceTime(0.35f);
            yield return null;
            Assert.That(primaryHits, Is.EqualTo(1));
            Assert.That(secondaryHits, Is.Zero);

            combat.AdvanceTime(0.3f);
            yield return null;
            Assert.That(controller.ActiveEffectCount, Is.Zero);
            Assert.That(combat.TryAbortRun(RunEndReason.UserExit, default, out _), Is.True);
            Assert.That(primaryHits, Is.EqualTo(1));
#else
            Assert.Ignore("The sandbox scene is loaded through the Editor PlayMode runner.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator DeadTargetLeavesVisualAtLastPositionWithoutSplash()
        {
#if UNITY_EDITOR
            yield return LoadSandbox();
            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            ThunderSkillController controller =
                sandbox.GetComponentInChildren<ThunderSkillController>(true);
            SetField(sandbox, "_autoHit", false);
            SetField(controller, "editorSkillAcquired", true);
            SetBaseStat(controller, "ImpactDelaySeconds", 0.5f);
            combat.ConfigureSkills(null);
            combat.AdvanceTime(0.01f);
            yield return null;

            ThunderEffect effect = ActiveEffect(controller);
            MonsterCombatTarget primary = TargetOf(effect);
            MonsterCombatTarget secondary = null;
            foreach (MonsterCombatTarget candidate in sandbox.GetComponentsInChildren<MonsterCombatTarget>())
                if (candidate != primary) { secondary = candidate; break; }
            Assert.That(secondary, Is.Not.Null);
            primary.enabled = false;
            secondary.enabled = false;
            primary.transform.position = new Vector3(2f, 0f, 0f);
            secondary.transform.position = new Vector3(2f, 0f, 0f);
            combat.AdvanceTime(0.2f);
            yield return null;
            Assert.That(effect.transform.position.x, Is.EqualTo(2f).Within(0.01f));

            int skillHits = 0;
            combat.DamageApplied += hit =>
            {
                if (hit.Source == CombatDamageSource.Skill) skillHits++;
            };
            Assert.That(primary.ApplyDamage(primary.RunId, long.MaxValue,
                out _, out bool killed), Is.True);
            Assert.That(killed, Is.True);
            primary.transform.position = new Vector3(4f, 0f, 0f);
            combat.AdvanceTime(0.35f);
            yield return null;
            Assert.That(effect.transform.position.x, Is.EqualTo(2f).Within(0.01f));
            Assert.That(skillHits, Is.Zero);
            combat.TryAbortRun(RunEndReason.UserExit, default, out _);
#else
            Assert.Ignore("The sandbox scene is loaded through the Editor PlayMode runner.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ReadySkillWaitsWithoutSpendingCooldownWhenNoEnemyExists()
        {
#if UNITY_EDITOR
            yield return LoadSandbox();
            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            ThunderSkillController controller =
                sandbox.GetComponentInChildren<ThunderSkillController>(true);
            SetField(sandbox, "_autoHit", false);
            MonsterCombatTarget[] targets = sandbox.GetComponentsInChildren<MonsterCombatTarget>();
            Assert.That(targets.Length, Is.GreaterThan(0));
            for (int i = 0; i < targets.Length; i++)
                targets[i].gameObject.SetActive(false);
            SetField(controller, "editorSkillAcquired", true);
            combat.ConfigureSkills(null);

            combat.AdvanceTime(1f);
            yield return null;
            Assert.That(controller.ActiveEffectCount, Is.Zero);
            targets[0].gameObject.SetActive(true);
            combat.AdvanceTime(0.01f);
            yield return null;
            Assert.That(controller.ActiveEffectCount, Is.EqualTo(1));
            combat.TryAbortRun(RunEndReason.UserExit, default, out _);
#else
            Assert.Ignore("The sandbox scene is loaded through the Editor PlayMode runner.");
            yield break;
#endif
        }

#if UNITY_EDITOR
        private IEnumerator LoadSandbox()
        {
            _sandboxScene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Combat/Scenes/CombatSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
        }

        private static ThunderEffect ActiveEffect(ThunderSkillController controller)
        {
            foreach (ThunderEffect candidate in controller.GetComponentsInChildren<ThunderEffect>(true))
                if (candidate.IsActive) return candidate;
            Assert.Fail("No active ThunderEffect was found.");
            return null;
        }

        private static MonsterCombatTarget TargetOf(ThunderEffect effect)
        {
            FieldInfo field = typeof(ThunderEffect).GetField("_target",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            return (MonsterCombatTarget)field.GetValue(effect);
        }

        private static void AssertHitPointAtVisualCenter(
            MonsterCombatTarget target, Transform hitPoint)
        {
            Assert.That(target.TryGetHitEffectBounds(out Bounds bounds), Is.True);
            Assert.That(Vector3.Distance(bounds.center, target.transform.position),
                Is.GreaterThan(0.1f));
            Assert.That(Vector3.Distance(hitPoint.position, bounds.center),
                Is.LessThan(0.01f));
        }

        private static void SetBaseStat(object instance, string name, object value)
        {
            FieldInfo field = instance.GetType().GetField("baseStat",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            object basis = field.GetValue(instance);
            FieldInfo statField = basis.GetType().GetField(name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(statField, Is.Not.Null, name);
            statField.SetValue(basis, value);
            field.SetValue(instance, basis);
        }

        private static void SetField(object instance, string name, object value)
        {
            FieldInfo field = instance.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(instance, value);
        }
#endif
    }
}
