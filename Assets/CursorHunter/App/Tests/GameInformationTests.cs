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
            var data = new GameInformation {
                schemaVersion = 4, balanceVersion = 4,
                stats = new StatInformation {
                    attackPower = 10, attackRadiusWorldUnits = 5, attackCooldownSeconds = .5f,
                    criticalChancePercent = 15, bossDamageMultiplier = 1, normalFieldDurationSeconds = 30
                },
                gemstones = new GemstoneInformation[6],
                monsters = new MonsterInformation[10],
                skills = new SkillInformation[1],
                relics = new RelicInformation[20],
                relicDropRules = new RelicDropRulesInformation()
            };
            for (int i = 0; i < gems.Length; i++)
                data.gemstones[i] = new GemstoneInformation { id = gems[i], enabled = i == 0, amount = i == 0 ? long.MaxValue : 0 };
            for (int i = 0; i < data.monsters.Length; i++)
                data.monsters[i] = new MonsterInformation {
                    id = "monster." + (i + 1).ToString("00"), enabled = i == 0, productionCount = 3,
                    hitPoints = 20, spawnIntervalSeconds = 1, garnetReward = 3,
                    gemstoneId = gems[0], behaviorType = MonsterBehaviorType.None
                };
            data.skills[0] = new SkillInformation { id = "skill.tornado", enabled = true };
            string[] relicIds = {
                "relic.red_potion", "relic.blue_potion", "relic.monster_meat",
                "relic.thief_shoes", "relic.archers_bow", "relic.warrior_armor",
                "relic.wizards_hat", "relic.valor_ring", "relic.lucky_dice",
                "relic.kraken_eye", "relic.four_leaf_clover", "relic.ancient_book",
                "relic.adventurers_bag", "relic.compass", "relic.megaphone",
                "relic.telescope", "relic.stone", "relic.rainbow_feather",
                "relic.scales_of_judgment", "relic.treasure_chest"
            };
            string[] effectIds = {
                "cursorAttackDamagePercent", "autoSkillDamagePercent", "petDamagePercent",
                "petMoveSpeedPercent", "petAttackIntervalReductionPercent",
                "normalFieldDurationBonusSeconds", "areaSkillRadiusPercent",
                "criticalDamageMultiplierBonus", "petDoubleDamageChancePercent",
                "lowHealthExecuteChancePercent", "bonusGemDropChancePercent",
                "petAttackRadiusPercent", "garnetKillRewardPercent",
                "petDetectionRadiusPercent", "monsterSpawnIntervalReductionPercent",
                "cursorAttackRadiusPercent", "fullHealthFirstHitDamagePercent",
                "autoSkillCooldownReductionPercent", "bossDamagePercent",
                "monsterKillRewardPercent"
            };
            for (int i = 0; i < data.relics.Length; i++)
                data.relics[i] = new RelicInformation {
                    id = relicIds[i], owned = false,
                    sourceMonsterId = "monster." + (i / 2 + 1).ToString("00"),
                    dropChancePercent = i % 2 == 0 ? 10f : 4f,
                    effectId = effectIds[i], effectValue = 5f,
                    effectValue2 = i == 9 ? 30f : 0f
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
            Assert.That(snapshot.Skills[0].SkillId, Is.EqualTo("skill.tornado"));
            Assert.That(snapshot.Skills[0].Unlocked, Is.True);
            Assert.That(snapshot.Relics, Has.Count.EqualTo(20));
            Assert.That(snapshot.Relics[0].Owned, Is.False);
            Assert.That(snapshot.Relics[0].DropChancePercent, Is.EqualTo(10f));
            Assert.That(snapshot.RelicDropRules.MaxPerKill, Is.EqualTo(1));
            Assert.That(snapshot.SourceJson, Is.EqualTo(json));
        }

        [Test]
        public void LaterProfileEditsCannotMutateTheCapturedRun()
        {
            var data = ValidInformation();
            var captured = data.ToCombatSnapshot(GameInformationJson.Serialize(data));
            Assert.That(captured.Skills, Is.Not.InstanceOf<SkillCombatSnapshot[]>());
            Assert.That(captured.Monsters, Is.Not.InstanceOf<MonsterCombatSnapshot[]>());
            Assert.That(captured.Relics, Is.Not.InstanceOf<RelicCombatSnapshot[]>());
            data.stats.attackPower = 999;
            data.monsters[0].productionCount = 8;
            data.skills[0].enabled = false;
            data.relics[0].owned = true;
            Assert.That(captured.Combat.AttackPower, Is.EqualTo(10));
            Assert.That(captured.Monsters[0].ProductionCount, Is.EqualTo(3));
            Assert.That(captured.Skills[0].Unlocked, Is.True);
            Assert.That(captured.Relics[0].Owned, Is.False);
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
            data.skills[0].id = "skill.unknown";
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
            Assert.That(json, Does.Contain("\"relics\""));
            Assert.That(json, Does.Contain("\"relicDropRules\""));
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
