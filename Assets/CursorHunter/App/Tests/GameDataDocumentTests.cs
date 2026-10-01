using System.Collections.Generic;
using CursorHunter.Contracts;
using CursorHunter.Data;
using CursorHunter.Progression;
using NUnit.Framework;
using UnityEngine;

namespace CursorHunter.App.Tests
{
    // Provided for later Editor execution; Unity is not launched by this task.
    public sealed class GameDataDocumentTests
    {
        private static GameInformation Build(GameDataDocument document, HashSet<string> purchased = null)
        {
            return GameInformationBuilder.Build(document, purchased ?? new HashSet<string>(),
                id => System.Array.Find(document.information.gemstones, value => value.id == id).amount);
        }

        [Test]
        public void OnlyEnglishRuntimeDocumentIsAccepted()
        {
            var document = GameDataDocument.Load();
            Assert.That(GameDataDocument.RuntimeResourcePath, Is.EqualTo("GameData/game-data.en"));
            Assert.That(document.locale, Is.EqualTo("en"));
            Assert.That(document.progression.categories.Length, Is.EqualTo(23));
            int count = 0;
            foreach (var category in document.progression.categories) count += category.nodes.Length;
            Assert.That(count, Is.EqualTo(111));
            document.locale = "ko";
            Assert.That(document.TryValidate(out _), Is.False);
            Assert.That(GameDataDocument.TryParse("{\"문서안내\":{}}", "{}", out _, out _), Is.False);
        }

        [Test]
        public void CombatJsonAndGrowthConfigurationAreSeparate()
        {
            var infoJson = Resources.Load<TextAsset>(GameDataDocument.RuntimeResourcePath).text;
            var configJson = Resources.Load<TextAsset>(GameDataDocument.ProgressionResourcePath).text;
            Assert.That(GameDataDocument.TryParse(infoJson, configJson, out var document, out var error), Is.True, error);
            Assert.That(infoJson, Does.Not.Contain("\"progression\""));
            Assert.That(infoJson, Does.Not.Contain("\"relics\""));
            Assert.That(configJson, Does.Not.Contain("\"information\""));
            Assert.That(GameDataDocument.TryParse(configJson, infoJson, out _, out _), Is.False);
            Assert.That(GameDataDocument.TryParse(infoJson, null, out _, out _), Is.False);
            Assert.That(GameInformationJson.Serialize(Build(document)), Does.Not.Contain("\"categories\""));
        }

        [Test]
        public void BaselineEditsAreConsumedAndSourceIsNeverMutated()
        {
            var document = GameDataDocument.Load();
            document.information.stats.attackPower = 10;
            document.information.gemstones[0].amount = long.MaxValue;
            document.information.monsters[0].hitPoints = 123;
            document.information.monsters[0].productionCount = 3;
            document.information.skills[0].damage = 41;
            document.information.rules.criticalDamageMultiplier = 3;
            document.information.rules.globalAliveLimit = 40;
            document.information.rules.perMonsterAliveLimit = 20;
            string before = JsonUtility.ToJson(document.information);
            var result = Build(document);
            Assert.That(result.stats.attackPower, Is.EqualTo(10));
            Assert.That(result.skills[0].damage, Is.EqualTo(41));
            Assert.That(result.monsters[0].hitPoints, Is.EqualTo(123));
            Assert.That(result.monsters[0].productionCount, Is.EqualTo(3));
            Assert.That(result.gemstones[0].amount, Is.EqualTo(long.MaxValue));
            var snapshot = result.ToCombatSnapshot(GameInformationJson.Serialize(result));
            Assert.That(snapshot.Combat.CriticalDamageMultiplier, Is.EqualTo(3));
            Assert.That(snapshot.GlobalAliveLimit, Is.EqualTo(40));
            Assert.That(snapshot.PerMonsterAliveLimit, Is.EqualTo(20));
            Assert.That(JsonUtility.ToJson(document.information), Is.EqualTo(before));
        }

        [Test]
        public void EveryPurchasedNodeUsesItsJsonEffect()
        {
            var document = GameDataDocument.Load();
            var purchased = new HashSet<string>();
            foreach (var category in document.progression.categories)
                foreach (var node in category.nodes) purchased.Add(node.id);
            var result = Build(document, purchased);
            Assert.That(result.stats.attackPower, Is.EqualTo(100));
            Assert.That(result.stats.bossDamageMultiplier, Is.EqualTo(4));
            Assert.That(result.stats.normalFieldDurationSeconds, Is.EqualTo(30));
            Assert.That(result.skills[0].damage, Is.EqualTo(540));
            foreach (var monster in result.monsters)
            {
                Assert.That(monster.enabled, Is.True);
                Assert.That(monster.productionCount, Is.EqualTo(4));
            }
        }

        [Test]
        public void SkillDamageRoundsOnlyAfterAllMultipliers()
        {
            var document = GameDataDocument.Load();
            var result = Build(document, new HashSet<string> { "stat.attack.04", "skill.fireball.damage" });
            Assert.That(result.stats.attackPower, Is.EqualTo(10));
            Assert.That(result.skills[0].damage, Is.EqualTo(54));
        }

        [Test]
        public void BadReferencesAndCyclesCannotBecomeACombatDocument()
        {
            var document = GameDataDocument.Load();
            document.progression.categories[0].nodes[1].prerequisiteNodeId = "missing";
            Assert.That(document.TryValidate(out _), Is.False);
            document.progression.categories[0].nodes[1].prerequisiteNodeId = document.progression.categories[0].nodes[1].id;
            Assert.That(document.TryValidate(out _), Is.False);
        }

        [Test]
        public void InvalidEffectsPoliciesAndMissingSectionsAreRejected()
        {
            var document = GameDataDocument.Load();
            document.progression.savedProgressPolicy = "eraseSave";
            Assert.That(document.TryValidate(out _), Is.False);
            document = GameDataDocument.Load();
            document.progression.categories[2].nodes[1].value = -1;
            Assert.That(document.TryValidate(out _), Is.False);
            Assert.That(GameDataDocument.TryParse("{}", "{}", out _, out _), Is.False);
            Assert.That(GameDataDocument.TryParse(null, null, out _, out _), Is.False);
        }
    }
}
