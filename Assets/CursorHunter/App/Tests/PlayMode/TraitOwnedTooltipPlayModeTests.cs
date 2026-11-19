using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CursorHunter.Data;
using CursorHunter.Progression;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CursorHunter.App.Tests
{
    public sealed class TraitOwnedTooltipPlayModeTests
    {
        private const string Key = "cursor_hunter.progression.v1";
        private static readonly string[] Ids = { "trait_card.hunting_wisdom", "trait_card.lucky_settlement",
            "trait_card.treasure_chest", "trait_card.expanding_focus" };

        [UnityTest]
        public IEnumerator OwnedIconsShowLocalizedSoEffectsAndTooltipNeverBlocksInput()
        {
#if UNITY_EDITOR
            string[] keys = { Key, Key + ".before-v6" };
            var previous = keys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
            Scene scene = default;
            float previousTimeScale = Time.timeScale;
            for (int frame = 0; frame < 180 && LocalizationSettings.AvailableLocales.GetLocale("ko") == null; frame++)
                yield return null;
            var originalLocale = LocalizationSettings.SelectedLocale;
            try
            {
                foreach (string key in keys) PlayerPrefs.DeleteKey(key);
                Assert.That(TraitProgressionStore.Save(25, new long[] { 25 }, new string[0], null, null,
                    newTraitCards: new TraitCardProgressData { ownedIds = new List<string>(Ids) }), Is.True);
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("ko");
                scene = LoadMain(); yield return null; yield return null;
                var ui = Find<ProgressionTopMenu>(scene);
                ui.ShowTalents(); yield return null;
                var canvas = Find<TraitCardCanvas>(scene);
                var events = Find<EventSystem>(scene);
                var tooltip = canvas.transform.Find("TraitOwnedTooltip");
                var panel = (RectTransform)tooltip.Find("Popup");
                var title = tooltip.Find("Popup/TopFrame_Dark/ResearchTitle").GetComponent<TMP_Text>();
                var description = tooltip.Find("Popup/EffectDescription").GetComponent<TMP_Text>();
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
                var icons = Ids.Select(id => canvas.transform.Find("IconLayout/Owned_" + id)
                    .GetComponent<TraitCardOwnedIcon>()).ToArray();
                Assert.That(icons.All(x => x != null), Is.True);
                var koTable = LocalizationSettings.StringDatabase.GetTable("TraitCard", LocalizationSettings.SelectedLocale);
                string[] titles = Ids.Select(id => koTable.GetEntry(id + ".title").Value).ToArray();
                string[] effects = { "10.00%", "5배", "1개", "50.00%" };
                Time.timeScale = 0;
                for (int i = 0; i < icons.Length; i++)
                {
                    Canvas.ForceUpdateCanvases();
                    var pointer = new PointerEventData(events) {
                        position = RectTransformUtility.WorldToScreenPoint(null, icons[i].transform.position) };
                    var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
                    Assert.That(hits, Is.Not.Empty, icons[i].CardId);
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerEnterHandler>(hits[0].gameObject),
                        Is.EqualTo(icons[i].gameObject), "Owned icon must be reachable by a real UI raycast.");
                    ExecuteEvents.Execute(icons[i].gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                    Assert.That(tooltip.gameObject.activeSelf, Is.True);
                    for (int frame = 0; frame < 180 && (title.text != titles[i] || !description.text.Contains(effects[i])); frame++)
                        yield return null;
                    Assert.That(title.text, Is.EqualTo(titles[i]));
                    Assert.That(Plain(description.text), Does.Contain(effects[i]));
                    Assert.That(description.text, Does.Not.Contain("{"));
                    yield return new WaitForSecondsRealtime(.4f);
                    Canvas.ForceUpdateCanvases();
                    description.ForceMeshUpdate();
                    Assert.That(description.isTextOverflowing, Is.False, icons[i].CardId);
                    Assert.That(description.fontSize, Is.GreaterThanOrEqualTo(18));
                    AssertPanelInsideCanvas(canvas.transform as RectTransform, panel);
                    var tooltipPointer = new PointerEventData(events) {
                        position = RectTransformUtility.WorldToScreenPoint(null, panel.position) };
                    var tooltipHits = new List<RaycastResult>(); events.RaycastAll(tooltipPointer, tooltipHits);
                    Assert.That(tooltipHits.Any(hit => hit.gameObject.transform.IsChildOf(tooltip)), Is.False,
                        "Tooltip must not intercept clicks or hover events.");
                }
                // An old pointer-exit event must not dismiss the newly hovered icon.
                icons[0].OnPointerExit(new PointerEventData(events));
                Assert.That(tooltip.gameObject.activeSelf, Is.True);
                icons[3].OnPointerExit(new PointerEventData(events));
                Assert.That(tooltip.gameObject.activeSelf, Is.False);

                var hunting = icons[0];
                hunting.OnPointerEnter(new PointerEventData(events));
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en");
                var enTable = LocalizationSettings.StringDatabase.GetTable("TraitCard", LocalizationSettings.SelectedLocale);
                string[] englishTitles = Ids.Select(id => enTable.GetEntry(id + ".title").Value).ToArray();
                for (int frame = 0; frame < 180 && title.text != englishTitles[0]; frame++) yield return null;
                Assert.That(title.text, Is.EqualTo(englishTitles[0]));
                for (int frame = 0; frame < 180 && !description.text.Contains("experience"); frame++) yield return null;
                Assert.That(Plain(description.text), Does.Contain("10.00%"));
                Assert.That(Plain(description.text), Does.Contain("normal monsters"));
                for (int i = 0; i < icons.Length; i++)
                {
                    icons[i].OnPointerEnter(new PointerEventData(events));
                    for (int frame = 0; frame < 180 && title.text != englishTitles[i]; frame++) yield return null;
                    Assert.That(title.text, Is.EqualTo(englishTitles[i]));
                    for (int frame = 0; frame < 180 && (string.IsNullOrEmpty(description.text) || description.text.Contains("{")); frame++)
                        yield return null;
                    Canvas.ForceUpdateCanvases();
                    description.ForceMeshUpdate();
                    Assert.That(description.isTextOverflowing, Is.False, icons[i].CardId + " in English");
                    Assert.That(description.fontSize, Is.GreaterThanOrEqualTo(18));
                }

                // Tooltip values come from the SO rather than a hard-coded description.
                TraitCardDefinition definition = Find<TraitScreenController>(scene).GetTraitCard(Ids[0]);
                float originalBonus = definition.gameplay.normalMonsterExperience.bonusPercent;
                try
                {
                    definition.gameplay.normalMonsterExperience.bonusPercent = 12.5f;
                    hunting.OnPointerExit(new PointerEventData(events));
                    hunting.OnPointerEnter(new PointerEventData(events));
                    for (int frame = 0; frame < 180 && !description.text.Contains("12.50%"); frame++) yield return null;
                    Assert.That(Plain(description.text), Does.Contain("12.50%"));
                }
                finally { definition.gameplay.normalMonsterExperience.bonusPercent = originalBonus; }

                var chestDefinition = Find<TraitScreenController>(scene).GetTraitCard(Ids[2]);
                float originalChance = chestDefinition.gameplay.treasureChest.chancePercent;
                try
                {
                    chestDefinition.gameplay.treasureChest.chancePercent = 37.5f;
                    icons[2].OnPointerEnter(new PointerEventData(events));
                    for (int frame = 0; frame < 180 && !description.text.Contains("37.50%"); frame++) yield return null;
                    Assert.That(Plain(description.text), Does.Contain("37.50%"));
                    Assert.That(Plain(description.text), Does.Contain("1 treasure chest"));
                }
                finally { chestDefinition.gameplay.treasureChest.chancePercent = originalChance; }

                // The tooltip flips to the left when an anchor is near the top-right edge.
                var probeObject = new GameObject("TooltipEdgeProbe", typeof(RectTransform));
                try
                {
                    var probe = (RectTransform)probeObject.transform;
                    probe.SetParent(canvas.transform, false);
                    probe.anchorMin = probe.anchorMax = probe.pivot = Vector2.one;
                    probe.anchoredPosition = new Vector2(-10, -10);
                    probe.sizeDelta = new Vector2(50, 50);
                    canvas.GetComponent<TraitCardTooltip>().Show(definition, probe);
                    yield return new WaitForSecondsRealtime(.4f);
                    Canvas.ForceUpdateCanvases();
                    AssertPanelInsideCanvas(canvas.transform as RectTransform, panel);
                    Assert.That(panel.position.x, Is.LessThan(probe.position.x));
                }
                finally { UnityEngine.Object.Destroy(probeObject); }
                ui.ShowResearch();
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
                ui.ShowTalents(); yield return null;
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                LocalizationSettings.SelectedLocale = originalLocale;
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                for (int i = 0; i < keys.Length; i++)
                    if (previous[i] == null) PlayerPrefs.DeleteKey(keys[i]); else PlayerPrefs.SetString(keys[i], previous[i]);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator RealMouseMovementOpensAndClosesTheOwnedTooltip()
        {
#if UNITY_EDITOR
            string[] keys = { Key, Key + ".before-v6" };
            var previous = keys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>();
            Scene scene = default;
            float oldTimeScale = Time.timeScale;
            try
            {
                foreach (string key in keys) PlayerPrefs.DeleteKey(key);
                Assert.That(TraitProgressionStore.Save(25, new long[] { 25 }, new string[0], null, null,
                    newTraitCards: new TraitCardProgressData { ownedIds = new List<string> { Ids[0] } }), Is.True);
                scene = LoadMain(); yield return null; yield return null;
                var menu = Find<ProgressionTopMenu>(scene);
                menu.ShowTalents(); yield return null;
                var canvas = Find<TraitCardCanvas>(scene);
                var icon = canvas.transform.Find("IconLayout/Owned_" + Ids[0]);
                var tooltip = canvas.transform.Find("TraitOwnedTooltip");
                Vector3 baseScale = icon.localScale;
                Time.timeScale = 0;
                Canvas.ForceUpdateCanvases();
                Vector2 position = RectTransformUtility.WorldToScreenPoint(null, icon.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(tooltip.gameObject.activeSelf, Is.True, "Moving the real pointer over the icon opens its tooltip.");
                Assert.That(icon.localScale.x, Is.EqualTo(baseScale.x * 1.08f).Within(.001f),
                    "The owned icon grows while its existing tooltip is visible.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width * .5f, Screen.height * .5f) });
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(tooltip.gameObject.activeSelf, Is.False, "Leaving the icon closes the tooltip.");
                Assert.That(Vector3.Distance(icon.localScale, baseScale), Is.LessThan(.001f));

                InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(icon.localScale.x, Is.GreaterThan(baseScale.x));
                menu.ShowResearch();
                Assert.That(Vector3.Distance(icon.localScale, baseScale), Is.LessThan(.001f),
                    "Leaving the screen stops the tween and restores the authored scale.");
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width * .5f, Screen.height * .5f) });
                yield return null;
                menu.ShowTalents(); yield return null;
                Assert.That(Vector3.Distance(icon.localScale, baseScale), Is.LessThan(.001f));
            }
            finally
            {
                Time.timeScale = oldTimeScale;
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
                for (int i = 0; i < keys.Length; i++)
                    if (previous[i] == null) PlayerPrefs.DeleteKey(keys[i]); else PlayerPrefs.SetString(keys[i], previous[i]);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }

        [UnityTest]
        public IEnumerator ArrivalAndResetNeverLeaveAnObsoleteTooltip()
        {
#if UNITY_EDITOR
            string[] keys = { Key, Key + ".before-v6" };
            var previous = keys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
            Scene scene = default;
            try
            {
                foreach (string key in keys) PlayerPrefs.DeleteKey(key);
                Assert.That(TraitProgressionStore.Save(25, new long[] { 25 }, new string[0], null, null,
                    newTraitCards: new TraitCardProgressData { points = 1 }), Is.True);
                scene = LoadMain(); yield return null; yield return null;
                Find<ProgressionTopMenu>(scene).ShowTalents(); yield return null;
                var canvas = Find<TraitCardCanvas>(scene);
                var events = Find<EventSystem>(scene);
                var tooltip = canvas.transform.Find("TraitOwnedTooltip");
                canvas.RevealCards(); yield return new WaitForSecondsRealtime(.8f);
                var selected = canvas.GetComponentsInChildren<TraitCardSlot>(true).First(slot => slot.IsSelectable);
                string id = selected.CardId;
                selected.OnPointerClick(new PointerEventData(events) { button = PointerEventData.InputButton.Left });
                var icon = canvas.transform.Find("IconLayout/Owned_" + id).GetComponent<TraitCardOwnedIcon>();
                Vector3 baseScale = icon.transform.localScale;
                Assert.That(canvas.IsAnimating, Is.True);
                Assert.That(icon.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                icon.OnPointerEnter(new PointerEventData(events));
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(Vector3.Distance(icon.transform.localScale, baseScale), Is.LessThan(.001f),
                    "The arriving icon cannot enlarge before the flight completes.");
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(icon.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
                icon.OnPointerEnter(new PointerEventData(events));
                Assert.That(tooltip.gameObject.activeSelf, Is.True);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(icon.transform.localScale.x, Is.EqualTo(baseScale.x * 1.08f).Within(.001f));
                Find<HuntManager>(scene).ResetTraitCardProgress();
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
                Assert.That(icon.gameObject.activeSelf, Is.False);
                Assert.That(Vector3.Distance(icon.transform.localScale, baseScale), Is.LessThan(.001f));
                yield return null;
                Assert.That(tooltip.gameObject.activeSelf, Is.False);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                for (int i = 0; i < keys.Length; i++)
                    if (previous[i] == null) PlayerPrefs.DeleteKey(keys[i]); else PlayerPrefs.SetString(keys[i], previous[i]);
                PlayerPrefs.Save();
            }
#else
            yield return null;
#endif
        }

        private static string Plain(string value) => Regex.Replace(value, "<[^>]+>", "");
        private static void AssertPanelInsideCanvas(RectTransform canvas, RectTransform panel)
        {
            var corners = new Vector3[4]; panel.GetWorldCorners(corners);
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 point = canvas.InverseTransformPoint(corners[i]);
                Assert.That(point.x, Is.InRange(canvas.rect.xMin, canvas.rect.xMax));
                Assert.That(point.y, Is.InRange(canvas.rect.yMin, canvas.rect.yMax));
            }
        }
#if UNITY_EDITOR
        private static Scene LoadMain() => UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
#endif
    }
}
