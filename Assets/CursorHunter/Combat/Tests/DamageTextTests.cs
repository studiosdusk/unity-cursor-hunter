using System.Reflection;
using CursorHunter.Contracts;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CursorHunter.Combat.Tests
{
    public sealed class DamageTextTests
    {
        private const string PrefabPath = "Assets/CursorHunter/UI/Scripts/DamageText/DamageText_Root.prefab";
        private GameObject _root;
        private CombatRunController _combat;
        private DamageTextManager _manager;
        private static readonly MethodInfo HandleHit = typeof(DamageTextManager).GetMethod(
            "HandleDamageApplied", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ApplyHit = typeof(CombatRunController).GetMethod(
            "ApplyBundle", BindingFlags.Instance | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("DamageTextTests");
            _combat = _root.AddComponent<CombatRunController>();
            StartRun("damage-test");
            _manager = _root.AddComponent<DamageTextManager>();
            var settings = new SerializedObject(_manager);
            settings.FindProperty("combatRunController").objectReferenceValue = _combat;
            settings.FindProperty("damageTextPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<DamageText_Root>();
            settings.FindProperty("initialPoolSize").intValue = 2;
            settings.FindProperty("maximumPoolSize").intValue = 4;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(_manager.Initialize(), Is.True);
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_root); }

        private void StartRun(string id, float criticalChance = 0f)
        {
            Assert.That(_combat.StartRun(new RunRequest(id, 30f),
                new CombatSnapshot(10L, 1f, 0.1f, 1, criticalChance, 1f, true, 0.1f)), Is.True);
        }

        private void Show(FakeTarget target, long damage = 10L, bool critical = false, RunId? run = null)
        {
            HandleHit.Invoke(_manager, new object[] { new CombatDamageApplied(
                run ?? _combat.CurrentRunId, target, damage, critical, target.DamageTextPosition, true) });
        }

        [Test]
        public void MergeIsLimitedToSameTargetAndSameHitKind()
        {
            var a = new FakeTarget();
            Show(a, 12); Show(a, 34);
            Assert.That(_manager.ActiveCount, Is.EqualTo(1));
            Assert.That(_manager.MergedCount, Is.EqualTo(1));
            Assert.That(ActiveText().GetParsedText(), Is.EqualTo("46"));
            Show(a, 10, true); Show(new FakeTarget());
            Assert.That(_manager.ActiveCount, Is.EqualTo(3));
        }

        [Test]
        public void MergeWindowDoesNotSlideOrExtendLifetime()
        {
            var target = new FakeTarget();
            Show(target);
            _manager.AdvancePresentation(0.08f);
            Show(target);
            _manager.AdvancePresentation(0.08f);
            Show(target);
            Assert.That(_manager.ActiveCount, Is.EqualTo(2));
            _manager.AdvancePresentation(0.65f);
            Assert.That(_manager.ActiveCount, Is.EqualTo(1));
            _manager.AdvancePresentation(0.16f);
            Assert.That(_manager.ActiveCount, Is.Zero);
        }

        [Test]
        public void ThousandsOfRequestsStayWithinCapacity()
        {
            var targets = new FakeTarget[8];
            for (int i = 0; i < targets.Length; i++) targets[i] = new FakeTarget();
            for (int i = 0; i < 10000; i++) Show(targets[i % targets.Length]);
            Assert.That(_manager.CreatedCount, Is.EqualTo(4));
            Assert.That(_manager.ActiveCount, Is.EqualTo(4));
            Assert.That(_manager.PeakActiveCount, Is.EqualTo(4));
            Assert.That(_manager.RecycledCount, Is.EqualTo(9996));
            _manager.AdvancePresentation(1f);
            Show(targets[0]);
            Assert.That(_manager.CreatedCount, Is.EqualTo(4));
        }

        [Test]
        public void CriticalNumbersDisplaceNormalsAndRejectNormalPressure()
        {
            for (int i = 0; i < 4; i++) Show(new FakeTarget());
            for (int i = 0; i < 4; i++) Show(new FakeTarget(), 20, true);
            Assert.That(_manager.RecycledCount, Is.EqualTo(4));
            Show(new FakeTarget());
            Assert.That(_manager.SuppressedCount, Is.EqualTo(1));
            Show(new FakeTarget(), 20, true);
            Assert.That(_manager.RecycledCount, Is.EqualTo(5));
            Assert.That(_manager.ActiveCount, Is.EqualTo(4));
        }

        [Test]
        public void LongFormattingAndMergeOverflowPreserveExactNumbers()
        {
            var target = new FakeTarget();
            Show(target, long.MaxValue);
            TMP_Text text = ActiveText();
            Assert.That(text.GetParsedText(), Is.EqualTo("9223372036854775807"));
            Show(target, 1L);
            Assert.That(_manager.ActiveCount, Is.EqualTo(2));
            Assert.That(_manager.MergedCount, Is.Zero);
        }

        [Test]
        public void PauseFreezesPresentationAndStopClearsImmediately()
        {
            Show(new FakeTarget());
            Assert.That(_combat.PauseRun(), Is.True);
            _manager.AdvancePresentation(2f);
            Assert.That(_manager.ActiveCount, Is.EqualTo(1));
            Assert.That(_combat.TryAbortRun(RunEndReason.UserExit, default, out _), Is.True);
            Assert.That(_manager.ActiveCount, Is.Zero);
        }

        [Test]
        public void RestartRejectsOldRunAndDisableUnsubscribes()
        {
            RunId oldRun = _combat.CurrentRunId;
            Show(new FakeTarget());
            _combat.TryAbortRun(RunEndReason.UserExit, default, out _);
            StartRun("next-run");
            Show(new FakeTarget(), run: oldRun);
            Assert.That(_manager.ActiveCount, Is.Zero);
            _manager.enabled = false;
            _manager.enabled = true;
            Assert.That(_manager.Initialize(), Is.True);
            var target = new FakeTarget { RunId = _combat.CurrentRunId };
            ApplyHit.Invoke(_combat, new object[] { target, 0L });
            Assert.That(_manager.ActiveCount, Is.EqualTo(1));
            Assert.That(_manager.MergedCount, Is.Zero, "Duplicate subscriptions would merge the event twice.");
        }

        [Test]
        public void EventCarriesClampedLethalDamageAndPreDeathAnchor()
        {
            var target = new FakeTarget { RunId = _combat.CurrentRunId, Health = 3L };
            CombatDamageApplied captured = default;
            int count = 0;
            _combat.DamageApplied += hit => { captured = hit; count++; };
            ApplyHit.Invoke(_combat, new object[] { target, 0L });
            Assert.That(count, Is.EqualTo(1));
            Assert.That(captured.EffectiveDamage, Is.EqualTo(3L));
            Assert.That(captured.WorldPosition, Is.EqualTo(new Vector3(1f, 2f, 0f)));
            Assert.That(_manager.ActiveCount, Is.EqualTo(1));
            ApplyHit.Invoke(_combat, new object[] { target, 0L });
            Assert.That(count, Is.EqualTo(1), "Dead target must not emit another number.");
        }

        [Test]
        public void SkillDamageAndCriticalFlagUseTheSameEvent()
        {
            _combat.TryAbortRun(RunEndReason.UserExit, default, out _);
            StartRun("critical-run", 100f);
            var target = new FakeTarget { RunId = _combat.CurrentRunId, Health = 1000L };
            CombatDamageApplied captured = default;
            _combat.DamageApplied += hit => captured = hit;
            ApplyHit.Invoke(_combat, new object[] { target, 25L });
            Assert.That(captured.IsCritical, Is.True);
            Assert.That(captured.EffectiveDamage, Is.EqualTo(50L));
            Assert.That(ActiveText().name, Is.EqualTo("DamageText_Critical"));
        }

        [Test]
        public void FadeAndReuseKeepFontsMaterialsAndRestoreOpacity()
        {
            Show(new FakeTarget(), 1234L);
            TMP_Text text = ActiveText();
            var font = text.font;
            var material = text.fontSharedMaterial;
            _manager.AdvancePresentation(0.7f);
            Assert.That(text.textInfo.meshInfo[0].colors32[0].a, Is.LessThan(255));
            _manager.AdvancePresentation(0.2f);
            Show(new FakeTarget(), 12L, true);
            _manager.AdvancePresentation(1f);
            Show(new FakeTarget(), 5L);
            TMP_Text reused = ActiveText();
            Assert.That(reused, Is.SameAs(text));
            Assert.That(reused.font, Is.SameAs(font));
            Assert.That(reused.fontSharedMaterial, Is.SameAs(material));
            Assert.That(reused.textInfo.meshInfo[0].colors32[0].a, Is.EqualTo(255));
            Assert.That(_root.GetComponentsInChildren<TMP_Text>().Length, Is.EqualTo(1));
        }

        private TMP_Text ActiveText() => _root.GetComponentInChildren<TMP_Text>();

        private sealed class FakeTarget : ICombatTarget, IDamageTextAnchor
        {
            public RunId RunId { get; set; }
            public long Health = 100L;
            public bool IsRegistered => Health > 0L;
            public bool IsActive => Health > 0L;
            public long GarnetReward => 0L;
            public string BonusDropCurrencyId => string.Empty;
            public long BonusDropAmount => 0L;
            public float BonusDropChancePercent => 0f;
            public Vector3 DamageTextPosition => Health > 0L ? new Vector3(1f, 2f, 0f) : Vector3.zero;
            public bool ApplyDamage(RunId runId, long damage, out long effectiveDamage, out bool killed)
            {
                effectiveDamage = 0L; killed = false;
                if (!IsActive || runId != RunId || damage <= 0L) return false;
                effectiveDamage = System.Math.Min(Health, damage);
                Health -= effectiveDamage;
                killed = Health == 0L;
                return true;
            }
        }
    }
}
