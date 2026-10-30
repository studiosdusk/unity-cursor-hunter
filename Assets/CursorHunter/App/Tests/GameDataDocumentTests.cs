using System.Collections.Generic;
using CursorHunter.Contracts;
using CursorHunter.Data;
using CursorHunter.Progression;
using NUnit.Framework;
using UnityEngine;

namespace CursorHunter.App.Tests
{
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
            Assert.That(document.progression.categories.Length, Is.EqualTo(22));
            int count = 0;
            foreach (var category in document.progression.categories) count += category.nodes.Length;
            Assert.That(count, Is.EqualTo(103));
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
            Assert.That(infoJson, Does.Contain("\"relics\""));
            Assert.That(Build(document).relics, Has.Length.EqualTo(20));
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
            document.information.skills[0].enabled = true;
            document.information.rules.criticalDamageMultiplier = 3;
            document.information.rules.globalAliveLimit = 40;
            document.information.rules.perMonsterAliveLimit = 20;
            string before = JsonUtility.ToJson(document.information);
            var result = Build(document);
            Assert.That(result.stats.attackPower, Is.EqualTo(10));
            Assert.That(result.skills[0].enabled, Is.True);
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
        public void CombatMonsterDefinitionsMatchJsonHealthAndGemstoneRewards()
        {
            GameDataDocument document = GameDataDocument.Load();
            MonsterDefinition[] definitions = Resources.LoadAll<MonsterDefinition>("MonsterDefinitions");
            var definitionsById = new Dictionary<string, MonsterDefinition>();
            foreach (MonsterDefinition definition in definitions)
                definitionsById.Add(definition.MonsterId, definition);
            Assert.That(definitionsById.TryGetValue("monster.01", out MonsterDefinition baselineDefinition),
                Is.True, "Missing baseline monster definition.");
            SpawnSnapshot baselineSnapshot = baselineDefinition.CreateSnapshot(1);

            foreach (MonsterInformation information in document.information.monsters)
            {
                Assert.That(definitionsById.TryGetValue(information.id, out MonsterDefinition definition),
                    Is.True, "Missing combat definition for " + information.id);
                Assert.That(string.IsNullOrWhiteSpace(definition.DisplayName), Is.False,
                    information.id + " needs its trait-screen display name");
                Assert.That(definition.CreateSnapshot(80).IsValid, Is.True,
                    information.id + " needs a valid combat snapshot for its trait summary");
                Assert.That(string.IsNullOrWhiteSpace(
                    definition.BuildTraitDescription(baselineSnapshot)), Is.False,
                    information.id + " needs a user-facing summary of its combat behavior");
                Assert.That(definition.MaxHealth, Is.EqualTo(information.hitPoints), information.id + " HP");
                Assert.That(definition.SpawnIntervalSeconds,
                    Is.EqualTo(information.spawnIntervalSeconds).Within(0.001f), information.id + " spawn interval");
                Assert.That(definition.GarnetReward, Is.EqualTo(information.garnetReward), information.id + " garnet reward");
                Assert.That(definition.BonusDropAmount, Is.EqualTo(information.gemstoneAmount), information.id + " bonus drop amount");
                Assert.That(definition.BonusDropChancePercent,
                    Is.EqualTo(information.gemstoneChancePercent).Within(0.001f), information.id + " bonus drop chance");
                if (information.gemstoneAmount > 0)
                    Assert.That(definition.BonusDropCurrencyId, Is.EqualTo(information.gemstoneId),
                        information.id + " bonus drop currency");
            }
        }

        [Test]
        public void EveryPurchasedNodeUsesItsJsonEffect()
        {
            var document = GameDataDocument.Load();
            var purchased = new HashSet<string>();
            foreach (var category in document.progression.categories)
                foreach (var node in category.nodes) purchased.Add(node.id);
            var result = Build(document, purchased);
            Assert.That(result.stats.attackPower, Is.EqualTo(36));
            Assert.That(result.stats.bossDamageMultiplier, Is.EqualTo(2f).Within(0.001f));
            Assert.That(result.stats.normalFieldDurationSeconds, Is.EqualTo(30));
            Assert.That(result.skills[0].enabled, Is.True);
            Assert.That(result.skills[6].enabled, Is.True);
            foreach (var monster in result.monsters)
            {
                Assert.That(monster.enabled, Is.True);
                Assert.That(monster.productionCount, Is.EqualTo(5));
            }
        }

        [Test]
        public void TornadoUnlockAndIndependentDamageAndRadiusResearchAreConfigured()
        {
            var document = GameDataDocument.Load();
            UpgradeCategoryData category = System.Array.Find(
                document.progression.categories, value => value.id == "skill.tornado");
            Assert.That(category.nodes, Has.Length.EqualTo(3));
            Assert.That(category.nodes[0].id, Is.EqualTo("skill.tornado"));
            Assert.That(category.nodes[0].operation, Is.EqualTo("enable"));
            Assert.That(category.nodes[1].statId, Is.EqualTo("damage"));
            Assert.That(category.nodes[1].prerequisiteNodeId, Is.EqualTo("skill.tornado"));
            Assert.That(category.nodes[2].statId, Is.EqualTo("radius"));
            Assert.That(category.nodes[2].prerequisiteNodeId,
                Is.EqualTo("skill.tornado.damage.01"));
            Assert.That(Build(document, new HashSet<string> { "skill.tornado" })
                .skills[0].enabled, Is.True);
        }

        [Test]
        public void EachBossStageUnlocksOneNewCombatSkillWithSeparateDamageAndRadiusResearch()
        {
            GameDataDocument document = GameDataDocument.Load();
            string[] expectedSkills =
            {
                "skill.tornado", "skill.thunder", "skill.flame_exhaust",
                "skill.magic_ball", "skill.laser"
            };
            int[] unlocksPerBossStage = new int[6];

            foreach (UpgradeCategoryData category in document.progression.categories)
            {
                if (category == null || category.id == null ||
                    !category.id.StartsWith("skill.", System.StringComparison.Ordinal) ||
                    category.id == "skill.pet_baby_dragon")
                {
                    continue;
                }

                UpgradeNodeData unlock = System.Array.Find(category.nodes,
                    node => node != null && node.operation == "enable");
                Assert.That(unlock, Is.Not.Null, category.id + " must have an unlock node.");
                Assert.That(unlock.id, Is.EqualTo(category.id), category.id);
                Assert.That(unlock.requiredBossTier, Is.InRange(1, 5), category.id);
                unlocksPerBossStage[unlock.requiredBossTier]++;

                UpgradeNodeData damage = System.Array.Find(category.nodes,
                    node => node != null && node.statId == "damage");
                UpgradeNodeData radius = System.Array.Find(category.nodes,
                    node => node != null && node.statId == "radius");
                Assert.That(damage, Is.Not.Null, category.id + " needs independent damage research.");
                Assert.That(radius, Is.Not.Null, category.id + " needs independent radius research.");
                Assert.That(damage.operation, Is.EqualTo("add"), category.id);
                Assert.That(radius.operation, Is.EqualTo("add"), category.id);
                Assert.That(damage.value, Is.GreaterThan(0d), category.id);
                Assert.That(radius.value, Is.GreaterThan(0d), category.id);
                Assert.That(damage.requiredBossTier, Is.EqualTo(unlock.requiredBossTier), category.id);
                Assert.That(radius.requiredBossTier,
                    Is.EqualTo(System.Math.Min(5, unlock.requiredBossTier + 1)), category.id);
            }

            for (int tier = 1; tier <= 5; tier++)
            {
                Assert.That(unlocksPerBossStage[tier], Is.EqualTo(1),
                    "Boss stage " + tier + " must reveal exactly one new skill type.");
                UpgradeCategoryData category = System.Array.Find(
                    document.progression.categories,
                    value => value.id == expectedSkills[tier - 1]);
                Assert.That(category, Is.Not.Null);
                UpgradeNodeData unlock = System.Array.Find(category.nodes,
                    node => node != null && node.operation == "enable");
                Assert.That(unlock.requiredBossTier, Is.EqualTo(tier), expectedSkills[tier - 1]);
            }

            UpgradeCategoryData pet = System.Array.Find(document.progression.categories,
                value => value.id == "skill.pet_baby_dragon");
            Assert.That(pet, Is.Not.Null);
            Assert.That(System.Array.Exists(pet.nodes,
                node => node != null && node.statId == "damage" && node.requiredBossTier == 0), Is.True,
                "The starter pet must have its own damage research.");
            Assert.That(System.Array.Exists(pet.nodes,
                node => node != null && node.statId == "simultaneousHits"), Is.True,
                "The pet must have research for simultaneous target count.");
        }

        [Test]
        public void BabyDragonUnlockFlowsIntoTheSeventhSkillSnapshot()
        {
            var document = GameDataDocument.Load();
            UpgradeCategoryData category = System.Array.Find(
                document.progression.categories,
                value => value.id == "skill.pet_baby_dragon");
            Assert.That(category.nodes, Has.Length.EqualTo(5));
            Assert.That(category.nodes[0].cost, Is.EqualTo(80));
            Assert.That(category.nodes[0].prerequisiteNodeId,
                Is.EqualTo("stat.attack.01"));
            Assert.That(Build(document).skills[6].enabled, Is.False);

            GameInformation purchased = Build(document,
                new HashSet<string> { "skill.pet_baby_dragon" });
            Assert.That(purchased.skills[6].enabled, Is.True);
            var snapshot = purchased.ToCombatSnapshot(
                GameInformationJson.Serialize(purchased));
            Assert.That(snapshot.Skills[6].SkillId,
                Is.EqualTo("skill.pet_baby_dragon"));
            Assert.That(snapshot.Skills[6].Unlocked, Is.True);
        }

        [Test]
        public void PurchasedSkillDeltasAreSummedAndCaptured()
        {
            var document = GameDataDocument.Load();
            UpgradeCategoryData category = System.Array.Find(
                document.progression.categories,
                value => value.id == "skill.tornado");
            var nodes = new List<UpgradeNodeData>(category.nodes)
            {
                new UpgradeNodeData
                {
                    id = "skill.tornado.testCooldownA",
                    operation = "add", statId = "cooldownSeconds", value = -0.25
                },
                new UpgradeNodeData
                {
                    id = "skill.tornado.testCooldownB",
                    operation = "add", statId = "cooldownSeconds", value = -0.5
                }
            };
            category.nodes = nodes.ToArray();
            var purchased = new HashSet<string>
            {
                "skill.tornado.testCooldownA", "skill.tornado.testCooldownB"
            };
            GameInformation result = Build(document, purchased);
            Assert.That(result.skills[0].upgrades, Has.Length.EqualTo(1));
            Assert.That(result.skills[0].upgrades[0].delta,
                Is.EqualTo(-0.75f));
            var captured = result.ToCombatSnapshot(
                GameInformationJson.Serialize(result));
            result.skills[0].upgrades[0].delta = -99f;
            Assert.That(captured.Skills[0].GetUpgrade("cooldownSeconds"),
                Is.EqualTo(-0.75f));
            Assert.That(document.information.skills[0].upgrades,
                Is.Empty);
        }

        [Test]
        public void MissingTornadoUnlockIsRejected()
        {
            var document = GameDataDocument.Load();
            UpgradeCategoryData category = System.Array.Find(
                document.progression.categories, value => value.id == "skill.tornado");
            category.nodes = System.Array.FindAll(
                category.nodes, value => value.id != "skill.tornado");
            Assert.That(document.TryValidate(out _), Is.False);
        }

        [Test]
        public void MissingBabyDragonUnlockIsRejected()
        {
            var document = GameDataDocument.Load();
            UpgradeCategoryData category = System.Array.Find(
                document.progression.categories,
                value => value.id == "skill.pet_baby_dragon");
            category.nodes = System.Array.FindAll(category.nodes,
                value => value.id != "skill.pet_baby_dragon");
            Assert.That(document.TryValidate(out _), Is.False);
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
            document.progression.categories[2].nodes[1].value = 1;
            Assert.That(document.TryValidate(out _), Is.False);
            Assert.That(GameDataDocument.TryParse("{}", "{}", out _, out _), Is.False);
            Assert.That(GameDataDocument.TryParse(null, null, out _, out _), Is.False);
        }
    }
}
