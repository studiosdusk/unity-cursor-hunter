using System.Reflection;
using CursorHunter.Contracts;
using CursorHunter.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CursorHunter.App.Tests
{
    public sealed class MonsterAuthoredBalanceTests
    {
        private MonsterDefinition _definition;
        private MonsterBehaviorProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<MonsterDefinition>();
            _profile = ScriptableObject.CreateInstance<MonsterBehaviorProfile>();
            SetField(_definition, "monsterId", "monster.07");
            SetField(_definition, "behaviorProfiles", new[] { _profile });
            SetField(_profile, "healthMultiplier", 2.5f);
            SetField(_profile, "moveSpeedMultiplier", 1.8f);
            SetField(_profile, "visualScaleMultiplier", 2f);
            SetField(_profile, "hitAreaScaleMultiplier", 1.8f);
            SetField(_profile, "overridesMovementMode", true);
            SetField(_profile, "movementMode", MonsterMovementMode.Circular);
            SetField(_profile, "orbitRadius", 1.1f);
            SetField(_profile, "orbitAngularSpeedDegrees", 75f);
            SetField(_profile, "specialAbility", MonsterSpecialAbility.Shield);
            SetField(_profile, "specialAbilityIntervalSeconds", 4.5f);
            SetField(_profile, "specialAbilityDurationSeconds", 1.7f);
            SetField(_profile, "specialAbilityRadius", 3.2f);
            SetField(_profile, "specialAbilityStrength", 0.65f);
            SetField(_definition.BaseStats, "maxHealth", 20L);
            SetField(_definition.BaseStats, "moveSpeed", 1.25f);
            SetField(_definition.BaseStats, "visualScale", 1.45f);
            SetField(_definition.BaseStats, "hitAreaWidth", 0.9f);
            SetField(_definition.BaseStats, "hitAreaHeight", 1.3f);
            SetField(_definition.BaseStats, "packSize", 3);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_definition);
            Object.DestroyImmediate(_profile);
        }

        [Test]
        public void ExistingDefinitionsStillApplyAllProfileStatMultipliers()
        {
            Assert.That(_definition.BaseStatsAreFinalValues, Is.False);
            SpawnSnapshot snapshot = _definition.CreateSnapshot(12);
            Assert.That(snapshot.IsValid, Is.True);
            Assert.That(snapshot.MaxHealth, Is.EqualTo(50L));
            Assert.That(snapshot.MoveSpeed, Is.EqualTo(2.25f).Within(0.00001f));
            Assert.That(snapshot.VisualScale, Is.EqualTo(2.9f).Within(0.00001f));
            Assert.That(snapshot.HitAreaWidth, Is.EqualTo(1.62f).Within(0.00001f));
            Assert.That(snapshot.HitAreaHeight, Is.EqualTo(2.34f).Within(0.00001f));
            Assert.That(snapshot.PackSize, Is.EqualTo(3));
        }

        [TestCase(101L)]
        [TestCase(9007199254740993L)]
        [TestCase(long.MaxValue - 1L)]
        [TestCase(long.MaxValue)]
        public void SavedFinalValuesBypassMultipliersAndKeepExactInt64Health(long maxHealth)
        {
            // Exercise the same serialized field paths as the editor's source writer.
            var serialized = new SerializedObject(_definition);
            serialized.FindProperty("baseStatsAreFinalValues").boolValue = true;
            serialized.FindProperty("baseStats.maxHealth").longValue = maxHealth;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SpawnSnapshot snapshot = _definition.CreateSnapshot(12);
            Assert.That(_definition.BaseStatsAreFinalValues, Is.True);
            Assert.That(snapshot.IsValid, Is.True);
            Assert.That(snapshot.MaxHealth, Is.EqualTo(maxHealth));
            Assert.That(snapshot.MoveSpeed, Is.EqualTo(1.25f));
            Assert.That(snapshot.VisualScale, Is.EqualTo(1.45f));
            Assert.That(snapshot.HitAreaWidth, Is.EqualTo(0.9f));
            Assert.That(snapshot.HitAreaHeight, Is.EqualTo(1.3f));
            Assert.That(_profile.HealthMultiplier, Is.EqualTo(2.5f));
            Assert.That(_profile.MoveSpeedMultiplier, Is.EqualTo(1.8f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FinalValuePolicyPreservesMovementAbilitiesAndProductionBonuses(bool finalValues)
        {
            SetField(_definition, "baseStatsAreFinalValues", finalValues);
            SetField(_definition.BaseStats, "spawnIntervalSeconds", 2.3f);
            SetField(_definition.BaseStats, "garnetReward", 13L);
            SetField(_definition.BaseStats, "bonusDropCurrencyId", "gem.sapphire");
            SetField(_definition.BaseStats, "bonusDropAmount", 2L);
            SetField(_definition.BaseStats, "bonusDropChancePercent", 25f);
            SpawnSnapshot snapshot = _definition.CreateSnapshotWithProductionBonus(12, 2);

            Assert.That(snapshot.MovementMode, Is.EqualTo(MonsterMovementMode.Circular));
            Assert.That(snapshot.OrbitRadius, Is.EqualTo(1.1f));
            Assert.That(snapshot.OrbitAngularSpeedDegrees, Is.EqualTo(75f));
            Assert.That(snapshot.SpecialAbility, Is.EqualTo(MonsterSpecialAbility.Shield));
            Assert.That(snapshot.SpecialAbilityIntervalSeconds, Is.EqualTo(4.5f));
            Assert.That(snapshot.SpecialAbilityDurationSeconds, Is.EqualTo(1.7f));
            Assert.That(snapshot.SpecialAbilityRadius, Is.EqualTo(3.2f));
            Assert.That(snapshot.SpecialAbilityStrength, Is.EqualTo(0.65f));
            Assert.That(snapshot.PackSize, Is.EqualTo(5));
            Assert.That(_definition.CreateSnapshot(12, 7).PackSize, Is.EqualTo(7));
            Assert.That(snapshot.AliveLimit, Is.EqualTo(12));
            Assert.That(snapshot.SpawnIntervalSeconds, Is.EqualTo(2.3f));
            Assert.That(snapshot.GarnetReward, Is.EqualTo(13L));
            Assert.That(snapshot.BonusDropCurrencyId, Is.EqualTo("gem.sapphire"));
            Assert.That(snapshot.BonusDropAmount, Is.EqualTo(2L));
            Assert.That(snapshot.BonusDropChancePercent, Is.EqualTo(25f));
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing field: " + name);
            field.SetValue(target, value);
        }
    }
}
