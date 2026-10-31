using System.Collections;
using System.Reflection;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.Combat.Tests
{
    public sealed class HeadshotSkillPlayModeTests
    {
        private Scene _sandboxScene;

        [UnityTearDown]
        public IEnumerator UnloadSandbox()
        {
            if (_sandboxScene.IsValid() && _sandboxScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_sandboxScene);
        }

        [UnityTest]
        public IEnumerator EditorUnlockFollowsTargetPausesAndHitsOnce()
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
            HeadshotSkillController controller =
                sandbox.GetComponentInChildren<HeadshotSkillController>(true);
            Assert.That(controller, Is.Not.Null);
            SetField(sandbox, "_autoHit", false);
            SetField(controller, "editorSkillAcquired", true);
            SetBaseStat(controller, "FirstCastDelaySeconds", 0f);
            SetBaseStat(controller, "ExplosionDelaySeconds", 0.5f);
            SetBaseStat(controller, "EffectDurationSeconds", 1f);
            combat.ConfigureSkills(null);

            combat.AdvanceTime(0.01f);
            yield return null;
            Assert.That(controller.ActiveEffectCount, Is.EqualTo(1));

            HeadshotEffect effect = null;
            foreach (HeadshotEffect candidate in controller.GetComponentsInChildren<HeadshotEffect>(true))
                if (candidate.IsActive) effect = candidate;
            Assert.That(effect, Is.Not.Null);
            MonsterCombatTarget primary = (MonsterCombatTarget)
                typeof(HeadshotEffect).GetField("_target",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(effect);
            Assert.That(primary, Is.Not.Null);

            MonsterCombatTarget secondary = null;
            foreach (MonsterCombatTarget candidate in sandbox.GetComponentsInChildren<MonsterCombatTarget>())
                if (candidate != primary) { secondary = candidate; break; }
            Assert.That(secondary, Is.Not.Null);
            primary.enabled = false;
            secondary.enabled = false;
            primary.transform.position = new Vector3(2f, 0f, 0f);
            secondary.transform.position = new Vector3(3f, 0f, 0f);

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
            Assert.That(combat.PauseRun(), Is.True);
            combat.AdvanceTime(5f);
            yield return null;
            Assert.That(primaryHits + secondaryHits, Is.Zero);
            Assert.That(combat.ResumeRun(), Is.True);
            combat.AdvanceTime(0.35f);
            yield return null;
            Assert.That(primaryHits, Is.EqualTo(1));
            Assert.That(secondaryHits, Is.EqualTo(1));
            Assert.That(primary.IsDead, Is.True);
            Assert.That(combat.DefeatedCount, Is.GreaterThanOrEqualTo(1));

            combat.AdvanceTime(0.6f);
            yield return null;
            Assert.That(controller.ActiveEffectCount, Is.Zero);
            Assert.That(controller.PooledEffectCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(combat.TryAbortRun(RunEndReason.UserExit, default, out _), Is.True);
            combat.AdvanceTime(5f);
            yield return null;
            Assert.That(primaryHits, Is.EqualTo(1));
            Assert.That(secondaryHits, Is.EqualTo(1));
#else
            Assert.Ignore("The sandbox scene is loaded through the Editor PlayMode runner.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator LostTargetExplodesAtLastPositionAndAbortCancelsPendingEffect()
        {
#if UNITY_EDITOR
            _sandboxScene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Combat/Scenes/CombatSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;

            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            HeadshotSkillController controller =
                sandbox.GetComponentInChildren<HeadshotSkillController>(true);
            SetField(sandbox, "_autoHit", false);
            SetField(controller, "editorSkillAcquired", true);
            SetBaseStat(controller, "FirstCastDelaySeconds", 0f);
            SetBaseStat(controller, "ExplosionDelaySeconds", 0.5f);
            SetBaseStat(controller, "EffectDurationSeconds", 0.7f);
            SetBaseStat(controller, "CooldownSeconds", 0.8f);
            combat.ConfigureSkills(null);
            combat.AdvanceTime(0.01f);
            yield return null;

            HeadshotEffect effect = null;
            foreach (HeadshotEffect candidate in controller.GetComponentsInChildren<HeadshotEffect>(true))
                if (candidate.IsActive) effect = candidate;
            Assert.That(effect, Is.Not.Null);
            MonsterCombatTarget primary = (MonsterCombatTarget)
                typeof(HeadshotEffect).GetField("_target",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(effect);
            MonsterCombatTarget secondary = null;
            foreach (MonsterCombatTarget candidate in sandbox.GetComponentsInChildren<MonsterCombatTarget>())
                if (candidate != primary) { secondary = candidate; break; }
            Assert.That(secondary, Is.Not.Null);

            primary.enabled = false;
            secondary.enabled = false;
            primary.transform.position = new Vector3(2f, 0f, 0f);
            secondary.transform.position = new Vector3(3f, 0f, 0f);
            combat.AdvanceTime(0.2f);
            yield return null;
            primary.gameObject.SetActive(false);
            int secondaryHits = 0;
            combat.DamageApplied += hit =>
            {
                if (hit.Source == CombatDamageSource.Skill &&
                    ReferenceEquals(hit.Target, secondary)) secondaryHits++;
            };
            combat.AdvanceTime(0.35f);
            yield return null;
            Assert.That(effect.transform.position.x, Is.EqualTo(2f).Within(0.01f));
            Assert.That(secondaryHits, Is.EqualTo(1));

            combat.AdvanceTime(0.3f);
            yield return null;
            Assert.That(controller.ActiveEffectCount, Is.EqualTo(1));
            Assert.That(combat.TryAbortRun(RunEndReason.UserExit, default, out _), Is.True);
            combat.AdvanceTime(1f);
            yield return null;
            Assert.That(controller.ActiveEffectCount, Is.Zero);
            Assert.That(secondaryHits, Is.EqualTo(1));
#else
            Assert.Ignore("The sandbox scene is loaded through the Editor PlayMode runner.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ReadyCooldownWaitsForAnAvailableEnemy()
        {
#if UNITY_EDITOR
            _sandboxScene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Combat/Scenes/CombatSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;

            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            HeadshotSkillController controller =
                sandbox.GetComponentInChildren<HeadshotSkillController>(true);
            SetField(sandbox, "_autoHit", false);
            MonsterCombatTarget[] targets = sandbox.GetComponentsInChildren<MonsterCombatTarget>();
            Assert.That(targets.Length, Is.GreaterThan(0));
            for (int i = 0; i < targets.Length; i++)
                targets[i].gameObject.SetActive(false);
            SetField(controller, "editorSkillAcquired", true);
            combat.ConfigureSkills(null);

            combat.AdvanceTime(2f);
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

        [UnityTest]
        public IEnumerator BossHitUsesIndependentDamageMultiplierInsteadOfPlayerAttackOrRemainingHealth()
        {
#if UNITY_EDITOR
            _sandboxScene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Combat/Scenes/CombatSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;

            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            HeadshotSkillController controller =
                sandbox.GetComponentInChildren<HeadshotSkillController>(true);
            SetField(sandbox, "_autoHit", false);
            MonsterCombatTarget[] targets = sandbox.GetComponentsInChildren<MonsterCombatTarget>();
            Assert.That(combat.TryAbortRun(RunEndReason.UserExit, default, out _), Is.True);

            RunId bossRunId = RunId.Create();
            Assert.That(combat.StartRun(
                new RunRequest(bossRunId, 1, 1, RunMode.Boss, "boss.v1", 7UL, 30f),
                new CombatSnapshot(100L, 1f, 1f, 1, 0f, 1f, false, 0f)), Is.True);
            for (int i = 1; i < targets.Length; i++)
                targets[i].gameObject.SetActive(false);
            MonsterCombatTarget boss = targets[0];
            boss.enabled = false;
            boss.Initialize(new SpawnSnapshot("boss.v1", "sandbox", 5000L, 1f, 1, 1, 0L),
                bossRunId);
            SetField(controller, "editorSkillAcquired", true);
            SetBaseStat(controller, "FirstCastDelaySeconds", 0f);
            SetBaseStat(controller, "ExplosionDelaySeconds", 0.1f);
            SetBaseStat(controller, "BossDamageMultiplier", 10f);
            combat.ConfigureSkills(null);
            combat.AdvanceTime(0.01f);
            yield return null;
            combat.AdvanceTime(0.15f);
            yield return null;

            Assert.That(boss.CurrentHealth, Is.EqualTo(4980L),
                "The configured 2x skill damage and 10x boss modifier deal 20 damage independently of 100 player attack.");
            Assert.That(boss.IsDead, Is.False);
            combat.TryAbortRun(RunEndReason.UserExit, default, out _);
#else
            Assert.Ignore("The sandbox scene is loaded through the Editor PlayMode runner.");
            yield break;
#endif
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
    }
}
