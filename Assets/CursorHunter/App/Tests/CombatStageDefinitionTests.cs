using System.Reflection;
using CursorHunter.Contracts;
using CursorHunter.Data;
using NUnit.Framework;
using UnityEngine;

namespace CursorHunter.App.Tests
{
    public sealed class CombatStageDefinitionTests
    {
        private CombatStageDefinition _stage;
        private MonsterDefinition _first;
        private MonsterDefinition _second;
        private GameObject _firstVisual;
        private GameObject _secondVisual;

        [SetUp]
        public void SetUp()
        {
            _stage = ScriptableObject.CreateInstance<CombatStageDefinition>();
            _first = ScriptableObject.CreateInstance<MonsterDefinition>();
            _second = ScriptableObject.CreateInstance<MonsterDefinition>();
            _firstVisual = new GameObject("FirstVisual");
            _secondVisual = new GameObject("SecondVisual");
            SetField(_first, "monsterId", "monster.01");
            SetField(_first, "visualPrefab", _firstVisual);
            SetField(_second, "monsterId", "monster.02");
            SetField(_second, "visualPrefab", _secondVisual);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_stage);
            Object.DestroyImmediate(_first);
            Object.DestroyImmediate(_second);
            Object.DestroyImmediate(_firstVisual);
            Object.DestroyImmediate(_secondVisual);
        }

        [Test]
        public void StageWhitelistsMonstersAndUsesSelectedProductionNode()
        {
            GameInformation playerInformation = GameDataDocument.Load().information;
            playerInformation.monsters[0].enabled = false;
            playerInformation.monsters[1].enabled = true;
            GameInformation runInformation = Copy(playerInformation);
            SetRules(
                Rule(_first, true, StageProductionOption.Production4),
                Rule(_second, false, StageProductionOption.UsePlayerUpgrade));

            Assert.That(_stage.TryApplyToInformation(runInformation, out string error),
                Is.True, error);
            Assert.That(runInformation.monsters[0].enabled, Is.True);
            Assert.That(runInformation.monsters[0].productionCount, Is.EqualTo(4));
            Assert.That(runInformation.monsters[1].enabled, Is.False);
            Assert.That(playerInformation.monsters[0].enabled, Is.False);
            Assert.That(playerInformation.monsters[1].enabled, Is.True);

            string sourceJson = GameInformationJson.Serialize(runInformation);
            ProgressionCombatSnapshot runSnapshot =
                runInformation.ToCombatSnapshot(sourceJson);
            Assert.That(_stage.TryCreateSpawnPlan(
                runSnapshot, out var plan, out error), Is.True, error);
            Assert.That(plan.Entries.Count, Is.EqualTo(1));
            Assert.That(plan.Entries[0].Snapshot.MonsterId, Is.EqualTo("monster.01"));
            Assert.That(plan.Entries[0].Snapshot.PackSize,
                Is.EqualTo(_first.BaseStats.PackSize + 3));
            Assert.That(runSnapshot.SourceJson, Is.EqualTo(sourceJson));
        }

        [Test]
        public void UsePlayerUpgradePreservesResolvedProductionCount()
        {
            GameInformation information = GameDataDocument.Load().information;
            information.monsters[1].productionCount = 3;
            SetRules(Rule(_second, true, StageProductionOption.UsePlayerUpgrade));

            Assert.That(_stage.TryApplyToInformation(information, out string error),
                Is.True, error);
            Assert.That(information.monsters[1].productionCount, Is.EqualTo(3));
            var runSnapshot = information.ToCombatSnapshot(
                GameInformationJson.Serialize(information));
            Assert.That(_stage.TryCreateSpawnPlan(
                runSnapshot, out var plan, out error), Is.True, error);
            Assert.That(plan.Entries[0].Snapshot.PackSize,
                Is.EqualTo(_second.BaseStats.PackSize + 2));
        }

        [Test]
        public void DuplicateOrEmptyRosterCannotStart()
        {
            GameInformation information = GameDataDocument.Load().information;
            SetRules(
                Rule(_first, true, StageProductionOption.Base),
                Rule(_first, true, StageProductionOption.Production2));
            Assert.That(_stage.TryApplyToInformation(information, out _), Is.False);

            SetRules(Rule(_first, false, StageProductionOption.Base));
            Assert.That(_stage.TryApplyToInformation(information, out _), Is.False);
        }

        private void SetRules(params StageMonsterSpawnRule[] rules)
        {
            SetField(_stage, "monsters", rules);
        }

        private static StageMonsterSpawnRule Rule(
            MonsterDefinition monster,
            bool allowSpawn,
            StageProductionOption production)
        {
            var rule = new StageMonsterSpawnRule();
            SetField(rule, "monster", monster);
            SetField(rule, "allowSpawn", allowSpawn);
            SetField(rule, "production", production);
            return rule;
        }

        private static GameInformation Copy(GameInformation value)
        {
            return JsonUtility.FromJson<GameInformation>(JsonUtility.ToJson(value));
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing field: " + name);
            field.SetValue(target, value);
        }
    }
}
