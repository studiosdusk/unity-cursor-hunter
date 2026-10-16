using System.Collections;
using System.Reflection;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.Combat.Tests
{
    public sealed class SkillRegistryPlayModeTests
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
        public IEnumerator AllSixSkillsConfigureAndStopTogether()
        {
#if UNITY_EDITOR
            _sandboxScene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Combat/Scenes/CombatSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;

            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            SetField(sandbox, "_autoHit", false);
            FlameExhaustSkillController flame =
                sandbox.GetComponentInChildren<FlameExhaustSkillController>(true);
            _cursor = new GameObject("RegistryTestCursor");
            CircleCollider2D hitArea = _cursor.AddComponent<CircleCollider2D>();
            hitArea.isTrigger = true;
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(_cursor.transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            _sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            renderer.sprite = _sprite;
            SetField(flame, "sourceCursorHitArea", hitArea);
            SetField(flame, "sourceCursorVisual", renderer);

            Assert.That(combat.ConfigureSkills(new[]
            {
                new SkillCombatSnapshot(TornadoSkillController.SkillId, true),
                new SkillCombatSnapshot(HeadshotSkillController.SkillId, true),
                new SkillCombatSnapshot(ThunderSkillController.SkillId, true),
                new SkillCombatSnapshot(FlameExhaustSkillController.SkillId, true),
                new SkillCombatSnapshot(MagicBallSkillController.SkillId, true),
                new SkillCombatSnapshot(LaserSkillController.SkillId, true)
            }), Is.True);

            TornadoSkillController tornado =
                sandbox.GetComponentInChildren<TornadoSkillController>(true);
            HeadshotSkillController headshot =
                sandbox.GetComponentInChildren<HeadshotSkillController>(true);
            ThunderSkillController thunder =
                sandbox.GetComponentInChildren<ThunderSkillController>(true);
            MagicBallSkillController magicBall =
                sandbox.GetComponentInChildren<MagicBallSkillController>(true);
            LaserSkillController laser =
                sandbox.GetComponentInChildren<LaserSkillController>(true);

            combat.AdvanceTime(0.01f);
            yield return null;
            Assert.That(magicBall.BallIsActive, Is.True);
            Assert.That(laser.ActiveEffectCount, Is.EqualTo(1));
            Assert.That(flame.ActiveProjectileCount, Is.EqualTo(1));
            combat.AdvanceTime(1.01f);
            yield return null;
            Assert.That(tornado.ActiveObjectCount, Is.GreaterThan(0));
            Assert.That(headshot.ActiveEffectCount, Is.GreaterThan(0));

            Assert.That(combat.PauseRun(), Is.True);
            combat.AdvanceTime(10f);
            yield return null;
            Assert.That(tornado.ActiveObjectCount, Is.GreaterThan(0));
            Assert.That(combat.ResumeRun(), Is.True);
            Assert.That(combat.TryAbortRun(RunEndReason.UserExit, default, out _),
                Is.True);
            Assert.That(tornado.ActiveObjectCount, Is.Zero);
            Assert.That(headshot.ActiveEffectCount, Is.Zero);
            Assert.That(thunder.ActiveEffectCount, Is.Zero);
            Assert.That(flame.ActiveProjectileCount, Is.Zero);
            Assert.That(flame.ActiveHitEffectCount, Is.Zero);
            Assert.That(magicBall.BallIsActive, Is.False);
            Assert.That(laser.ActiveEffectCount, Is.Zero);
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
