using System.Collections.Generic;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace CursorHunter.App.Tests
{
    public sealed class CombatBalanceSettingsTests
    {
        [Test]
        public void FinalValuesAndInt64SurviveThePlayTransitionCopy()
        {
            var source = new CombatBalanceSettings { AttackPower = long.MaxValue };
            var copy = source.Clone();
            Assert.That(copy.TryValidate(out string error), Is.True, error);
            Assert.That(copy.ToCombatSnapshot().AttackPower, Is.EqualTo(long.MaxValue));
            copy.AttackPower = 20L;
            Assert.That(copy.ToCombatSnapshot().AttackPower, Is.EqualTo(20L));
            Assert.That(source.AttackPower, Is.EqualTo(long.MaxValue));
        }

        [TestCase(0.001f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void UnrepresentableCursorRadiusIsRejected(float radius)
        {
            var settings = new CombatBalanceSettings { AttackRadiusWorldUnits = radius };
            Assert.That(settings.TryValidate(out _), Is.False);
        }

        [Test]
        public void LiveChangesKeepRunIdentityClockAndRejectInvalidSkills()
        {
            var root = new GameObject("Balance clock test");
            try
            {
                var combat = root.AddComponent<CombatRunController>();
                var request = new RunRequest("balance-clock", 30f);
                Assert.That(combat.StartRun(request,
                    new CombatSnapshot(10L, 1f, 0.8f, 1)), Is.True);
                combat.AdvanceTime(20f);
                var settings = new CombatBalanceSettings { AttackPower = 20L, DurationSeconds = 15f };
                Assert.That(combat.TryApplyLiveBalance(settings.ToCombatSnapshot(), 15f,
                    new List<SkillBalanceSettings>(), out string error), Is.True, error);
                Assert.That(combat.CurrentRunId, Is.EqualTo(request.RunId));
                Assert.That(combat.ElapsedSeconds, Is.EqualTo(20f));
                Assert.That(combat.CombatSnapshot.AttackPower, Is.EqualTo(20L));
                bool expired = false;
                combat.CompletionRequested += _ => expired = true;
                combat.AdvanceTime(0.01f);
                Assert.That(expired, Is.True);
                Assert.That(combat.ElapsedSeconds, Is.EqualTo(20f).Within(0.0001f),
                    "Shortening a deadline must not rewind elapsed gameplay.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
