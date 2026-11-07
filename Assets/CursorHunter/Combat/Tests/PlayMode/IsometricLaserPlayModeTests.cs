using System.Collections;
using System.Reflection;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.Combat.Tests
{
    public sealed class IsometricLaserPlayModeTests
    {
        private Scene _scene;
        private float _timeScale;
        private CombatRunController _combat;
        private LaserSkillController _controller;
        private BattlefieldArea _field;
        private MonsterCombatTarget[] _targets;
        private readonly int[] _hits = new int[4];

        [UnitySetUp] public IEnumerator SetUp()
        {
            _timeScale = Time.timeScale;
            Time.timeScale = 0f;
#if UNITY_EDITOR
            _scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Combat/Scenes/CombatSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            yield return null;
            var sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            Set(sandbox, "_autoHit", false);
            _combat = sandbox.GetComponent<CombatRunController>();
            _controller = sandbox.GetComponentInChildren<LaserSkillController>(true);
            _field = sandbox.gameObject.AddComponent<BattlefieldArea>();
            Set(_combat, "battlefield", _field);
            _combat.TryAbortRun(RunEndReason.UserExit, default, out _);
            Assert.That(_combat.StartRun(new RunRequest("diagonal-laser", 30f),
                new CombatSnapshot(1L, 1f, 0.8f, 1)), Is.True);
            _targets = sandbox.GetComponentsInChildren<MonsterCombatTarget>();
            Assert.That(_targets.Length, Is.GreaterThanOrEqualTo(4));
            for (int i = 0; i < _targets.Length; i++)
            {
                _targets[i].enabled = false;
                if (i >= 4) { _targets[i].gameObject.SetActive(false); continue; }
                _hits[i] = 0;
                _targets[i].Initialize(new SpawnSnapshot("sandbox", "sandbox", 100000L,
                    1f, 1, 80, 0L), _combat.CurrentRunId);
                _targets[i].transform.position = i == 0 ? Vector3.zero : Vector3.left * (100 + i);
            }
            _combat.DamageApplied += OnDamage;
        }

        private void OnDamage(CombatDamageApplied damage)
        {
            if (damage.Source != CombatDamageSource.Skill) return;
            Assert.That(damage.EffectiveDamage, Is.EqualTo(11L), "Authored laser damage must remain 11.");
            for (int i = 0; i < 4; i++)
                if (ReferenceEquals(damage.Target, _targets[i])) _hits[i]++;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (_combat != null) _combat.DamageApplied -= OnDamage;
            Time.timeScale = _timeScale;
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }

        private IEnumerator Cast()
        {
            Assert.That(_combat.ConfigureSkills(new[]
                { new SkillCombatSnapshot(LaserSkillController.SkillId, true) }), Is.True);
            _combat.AdvanceTime(0.01f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WarningFireAfterglowPreserveDamageCadenceAndPoolReuse()
        {
            yield return Cast();
            LaserEffect beam = _controller.GetComponentInChildren<LaserEffect>();
            Assert.That(beam, Is.Not.Null);
            float castAt = _combat.ElapsedSeconds;
            BoxCollider2D box = beam.GetComponentInChildren<BoxCollider2D>();
            Vector3 center = beam.transform.position;
            Vector3 along = beam.transform.up;
            Vector3 side = beam.transform.right;
            Assert.That(Mathf.Abs(along.x), Is.GreaterThan(0.7f));
            Assert.That(Mathf.Abs(along.y), Is.InRange(0.4f, 0.6f));
            Assert.That(box.size.x, Is.EqualTo(1.6f * _field.SkillScale).Within(0.001f));
            Assert.That(beam.Phase, Is.EqualTo(LaserEffectPhase.Telegraph));
            Assert.That(box.enabled, Is.False);
            _targets[0].transform.position = center - along * 3f;
            _targets[1].transform.position = center + along * 3f;
            _targets[2].transform.position = center + side * 4f;
            _combat.AdvanceTime(0.1f);
            yield return null;
            Assert.That(_hits, Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Assert.That(beam.transform.position, Is.EqualTo(center), "The advertised path stays locked.");
            var core = beam.transform.Find("Beam core").GetComponent<LineRenderer>();
            Color warningColor = core.startColor;
            Assert.That(_combat.PauseRun(), Is.True);
            _combat.AdvanceTime(5f);
            yield return null;
            Assert.That(beam.Phase, Is.EqualTo(LaserEffectPhase.Telegraph));
            Assert.That(core.startColor, Is.EqualTo(warningColor));
            Assert.That(_combat.ResumeRun(), Is.True);
            _combat.AdvanceTime(0.1f);
            yield return null;
            Assert.That(beam.Phase, Is.EqualTo(LaserEffectPhase.Firing));
            Assert.That(box.enabled, Is.True);
            Assert.That(_hits, Is.EqualTo(new[] { 1, 1, 0, 0 }));
            _targets[2].transform.position = center;
            _combat.AdvanceTime(0.1f);
            yield return null;
            Assert.That(_hits, Is.EqualTo(new[] { 1, 1, 1, 0 }));
            _combat.AdvanceTime(0.25f);
            yield return null;
            Assert.That(beam.Phase, Is.EqualTo(LaserEffectPhase.Afterglow));
            Assert.That(box.enabled, Is.False);
            Assert.That(core.enabled, Is.True);
            _targets[3].transform.position = center;
            _combat.AdvanceTime(0.1f);
            yield return null;
            Assert.That(_hits[3], Is.Zero, "Afterglow is visual only.");
            _combat.AdvanceTime(0.4f);
            yield return null;
            Assert.That(beam.IsActive, Is.False);
            Assert.That(core.enabled, Is.False);
            _combat.AdvanceTime(castAt + 4.99f - _combat.ElapsedSeconds);
            yield return null;
            Assert.That(_controller.ActiveEffectCount, Is.Zero);
            _combat.AdvanceTime(0.02f);
            yield return null;
            Assert.That(_controller.ActiveEffectCount, Is.EqualTo(1));
            Assert.That(beam.IsActive, Is.True, "Reuse the prewarmed beam.");
            Assert.That(beam.Phase, Is.EqualTo(LaserEffectPhase.Telegraph));
            _targets[0].transform.position = beam.transform.position;
            _combat.AdvanceTime(0.2f);
            yield return null;
            Assert.That(_hits[0], Is.EqualTo(2), "A new cast clears its previous hit history.");
            _combat.TryAbortRun(RunEndReason.UserExit, default, out _);
            Assert.That(beam.Phase, Is.EqualTo(LaserEffectPhase.Inactive));
            Assert.That(core.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator LongFrameFiresOnceButRunExpiryCancelsWarning()
        {
            yield return Cast();
            LaserEffect beam = _controller.GetComponentInChildren<LaserEffect>();
            _targets[0].transform.position = beam.transform.position;
            _combat.AdvanceTime(1.2f);
            yield return null;
            Assert.That(_hits[0], Is.EqualTo(1));
            Assert.That(beam.IsActive, Is.False);
            _combat.AdvanceTime(4f);
            yield return null;
            Assert.That(beam.Phase, Is.EqualTo(LaserEffectPhase.Telegraph));
            _targets[0].transform.position = beam.transform.position;
            _combat.AdvanceTime(30f);
            yield return null;
            Assert.That(_hits[0], Is.EqualTo(1), "Run timeout wins over a queued fire.");
            Assert.That(beam.IsActive, Is.False);
        }

        [UnityTest]
        public IEnumerator LiveResizeKeepsAdvertisedAxisPhaseAndHitHistory()
        {
            yield return Cast();
            LaserEffect beam = _controller.GetComponentInChildren<LaserEffect>();
            _targets[0].transform.position = beam.transform.position;
            _combat.AdvanceTime(0.22f);
            yield return null;
            Assert.That(_hits[0], Is.EqualTo(1));
            Quaternion rotation = beam.transform.rotation;
            var box = beam.GetComponentInChildren<BoxCollider2D>();
            float width = box.size.x;
            var settings = _controller.CaptureBalanceSettings();
            settings.Values.Find(v => v.Id == "sizeMultiplier").Value *= 1.5f;
            for (int i = 0; i < 3; i++)
                Assert.That(_controller.ApplyBalanceSettings(_combat, _combat.CurrentRunId, 99UL, settings), Is.True);
            Assert.That(box.size.x, Is.EqualTo(width * 1.5f).Within(0.001f));
            Assert.That(beam.transform.rotation, Is.EqualTo(rotation));
            Assert.That(beam.Phase, Is.EqualTo(LaserEffectPhase.Firing));
            _combat.AdvanceTime(0.1f);
            yield return null;
            Assert.That(_hits[0], Is.EqualTo(1));
            foreach (float x in new[] { -0.5f, 0.5f })
                foreach (float y in new[] { -0.5f, 0.5f })
                    Assert.That(_field.Contains(box.transform.TransformPoint(
                        box.offset + new Vector2(x * box.size.x, y * box.size.y))), Is.True);
        }

        private static void Set(object instance, string name, object value) =>
            instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    }
}
