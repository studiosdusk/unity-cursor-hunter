using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CursorHunter.App;
using CursorHunter.Progression;
using CursorHunter.Progression.ResearchPrototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CursorHunter.App.Tests
{
    public sealed class ProgressionTopMenuPlayModeTests
    {
        [UnityTest]
        public IEnumerator BottomButtonsStartHuntAndBossRuns()
        {
#if UNITY_EDITOR
            const string key = "cursor_hunter.progression.v1";
            string backupKey = key + ".before-v6";
            bool existed = PlayerPrefs.HasKey(key), backupExisted = PlayerPrefs.HasKey(backupKey);
            string previous = PlayerPrefs.GetString(key), backup = PlayerPrefs.GetString(backupKey);
            Assert.That(TraitProgressionStore.Save(1500, new long[] { 1500, 0, 0, 0, 0, 0 },
                new string[0], new Dictionary<string, int>(), new Dictionary<string, string>()), Is.True);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            try
            {
                yield return null;
                yield return null;
                var canvas = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                var app = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AppUiRootController>(true)).Single();
                var hunt = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HuntManager>(true)).Single();
                var bottom = canvas.transform.Find("ButtomMenu");
                var progression = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TraitScreenController>(true)).Single();
                Assert.That(progression.CanStartPlayerCombat, Is.True);
                var information = JsonUtility.FromJson<CursorHunter.Contracts.GameInformation>(progression.CreateGameInformationJson());
                Assert.That(information.monsters.Single(monster => monster.id == "monster.01").enabled, Is.True);
                Assert.That(TraitProgressionStore.TryLoad(out var saved), Is.True);
                Assert.That(saved.purchasedNodeIds, Is.Empty, "The baseline must not write legacy purchases.");
                var huntButton = bottom.Find("Menus/Button_MainMenu/Button_Battle").GetComponent<Button>();
                huntButton.onClick.Invoke();
                yield return null;
                Assert.That(app.CurrentScreen, Is.EqualTo(UiScreenId.Combat), "Hunt button enters combat.");
                Assert.That(bottom.gameObject.activeSelf, Is.False);
                var spawner = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CursorHunter.Combat.MonsterSpawner>(true)).Single();
                float deadline = Time.realtimeSinceStartup + 4f;
                while (spawner.ActiveSpawnedCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(spawner.ActiveSpawnedCount, Is.GreaterThan(0), "A new profile spawns the basic monster.");
                hunt.ResetPrototypeRun();
                app.ShowTrait();
                yield return null;
                var bossButton = bottom.Find("Menus/Button_MainMenu/Button_Boss").GetComponent<Button>();
                bossButton.onClick.Invoke();
                yield return null;
                Assert.That(app.CurrentScreen, Is.EqualTo(UiScreenId.Combat), "Boss button enters combat.");
                Assert.That(hunt.IsBossCombatActive, Is.True);
                hunt.ResetPrototypeRun();
                app.ShowTrait();
                yield return null;
                var fieldButton = canvas.transform.Find("TopMenu/Menus/Button_MainMenu/Button_Shop (3)").GetComponent<Button>();
                fieldButton.onClick.Invoke();
                yield return null;
                var fieldCard = progression.GetComponentsInChildren<Button>(true).Single(button => button.name == "SelectField_1");
                fieldCard.onClick.Invoke();
                yield return null;
                Assert.That(app.CurrentScreen, Is.EqualTo(UiScreenId.Combat), "Selecting a field starts a hunt on a new profile.");
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
        [UnityTest]
        public IEnumerator MenusSwitchCollectionsAndFollowAppScreenWithoutLosingResearch()
        {
#if UNITY_EDITOR
            const string key = "cursor_hunter.progression.v1";
            string backupKey = key + ".before-v6";
            bool existed = PlayerPrefs.HasKey(key), backupExisted = PlayerPrefs.HasKey(backupKey);
            string previous = PlayerPrefs.GetString(key), backup = PlayerPrefs.GetString(backupKey);
            Assert.That(TraitProgressionStore.Save(1500, new long[] { 1500, 0, 0, 0, 0, 0 },
                new string[0], new Dictionary<string, int>(), new Dictionary<string, string>()), Is.True);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            try
            {
                yield return null;
                yield return null;
                var canvas = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                var top = canvas.transform.Find("TopMenu");
                var nav = top.Find("Menus/Button_MainMenu");
                var research = nav.Find("Button_Research").GetComponent<Button>();
                var relic = nav.Find("Button_Artifact").GetComponent<Button>();
                var talent = nav.Find("Button_TraitCard").GetComponent<Button>();
                var field = nav.Find("Button_Shop (3)").GetComponent<Button>();
                var bottom = canvas.transform.Find("ButtomMenu");
                var bottomNav = bottom.Find("Menus/Button_MainMenu");
                var boss = bottomNav.Find("Button_Boss").GetComponent<Button>();
                var huntButton = bottomNav.Find("Button_Battle").GetComponent<Button>();
                Assert.That(research.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text, Is.EqualTo("연구"));
                Assert.That(relic.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text, Is.EqualTo("유물"));
                Assert.That(talent.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text, Is.EqualTo("특성"));
                Assert.That(field.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text, Is.EqualTo("스테이지"));
                Assert.That(boss.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text, Is.EqualTo("보스"));
                Assert.That(huntButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text, Is.EqualTo("사냥"));
                var map = canvas.transform.Find("ResearchPrototype_Canvas").GetComponent<ResearchTestMap>();
                var progression = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TraitScreenController>(true)).Single();
                var app = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AppUiRootController>(true)).Single();
                var runtime = progression.transform.Find("TraitScreen_Runtime");
                var artwork = runtime.Find("TraitBackdropArtwork").GetComponent<Image>();
                var veil = runtime.Find("TraitBackdropDesaturation").GetComponent<Image>();
                var atmosphere = runtime.Find("TraitBackdropAtmosphere").GetComponent<Image>();
                var authoredColor = new Color(0.3f, 0.4f, 0.5f, 0.7f);
                artwork.color = authoredColor;
                var authoredPosition = new Vector2(13f, 21f);
                veil.rectTransform.anchoredPosition = authoredPosition;
                Assert.That(app.CurrentScreen, Is.EqualTo(UiScreenId.Trait));
                Assert.That(top.gameObject.activeInHierarchy, Is.True);
                Assert.That(map.gameObject.activeInHierarchy, Is.True);
                Assert.That(runtime.gameObject.activeSelf, Is.False);
                Assert.That(research.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));
                Assert.That(relic.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_n"));
                Assert.That(map.title.font.name, Is.EqualTo("KoreanNotoBold"));
                Assert.That(map.costValue.font.name, Is.EqualTo("KoreanNotoBold"));
                Assert.That(map.gemstoneValues[0].font.name, Is.EqualTo("KoreanNotoBold"));
                foreach (Button button in new[] { research, relic, talent })
                    Assert.That(button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).font.name, Is.EqualTo("KoreanNotoBold"), button.name);
                Canvas.ForceUpdateCanvases();
                var eventSystem = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Single();
                foreach (Button button in new[] { research, relic, talent, field, boss, huntButton })
                {
                    var pointer = new PointerEventData(eventSystem) { position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position) };
                    var hits = new List<RaycastResult>();
                    eventSystem.RaycastAll(pointer, hits);
                    Assert.That(hits.Count, Is.GreaterThan(0), button.name);
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject), button.name);
                }
                map.Purchase(map.nodes[0]);
                Assert.That(map.nodes[0].rank, Is.EqualTo(1));
                long balance = progression.GetResearchGemstoneBalance(0);
                relic.onClick.Invoke();
                yield return null;
                Assert.That(map.gameObject.activeSelf, Is.False);
                Assert.That(runtime.gameObject.activeInHierarchy, Is.True);
                Assert.That(relic.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));
                Assert.That(research.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_n"));
                Assert.That(runtime.GetComponentsInChildren<Transform>(false).Any(t => t.name == "RelicSlot_01"), Is.True);
                Assert.That(runtime.Find("ResearchCategoryTabs").gameObject.activeSelf, Is.False);
                talent.onClick.Invoke();
                yield return null;
                Assert.That(talent.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));
                Assert.That(relic.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_n"));
                Assert.That(runtime.GetComponentsInChildren<Transform>(false).Any(t => t.name == "TalentCollectionPanel"), Is.True);
                field.onClick.Invoke();
                yield return null;
                Assert.That(field.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));
                Assert.That(runtime.GetComponentsInChildren<Transform>(false).Any(t => t.name == "FieldCard_1"), Is.True);
                Assert.That(runtime.Find("TraitBackdropArtwork").GetComponent<Image>(), Is.SameAs(artwork));
                Assert.That(artwork.color, Is.EqualTo(authoredColor));
                Assert.That(runtime.Find("TraitBackdropDesaturation").GetComponent<Image>(), Is.SameAs(veil));
                Assert.That(veil.rectTransform.anchoredPosition, Is.EqualTo(authoredPosition));
                Assert.That(runtime.Find("TraitBackdropAtmosphere").GetComponent<Image>(), Is.SameAs(atmosphere));
                talent.onClick.Invoke();
                yield return null;
                app.ShowSettings();
                yield return null;
                Assert.That(top.gameObject.activeSelf, Is.False);
                Assert.That(bottom.gameObject.activeSelf, Is.False);
                Assert.That(map.gameObject.activeSelf, Is.False);
                app.ShowTrait();
                yield return null;
                Assert.That(top.gameObject.activeInHierarchy, Is.True);
                Assert.That(bottom.gameObject.activeInHierarchy, Is.True);
                Assert.That(talent.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));
                Assert.That(runtime.GetComponentsInChildren<Transform>(false).Any(t => t.name == "TalentCollectionPanel"), Is.True);
                research.onClick.Invoke();
                yield return null;
                Assert.That(map.gameObject.activeInHierarchy, Is.True);
                Assert.That(runtime.gameObject.activeSelf, Is.False);
                Assert.That(research.image.sprite.name, Is.EqualTo("Btn_MenuButton_Rectangle00_s"));
                Assert.That(map.nodes[0].rank, Is.EqualTo(1));
                Assert.That(progression.GetResearchGemstoneBalance(0), Is.EqualTo(balance));
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
