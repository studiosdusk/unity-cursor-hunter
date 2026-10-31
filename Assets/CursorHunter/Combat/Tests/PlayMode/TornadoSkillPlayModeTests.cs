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
        private AudioSource[] _busyVoices;
        private float _listenerVolume;

        [UnitySetUp]
        public IEnumerator SilenceAutomatedPlayback()
        {
            _listenerVolume = AudioListener.volume;
            AudioListener.volume = 0f;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadSandbox()
        {
            if (_busyVoices != null)
            {
                foreach (AudioSource source in _busyVoices)
                {
                    if (source == null) continue;
                    source.Stop();
                    source.loop = false;
                    source.clip = null;
                }
                _busyVoices = null;
            }
            if (_sandboxScene.IsValid() && _sandboxScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_sandboxScene);
            AudioListener.volume = _listenerVolume;
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
                if (hit.Source == CombatDamageSource.Skill && ReferenceEquals(hit.Target, target))
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

        [UnityTest]
        public IEnumerator TornadoAudioSurvivesBusyHitVoicesAndFollowsTheSkillLifetime()
        {
#if UNITY_EDITOR
            _sandboxScene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Combat/Scenes/CombatSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            typeof(CursorHitEffectSandbox).GetField("_autoHit",
                BindingFlags.NonPublic | BindingFlags.Instance).SetValue(sandbox, false);
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            TornadoSkillController controller = sandbox.GetComponentInChildren<TornadoSkillController>(true);
            SetBaseStat(controller, "Count", 3);
            SetBaseStat(controller, "FirstCastDelaySeconds", 0.05f);
            SetBaseStat(controller, "DurationSeconds", 0.6f);
            SetBaseStat(controller, "ShrinkOutSeconds", 0.15f);
            SetBaseStat(controller, "CooldownSeconds", 10f);
            combat.ConfigureSkills(new[] { new SkillCombatSnapshot(TornadoSkillController.SkillId, true) });

            AudioSource cast = controller.transform.Find("TornadoAudio/Cast").GetComponent<AudioSource>();
            AudioSource wind = controller.transform.Find("TornadoAudio/Wind").GetComponent<AudioSource>();
            object audioFeedback = typeof(TornadoSkillController).GetField("_audio",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
            System.Type audioType = audioFeedback.GetType();
            var tickAudio = audioType.GetMethod("Tick", BindingFlags.Public | BindingFlags.Instance);
            var stopAudio = audioType.GetMethod("Stop", BindingFlags.Public | BindingFlags.Instance);
            stopAudio.Invoke(audioFeedback, null);
            tickAudio.Invoke(audioFeedback, new object[] { 3, 0.22f, 0.1f });
            float fadeInVolume = wind.volume;
            float targetWindVolume = 0.22f * 0.24f * 1.08f;
            Assert.That(fadeInVolume, Is.GreaterThan(0f).And.LessThan(targetWindVolume),
                "Wind must fade in over time instead of jumping to its target volume.");
            tickAudio.Invoke(audioFeedback, new object[] { 0, 0.22f, 0.05f });
            Assert.That(wind.volume, Is.GreaterThan(0f).And.LessThan(fadeInVolume),
                "Wind must fade out gradually after the final tornado ends.");
            tickAudio.Invoke(audioFeedback, new object[] { 0, 0.22f, 1f });
            Assert.That(wind.isPlaying, Is.False);
            stopAudio.Invoke(audioFeedback, null);

            System.Type sharedAudio = typeof(TornadoSkillController).Assembly.GetType(
                "CursorHunter.Combat.CombatAudioFeedback");
            sharedAudio.GetMethod("PlayMonsterHit", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { "monster.01" });
            _busyVoices = (AudioSource[])sharedAudio.GetField("ImpactSources",
                BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            foreach (AudioSource source in _busyVoices)
            {
                source.Stop();
                source.clip = wind.clip;
                source.loop = true;
                source.Play();
                Assert.That(source.isPlaying, Is.True);
                Assert.That(cast.priority, Is.LessThan(source.priority));
            }

            combat.AdvanceTime(0.08f);
            yield return null;
            Assert.That(controller.ActiveObjectCount, Is.EqualTo(3));
            Assert.That(cast.isPlaying, Is.True, "Busy hit/death channels must not drop the cast cue.");
            Assert.That(wind.isPlaying && wind.loop, Is.True);
            Assert.That(cast.volume, Is.InRange(0.1f, 0.25f),
                "The tornado cast cue must stay audible without returning to the former overpowering volume.");
            Assert.That(wind.volume, Is.GreaterThan(0f));
            AssertAudibleStereoClip(cast.clip, 0.8f);
            AssertAudibleStereoClip(wind.clip, 1.5f);

            Assert.That(combat.PauseRun(), Is.True);
            yield return null;
            Assert.That(cast.isPlaying || wind.isPlaying, Is.False);
            Assert.That(combat.ResumeRun(), Is.True);
            yield return null;
            Assert.That(wind.isPlaying, Is.True);

            combat.AdvanceTime(0.7f);
            yield return null;
            Assert.That(controller.ActiveObjectCount, Is.Zero);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(wind.isPlaying, Is.False, "The wind must fade out after the last object expires.");

            combat.AdvanceTime(10f);
            yield return null;
            Assert.That(wind.isPlaying, Is.True);
            Assert.That(controller.transform.Find("TornadoAudio").childCount, Is.EqualTo(2),
                "Overlapping casts reuse the same two sources.");
            Assert.That(combat.TryAbortRun(RunEndReason.UserExit, default, out _), Is.True);
            yield return null;
            Assert.That(cast.isPlaying || wind.isPlaying, Is.False);
#else
            Assert.Ignore("The sandbox scene is loaded through the Editor PlayMode runner.");
            yield break;
#endif
        }

        private static void AssertAudibleStereoClip(AudioClip clip, float minimumDuration)
        {
            Assert.That(clip.channels, Is.EqualTo(2));
            Assert.That(clip.length, Is.GreaterThan(minimumDuration));
            var samples = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(samples, 0), Is.True);
            double energy = 0;
            float peak = 0;
            foreach (float sample in samples)
            {
                energy += sample * sample;
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }
            Assert.That(System.Math.Sqrt(energy / samples.Length), Is.GreaterThan(0.05));
            Assert.That(peak, Is.InRange(0.5f, 0.9f));
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
