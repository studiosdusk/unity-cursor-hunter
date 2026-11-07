using NUnit.Framework;
using System.Reflection;
using CursorHunter.Contracts;
using UnityEditor;
using UnityEngine;

namespace CursorHunter.Combat.Tests
{
    public sealed class BattlefieldAreaTests
    {
        private GameObject _root;
        private BattlefieldArea _field;

        [SetUp] public void SetUp()
        {
            _root = new GameObject("field");
            _field = _root.AddComponent<BattlefieldArea>();
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void DiamondRejectsTheOldViewportCornersAndClampsToSlopingEdges()
        {
            Assert.That(_field.Contains(new Vector2(15f, 8f)), Is.False);
            Assert.That(_field.Contains(new Vector2(0f, 8f)), Is.True);
            Vector2 p = _field.Clamp(new Vector2(15f, 8f), 0.75f);
            Assert.That(_field.Contains(p, 0.75f), Is.True);
            Assert.That(_field.Clearance(p), Is.EqualTo(0.75f).Within(0.001f));
        }

        [Test]
        public void SamplingAndMarginsFollowMovedRotatedAndScaledMap()
        {
            _root.transform.SetPositionAndRotation(new Vector3(6f, -3f), Quaternion.Euler(0f, 0f, 27f));
            _root.transform.localScale = new Vector3(-1.2f, 0.8f, 1f);
            Vector2 mean = Vector2.zero;
            for (int x = 0; x < 21; x++)
                for (int y = 0; y < 21; y++)
                {
                    Vector2 p = _field.Sample(x / 20f, y / 20f, 1f);
                    Assert.That(_field.Contains(p, 1f), Is.True);
                    mean += p;
                }
            Assert.That(Vector2.Distance(mean / 441f, _root.transform.position), Is.LessThan(0.001f));
            _root.transform.position += Vector3.right * 10f;
            Assert.That(_field.Contains(_root.transform.position), Is.True);
            Assert.That(_field.WorldBounds.center.x, Is.EqualTo(16f).Within(0.001f));
        }

        [TestCase(1f, 0f)]
        [TestCase(0f, 1f)]
        [TestCase(0.8f, 0.7f)]
        public void LongFramesReflectAtEdgesAndCornersWithoutEscaping(float x, float y)
        {
            Vector2 velocity = new Vector2(x, y).normalized * 15f;
            Vector2 p = _field.MoveReflected(Vector2.zero, ref velocity, 30f, 0.8f);
            Assert.That(_field.Contains(p, 0.8f), Is.True);
            Assert.That(velocity.magnitude, Is.EqualTo(15f).Within(0.001f));
            Assert.That(float.IsNaN(p.x) || float.IsNaN(p.y), Is.False);
        }

        [Test]
        public void BeamCornersFitTheDiamondAcrossItsWholeWidth()
        {
            foreach (float x in new[] { -12f, -5f, 0f, 5f, 12f })
            {
                Assert.That(_field.TryGetVerticalSpan(x, 0.7f, out float min, out float max), Is.True);
                foreach (float edgeX in new[] { x - 0.7f, x + 0.7f })
                    foreach (float edgeY in new[] { min, max })
                        Assert.That(_field.Contains(new Vector2(edgeX, edgeY), _field.GroundMargin), Is.True);
            }
            Assert.That(_field.TryGetVerticalSpan(30f, 0.7f, out _, out _), Is.False);
            Assert.That(_field.Fits(20f), Is.False);
        }

        [Test]
        public void DiagonalBeamCornersFollowBothMapAxesIncludingMirroredTransforms()
        {
            foreach (bool transformed in new[] { false, true })
            {
                if (transformed)
                {
                    _root.transform.SetPositionAndRotation(new Vector3(4f, -2f), Quaternion.Euler(0f, 0f, 23f));
                    _root.transform.localScale = new Vector3(-1.3f, 0.8f, 1f);
                }
                for (int axis = 0; axis < 2; axis++)
                {
                    Vector2 direction = _field.GetGroundAxis(axis);
                    Vector2 side = new Vector2(-direction.y, direction.x) * 0.9f;
                    foreach (float offset in new[] { -3f, 0f, 3f })
                    {
                        Vector2 point = (Vector2)_root.transform.position + side * offset;
                        Assert.That(_field.TryGetBeamSegment(point, direction, 1.8f,
                            out Vector2 start, out Vector2 end), Is.True);
                        foreach (Vector2 tip in new[] { start, end })
                            foreach (Vector2 corner in new[] { tip + side, tip - side })
                                Assert.That(_field.Contains(corner, _field.GroundMargin), Is.True);
                        Assert.That(Vector2.Dot(end - start, direction), Is.GreaterThan(5f));
                    }
                }
            }
            Assert.That(_field.TryGetBeamSegment(Vector2.zero, Vector2.zero, 1f, out _, out _), Is.False);
            Assert.That(_field.TryGetBeamSegment(_root.transform.position, Vector2.right, 100f, out _, out _), Is.False);
            Assert.That(_field.TryGetBeamSegment(new Vector2(float.NaN, 0f), Vector2.right, 1f, out _, out _), Is.False);
        }

        [Test]
        public void CursorLeavingMapClearsSweepBeforeReentry()
        {
            var combat = _root.AddComponent<CombatRunController>();
            typeof(CombatRunController).GetField("battlefield", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(combat, _field);
            Assert.That(combat.StartRun(new RunRequest("field-cursor", 30f),
                new CombatSnapshot(10L, 1f, 0.1f, 1)), Is.True);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CursorHunter/Combat/Prefabs/Monsters/MonsterRoot.prefab");
            var target = Object.Instantiate(prefab, _root.transform).GetComponent<MonsterCombatTarget>();
            target.Initialize(new SpawnSnapshot("test", "test", 1000L, 1f, 1, 80, 0L), combat.CurrentRunId);
            var cursor = new GameObject("cursor").AddComponent<CircleCollider2D>();
            cursor.transform.SetParent(_root.transform);
            cursor.radius = 0.25f;
            cursor.transform.position = new Vector3(-5f, 0.5f);
            combat.TryAttack(cursor);
            cursor.transform.position = new Vector3(30f, 0.5f);
            Assert.That(combat.TryAttack(cursor), Is.False);
            cursor.transform.position = new Vector3(5f, 0.5f);
            combat.TryAttack(cursor);
            Assert.That(target.CurrentHealth, Is.EqualTo(1000L), "Reentry cannot sweep across the target.");
            cursor.transform.position = new Vector3(-5f, 0.5f);
            combat.TryAttack(cursor);
            Assert.That(target.CurrentHealth, Is.LessThan(1000L), "A continuous path inside the map still hits.");
        }
    }
}
