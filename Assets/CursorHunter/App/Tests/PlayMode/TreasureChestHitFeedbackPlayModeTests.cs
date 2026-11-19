using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CursorHunter.Combat;
using CursorHunter.Data;
using CursorHunter.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CursorHunter.App.Tests
{
    public sealed class TreasureChestHitFeedbackPlayModeTests
    {
        private const string ProfileKey = "cursor_hunter.progression.v1";

        [UnityTest]
        public IEnumerator RealChestMatchesItsVisualAndRespondsToEveryHitWithoutDuplicateRewards()
        {
#if UNITY_EDITOR
            string[] keys = { ProfileKey, ProfileKey + ".before-v6", ProfileKey + ".before-v12" };
            string[] previous = keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            Scene scene = default;
            HuntManager hunt = null;
            GameObject cursorObject = null;
            TraitCardDefinition card = null;
            MonsterBaseStats stats = null;
            FieldInfo healthField = null;
            long originalHealth = 0L;
            float originalChance = 0f;
            try
            {
                foreach (string key in keys) PlayerPrefs.DeleteKey(key);
                Assert.That(TraitProgressionStore.Save(25, new long[] { 25 }, Array.Empty<string>(), null, null,
                    newTraitCards: new TraitCardProgressData { ownedIds = new List<string> { "trait_card.treasure_chest" } }), Is.True);
                scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;
                yield return null;

                var progression = Find<TraitScreenController>(scene);
                card = progression.GetTraitCard("trait_card.treasure_chest");
                originalChance = card.gameplay.treasureChest.chancePercent;
                card.gameplay.treasureChest.chancePercent = 100f;
                stats = card.gameplay.treasureChest.monsterDefinition.BaseStats;
                healthField = typeof(MonsterBaseStats).GetField("maxHealth", BindingFlags.Instance | BindingFlags.NonPublic);
                originalHealth = (long)healthField.GetValue(stats);
                healthField.SetValue(stats, 100L);
                hunt = Find<HuntManager>(scene);
                SetField(hunt, "playEntranceSequence", false);
                SetField(hunt, "_ignoreFocusPauseForAutomatedPlayModeTest", true);
                hunt.BeginPrototypeRun();

                var combat = Find<CombatRunController>(scene);
                Assert.That(combat.IsRunning, Is.True);
                var chest = Object.FindObjectsByType<MonsterCombatTarget>(FindObjectsSortMode.None)
                    .Single(target => target.RunId == combat.CurrentRunId && target.IsTraitChest);
                chest.GetComponent<MonsterSpawnPresentation>().Advance(1f);
                chest.transform.position = combat.Battlefield.transform.position;
                chest.ConfigureVisualSorting(Camera.main);
                foreach (var other in Object.FindObjectsByType<MonsterCombatTarget>(FindObjectsSortMode.None))
                {
                    if (other == chest || other.RunId != combat.CurrentRunId) continue;
                    Collider2D otherHitbox = other.transform.Find("HitArea")?.GetComponent<Collider2D>();
                    if (otherHitbox != null) otherHitbox.enabled = false;
                    foreach (var renderer in other.GetComponentsInChildren<SpriteRenderer>()) renderer.enabled = false;
                }
                yield return null;
                var feedback = chest.GetComponentInChildren<TreasureChestHitFeedback>();
                Assert.That(feedback, Is.Not.Null);
                var body = (SpriteRenderer)Field(feedback, "body");
                var flash = (SpriteRenderer)Field(feedback, "flashOverlay");
                var shadow = feedback.transform.Find("GroundShadow");
                var hitbox = chest.transform.Find("HitArea").GetComponent<BoxCollider2D>();
                Physics2D.SyncTransforms();
                Bounds visualBounds = body.bounds;
                Assert.That(Vector2.Distance(hitbox.bounds.center, visualBounds.center), Is.LessThan(.001f));
                float spatialScale = combat.Battlefield.MonsterScale(chest.MonsterId);
                Assert.That(hitbox.bounds.size.x, Is.EqualTo(stats.HitAreaWidth * spatialScale).Within(.001f));
                Assert.That(hitbox.bounds.size.y, Is.EqualTo(stats.HitAreaHeight * spatialScale).Within(.001f));
                Assert.That(hitbox.OverlapPoint(visualBounds.center + Vector3.right * visualBounds.extents.x * .85f), Is.True);
                Assert.That(hitbox.OverlapPoint(visualBounds.center + Vector3.down * visualBounds.extents.y * .65f), Is.True);
                Assert.That(hitbox.OverlapPoint(visualBounds.center + Vector3.right * visualBounds.extents.x * 1.1f), Is.False);

                Vector3 shadowPosition = shadow.position;
                Quaternion shadowRotation = shadow.rotation;
                Quaternion rest = body.transform.localRotation;
                var runId = combat.CurrentRunId;
                bool canRender = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
                Color idlePixel = canRender ? SampleWorldPixel(Camera.main, visualBounds.center) : default;
                cursorObject = new GameObject("ChestEdgeCursor");
                var cursor = cursorObject.AddComponent<CircleCollider2D>();
                cursor.isTrigger = true;
                cursor.radius = .02f;
                cursorObject.transform.position = visualBounds.center + Vector3.down * visualBounds.extents.y * .65f;
                long healthBefore = chest.CurrentHealth;
                Assert.That(combat.TryAttack(cursor, false), Is.True);
                Assert.That(chest.CurrentHealth, Is.LessThan(healthBefore), "A cursor at the visible lower edge must hit.");
                Assert.That(chest.IsActive, Is.True);
                Assert.That(flash.color.a, Is.GreaterThan(.5f));
                if (canRender)
                {
                    Color hitPixel = SampleWorldPixel(Camera.main, visualBounds.center);
                    Assert.That(idlePixel.b, Is.LessThan(.8f), "The idle chest must retain its original color.");
                    Assert.That(hitPixel.b, Is.GreaterThan(idlePixel.b + .08f), "The rendered hit flash must brighten it.");
                }
                Advance(feedback, .025f);
                Assert.That(Quaternion.Angle(body.transform.localRotation, rest), Is.GreaterThan(1f));
                Assert.That(Vector3.Distance(shadow.position, shadowPosition), Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(shadow.rotation, shadowRotation), Is.LessThan(.001f));
                Assert.That(Vector2.Distance(hitbox.bounds.center, visualBounds.center), Is.LessThan(.001f));

                Assert.That(combat.TryApplySkillDamage(runId, chest, 1L), Is.True);
                Assert.That(flash.color.a, Is.GreaterThan(.5f), "Repeated hits restart the flash.");
                var coordinator = (RunCoordinator)Field(hunt, "_runCoordinator");
                Assert.That(coordinator.Pause(), Is.True);
                float pausedOpacity = flash.color.a;
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(flash.color.a, Is.EqualTo(pausedOpacity).Within(.001f));
                Assert.That(coordinator.Resume(), Is.True);
                Advance(feedback, .3f);
                Assert.That(flash.color.a, Is.Zero.Within(.001f));
                Assert.That(Quaternion.Angle(body.transform.localRotation, rest), Is.LessThan(.001f));

                long reward = chest.GarnetReward;
                Assert.That(combat.TryApplySkillDamage(runId, chest, chest.CurrentHealth), Is.True);
                Assert.That(chest.IsDead, Is.True);
                Assert.That(flash.color.a, Is.GreaterThan(.5f), "The lethal hit also flashes before removal.");
                Assert.That(combat.GarnetEarned, Is.EqualTo(reward));
                Assert.That(combat.TryApplySkillDamage(runId, chest, 1L), Is.False);
                Assert.That(combat.GarnetEarned, Is.EqualTo(reward));
                Assert.That(combat.DefeatedCount, Is.Zero);
                Advance(feedback, .3f);
                yield return null;
                Assert.That(chest == null, Is.True);

                hunt.ResetPrototypeRun();
                hunt.BeginPrototypeRun();
                Assert.That(combat.IsRunning, Is.True);
                var secondChest = Object.FindObjectsByType<MonsterCombatTarget>(FindObjectsSortMode.None)
                    .Single(target => target.RunId == combat.CurrentRunId && target.IsTraitChest);
                var secondFeedback = secondChest.GetComponentInChildren<TreasureChestHitFeedback>();
                Assert.That(combat.TryApplySkillDamage(combat.CurrentRunId, secondChest, secondChest.CurrentHealth), Is.True);
                secondFeedback.enabled = false;
                yield return null;
                Assert.That(secondChest == null, Is.True, "Disabling a lethal presentation must not leave a dead chest behind.");
            }
            finally
            {
                if (card != null) card.gameplay.treasureChest.chancePercent = originalChance;
                if (stats != null && healthField != null) healthField.SetValue(stats, originalHealth);
                if (cursorObject != null) Object.Destroy(cursorObject);
                if (hunt != null) hunt.ResetPrototypeRun();
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                for (int i = 0; i < keys.Length; i++)
                    if (previous[i] == null) PlayerPrefs.DeleteKey(keys[i]); else PlayerPrefs.SetString(keys[i], previous[i]);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }

#if UNITY_EDITOR
        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private static object Field(object value, string name) => value.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        private static void SetField(object value, string name, object data) => value.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, data);
        private static void Advance(TreasureChestHitFeedback feedback, float seconds) => typeof(TreasureChestHitFeedback)
            .GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(feedback, new object[] { seconds });

        private static Color SampleWorldPixel(Camera camera, Vector3 worldPosition)
        {
            Assert.That(camera, Is.Not.Null);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(640, 360, 24);
            Texture2D pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
                int x = Mathf.Clamp((int)(viewport.x * target.width), 0, target.width - 1);
                int y = Mathf.Clamp((int)(viewport.y * target.height), 0, target.height - 1);
                pixel.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
                pixel.Apply();
                return pixel.GetPixel(0, 0);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(pixel);
            }
        }
#endif
    }
}
