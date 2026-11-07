using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CursorHunter.App;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.Combat.Tests
{
    public sealed class IsometricMainPlayModeTests
    {
        private Scene _scene;
        private float _timeScale;

        [UnitySetUp] public IEnumerator SetUp()
        {
            _timeScale = Time.timeScale;
            Time.timeScale = 0f;
#if UNITY_EDITOR
            _scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            yield return null;
            Set(Object.FindFirstObjectByType<HuntManager>(), "_ignoreFocusPauseForAutomatedPlayModeTest", true);
        }

        [UnityTearDown] public IEnumerator CleanUp()
        {
            Time.timeScale = _timeScale;
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }

        [UnityTest]
        public IEnumerator TwentyFortyEightyMonstersStayOnMapAndLiveBalanceDoesNotCompoundScale()
        {
            var hunt = Object.FindFirstObjectByType<HuntManager>();
            var combat = Object.FindFirstObjectByType<CombatRunController>();
            var spawner = Object.FindFirstObjectByType<MonsterSpawner>();
            BattlefieldArea field = combat.Battlefield;
            Assert.That(field, Is.Not.Null, "Main must reference its actual map instance.");
            Assert.That(field.transform.position.x, Is.EqualTo(-0.2981043f).Within(0.001f));
            Set(hunt, "playEntranceSequence", false);
            Set(hunt, "connectTraitScreenToCombat", false);
            var targets = new List<MonsterCombatTarget>();
            foreach (int count in new[] { 20, 40, 80 })
            {
                CombatBalanceSettings settings = hunt.CaptureCombatBalanceSettings();
                settings.UsePlayerProgression = false;
                settings.DurationSeconds = 30f;
                foreach (SkillBalanceSettings skill in settings.Skills) skill.Enabled = false;
                for (int index = 0; index < settings.Monsters.Count; index++)
                {
                    MonsterBalanceSettings monster = settings.Monsters[index];
                    monster.Enabled = true;
                    monster.PackSize = count / settings.Monsters.Count +
                        (index < count % settings.Monsters.Count ? 1 : 0);
                    monster.MaxHealth = 100000L;
                    monster.SpawnIntervalSeconds = 120f;
                }
                Assert.That(hunt.TrySetCombatBalanceSettings(settings, out string error), Is.True, error);
                hunt.BeginPrototypeRun();
                Assert.That(combat.IsRunning, Is.True);
                targets.Clear();
                foreach (var target in Object.FindObjectsByType<MonsterCombatTarget>(FindObjectsSortMode.None))
                    if (target.IsActive && target.RunId == combat.CurrentRunId) targets.Add(target);
                Assert.That(targets.Count, Is.EqualTo(count));
                for (int i = 0; i < 50; i++)
                {
                    combat.AdvanceTime(0.5f);
                    yield return null;
                    foreach (MonsterCombatTarget target in targets)
                        Assert.That(field.Contains(target.transform.position), Is.True, target.MonsterId);
                }
                Assert.That(combat.ElapsedSeconds, Is.EqualTo(25f).Within(0.001f));
                foreach (SpawnPlanEntry entry in spawner.CurrentSpawnPlan.Entries)
                {
                    MonsterCombatTarget target = targets.Find(t => t.MonsterId == entry.Snapshot.MonsterId);
                    Assert.That(target, Is.Not.Null);
                    float scale = field.MonsterScale(target.MonsterId);
                    Assert.That(Mathf.Abs(target.transform.Find("VisualRoot").localScale.x),
                        Is.EqualTo(entry.Snapshot.VisualScale * scale).Within(0.0001f));
                    var collider = target.GetComponentInChildren<BoxCollider2D>();
                    Assert.That(collider.size.x, Is.EqualTo(entry.Snapshot.HitAreaWidth * scale).Within(0.0001f));
                    Assert.That(field.Contains(target.transform.position, field.MonsterInset(entry.Snapshot)), Is.True);
                }
                Vector3 before = targets[0].transform.Find("VisualRoot").localScale;
                for (int i = 0; i < 3; i++)
                    Assert.That(hunt.TrySetCombatBalanceSettings(settings, out error), Is.True, error);
                Assert.That(targets[0].transform.Find("VisualRoot").localScale, Is.EqualTo(before));
                Assert.That(hunt.CaptureCombatBalanceSettings().Monsters[0].VisualScale,
                    Is.EqualTo(settings.Monsters[0].VisualScale));
                hunt.ReturnToMainMenu();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AllSkillsUseFieldScalesAndMapBoundsAcrossPoolReuse()
        {
            var hunt = Object.FindFirstObjectByType<HuntManager>();
            var combat = Object.FindFirstObjectByType<CombatRunController>();
            var cursor = Object.FindFirstObjectByType<MainCursorController>();
            var cursorAttack = Object.FindFirstObjectByType<CursorAttackController>();
            var pet = Object.FindFirstObjectByType<PetSkillController>();
            Transform petRoot = Get<Transform>(pet, "petRoot");
            Vector3 authoredPetScale = petRoot.localScale;
            cursor.enabled = false;
            cursorAttack.enabled = false;
            Set(hunt, "playEntranceSequence", false);
            Set(hunt, "connectTraitScreenToCombat", false);
            BattlefieldArea field = combat.Battlefield;
            for (int run = 0; run < 2; run++)
            {
                CombatBalanceSettings settings = hunt.CaptureCombatBalanceSettings();
                settings.UsePlayerProgression = false;
                settings.DurationSeconds = 30f;
                foreach (SkillBalanceSettings skill in settings.Skills) skill.Enabled = true;
                foreach (MonsterBalanceSettings monster in settings.Monsters)
                {
                    monster.Enabled = true;
                    monster.PackSize = 4;
                    monster.MaxHealth = 100000L;
                }
                Assert.That(hunt.TrySetCombatBalanceSettings(settings, out string error), Is.True, error);
                hunt.BeginPrototypeRun();
                cursor.SetRangeMultiplier(1f);
                cursor.SetSpatialScale(field.CursorScale);
                cursor.ShowCursorImage();
                cursor.CursorTransform.position = field.transform.position;
                var seen = new HashSet<string>();
                for (int frame = 0; frame < 100; frame++)
                {
                    combat.AdvanceTime(0.2f);
                    yield return null;
                    Assert.That(field.Contains(petRoot.position), Is.True);
                    Assert.That(petRoot.localScale, Is.EqualTo(authoredPetScale * field.PetScale));
                    foreach (MagicBallObject ball in Object.FindObjectsByType<MagicBallObject>(FindObjectsSortMode.None))
                    {
                        if (!ball.IsActive) continue;
                        seen.Add("ball");
                        var body = ball.GetComponent<CircleCollider2D>();
                        Assert.That(field.Contains(body.bounds.center, body.bounds.extents.x), Is.True);
                    }
                    foreach (TornadoObject tornado in Object.FindObjectsByType<TornadoObject>(FindObjectsSortMode.None))
                    {
                        if (!tornado.IsActive) continue;
                        seen.Add("tornado");
                        Assert.That(field.Contains(tornado.transform.position), Is.True);
                    }
                    foreach (LaserEffect laser in Object.FindObjectsByType<LaserEffect>(FindObjectsSortMode.None))
                    {
                        if (!laser.IsActive) continue;
                        seen.Add("laser");
                        BoxCollider2D box = laser.GetComponentInChildren<BoxCollider2D>();
                        foreach (float x in new[] { -0.5f, 0.5f })
                            foreach (float y in new[] { -0.5f, 0.5f })
                                Assert.That(field.Contains(box.transform.TransformPoint(box.offset +
                                    new Vector2(x * box.size.x, y * box.size.y))), Is.True,
                                    "Laser corner outside map.");
                    }
                    foreach (FlameExhaustProjectile flame in Object.FindObjectsByType<FlameExhaustProjectile>(FindObjectsSortMode.None))
                    {
                        if (!flame.IsActive) continue;
                        seen.Add("flame");
                        Assert.That(field.Contains(flame.transform.position), Is.True);
                    }
                    if (Object.FindFirstObjectByType<ThunderEffect>() != null) seen.Add("thunder");
                    if (Object.FindFirstObjectByType<HeadshotEffect>() != null) seen.Add("headshot");
                }
                Assert.That(seen, Is.EquivalentTo(new[] { "ball", "tornado", "laser", "flame", "thunder", "headshot" }));
                Assert.That(cursor.CursorTransform.localScale.x, Is.EqualTo(2f * field.CursorScale).Within(0.0001f));
                hunt.ReturnToMainMenu();
                yield return null;
                Assert.That(petRoot.localScale, Is.EqualTo(authoredPetScale));
            }
        }

        [UnityTest]
        public IEnumerator AllTwentyBossStagesUseTheMapAndScaledHitAreas()
        {
            var hunt = Object.FindFirstObjectByType<HuntManager>();
            var combat = Object.FindFirstObjectByType<CombatRunController>();
            var spawner = Object.FindFirstObjectByType<MonsterSpawner>();
            Set(hunt, "playEntranceSequence", false);
            Set(hunt, "connectTraitScreenToCombat", false);
            CombatBalanceSettings settings = hunt.CaptureCombatBalanceSettings();
            settings.UsePlayerProgression = false;
            foreach (SkillBalanceSettings skill in settings.Skills) skill.Enabled = false;
            Assert.That(hunt.TrySetCombatBalanceSettings(settings, out string error), Is.True, error);
            for (int stage = 1; stage <= 20; stage++)
            {
                hunt.BeginBossStage(stage);
                Assert.That(combat.IsBossRun, Is.True, "stage " + stage);
                MonsterCombatTarget boss = spawner.BossTarget;
                Assert.That(boss, Is.Not.Null);
                SpawnSnapshot snapshot = spawner.CurrentSpawnPlan.Entries[0].Snapshot;
                Assert.That(Mathf.Abs(boss.transform.Find("VisualRoot").localScale.x),
                    Is.EqualTo(snapshot.VisualScale * combat.Battlefield.MonsterScale(snapshot.MonsterId)).Within(0.0001f));
                for (int step = 0; step < 5; step++)
                {
                    combat.AdvanceTime(1f);
                    yield return null;
                    Assert.That(combat.Battlefield.Contains(boss.transform.position,
                        combat.Battlefield.MonsterInset(snapshot)), Is.True);
                }
                hunt.ReturnToMainMenu();
                yield return null;
            }
        }

        private static T Get<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
