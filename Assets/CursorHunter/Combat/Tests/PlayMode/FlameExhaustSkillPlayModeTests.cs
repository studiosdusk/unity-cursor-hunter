using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.Combat.Tests
{
    public sealed class FlameExhaustSkillPlayModeTests
    {
        private Scene _sandboxScene;
        private GameObject _cursor;
        private Sprite _sprite;

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            if (_cursor != null) Object.Destroy(_cursor);
            if (_sprite != null) Object.Destroy(_sprite);
            if (_sandboxScene.IsValid() && _sandboxScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_sandboxScene);
        }

        [UnityTest]
        public IEnumerator PiercesTwoTargetsOnceEachAndPlaysHitVfx()
        {
#if UNITY_EDITOR
            yield return LoadSandbox();
            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            FlameExhaustSkillController controller =
                sandbox.GetComponentInChildren<FlameExhaustSkillController>(true);
            Assert.That(controller, Is.Not.Null);
            SetField(sandbox, "_autoHit", false);
            CreateCursor(controller);
            SetField(controller, "editorSkillAcquired", true);
            SetBaseStat(controller, "CooldownSeconds", 5f);
            SetBaseStat(controller, "ProjectileDurationSeconds", 1f);
            MonsterCombatTarget[] targets =
                sandbox.GetComponentsInChildren<MonsterCombatTarget>();
            Assert.That(targets.Length, Is.GreaterThanOrEqualTo(2));
            for (int i = 2; i < targets.Length; i++)
                targets[i].gameObject.SetActive(false);
            targets[0].enabled = false;
            targets[1].enabled = false;
            Transform firstBody = targets[0].transform.Find("VisualRoot/SandboxBody");
            Transform secondBody = targets[1].transform.Find("VisualRoot/SandboxBody");
            Assert.That(firstBody, Is.Not.Null);
            Assert.That(secondBody, Is.Not.Null);
            firstBody.localScale *= 0.5f;
            secondBody.localScale *= 2f;
            targets[0].transform.position = new Vector3(100f, 100f, 0f);
            targets[1].transform.position = new Vector3(100f, 100f, 0f);
            combat.ConfigureSkills(null);
            Assert.That(controller.PooledHitEffectCount, Is.EqualTo(16));
            combat.AdvanceTime(0.01f);
            yield return null;

            FlameExhaustProjectile projectile = ActiveProjectile(controller);
            Assert.That(projectile.GetComponentInChildren<SpriteRenderer>().sprite,
                Is.SameAs(_sprite));
            Assert.That(projectile.GetComponent<CircleCollider2D>().radius,
                Is.EqualTo(_cursor.GetComponent<CircleCollider2D>().bounds.extents.x * 1.2f)
                    .Within(0.01f));
            Assert.That(projectile.GetComponentInChildren<SpriteRenderer>().transform.localScale.x,
                Is.EqualTo(0.9f).Within(0.01f));
            Vector2 direction = GetField<Vector2>(projectile, "_direction");
            targets[0].transform.position = (Vector3)(direction * 1.5f);
            targets[1].transform.position = (Vector3)(direction * 3f);

            int firstHits = 0;
            int secondHits = 0;
            combat.DamageApplied += hit =>
            {
                if (hit.Source != CombatDamageSource.Skill) return;
                if (ReferenceEquals(hit.Target, targets[0])) firstHits++;
                if (ReferenceEquals(hit.Target, targets[1])) secondHits++;
            };
            combat.AdvanceTime(0.5f);
            yield return null;
            Assert.That(firstHits, Is.EqualTo(1));
            Assert.That(secondHits, Is.EqualTo(1));
            Assert.That(controller.ActiveHitEffectCount, Is.EqualTo(2));
            var effects = new List<FlameExhaustHitEffect>(2);
            foreach (FlameExhaustHitEffect candidate in
                     controller.GetComponentsInChildren<FlameExhaustHitEffect>(true))
                if (candidate.IsActive) effects.Add(candidate);
            Assert.That(effects.Count, Is.EqualTo(2));
            Assert.That(targets[0].TryGetHitEffectBounds(out Bounds firstBounds), Is.True);
            Assert.That(targets[1].TryGetHitEffectBounds(out Bounds secondBounds), Is.True);
            Assert.That(secondBounds.size.y, Is.GreaterThan(firstBounds.size.y));
            Assert.That(Vector3.Distance(effects[0].transform.position, firstBounds.center) < 0.01f ||
                Vector3.Distance(effects[1].transform.position, firstBounds.center) < 0.01f,
                Is.True);
            Assert.That(Vector3.Distance(effects[0].transform.position, secondBounds.center) < 0.01f ||
                Vector3.Distance(effects[1].transform.position, secondBounds.center) < 0.01f,
                Is.True);
            FlameExhaustHitEffect firstEffect =
                Vector3.Distance(effects[0].transform.position, firstBounds.center) < 0.01f
                    ? effects[0] : effects[1];
            FlameExhaustHitEffect secondEffect = firstEffect == effects[0]
                ? effects[1] : effects[0];
            float effectSizeMultiplier =
                GetField<float>(firstEffect, "sizeMultiplier");
            Assert.That(firstEffect.transform.localScale.x,
                Is.EqualTo(Mathf.Max(firstBounds.size.x, firstBounds.size.y) *
                    effectSizeMultiplier)
                    .Within(0.01f));
            Assert.That(secondEffect.transform.localScale.x,
                Is.EqualTo(Mathf.Max(secondBounds.size.x, secondBounds.size.y) *
                    effectSizeMultiplier)
                    .Within(0.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(effects[0].transform.eulerAngles.z,
                effects[1].transform.eulerAngles.z)), Is.GreaterThan(0.01f));
            combat.AdvanceTime(0.1f);
            yield return null;
            Assert.That(firstHits, Is.EqualTo(1));
            Assert.That(secondHits, Is.EqualTo(1));
            combat.TryAbortRun(RunEndReason.UserExit, default, out _);
            Assert.That(controller.ActiveProjectileCount, Is.Zero);
            Assert.That(controller.ActiveHitEffectCount, Is.Zero);
            Assert.That(firstEffect.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(secondEffect.transform.localScale, Is.EqualTo(Vector3.one));
#else
            Assert.Ignore("The sandbox scene is loaded through the Editor PlayMode runner.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator HitVfxPoolGrowsAfterExhaustionWithoutDroppingDamage()
        {
#if UNITY_EDITOR
            yield return LoadSandbox();
            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            FlameExhaustSkillController controller =
                sandbox.GetComponentInChildren<FlameExhaustSkillController>(true);
            SetField(sandbox, "_autoHit", false);
            CreateCursor(controller);
            SetField(controller, "editorSkillAcquired", true);
            SetField(controller, "initialPooledHitEffects", 1);
            SetField(controller, "maximumPooledHitEffects", 3);
            SetField(controller, "hitEffectGrowthPerFrame", 2);
            Assert.That(combat.ConfigureSkills(null), Is.True);
            Assert.That(controller.PooledHitEffectCount, Is.EqualTo(1));

            MonsterCombatTarget[] targets =
                sandbox.GetComponentsInChildren<MonsterCombatTarget>();
            int damageEvents = 0;
            combat.DamageApplied += hit =>
            {
                if (hit.Source == CombatDamageSource.Skill) damageEvents++;
            };
            MethodInfo hitMethod = typeof(FlameExhaustSkillController).GetMethod(
                "Hit", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(hitMethod, Is.Not.Null);
            Assert.That(hitMethod.Invoke(controller, new object[]
                { combat.CurrentRunId, targets[0] }), Is.True);
            Assert.That(hitMethod.Invoke(controller, new object[]
                { combat.CurrentRunId, targets[1] }), Is.True);
            Assert.That(damageEvents, Is.EqualTo(2));
            Assert.That(controller.ActiveHitEffectCount, Is.EqualTo(1));

            yield return null;
            Assert.That(controller.PooledHitEffectCount, Is.EqualTo(3));
            combat.TryAbortRun(RunEndReason.UserExit, default, out _);
#else
            Assert.Ignore("The sandbox scene is loaded through the Editor PlayMode runner.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator HitVfxPoolRecyclesOldestEffectWhenCapacityIsReached()
        {
#if UNITY_EDITOR
            yield return LoadSandbox();
            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            FlameExhaustSkillController controller =
                sandbox.GetComponentInChildren<FlameExhaustSkillController>(true);
            SetField(sandbox, "_autoHit", false);
            CreateCursor(controller);
            SetField(controller, "editorSkillAcquired", true);
            SetField(controller, "initialPooledHitEffects", 1);
            SetField(controller, "maximumPooledHitEffects", 1);
            Assert.That(combat.ConfigureSkills(null), Is.True);

            MonsterCombatTarget[] targets =
                sandbox.GetComponentsInChildren<MonsterCombatTarget>();
            Assert.That(targets.Length, Is.GreaterThanOrEqualTo(2));
            int damageEvents = 0;
            combat.DamageApplied += hit =>
            {
                if (hit.Source == CombatDamageSource.Skill) damageEvents++;
            };
            MethodInfo hitMethod = typeof(FlameExhaustSkillController).GetMethod(
                "Hit", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(hitMethod, Is.Not.Null);
            Assert.That(hitMethod.Invoke(controller, new object[]
                { combat.CurrentRunId, targets[0] }), Is.True);
            Assert.That(hitMethod.Invoke(controller, new object[]
                { combat.CurrentRunId, targets[1] }), Is.True);

            Assert.That(damageEvents, Is.EqualTo(2));
            Assert.That(controller.PooledHitEffectCount, Is.EqualTo(1));
            Assert.That(controller.ActiveHitEffectCount, Is.EqualTo(1));
            FlameExhaustHitEffect[] effects =
                controller.GetComponentsInChildren<FlameExhaustHitEffect>(true);
            FlameExhaustHitEffect activeEffect = null;
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i].IsActive) activeEffect = effects[i];
            }
            Assert.That(activeEffect, Is.Not.Null);
            Assert.That(targets[1].TryGetHitEffectBounds(out Bounds secondBounds), Is.True);
            Assert.That(Vector3.Distance(activeEffect.transform.position,
                secondBounds.center), Is.LessThan(0.01f));
            combat.TryAbortRun(RunEndReason.UserExit, default, out _);
#else
            Assert.Ignore("The sandbox scene is loaded through the Editor PlayMode runner.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator FiresAgainWhilePreviousProjectileIsAliveWithoutEnemies()
        {
#if UNITY_EDITOR
            yield return LoadSandbox();
            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            FlameExhaustSkillController controller =
                sandbox.GetComponentInChildren<FlameExhaustSkillController>(true);
            SetField(sandbox, "_autoHit", false);
            foreach (MonsterCombatTarget target in
                     sandbox.GetComponentsInChildren<MonsterCombatTarget>())
                target.gameObject.SetActive(false);
            CreateCursor(controller);
            SetField(controller, "editorSkillAcquired", true);
            SetBaseStat(controller, "CooldownSeconds", 0.1f);
            SetBaseStat(controller, "ProjectileDurationSeconds", 1f);
            combat.ConfigureSkills(null);

            combat.AdvanceTime(0.01f);
            yield return null;
            Assert.That(controller.ActiveProjectileCount, Is.EqualTo(1));
            FlameExhaustProjectile projectile = ActiveProjectile(controller);
            Vector3 beforePause = projectile.transform.position;
            Assert.That(combat.PauseRun(), Is.True);
            combat.AdvanceTime(5f);
            yield return null;
            Assert.That(projectile.transform.position, Is.EqualTo(beforePause));
            Assert.That(combat.ResumeRun(), Is.True);
            combat.AdvanceTime(0.11f);
            yield return null;
            Assert.That(controller.ActiveProjectileCount, Is.EqualTo(2));
            Assert.That(projectile.transform.position, Is.Not.EqualTo(beforePause));
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

        private void CreateCursor(FlameExhaustSkillController controller)
        {
            _cursor = new GameObject("TestFlameCursor");
            _cursor.transform.localScale = new Vector3(2f, 2f, 1f);
            CircleCollider2D circle = _cursor.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 1f;
            GameObject visual = new GameObject("CursorVisual");
            visual.transform.SetParent(_cursor.transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            _sprite = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            renderer.sprite = _sprite;
            SetField(controller, "sourceCursorHitArea", circle);
            SetField(controller, "sourceCursorVisual", renderer);
        }

        private static FlameExhaustProjectile ActiveProjectile(
            FlameExhaustSkillController controller)
        {
            foreach (FlameExhaustProjectile candidate in
                     controller.GetComponentsInChildren<FlameExhaustProjectile>(true))
                if (candidate.IsActive) return candidate;
            Assert.Fail("No active Flame Exhaust projectile was found.");
            return null;
        }

        private static T GetField<T>(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(instance);
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
