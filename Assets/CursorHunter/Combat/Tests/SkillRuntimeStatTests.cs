using CursorHunter.Contracts;
using NUnit.Framework;

namespace CursorHunter.Combat.Tests
{
    public sealed class SkillRuntimeStatTests
    {
        [Test]
        public void FourSkillsResolvePurchasedDeltasAtRunStart()
        {
            var tornadoBase = new TornadoBaseStat
            {
                Count = 1, DurationSeconds = 5f, DamageMultiplier = 2f,
                CooldownSeconds = 8f, FirstCastDelaySeconds = 1f,
                HitIntervalSeconds = 0.5f, MoveSpeedWorldUnitsPerSecond = 4f,
                DirectionChangeSeconds = 3f, StopChanceOnDirectionChange = 0.25f,
                StopDurationSeconds = 0.75f, ShrinkOutSeconds = 1f
            };
            var tornado = new TornadoRuntimeStat(tornadoBase,
                new SkillCombatSnapshot("skill.tornado", true, new[]
                {
                    new SkillStatUpgrade("count", 2f),
                    new SkillStatUpgrade("cooldownSeconds", -2f),
                    new SkillStatUpgrade("damageMultiplier", 0.5f)
                }), 10L, 16);
            Assert.That(tornado.Count, Is.EqualTo(3));
            Assert.That(tornado.CooldownSeconds, Is.EqualTo(6f));
            Assert.That(tornado.Damage, Is.EqualTo(25L));
            Assert.That(tornado.MoveSpeedWorldUnitsPerSecond, Is.EqualTo(4f));

            var headshotBase = new HeadshotBaseStat
            {
                CooldownSeconds = 5f, FirstCastDelaySeconds = 1f,
                ExplosionDelaySeconds = 1.15f, EffectDurationSeconds = 2.5f,
                SplashDamageMultiplier = 2f, BossDamageMultiplier = 10f
            };
            var headshot = new HeadshotRuntimeStat(headshotBase,
                new SkillCombatSnapshot("skill.headshot", true, new[]
                {
                    new SkillStatUpgrade("splashDamageMultiplier", 1f),
                    new SkillStatUpgrade("bossDamageMultiplier", 2f)
                }), 10L);
            Assert.That(headshot.SplashDamage, Is.EqualTo(30L));
            Assert.That(headshot.BossDamage, Is.EqualTo(120L));

            var thunderBase = new ThunderBaseStat
            {
                CooldownSeconds = 2f, FirstCastDelaySeconds = 0f,
                ImpactDelaySeconds = 0.1f, EffectDurationSeconds = 0.5f,
                DamageMultiplier = 4f
            };
            var thunder = new ThunderRuntimeStat(thunderBase,
                new SkillCombatSnapshot("skill.thunder", true, new[]
                {
                    new SkillStatUpgrade("cooldownSeconds", -0.5f),
                    new SkillStatUpgrade("damageMultiplier", 1f)
                }), 10L);
            Assert.That(thunder.CooldownSeconds, Is.EqualTo(1.5f));
            Assert.That(thunder.Damage, Is.EqualTo(50L));

            var flameBase = new FlameExhaustBaseStat
            {
                CooldownSeconds = 2f, FirstCastDelaySeconds = 0f,
                ProjectileDurationSeconds = 1f, SpeedWorldUnitsPerSecond = 8f,
                CursorSizeRatio = 0.45f, HitRadiusMultiplier = 1.2f,
                DamageMultiplier = 1.5f, HitEffectDurationSeconds = 0.8f
            };
            var flame = new FlameExhaustRuntimeStat(flameBase,
                new SkillCombatSnapshot("skill.flame_exhaust", true, new[]
                {
                    new SkillStatUpgrade("cooldownSeconds", -0.5f)
                }), 10L);
            Assert.That(flame.CooldownSeconds, Is.EqualTo(1.5f));
            Assert.That(flame.Damage, Is.EqualTo(15L));
            Assert.That(flame.ProjectileDurationSeconds, Is.EqualTo(1f));
        }

        [Test]
        public void UpgradeArrayIsCopiedAndUnknownFlameUpgradeIsRejected()
        {
            var input = new[] { new SkillStatUpgrade("cooldownSeconds", -0.5f) };
            var snapshot = new SkillCombatSnapshot("skill.flame_exhaust", true,
                input);
            input[0] = new SkillStatUpgrade("cooldownSeconds", -1f);
            Assert.That(snapshot.GetUpgrade("cooldownSeconds"), Is.EqualTo(-0.5f));
            Assert.That(snapshot.IsValid, Is.True);

            var invalid = new SkillCombatSnapshot("skill.flame_exhaust", true,
                new[] { new SkillStatUpgrade("count", 1f) });
            Assert.That(invalid.IsValid, Is.False);
        }
    }
}
