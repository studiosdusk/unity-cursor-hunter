using System.Collections;
using System.Linq;
using CursorHunter.Progression.ResearchPrototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.App.Tests
{
    public class ResearchLocalizationPlayModeTests
    {
        [UnityTest]
        public IEnumerator TooltipUsesKoreanFallbackWhenEnglishTranslationIsEmpty()
        {
#if UNITY_EDITOR
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Progression/Scenes/ResearchPrototype.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var map = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ResearchTestMap>(true)).Single();
            var original = LocalizationSettings.SelectedLocale;
            try
            {
                map.Select(map.nodes[0]);
                for (int i = 0; i < 120 && map.title.text != "초급 공격력 연구"; i++) yield return null;
                Assert.That(map.title.text, Is.EqualTo("초급 공격력 연구"));
                var english = LocalizationSettings.AvailableLocales.GetLocale("en");
                Assert.That(english, Is.Not.Null);
                LocalizationSettings.SelectedLocale = english;
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(map.effectDescription.text, Is.EqualTo("기본 공격 피해 증가"));
                Assert.That(map.title.text, Is.EqualTo("초급 공격력 연구"));
            }
            finally { LocalizationSettings.SelectedLocale = original; }
            yield return SceneManager.UnloadSceneAsync(scene);
#else
            yield return null;
#endif
        }
    }
}
