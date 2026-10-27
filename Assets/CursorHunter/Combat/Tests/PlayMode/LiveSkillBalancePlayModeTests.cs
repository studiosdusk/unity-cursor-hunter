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
    public sealed class LiveSkillBalancePlayModeTests
    {
        private Scene _scene;
        private GameObject _cursor;
        private Sprite _sprite;

        [UnityTearDown]
        public IEnumerator UnloadSandbox()
        {
            if (_cursor != null) Object.Destroy(_cursor);
            if (_sprite != null) Object.Destroy(_sprite);
            if (_scene.IsValid() && _scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_scene);
        }

        [UnityTest]
        public IEnumerator FlameCopiesUpdatedCursorGeometryBeforeNextSimulationFrame()
        {
#if UNITY_EDITOR
            yield return LoadSandbox();
            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            SetField(sandbox, "_autoHit", false);
            FlameExhaustSkillController controller =
                sandbox.GetComponentInChildren<FlameExhaustSkillController>(true);
            _cursor = new GameObject("Balance source cursor");
            CircleCollider2D source = _cursor.AddComponent<CircleCollider2D>();
            source.isTrigger = true;
            source.radius = 0.5f;
            var visualRoot = new GameObject("Source visual");
            visualRoot.transform.SetParent(_cursor.transform, false);
            SpriteRenderer visual = visualRoot.AddComponent<SpriteRenderer>();
            _sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            visual.sprite = _sprite;
            SetField(controller, "sourceCursorHitArea", source);
            SetField(controller, "sourceCursorVisual", visual);
            SkillBalanceSettings settings = controller.CaptureBalanceSettings();
            settings.Enabled = true;
            Set(settings, "speedWorldUnitsPerSecond", 0f);
            Set(settings, "projectileDurationSeconds", 10f);
            Assert.That(controller.ApplyBalanceSettings(combat, combat.CurrentRunId, 21UL,
                settings), Is.True);
            combat.AdvanceTime(0.01f);
            yield return null;
            FlameExhaustProjectile projectile = null;
            foreach (FlameExhaustProjectile candidate in
                controller.GetComponentsInChildren<FlameExhaustProjectile>(true))
                if (candidate.IsActive) { projectile = candidate; break; }
            Assert.That(projectile, Is.Not.Null);
            Assert.That(combat.PauseRun(), Is.True);
            Vector3 position = projectile.transform.position;
            float expiresAt = GetField<float>(projectile, "_expiresAt");
            MonsterCombatTarget target = sandbox.GetComponentInChildren<MonsterCombatTarget>();
            HashSet<MonsterCombatTarget> hits =
                GetField<HashSet<MonsterCombatTarget>>(projectile, "_hitTargets");
            hits.Add(target);
            settings = controller.CaptureBalanceSettings();
            Set(settings, "damageMultiplier", 3f);
            Assert.That(controller.ApplyBalanceSettings(combat, combat.CurrentRunId, 21UL,
                settings), Is.True);
            // App may update the cursor transform after an editor edit was accepted.
            _cursor.transform.localScale = Vector3.one * 2f;
            Physics2D.SyncTransforms();
            yield return null;
            Assert.That(projectile.GetComponent<CircleCollider2D>().radius,
                Is.EqualTo(source.bounds.extents.x * 1.2f).Within(0.0001f));
            Assert.That(projectile.transform.position, Is.EqualTo(position));
            Assert.That(GetField<float>(projectile, "_expiresAt"), Is.EqualTo(expiresAt));
            Assert.That(hits.Contains(target), Is.True);
#else
            Assert.Ignore("The sandbox scene needs the Editor PlayMode runner.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TornadoEditUpdatesActiveDamageAndPreservesCooldownProgress()
        {
#if UNITY_EDITOR
            yield return LoadSandbox();
            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            SetField(sandbox, "_autoHit", false);
            TornadoSkillController controller =
                sandbox.GetComponentInChildren<TornadoSkillController>(true);
            SkillBalanceSettings settings = controller.CaptureBalanceSettings();
            settings.Enabled = true;
            Set(settings, "count", 1f);
            Set(settings, "firstCastDelaySeconds", 0f);
            Set(settings, "moveSpeedWorldUnitsPerSecond", 0f);
            Set(settings, "cooldownSeconds", 8f);
            Assert.That(controller.ApplyBalanceSettings(combat, combat.CurrentRunId, 21UL,
                settings), Is.True);
            combat.AdvanceTime(0.01f);
            yield return null;
            TornadoObject active = null;
            foreach (TornadoObject candidate in controller.GetComponentsInChildren<TornadoObject>(true))
                if (candidate.IsActive) { active = candidate; break; }
            Assert.That(active, Is.Not.Null);
            MonsterCombatTarget target = sandbox.GetComponentInChildren<MonsterCombatTarget>();
            target.enabled = false;
            active.transform.position = target.transform.position;
            combat.AdvanceTime(0.25f);
            yield return null;
            Assert.That(combat.PauseRun(), Is.True);
            float now = combat.ElapsedSeconds;
            float oldCast = GetField<float>(controller, "_nextCastAt");
            Vector3 position = active.transform.position;
            object random = GetField<object>(controller, "_random");
            settings = controller.CaptureBalanceSettings();
            Set(settings, "damageMultiplier", 7f);
            Set(settings, "cooldownSeconds", 4f);
            Set(settings, "hitIntervalSeconds", 1f);
            Assert.That(controller.ApplyBalanceSettings(combat, combat.CurrentRunId, 999UL,
                settings), Is.True);
            Assert.That(controller.ActiveObjectCount, Is.EqualTo(1));
            Assert.That(active.IsActive, Is.True);
            Assert.That(active.transform.position, Is.EqualTo(position));
            Assert.That(GetField<object>(controller, "_random"), Is.SameAs(random));
            Assert.That(GetField<long>(active, "_damage"), Is.EqualTo(7L));
            Assert.That(GetField<float>(controller, "_nextCastAt"),
                Is.EqualTo(now + (oldCast - now) * 0.5f).Within(0.0001f));

            SkillBalanceSettings invalid = settings.Clone();
            Set(invalid, "cooldownSeconds", 0f);
            Assert.That(controller.ApplyBalanceSettings(combat, combat.CurrentRunId, 0UL,
                invalid), Is.False);
            Assert.That(controller.CaptureBalanceSettings().Values.Find(
                value => value.Id == "cooldownSeconds").Value, Is.EqualTo(4f));

            long applied = 0L;
            combat.DamageApplied += hit =>
            {
                if (hit.Source == CombatDamageSource.Skill && ReferenceEquals(hit.Target, target))
                    applied = hit.EffectiveDamage;
            };
            Assert.That(combat.ResumeRun(), Is.True);
            combat.AdvanceTime(0.6f);
            yield return null;
            Assert.That(applied, Is.EqualTo(7L).Or.EqualTo(14L),
                "The existing tornado must use the edited multiplier on its next hit.");
#else
            Assert.Ignore("The sandbox scene needs the Editor PlayMode runner.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator MagicBallEditKeepsBallPositionDirectionAndContactHistory()
        {
#if UNITY_EDITOR
            yield return LoadSandbox();
            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            SetField(sandbox, "_autoHit", false);
            MagicBallSkillController controller =
                sandbox.GetComponentInChildren<MagicBallSkillController>(true);
            Assert.That(combat.ConfigureSkills(new[]
            {
                new SkillCombatSnapshot(MagicBallSkillController.SkillId, true,
                    new[] { new SkillStatUpgrade("damageMultiplier", 2f) })
            }), Is.True);
            combat.AdvanceTime(0.01f);
            yield return null;
            MagicBallObject ball = controller.GetComponentInChildren<MagicBallObject>(true);
            Assert.That(ball.IsActive, Is.True);
            Assert.That(combat.PauseRun(), Is.True);
            Vector3 position = ball.transform.position;
            Vector2 direction = GetField<Vector2>(ball, "_direction");
            MonsterCombatTarget target = sandbox.GetComponentInChildren<MonsterCombatTarget>();
            HashSet<MonsterCombatTarget> contacts =
                GetField<HashSet<MonsterCombatTarget>>(ball, "_contactsLastFrame");
            contacts.Add(target);
            Dictionary<MonsterCombatTarget, float> hits =
                GetField<Dictionary<MonsterCombatTarget, float>>(ball, "_lastHitAt");
            hits[target] = combat.ElapsedSeconds - 0.25f;
            SkillBalanceSettings settings = controller.CaptureBalanceSettings();
            Assert.That(settings.Values.Find(value => value.Id == "damageMultiplier").Value,
                Is.EqualTo(3f), "Capture must expose the existing final upgraded value.");
            Set(settings, "damageMultiplier", 4f);
            Set(settings, "sizeMultiplier", 1.5f);
            Set(settings, "moveSpeedWorldUnitsPerSecond", 10f);
            Set(settings, "sameTargetHitCooldownSeconds", 1f);
            Assert.That(controller.ApplyBalanceSettings(combat, combat.CurrentRunId, 999UL,
                settings), Is.True);
            Assert.That(controller.GetComponentInChildren<MagicBallObject>(true), Is.SameAs(ball));
            Assert.That(ball.transform.position, Is.EqualTo(position));
            Assert.That(GetField<Vector2>(ball, "_direction"), Is.EqualTo(direction));
            Assert.That(contacts.Contains(target), Is.True);
            Assert.That(hits[target], Is.EqualTo(combat.ElapsedSeconds - 0.5f).Within(0.0001f));
            Assert.That(controller.CaptureBalanceSettings().Values.Find(
                value => value.Id == "damageMultiplier").Value, Is.EqualTo(4f),
                "Saved upgrades must not be added to a final editor value again.");

            SetField(controller, "editorSkillAcquired", true);
            settings.Enabled = false;
            Assert.That(controller.ApplyBalanceSettings(combat, combat.CurrentRunId, 0UL,
                settings), Is.True);
            Assert.That(ball.IsActive, Is.False);
            Assert.That(((IRunSkillController)controller).IsAcquiredForRun(true), Is.False,
                "The balance toggle overrides both saved acquisition and the Inspector.");
            controller.ClearBalanceSettings();
            Assert.That(((IRunSkillController)controller).IsAcquiredForRun(true), Is.True);
#else
            Assert.Ignore("The sandbox scene needs the Editor PlayMode runner.");
            yield break;
#endif
        }

#if UNITY_EDITOR
        private IEnumerator LoadSandbox()
        {
            _scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Combat/Scenes/CombatSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
        }
#endif

        private static void Set(SkillBalanceSettings settings, string id, float value)
        {
            SkillBalanceValue setting = settings.Values.Find(candidate => candidate.Id == id);
            Assert.That(setting, Is.Not.Null, id);
            setting.Value = value;
        }

        private static T GetField<T>(object instance, string name) =>
            (T)instance.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);

        private static void SetField(object instance, string name, object value) =>
            instance.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, value);
    }
}
