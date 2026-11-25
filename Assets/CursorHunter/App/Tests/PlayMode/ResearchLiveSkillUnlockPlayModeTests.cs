using System;
using System.Collections;
using System.Linq;
using CursorHunter.Data;
using CursorHunter.Progression.ResearchPrototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.App.Tests
{
    public sealed class ResearchLiveSkillUnlockPlayModeTests
    {
        [UnityTest]
        public IEnumerator LiveSkillUnlockShowsDescriptionAndPurchasesOnlyOnce()
        {
#if UNITY_EDITOR
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Progression/Scenes/ResearchPrototype.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var map = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)).Single();
            var originalLocale = LocalizationSettings.SelectedLocale;
            var live = UnityEditor.AssetDatabase.LoadAssetAtPath<ResearchDefinition>(
                "Assets/CursorHunter/Progression/ResearchPrototype/Live/skill.tornado.unlock.live.asset");
            var test = UnityEditor.AssetDatabase.LoadAssetAtPath<ResearchDefinition>(
                "Assets/CursorHunter/Progression/ResearchPrototype/skill.tornado.unlock.asset");
            var attack = UnityEditor.AssetDatabase.LoadAssetAtPath<ResearchDefinition>(
                "Assets/CursorHunter/Progression/ResearchPrototype/Live/cursor.attack.increase.asset");
            try
            {
                Assert.That(live, Is.Not.Null);
                Assert.That(live.TryValidate(out string error), Is.True, error);
                Assert.That(live.IsLiveSkillUnlock, Is.True);
                Assert.That(live.icon, Is.EqualTo(UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/DownLoadAssets/IconPack/2D Minimal-IconPack/Icons/128/Item_Book_05_Blue.png")));
                Assert.That(map.progression, Is.Null, "The fixture must not alter the player's saved progression.");
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("ko");
                var node = map.nodes[0];
                node.definition = live;
                node.rank = 0;
                node.prerequisites = Array.Empty<ResearchTestNode>();
                node.requiredPrerequisites = 0;
                node.usePlacementCosts = true;
                node.placementCosts = new[] { new ResearchCost { currencyId = "gem.garnet", amount = 2 } };
                map.testGarnet = 10;
                map.Select(node);
                const string description = "랜덤으로 움직이며 피해를 입히는 토네이도 생성";
                for (int i = 0; i < 120 && map.effectDescription.text != description; i++) yield return null;
                Assert.That(map.title.text, Is.EqualTo("토네이도"));
                Assert.That(map.effectDescription.text, Is.EqualTo(description));
                Assert.That(map.beforeValue.gameObject.activeSelf, Is.False);
                Assert.That(map.afterValue.gameObject.activeSelf, Is.False);
                Assert.That(map.arrow.activeSelf, Is.False);
                Assert.That(map.rankInfo.gameObject.activeSelf, Is.False);
                Assert.That(map.costValue.text, Is.EqualTo("2"));
                Canvas.ForceUpdateCanvases();
                map.effectDescription.ForceMeshUpdate();
                Assert.That(map.effectDescription.isTextTruncated, Is.False, "The complete skill description must fit in the tooltip.");
                map.costLabel.ForceMeshUpdate();
                map.costValue.ForceMeshUpdate();
                Assert.That(map.costLabel.textInfo.characterCount, Is.GreaterThan(0), "The cost label must be visible.");
                Assert.That(map.costValue.textInfo.characterCount, Is.GreaterThan(0), "The cost amount must be visible.");

                var info = GameDataDocument.Load().information;
                var tornado = info.skills.Single(x => x.id == "skill.tornado");
                tornado.enabled = false;
                ResearchEffectCalculator.ApplyAdditional(new[] { node }, _ => node.rank, info);
                Assert.That(tornado.enabled, Is.False);
                map.Purchase(node);
                Assert.That(node.rank, Is.EqualTo(1));
                Assert.That(map.testGarnet, Is.EqualTo(8));
                Assert.That(map.CanPurchase(node), Is.False);
                ResearchEffectCalculator.ApplyAdditional(new[] { node }, _ => node.rank, info);
                Assert.That(tornado.enabled, Is.True);
                map.Purchase(node);
                Assert.That(map.testGarnet, Is.EqualTo(8));
                for (int i = 0; i < 120 && map.costValue.text != "해금 완료"; i++) yield return null;
                Assert.That(map.costValue.text, Is.EqualTo("해금 완료"));
                map.costValue.ForceMeshUpdate();
                Assert.That(map.costValue.textInfo.characterCount, Is.GreaterThan(0), "The completed state must be visible.");
                Assert.That(map.effectDescription.text, Is.EqualTo(description));
                Assert.That(map.rankInfo.gameObject.activeSelf, Is.False);

                node.definition = test;
                node.rank = 0;
                map.Select(node);
                Assert.That(map.beforeValue.gameObject.activeSelf, Is.True);
                Assert.That(map.rankInfo.gameObject.activeSelf, Is.True);
                Assert.That(map.rankInfo.text, Is.EqualTo("0 / 1"));
                Assert.That(map.beforeValue.text, Does.Contain("→"));

                node.definition = attack;
                node.placementCosts = Enumerable.Repeat(new ResearchCost { currencyId = "gem.garnet", amount = 0 }, 5).ToArray();
                map.Select(node);
                for (int i = 0; i < 120 && !map.effectDescription.text.StartsWith("기본 공격력 증가"); i++) yield return null;
                Assert.That(map.effectDescription.text, Is.EqualTo("기본 공격력 증가 :"));
                Assert.That(map.beforeValue.gameObject.activeSelf, Is.True);
                Assert.That(map.rankInfo.text, Is.EqualTo("0 / 5"));
                Assert.That(map.beforeValue.text, Does.Contain("→"));
            }
            finally
            {
                LocalizationSettings.SelectedLocale = originalLocale;
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
#else
            yield return null;
#endif
        }
    }
}
