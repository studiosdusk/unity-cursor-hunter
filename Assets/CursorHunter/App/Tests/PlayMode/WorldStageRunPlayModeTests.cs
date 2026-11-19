using System.Collections;
using System.Linq;
using System.Reflection;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;
using CursorHunter.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.App.Tests
{
    public sealed class WorldStageRunPlayModeTests
    {
        [UnityTest]
        public IEnumerator PurchasedMapsBindNormalAndBossBattlefieldsAndRestoreAfterReload()
        {
#if UNITY_EDITOR
            const string key = "cursor_hunter.progression.v1";
            string[] keys = { key, key + ".before-v6" };
            var previous = keys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
            Scene scene = default;
            WorldStageDefinition desert = null;
            float hp = 1, xp = 1;
            try
            {
                // A level-unlocked legacy field must not grant purchased maps.
                PlayerPrefs.SetString(key, "{\"version\":12,\"selectedFieldTier\":5,\"garnetBalance\":456,\"hasNewTraitCardProgress\":true,\"newTraitCards\":{\"points\":9}}");
                scene = Load(); yield return null; yield return null;
                var progress = Find<TraitScreenController>(scene);
                var hunt = Find<HuntManager>(scene);
                var combat = Find<CombatRunController>(scene);
                var app = Find<AppUiRootController>(scene);
                typeof(HuntManager).GetField("playEntranceSequence", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hunt, false);
                var catalog = progress.WorldStages;
                Assert.That(catalog.Count, Is.EqualTo(6));
                Assert.That(catalog.Count(progress.IsStageUnlocked), Is.EqualTo(1));
                Assert.That(progress.SelectedWorldStage.stableId, Is.EqualTo("forest"));
                desert = catalog[1]; hp = desert.monsterHealthMultiplier; xp = desert.monsterExperienceMultiplier;
                desert.monsterHealthMultiplier = 1.10f;
                desert.monsterExperienceMultiplier = .50f;
                Assert.That(desert.ScaleMonsterHealth(15), Is.EqualTo(17));
                Assert.That(desert.ScaleMonsterHealth(10), Is.EqualTo(11));

                foreach (var stage in catalog)
                {
                    if (!stage.initiallyUnlocked) Assert.That(progress.TryChooseStage(stage, true), Is.True, stage.stableId);
                    var baseline = progress.CreateCombatSnapshot().Monsters.First(m => m.Unlocked);
                    foreach (bool boss in new[] { false, true })
                    {
                        if (boss) hunt.BeginBossRun(); else hunt.BeginPrototypeRun();
                        Assert.That(app.CurrentScreen, Is.EqualTo(UiScreenId.Combat));
                        Assert.That(combat.Battlefield.name, Is.EqualTo(stage.mapPrefab.name));
                        var active = combat.Battlefield.transform.parent.GetComponentsInChildren<BattlefieldArea>(false);
                        Assert.That(active, Is.EqualTo(new[] { combat.Battlefield }));
                        var request = (RunRequest)typeof(CombatRunController).GetField("_runRequest",
                            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(combat);
                        Assert.That(request.WorldExperienceMultiplier, Is.EqualTo(boss ? 1f : stage.monsterExperienceMultiplier));
                        Assert.That(request.FieldGemstoneRewardMultiplier, Is.EqualTo(1));
                        if (!boss)
                        {
                            var target = Object.FindObjectsByType<MonsterCombatTarget>(FindObjectsSortMode.None)
                                .First(m => m.MonsterId == baseline.MonsterId && m.RunId == request.RunId);
                            Assert.That(target.MaxHealth, Is.EqualTo(stage.ScaleMonsterHealth(baseline.HitPoints)));
                        }
                        hunt.ResetPrototypeRun(); app.ShowTrait();
                        if (stage == desert)
                        {
                            Assert.That(progress.TryChooseStage(catalog[0], false), Is.True);
                            hunt.ReplayLastRun();
                            Assert.That(hunt.IsBossCombatActive, Is.EqualTo(boss));
                            Assert.That(combat.Battlefield.name, Is.EqualTo(desert.mapPrefab.name));
                            hunt.ResetPrototypeRun(); app.ShowTrait();
                            Assert.That(progress.TryChooseStage(stage, false), Is.True);
                        }
                    }
                }
                yield return SceneManager.UnloadSceneAsync(scene);
                scene = Load(); yield return null; yield return null;
                progress = Find<TraitScreenController>(scene);
                Assert.That(progress.SelectedWorldStage.stableId, Is.EqualTo("construction"));
                Assert.That(progress.WorldStages.Count(progress.IsStageUnlocked), Is.EqualTo(6));
                Assert.That(progress.TraitCards.Points, Is.Zero);
                Assert.That(TraitProgressionStore.TryLoad(out var save), Is.True);
                Assert.That(save.garnetBalance, Is.EqualTo(456));
                Assert.That(save.version, Is.EqualTo(13));
            }
            finally
            {
                if (desert != null) { desert.monsterHealthMultiplier = hp; desert.monsterExperienceMultiplier = xp; }
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
        private static Scene Load() => UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));

        private static T Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
#endif
    }
}
