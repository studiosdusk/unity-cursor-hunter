using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CursorHunter.Progression;
using CursorHunter.Progression.ResearchPrototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.App.Tests
{
    public class ResearchPlacementCostPlayModeTests
    {
        [UnityTest]
        public IEnumerator TooltipAndPurchaseUseTheSamePlacementCost()
        {
#if UNITY_EDITOR
            const string key = "cursor_hunter.progression.v1";
            string previous = PlayerPrefs.GetString(key, "");
            bool existed = PlayerPrefs.HasKey(key);
            string backupKey = key + ".before-v6";
            string backup = PlayerPrefs.GetString(backupKey, "");
            bool backupExisted = PlayerPrefs.HasKey(backupKey);
            TraitProgressionStore.Save(1500, new long[] { 1500, 42, 3, 40, 5, 6 },
                new string[0], new Dictionary<string, int>(), new Dictionary<string, string>());
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            try
            {
                yield return null;
                var map = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)).Single();
                map.gameObject.SetActive(true);
                yield return null;
                var first = map.nodes.Single(x => x.placementId == "cursor.attack.start");
                var second = map.nodes.Single(x => x.placementId == "cursor.attack.next");
                Assert.That(first.definition, Is.EqualTo(second.definition));
                foreach (var node in new[] { first, second })
                {
                    node.usePlacementCosts = true;
                    node.placementCosts = node.definition.ranks.Select(x => x.cost).ToArray();
                }
                first.placementCosts[0] = new ResearchCost { currencyId = "gem.topaz", amount = 7 };
                second.placementCosts[0] = new ResearchCost { currencyId = "gem.sapphire", amount = 9 };
                map.Select(first);
                Assert.That(map.costValue.text, Is.EqualTo("7"));
                Assert.That(map.progression.TryPurchaseResearchRank(first.placementId, 0, "gem.garnet", 100), Is.False);
                map.Purchase(first);
                Assert.That(map.progression.GetResearchGemstoneBalance(1), Is.EqualTo(35));
                Assert.That(second.IsUnlocked, Is.True);
                map.Select(second);
                Assert.That(map.costValue.text, Is.EqualTo("9"));
                map.Purchase(second);
                Assert.That(map.progression.GetResearchGemstoneBalance(3), Is.EqualTo(31));
                Assert.That(map.progression.GetResearchRank(second.placementId), Is.EqualTo(1));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                if (existed) PlayerPrefs.SetString(key, previous); else PlayerPrefs.DeleteKey(key);
                if (backupExisted) PlayerPrefs.SetString(backupKey, backup); else PlayerPrefs.DeleteKey(backupKey);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }
    }
}
