using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CursorHunter.App.Tests
{
    public sealed class CursorSkinTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private CursorSkinController _controller;
        private CircleCollider2D _hitArea;
        private SpriteRenderer _renderer;

        [SetUp]
        public void SetUp()
        {
            var manager = Track(new GameObject("CursorManager"));
            var cursor = new GameObject("cursor_image");
            cursor.transform.SetParent(manager.transform);
            cursor.transform.localScale = new Vector3(2f, 2f, 1f);
            _hitArea = cursor.AddComponent<CircleCollider2D>();
            _hitArea.radius = 0.86269045f;
            _hitArea.offset = new Vector2(0f, 0.0051522255f);
            _hitArea.isTrigger = true;
            var visual = new GameObject("SkinVisual");
            visual.transform.SetParent(cursor.transform, false);
            _renderer = visual.AddComponent<SpriteRenderer>();
            _controller = manager.AddComponent<CursorSkinController>();
            SetField(_controller, "hitArea", _hitArea);
            SetField(_controller, "visualRenderer", _renderer);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
        }

        [Test]
        public void BothShippedSkinsSwitchWithoutChangingAttackGeometry()
        {
            var original = LoadSkin("cursor_image");
            var joystick = LoadSkin("Joystick_Skill01_Fill_White");
            SetField(_controller, "availableSkins", new[] { original, joystick });
            Physics2D.SyncTransforms();
            Bounds bounds = _hitArea.bounds;
            Vector3 rootScale = _hitArea.transform.localScale;

            foreach (var skin in new[] { joystick, original, joystick })
            {
                Assert.That(_controller.TrySelectSkin(skin.SkinId), Is.True);
                Assert.That(_renderer.sprite, Is.SameAs(skin.Sprite));
                Assert.That(_controller.SelectedSkin, Is.SameAs(skin));
                Physics2D.SyncTransforms();
                Assert.That(_hitArea.bounds, Is.EqualTo(bounds));
                Assert.That(_hitArea.transform.localScale, Is.EqualTo(rootScale));
                Assert.That(_hitArea.radius, Is.EqualTo(0.86269045f));
                Assert.That(_hitArea.isTrigger, Is.True);
                AssertVisualMatchesRange(skin);
            }
        }

        [Test]
        public void DifferentResolutionPpuAndPivotFitRangeAfterUpgrade()
        {
            var centered = CreateSkin("centered", 256, 100f, new Vector2(0.5f, 0.5f), 0.75f);
            var corner = CreateSkin("corner", 64, 32f, Vector2.zero, 1f);
            SetField(_controller, "availableSkins", new[] { centered, corner });
            foreach (float multiplier in new[] { 1f, 2.5f })
            {
                _hitArea.transform.localScale = new Vector3(2f * multiplier, 2f * multiplier, 1f);
                foreach (var skin in new[] { centered, corner })
                {
                    Assert.That(_controller.TrySelectSkin(skin.SkinId), Is.True);
                    AssertVisualMatchesRange(skin);
                }
            }
        }

        [Test]
        public void StableIdStillSelectsNewSkinAfterCatalogReorder()
        {
            var first = LoadSkin("cursor_image");
            var second = LoadSkin("Joystick_Skill01_Fill_White");
            var third = CreateSkin("third", 32, 64f, new Vector2(0.5f, 0.5f), 1f);
            SetField(_controller, "availableSkins", new[] { third, null, second, first });
            _controller.SelectSkin(third.SkinId);
            Assert.That(_renderer.sprite, Is.SameAs(third.Sprite));
            SetField(_controller, "availableSkins", new[] { first, second, third });
            Assert.That(_controller.TrySelectSkin(third.SkinId), Is.True);
            Assert.That(_controller.SelectedSkin, Is.SameAs(third));
        }

        [Test]
        public void InvalidAndAmbiguousIdsLeaveCurrentSkinUntouched()
        {
            var valid = LoadSkin("cursor_image");
            var invalid = Track(ScriptableObject.CreateInstance<CursorSkinDefinition>());
            SetField(invalid, "skinId", "missing-sprite");
            SetField(_controller, "availableSkins", new[] { valid, invalid });
            Assert.That(_controller.TrySelectSkin(valid.SkinId), Is.True);
            foreach (string id in new[] { null, "", "unknown", invalid.SkinId })
                Assert.That(_controller.TrySelectSkin(id), Is.False);
            SetField(_controller, "availableSkins", new[] { valid, valid });
            Assert.That(_controller.TrySelectSkin(valid.SkinId), Is.False);
            Assert.That(_controller.SelectedSkin, Is.SameAs(valid));
            Assert.That(_renderer.sprite, Is.SameAs(valid.Sprite));
        }

        [Test]
        public void SkinCanBeSelectedWhileCursorIsHidden()
        {
            var skin = LoadSkin("Joystick_Skill01_Fill_White");
            SetField(_controller, "availableSkins", new[] { skin });
            _hitArea.gameObject.SetActive(false);
            Assert.That(_controller.TrySelectSkin(skin.SkinId), Is.True);
            Assert.That(_hitArea.gameObject.activeSelf, Is.False);
            _hitArea.gameObject.SetActive(true);
            Assert.That(_renderer.sprite, Is.SameAs(skin.Sprite));
            AssertVisualMatchesRange(skin);
        }

        [Test]
        public void InspectorSelectionAndLiveRadiusChangesRefreshVisual()
        {
            var skin = LoadSkin("Joystick_Skill01_Fill_White");
            SetField(_controller, "selectedSkin", skin);
            Assert.That(_controller.RefreshSkin(), Is.True);
            _hitArea.radius = 1.5f;
            _hitArea.offset = new Vector2(0.2f, -0.1f);
            Assert.That(_controller.RefreshSkin(), Is.True);
            Assert.That(_hitArea.radius, Is.EqualTo(1.5f));
            AssertVisualMatchesRange(skin);
        }

        [Test]
        public void RendererOnColliderObjectIsRejectedWithoutResizingHitArea()
        {
            var skin = LoadSkin("Joystick_Skill01_Fill_White");
            SetField(_controller, "availableSkins", new[] { skin });
            SetField(_controller, "visualRenderer", _hitArea.gameObject.AddComponent<SpriteRenderer>());
            Vector3 scale = _hitArea.transform.localScale;
            Assert.That(_controller.TrySelectSkin(skin.SkinId), Is.False);
            Assert.That(_hitArea.transform.localScale, Is.EqualTo(scale));
        }

        private void AssertVisualMatchesRange(CursorSkinDefinition skin)
        {
            float visualDiameter = Mathf.Max(skin.Sprite.rect.width, skin.Sprite.rect.height) /
                skin.Sprite.pixelsPerUnit * skin.RangeDiameterRatio * _renderer.transform.lossyScale.x;
            float hitDiameter = _hitArea.radius * 2f * _hitArea.transform.lossyScale.x;
            Assert.That(visualDiameter, Is.EqualTo(hitDiameter).Within(0.0001f));
            Vector3 center = (skin.Sprite.rect.size * 0.5f - skin.Sprite.pivot) / skin.Sprite.pixelsPerUnit;
            Vector3 visualCenter = _renderer.transform.TransformPoint(center);
            Vector3 hitCenter = _hitArea.transform.TransformPoint(_hitArea.offset);
            Assert.That(Vector3.Distance(visualCenter, hitCenter), Is.LessThan(0.0001f));
        }

        private static CursorSkinDefinition LoadSkin(string name)
        {
            var skin = AssetDatabase.LoadAssetAtPath<CursorSkinDefinition>(
                "Assets/CursorHunter/App/Art/CursorSkins/" + name + ".asset");
            Assert.That(skin, Is.Not.Null);
            Assert.That(skin.IsValid, Is.True);
            return skin;
        }

        private CursorSkinDefinition CreateSkin(string id, int size, float ppu, Vector2 pivot, float ratio)
        {
            var texture = Track(new Texture2D(size, size));
            var sprite = Track(Sprite.Create(texture, new Rect(0, 0, size, size), pivot, ppu));
            var skin = Track(ScriptableObject.CreateInstance<CursorSkinDefinition>());
            SetField(skin, "skinId", id);
            SetField(skin, "sprite", sprite);
            SetField(skin, "rangeDiameterRatio", ratio);
            return skin;
        }

        private T Track<T>(T value) where T : Object
        {
            _objects.Add(value);
            return value;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }
    }
}
