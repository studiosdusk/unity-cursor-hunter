using System;
using System.Collections.Generic;
using CursorHunter.Progression.ResearchPrototype;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CursorHunter.App.Tests
{
    public sealed class ResearchDefinitionTests
    {
        [Test]
        public void ExistingDefinitionsHaveSeparatedValidPresentationAndGameplay()
        {
            string[] names = { "cursor.attack", "cursor.radius", "cursor.interval", "cursor.critical", "cursor.critical_damage", "cursor.boss_damage" };
            for (int i = 0; i < names.Length; i++)
            {
                var definition = AssetDatabase.LoadAssetAtPath<ResearchDefinition>(
                    "Assets/CursorHunter/Progression/ResearchPrototype/" + names[i] + ".asset");
                Assert.That(definition, Is.Not.Null, names[i]);
                Assert.That(definition.TryValidate(out string error), Is.True, error);
                Assert.That(definition.stableId, Is.EqualTo(names[i]));
                Assert.That(definition.presentation.icon, Is.Not.Null);
                Assert.That(definition.presentation.textKey, Is.EqualTo("research." + names[i]));
                Assert.That(definition.gameplay.target, Is.EqualTo((ResearchStat)i));
            }
        }

        [Test]
        public void SavedPlacementsAccumulateTheirOwnRankAcrossAllSixStats()
        {
            string[] names = { "cursor.attack", "cursor.radius", "cursor.interval", "cursor.critical", "cursor.critical_damage", "cursor.boss_damage" };
            var objects = new List<GameObject>();
            try
            {
                var nodes = new List<ResearchTestNode>();
                foreach (var name in names)
                {
                    var go = new GameObject(name); objects.Add(go);
                    var node = go.AddComponent<ResearchTestNode>();
                    node.placementId = name + ".start";
                    node.definition = AssetDatabase.LoadAssetAtPath<ResearchDefinition>("Assets/CursorHunter/Progression/ResearchPrototype/" + name + ".asset");
                    nodes.Add(node);
                }
                var second = new GameObject("second attack"); objects.Add(second);
                var duplicateDefinition = second.AddComponent<ResearchTestNode>();
                duplicateDefinition.placementId = "cursor.attack.next";
                duplicateDefinition.definition = nodes[0].definition;
                nodes.Add(duplicateDefinition);
                var ranks = new Dictionary<string, int> { ["cursor.attack.start"] = 2, ["cursor.attack.next"] = 1,
                    ["cursor.radius.start"] = 1, ["cursor.interval.start"] = 1, ["cursor.critical.start"] = 1,
                    ["cursor.critical_damage.start"] = 1, ["cursor.boss_damage.start"] = 1 };
                var result = ResearchEffectCalculator.Calculate(nodes.ToArray(), id => ranks.TryGetValue(id, out int rank) ? rank : 0);
                Assert.That(result.AttackPowerDelta, Is.EqualTo(6));
                Assert.That(result.AttackRadiusWorldUnitsDelta, Is.EqualTo(.1f).Within(.0001f));
                Assert.That(result.AttackCooldownSecondsDelta, Is.EqualTo(-.08f).Within(.0001f));
                Assert.That(result.CriticalChancePercentDelta, Is.EqualTo(5f).Within(.0001f));
                Assert.That(result.CriticalDamageMultiplierDelta, Is.EqualTo(.1f).Within(.0001f));
                Assert.That(result.BossDamageMultiplierDelta, Is.EqualTo(.01f).Within(.0001f));
                var zero = ResearchEffectCalculator.Calculate(nodes.ToArray(), id => 0);
                Assert.That(zero.AttackPowerDelta, Is.Zero);
                Assert.That(zero.BossDamageMultiplierDelta, Is.Zero);
                duplicateDefinition.placementId = nodes[0].placementId;
                Assert.Throws<InvalidOperationException>(() => ResearchEffectCalculator.Calculate(nodes.ToArray(), id => 1));
                duplicateDefinition.placementId = "cursor.attack.next";
                nodes[0].prerequisites = new[] { duplicateDefinition };
                duplicateDefinition.prerequisites = new[] { nodes[0] };
                Assert.Throws<InvalidOperationException>(() => ResearchEffectCalculator.ValidateMap(nodes.ToArray()), "Cycles must be rejected.");
            }
            finally { foreach (var go in objects) UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void MigratedDefinitionsPreserveEveryExistingRankEffect()
        {
            var expected = new Dictionary<string, float[]>
            {
                ["cursor.attack"] = new float[] { 2f, 4f, 7f, 10f, 14f },
                ["cursor.attack.intermediate"] = new float[] { 4f, 9f, 15f, 21f },
                ["cursor.boss_damage"] = new float[] { 0.01f, 0.25f, 0.45f, 0.7f, 1f },
                ["cursor.critical"] = new float[] { 5f, 10f, 15f, 20f, 25f },
                ["cursor.critical.intermediate"] = new float[] { 5f, 15f },
                ["cursor.critical_damage"] = new float[] { 0.1f, 0.2f, 0.3f },
                ["cursor.interval"] = new float[] { -0.08f, -0.16f, -0.24f, -0.32f },
                ["cursor.radius"] = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f },
                ["cursor.radius.intermediate"] = new float[] { 0.15f, 0.35f, 0.6f, 0.95f, 1.45f },
                ["field.duration"] = new float[] { 20f, 25f, 30f },
                ["monster.01.production"] = new float[] { 3f, 4f, 5f },
                ["monster.02.production"] = new float[] { 3f, 5f, 8f },
                ["monster.02.unlock"] = new float[] { 1f },
                ["monster.03.production"] = new float[] { 3f, 5f, 8f },
                ["monster.03.unlock"] = new float[] { 1f },
                ["monster.04.production"] = new float[] { 4f, 7f, 12f },
                ["monster.04.unlock"] = new float[] { 1f },
                ["monster.05.production"] = new float[] { 4f, 7f, 12f },
                ["monster.05.unlock"] = new float[] { 1f },
                ["monster.06.production"] = new float[] { 4f, 7f, 12f },
                ["monster.06.unlock"] = new float[] { 1f },
                ["monster.07.production"] = new float[] { 4f, 8f, 16f },
                ["monster.07.unlock"] = new float[] { 1f },
                ["monster.08.production"] = new float[] { 4f, 8f, 16f },
                ["monster.08.unlock"] = new float[] { 1f },
                ["monster.09.production"] = new float[] { 4f, 8f, 16f },
                ["monster.09.unlock"] = new float[] { 1f },
                ["monster.10.production"] = new float[] { 4f, 8f, 16f },
                ["monster.10.unlock"] = new float[] { 1f },
                ["skill.flame_exhaust.damage"] = new float[] { 6f, 13f },
                ["skill.flame_exhaust.radius"] = new float[] { 0.2f },
                ["skill.flame_exhaust.unlock"] = new float[] { 1f },
                ["skill.headshot.damage"] = new float[] { 4f, 9f },
                ["skill.headshot.radius"] = new float[] { 0.2f },
                ["skill.headshot.unlock"] = new float[] { 1f },
                ["skill.laser.damage"] = new float[] { 7f, 15f },
                ["skill.laser.radius"] = new float[] { 0.2f },
                ["skill.laser.unlock"] = new float[] { 1f },
                ["skill.magic_ball.damage"] = new float[] { 2f, 5f },
                ["skill.magic_ball.radius"] = new float[] { 0.2f },
                ["skill.magic_ball.unlock"] = new float[] { 1f },
                ["skill.pet_baby_dragon.damage"] = new float[] { 1f, 2f, 2.5f },
                ["skill.pet_baby_dragon.targets"] = new float[] { 1f, 2f },
                ["skill.pet_baby_dragon.unlock"] = new float[] { 1f },
                ["skill.thunder.damage"] = new float[] { 5f, 11f },
                ["skill.thunder.radius"] = new float[] { 0.2f },
                ["skill.thunder.unlock"] = new float[] { 1f },
                ["skill.tornado.damage"] = new float[] { 3f, 7f },
                ["skill.tornado.radius"] = new float[] { 0.2f },
                ["skill.tornado.unlock"] = new float[] { 1f },
            };
            foreach (var entry in expected)
            {
                var definition = AssetDatabase.LoadAssetAtPath<ResearchDefinition>(
                    "Assets/CursorHunter/Progression/ResearchPrototype/" + entry.Key + ".asset");
                Assert.That(definition, Is.Not.Null, entry.Key);
                Assert.That(definition.TryValidate(out string error), Is.True, entry.Key + ": " + error);
                Assert.That(definition.ranks.Length, Is.EqualTo(entry.Value.Length), entry.Key);
                Assert.That(definition.GetEffectValue(0), Is.Zero, entry.Key);
                for (int i = 0; i < entry.Value.Length; i++)
                    Assert.That(definition.GetEffectValue(i + 1), Is.EqualTo(entry.Value[i]).Within(0.0001f), entry.Key + " rank " + (i + 1));
            }
        }

        [Test]
        public void DesignerEnteredIncrementsProduceTheCumulativeEffect()
        {
            var definition = ScriptableObject.CreateInstance<ResearchDefinition>();
            try
            {
                definition.ranks = new[] { new ResearchRank { levelDelta = 3 }, new ResearchRank { levelDelta = 3 }, new ResearchRank { levelDelta = 3 } };
                Assert.That(definition.GetEffectValue(1), Is.EqualTo(3));
                Assert.That(definition.GetEffectValue(2), Is.EqualTo(6));
                Assert.That(definition.GetEffectValue(3), Is.EqualTo(9));
                definition.gameplay.kind = ResearchEffectKind.FieldDuration;
                definition.gameplay.baselineValue = 15;
                Assert.That(definition.GetEffectValue(1), Is.EqualTo(18));
                Assert.That(definition.GetEffectValue(3), Is.EqualTo(24));
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }
    }
}
