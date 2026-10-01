using System;
using CursorHunter.Contracts;
using CursorHunter.Data;
using CursorHunter.Progression;
using NUnit.Framework;

namespace CursorHunter.App.Tests
{
    // Editor tests are supplied for later use. This change does not run Unity.
    public sealed class GameInformationTests
    {
        private static GameInformation ValidInformation()
        {
            string[] gems = { "gem.garnet", "gem.topaz", "gem.amethyst", "gem.sapphire", "gem.diamond", "gem.dragon" };
            string[] skills = { "skill.fireball", "skill.lightning", "skill.freeze", "skill.hurricane", "skill.meteor", "skill.dragonBreath", "skill.cursorAura" };
            var data = new GameInformation {
                schemaVersion = 3, balanceVersion = 3,
                stats = new StatInformation {
                    attackPower = 10, attackRadiusWorldUnits = 5, attackCooldownSeconds = .5f,
                    criticalChancePercent = 15, bossDamageMultiplier = 1, normalFieldDurationSeconds = 30
                },
                gemstones = new GemstoneInformation[6],
                monsters = new MonsterInformation[10],
                skills = new SkillInformation[7]
            };
            for (int i = 0; i < gems.Length; i++)
                data.gemstones[i] = new GemstoneInformation { id = gems[i], enabled = i == 0, amount = i == 0 ? long.MaxValue : 0 };
            for (int i = 0; i < data.monsters.Length; i++)
                data.monsters[i] = new MonsterInformation {
                    id = "monster." + (i + 1).ToString("00"), enabled = i == 0, productionCount = 3,
                    hitPoints = 20, spawnIntervalSeconds = 1, garnetReward = 3,
                    gemstoneId = gems[0], behaviorType = MonsterBehaviorType.None
                };
            for (int i = 0; i < skills.Length; i++)
                data.skills[i] = new SkillInformation {
                    id = skills[i], enabled = i == 0, damage = 40, radiusWorldUnits = 2, cooldownSeconds = 4.5f
                };
            return data;
        }

        [Test]
        public void JsonRoundTripPreservesLongWalletAndConcreteCombatValues()
        {
            string json = GameInformationJson.Serialize(ValidInformation());
            Assert.That(GameInformationJson.TryDeserialize(json, out var loaded, out var error), Is.True, error);
            Assert.That(loaded.gemstones[0].amount, Is.EqualTo(long.MaxValue));
            var snapshot = loaded.ToCombatSnapshot(json);
            Assert.That(snapshot.Combat.AttackPower, Is.EqualTo(10L));
            Assert.That(snapshot.Combat.CriticalChancePercent, Is.EqualTo(15f));
            Assert.That(snapshot.Monsters[0].ProductionCount, Is.EqualTo(3));
            Assert.That(snapshot.Skills[0].Damage, Is.EqualTo(40L));
            Assert.That(snapshot.Skills[0].RadiusWorldUnits, Is.EqualTo(2f));
            Assert.That(snapshot.SourceJson, Is.EqualTo(json));
        }

        [Test]
        public void LaterProfileEditsCannotMutateTheCapturedRun()
        {
            var data = ValidInformation();
            var captured = data.ToCombatSnapshot(GameInformationJson.Serialize(data));
            Assert.That(captured.Skills, Is.Not.InstanceOf<SkillCombatSnapshot[]>());
            Assert.That(captured.Monsters, Is.Not.InstanceOf<MonsterCombatSnapshot[]>());
            data.stats.attackPower = 999;
            data.monsters[0].productionCount = 8;
            data.skills[0].damage = 999;
            Assert.That(captured.Combat.AttackPower, Is.EqualTo(10));
            Assert.That(captured.Monsters[0].ProductionCount, Is.EqualTo(3));
            Assert.That(captured.Skills[0].Damage, Is.EqualTo(40));
        }

        [TestCase(31f)]
        [TestCase(60f)]
        [TestCase(float.NaN)]
        public void InvalidFieldDurationsAreRejected(float seconds)
        {
            var data = ValidInformation();
            data.stats.normalFieldDurationSeconds = seconds;
            Assert.That(data.TryValidate(out _), Is.False);
        }

        [Test]
        public void UnknownAndDuplicateIdsAndInvalidNumbersAreRejected()
        {
            var data = ValidInformation();
            data.monsters[1].id = data.monsters[0].id;
            Assert.That(data.TryValidate(out _), Is.False);
            data = ValidInformation();
            data.skills[0].radiusWorldUnits = float.PositiveInfinity;
            Assert.That(data.TryValidate(out _), Is.False);
            data = ValidInformation();
            data.gemstones[0].amount = -1;
            Assert.That(data.TryValidate(out _), Is.False);
            data = ValidInformation();
            data.schemaVersion = 99;
            Assert.That(data.TryValidate(out _), Is.False);
        }

        [Test]
        public void OrdinaryRunCapDoesNotChangeBossDuration()
        {
            var normal = new RunRequest(RunId.Create(), 2, 2, RunMode.NormalField, "", 1, 60);
            var boss = new RunRequest(RunId.Create(), 2, 2, RunMode.Boss, "boss.v1", 1, 60);
            Assert.That(normal.DurationSeconds, Is.EqualTo(30));
            Assert.That(boss.DurationSeconds, Is.EqualTo(60));
            Assert.That(new CombatSnapshot(1, 1, .5f, 4).HitsPerBundle, Is.EqualTo(1));
        }

        [TestCase("stat.multiClick.04", "stat.cooldown.04")]
        [TestCase("pet.cursor.01", "skill.cursorAura")]
        [TestCase("pet.cursor.03", "skill.cursorAura.damage")]
        [TestCase("pet.cursor.05", "skill.cursorAura.radius")]
        [TestCase("loot.monster.20.level.04", "")]
        [TestCase("relic.monster.01.level.01", "")]
        public void OldPurchaseIdsMigrate(string oldId, string expected)
        {
            Assert.That(ProgressionMigration.NodeId(oldId), Is.EqualTo(expected));
        }

        [Test]
        public void ReservedBehaviorIsCopiedAndUnknownValuesAreRejected()
        {
            var data = ValidInformation();
            string json = GameInformationJson.Serialize(data);
            Assert.That(json, Does.Contain("\"behaviorType\""));
            Assert.That(json, Does.Not.Contain("relic"));
            var snapshot = data.ToCombatSnapshot(json);
            Assert.That(snapshot.Monsters[0].BehaviorType, Is.EqualTo(MonsterBehaviorType.None));
            var spawn = new SpawnSnapshot("monster.01", "Walker/Walker_Stump",
                10, 1, 1, 80, 3, "gem.garnet", 0, 0, snapshot.Monsters[0].BehaviorType);
            Assert.That(spawn.BehaviorType, Is.EqualTo(MonsterBehaviorType.None));
            data.monsters[0].behaviorType = (MonsterBehaviorType)99;
            Assert.That(data.TryValidate(out _), Is.False);
            Assert.That(snapshot.Monsters[0].BehaviorType, Is.EqualTo(MonsterBehaviorType.None));
        }

        [Test]
        public void RetiredNodesDoNotBecomeActivePurchases()
        {
            Assert.That(ProgressionMigration.IsRetiredNode("relic.boss.01.level.01"), Is.True);
            Assert.That(ProgressionMigration.IsRetiredNode("loot.monster.20.level.04"), Is.True);
            Assert.That(ProgressionMigration.IsRetiredNode("stat.attack.02"), Is.False);
            Assert.That(ProgressionMigration.IsRetiredNode(null), Is.False);
        }
    }
}
