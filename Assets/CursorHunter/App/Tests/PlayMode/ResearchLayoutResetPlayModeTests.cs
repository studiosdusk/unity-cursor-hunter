using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CursorHunter.Progression;
using CursorHunter.Progression.ResearchPrototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.App.Tests
{
    public class ResearchLayoutResetPlayModeTests
    {
        [UnityTest]
        public IEnumerator SceneStartupKeepsMatchingPurchasesAndResetsOutdatedPurchases()
        {
#if UNITY_EDITOR
            const string key = "cursor_hunter.progression.v1";
            const string backupKey = key + ".before-v6";
            bool hadSave = PlayerPrefs.HasKey(key), hadBackup = PlayerPrefs.HasKey(backupKey);
            string oldSave = PlayerPrefs.GetString(key, ""), oldBackup = PlayerPrefs.GetString(backupKey, "");
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(backupKey);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            try
            {
                yield return null;
                var map = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)).Single();
                Assert.That(map.TryGetLayoutFingerprint(out string current), Is.True);
                string rootId = map.nodes.First(x => x.prerequisites == null || x.prerequisites.Length == 0).placementId;
                yield return SceneManager.UnloadSceneAsync(scene);

                void Seed(string fingerprint)
                {
                    PlayerPrefs.DeleteKey(key);
                    Assert.That(TraitProgressionStore.Save(250, new long[] { 250, 77 },
                        new[] { "stat.attack.02" }, null, null, experiencePoints: 456,
                        researchRanks: new Dictionary<string, int> { [rootId] = 1 },
                        researchLayoutFingerprint: fingerprint), Is.True);
                }

                Seed(current);
                scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;
                map = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)).Single();
                Assert.That(map.progression.GetResearchRank(rootId), Is.EqualTo(1));
                yield return SceneManager.UnloadSceneAsync(scene);

                Seed("previous-layout");
                scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;
                map = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)).Single();
                Assert.That(map.progression.GetResearchRank(rootId), Is.Zero);
                Assert.That(TraitProgressionStore.TryLoad(out var saved), Is.True);
                Assert.That(saved.researchRanks, Is.Empty);
                Assert.That(saved.researchLayoutFingerprint, Is.EqualTo(current));
                Assert.That(saved.garnetBalance, Is.EqualTo(250));
                Assert.That(saved.gemstoneBalances[1], Is.EqualTo(77));
                Assert.That(saved.experiencePoints, Is.EqualTo(456));
                Assert.That(saved.purchasedNodeIds, Does.Contain("stat.attack.02"));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                if (hadSave) PlayerPrefs.SetString(key, oldSave); else PlayerPrefs.DeleteKey(key);
                if (hadBackup) PlayerPrefs.SetString(backupKey, oldBackup); else PlayerPrefs.DeleteKey(backupKey);
                PlayerPrefs.Save();
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator LayoutChangesResetOnlyResearchAndPersistTheNewFingerprint()
        {
#if UNITY_EDITOR
            const string key = "cursor_hunter.progression.v1";
            const string backupKey = key + ".before-v6";
            bool hadSave = PlayerPrefs.HasKey(key), hadBackup = PlayerPrefs.HasKey(backupKey);
            string oldSave = PlayerPrefs.GetString(key, ""), oldBackup = PlayerPrefs.GetString(backupKey, "");
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(backupKey);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            try
            {
                yield return null;
                var map = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)).Single();
                var controller = map.progression;
                var root = map.nodes.First(x => x.prerequisites == null || x.prerequisites.Length == 0);
                var rect = (RectTransform)root.transform;
                Assert.That(map.TryGetLayoutFingerprint(out string original), Is.True);
                var type = typeof(TraitScreenController);
                var initialize = type.GetMethod("InitializePurchasedState", BindingFlags.NonPublic | BindingFlags.Instance);
                var load = type.GetMethod("LoadSavedProgress", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(initialize, Is.Not.Null);
                Assert.That(load, Is.Not.Null);

                void Seed(string fingerprint)
                {
                    PlayerPrefs.DeleteKey(key);
                    Assert.That(TraitProgressionStore.Save(250, new long[] { 250, 77, 3, 4, 5, 6 },
                        new[] { "stat.attack.02" }, new Dictionary<string, int> { ["stat.attack.02"] = 9 },
                        new Dictionary<string, string> { ["stat.attack.02"] = "gem.garnet" },
                        ownedRelicIds: new[] { "relic.red_potion" }, experiencePoints: 456,
                        researchRanks: new Dictionary<string, int> { [root.placementId] = 1, ["archived.research"] = 2 },
                        researchLayoutFingerprint: fingerprint), Is.True);
                }
                void Reload()
                {
                    initialize.Invoke(controller, null);
                    load.Invoke(controller, null);
                }

                // A legacy save on the unchanged map is kept and receives a fingerprint.
                map.legacyResearchLayoutFingerprint = original;
                Seed(null);
                Reload();
                Assert.That(controller.GetResearchRank(root.placementId), Is.EqualTo(1));
                Assert.That(TraitProgressionStore.TryLoad(out var sameLegacy), Is.True);
                Assert.That(sameLegacy.researchLayoutFingerprint, Is.EqualTo(original));
                Assert.That(sameLegacy.researchRanks.Count, Is.EqualTo(2));

                // A pre-fingerprint save loaded after the scene changes uses the captured old baseline.
                rect.anchoredPosition += new Vector2(11, 0);
                Assert.That(map.TryGetLayoutFingerprint(out string changed), Is.True);
                Assert.That(changed, Is.Not.EqualTo(original));
                Seed(null);
                Reload();
                Assert.That(controller.GetResearchRank(root.placementId), Is.Zero);
                Assert.That(TraitProgressionStore.TryLoad(out var reset), Is.True);
                Assert.That(reset.researchRanks, Is.Empty);
                Assert.That(reset.researchLayoutFingerprint, Is.EqualTo(changed));
                Assert.That(reset.garnetBalance, Is.EqualTo(250));
                Assert.That(reset.gemstoneBalances[1], Is.EqualTo(77));
                Assert.That(reset.experiencePoints, Is.EqualTo(456));
                Assert.That(reset.ownedRelicIds, Does.Contain("relic.red_potion"));
                Assert.That(reset.purchasedNodeIds, Does.Contain("stat.attack.02"));
                Assert.That(reset.spentCosts, Does.Contain(9));

                // Reopening the same layout preserves new purchases; a later move resets them again.
                Seed(changed);
                Reload();
                Assert.That(controller.GetResearchRank(root.placementId), Is.EqualTo(1));
                rect.anchoredPosition += new Vector2(13, 0);
                Assert.That(map.TryGetLayoutFingerprint(out string movedAgain), Is.True);
                Reload();
                Assert.That(controller.GetResearchRank(root.placementId), Is.Zero);
                Assert.That(TraitProgressionStore.TryLoad(out var resetAgain), Is.True);
                Assert.That(resetAgain.researchRanks, Is.Empty);
                Assert.That(resetAgain.researchLayoutFingerprint, Is.EqualTo(movedAgain));
                Assert.That(resetAgain.garnetBalance, Is.EqualTo(250));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                if (hadSave) PlayerPrefs.SetString(key, oldSave); else PlayerPrefs.DeleteKey(key);
                if (hadBackup) PlayerPrefs.SetString(backupKey, oldBackup); else PlayerPrefs.DeleteKey(backupKey);
                PlayerPrefs.Save();
            }
#else
            yield break;
#endif
        }
    }
}
