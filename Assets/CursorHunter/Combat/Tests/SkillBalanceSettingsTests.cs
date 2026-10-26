using System;
using NUnit.Framework;
using UnityEngine;

namespace CursorHunter.Combat.Tests
{
    public sealed class SkillBalanceSettingsTests
    {
        [TestCase(typeof(TornadoSkillController), 11)]
        [TestCase(typeof(HeadshotSkillController), 6)]
        [TestCase(typeof(ThunderSkillController), 5)]
        [TestCase(typeof(FlameExhaustSkillController), 8)]
        [TestCase(typeof(MagicBallSkillController), 5)]
        [TestCase(typeof(LaserSkillController), 5)]
        [TestCase(typeof(PetSkillController), 7)]
        public void EveryAuthoredSkillFieldIsExposedWithFiniteMetadata(Type type, int count)
        {
            var root = new GameObject("Balance schema");
            try
            {
                var skill = (ISkillBalanceController)root.AddComponent(type);
                SkillBalanceSettings settings = skill.CaptureBalanceSettings();
                Assert.That(settings.Values.Count, Is.EqualTo(count));
                settings.Enabled = false;
                Assert.That(skill.ValidateBalanceSettings(settings, out string error),
                    Is.True, error);
                foreach (SkillBalanceValue value in settings.Values)
                {
                    Assert.That(float.IsNaN(value.Minimum) || float.IsInfinity(value.Minimum),
                        Is.False, value.Id);
                    Assert.That(float.IsNaN(value.Maximum) || float.IsInfinity(value.Maximum),
                        Is.False, value.Id);
                    Assert.That(value.Value, Is.InRange(value.Minimum, value.Maximum), value.Id);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ValidationUsesRuntimeSchemaAndRejectsInvalidOrRepeatedValues()
        {
            var root = new GameObject("Balance validation");
            try
            {
                var skill = root.AddComponent<FlameExhaustSkillController>();
                SkillBalanceSettings settings = skill.CaptureBalanceSettings();
                settings.Enabled = false;
                SkillBalanceValue ratio = settings.Values.Find(value => value.Id == "cursorSizeRatio");
                ratio.Minimum = -100f;
                ratio.Maximum = 100f;
                ratio.Value = 2f;
                Assert.That(skill.ValidateBalanceSettings(settings, out _), Is.False,
                    "Editable DTO metadata must not widen the actual runtime range.");
                ratio.Value = float.NaN;
                Assert.That(skill.ValidateBalanceSettings(settings, out _), Is.False);
                ratio.Value = 0.5f;
                settings.Values[0].Id = settings.Values[1].Id;
                Assert.That(skill.ValidateBalanceSettings(settings, out _), Is.False);
                Assert.That(skill.CaptureBalanceSettings().Values.Find(
                    value => value.Id == "cursorSizeRatio").Value, Is.EqualTo(0.45f));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void CaptureAndCloneCannotMutateAuthoredValues()
        {
            var root = new GameObject("Balance copy");
            try
            {
                var skill = root.AddComponent<MagicBallSkillController>();
                SkillBalanceSettings original = skill.CaptureBalanceSettings();
                SkillBalanceSettings copy = original.Clone();
                copy.Values.Find(value => value.Id == "damageMultiplier").Value = 99f;
                Assert.That(original.Values.Find(value => value.Id == "damageMultiplier").Value,
                    Is.EqualTo(1f));
                Assert.That(skill.CaptureBalanceSettings().Values.Find(
                    value => value.Id == "damageMultiplier").Value, Is.EqualTo(1f));
                Assert.That(copy.Values.Exists(value => value.Id == "cooldownSeconds"), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
