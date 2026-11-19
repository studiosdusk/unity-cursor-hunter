using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CursorHunter.Contracts;
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
    public sealed class TraitCardCanvasPlayModeTests
    {
        private const string Key = "cursor_hunter.progression.v1";

        [UnityTest]
        public IEnumerator RevealedTreasureChestShowsConfiguredChanceInBothLanguages()
        {
#if UNITY_EDITOR
            const string chestId = "trait_card.treasure_chest";
            string[] keys = { Key, Key + ".before-v6", Key + ".before-v12" };
            var previous = keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            for (int frame = 0; frame < 180 && LocalizationSettings.AvailableLocales.GetLocale("ko") == null; frame++) yield return null;
            var originalLocale = LocalizationSettings.SelectedLocale;
            Scene scene = default;
            CursorHunter.Data.TraitCardDefinition chest = null;
            float originalChance = 0;
            try
            {
                foreach (string key in keys) PlayerPrefs.DeleteKey(key);
                Assert.That(TraitProgressionStore.Save(25, new long[] { 25 }, new string[0], null, null,
                    newTraitCards: new TraitCardProgressData { revealed = true,
                        candidateIds = new List<string> { chestId, "trait_card.lucky_settlement", "trait_card.expanding_focus" } }), Is.True);
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("ko");
                scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null; yield return null;
                var ui = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                var progression = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TraitScreenController>(true)).Single();
                chest = progression.GetTraitCard(chestId);
                originalChance = chest.gameplay.treasureChest.chancePercent;
                chest.gameplay.treasureChest.chancePercent = 27.5f;
                ui.GetComponent<ProgressionTopMenu>().ShowTalents(); yield return null;
                var slot = ui.GetComponentInChildren<TraitCardCanvas>(true).GetComponentsInChildren<TraitCardSlot>(true)
                    .Single(card => card.CardId == chestId);
                var koTable = LocalizationSettings.StringDatabase.GetTable("TraitCard", LocalizationSettings.SelectedLocale);
                string koTitle = koTable.GetEntry(chestId + ".title").Value;
                for (int frame = 0; frame < 180 && (slot.DisplayedTitle != koTitle || !slot.DisplayedDescription.Contains("27.50%")); frame++) yield return null;
                Assert.That(slot.DisplayedTitle, Is.EqualTo(koTitle));
                Assert.That(slot.DisplayedDescription, Does.Contain("27.50%"));
                Assert.That(slot.DisplayedDescription, Does.Not.Contain("{chance}"));

                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en");
                var enTable = LocalizationSettings.StringDatabase.GetTable("TraitCard", LocalizationSettings.SelectedLocale);
                string enTitle = enTable.GetEntry(chestId + ".title").Value;
                string enDescription = TraitCardTextFormatter.Format(enTable.GetEntry(chestId + ".description").Value, chest.gameplay);
                for (int frame = 0; frame < 180 && (slot.DisplayedTitle != enTitle ||
                    Regex.Replace(slot.DisplayedDescription, "</?nobr>", "") != enDescription); frame++) yield return null;
                Assert.That(slot.DisplayedTitle, Is.EqualTo(enTitle));
                Assert.That(Regex.Replace(slot.DisplayedDescription, "</?nobr>", ""), Is.EqualTo(enDescription));
                Assert.That(slot.DisplayedDescription, Does.Contain("27.50%"));
                Assert.That(slot.DisplayedDescription, Does.Not.Contain("{count}"));
            }
            finally
            {
                if (chest != null) chest.gameplay.treasureChest.chancePercent = originalChance;
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
        public IEnumerator LockedCardsRespondToMouseHoverWithoutBeingSelected()
        {
#if UNITY_EDITOR
            string[] keys = { Key, Key + ".before-v6", Key + ".before-v12" };
            var previous = keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
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
                    newTraitCards: new TraitCardProgressData { points = 1 }), Is.True);
                scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null; yield return null;
                var ui = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                var progression = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TraitScreenController>(true)).Single();
                ui.GetComponent<ProgressionTopMenu>().ShowTalents(); yield return null;
                var canvas = ui.GetComponentInChildren<TraitCardCanvas>(true);
                var slots = canvas.GetComponentsInChildren<TraitCardSlot>(true);
                Time.timeScale = 0;
                Canvas.ForceUpdateCanvases();

                foreach (var slot in slots)
                {
                    Vector3 restingScale = slot.transform.localScale;
                    Vector2 position = RectTransformUtility.WorldToScreenPoint(null, slot.transform.position);
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
                    yield return new WaitForSecondsRealtime(.2f);
                    Assert.That(slot.transform.localScale.x, Is.EqualTo(restingScale.x * 1.06f).Within(.001f));
                    Assert.That(slot.IsLocked, Is.True);
                    Assert.That(slot.IsSelectable, Is.False);
                    Assert.That(slot.transform.Find("Text_ListTitle").gameObject.activeSelf, Is.False);
                    Assert.That(slot.GetComponentInChildren<RawImage>(true).enabled, Is.False,
                        "Hover must not replay the card reveal effect.");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(0, 0) });
                    yield return new WaitForSecondsRealtime(.2f);
                    Assert.That(Vector3.Distance(slot.transform.localScale, restingScale), Is.LessThan(.001f));
                }

                var hovered = slots[0];
                Vector3 originalScale = hovered.transform.localScale;
                Vector2 hoveredPosition = RectTransformUtility.WorldToScreenPoint(null, hovered.transform.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = hoveredPosition });
                yield return new WaitForSecondsRealtime(.2f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = hoveredPosition, buttons = 1 });
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = hoveredPosition });
                yield return null;
                Assert.That(progression.TraitCards.Level, Is.Zero);
                Assert.That(progression.TraitCards.Points, Is.EqualTo(1));
                Assert.That(progression.TraitCards.Revealed, Is.False);
                Assert.That(progression.TraitCards.OwnedIds, Is.Empty);

                canvas.RevealCards();
                Assert.That(canvas.IsAnimating, Is.True);
                Assert.That(Vector3.Distance(hovered.transform.localScale, originalScale), Is.LessThan(.001f));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(0, 0) });
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = hoveredPosition });
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(Vector3.Distance(hovered.transform.localScale, originalScale), Is.LessThan(.001f),
                    "Hover is disabled while the cards are being revealed.");
                yield return new WaitForSecondsRealtime(.6f);
                Assert.That(slots.All(slot => slot.IsSelectable), Is.True);
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
        public IEnumerator DraftPersistsAndSelectionFliesIntoOwnedListWithoutControlsBeingCreated()
        {
#if UNITY_EDITOR
            string[] keys = { Key, Key + ".before-v6", Key + ".before-v12" };
            var previous = keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            foreach (string key in keys) PlayerPrefs.DeleteKey(key);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            float oldTimeScale = Time.timeScale;
            try
            {
                yield return null; yield return null;
                var ui = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                var progression = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TraitScreenController>(true)).Single();
                var canvas = ui.GetComponentInChildren<TraitCardCanvas>(true);
                var menu = ui.GetComponent<ProgressionTopMenu>();
                menu.ShowTalents(); yield return null;
                var slots = canvas.GetComponentsInChildren<TraitCardSlot>(true);
                Assert.That(slots.Length, Is.EqualTo(3));
                Assert.That(slots.All(slot => slot.IsLocked && !slot.IsSelectable), Is.True);
                var revealButton = canvas.transform.Find("TraitCard_OpenButton").GetComponent<Button>();
                var buttonText = revealButton.transform.Find("Text").GetComponent<TMP_Text>();
                var costView = revealButton.transform.Find("RevealCostView").gameObject;
                var costHeading = costView.transform.Find("RevealCostHeading").GetComponent<TMP_Text>();
                var costRow = costView.transform.Find("RevealCostRow");
                var costValue = costRow.Find("RevealCostValue").GetComponent<TMP_Text>();
                var costIcon = costRow.Find("TraitPointIcon").GetComponent<Image>();
                var pointsText = canvas.transform.Find("TraitPointView").GetComponentInChildren<TMP_Text>();
                var levelText = canvas.transform.Find("TraitLevel_Text").GetComponent<TMP_Text>();
                Assert.That(canvas.GetComponentsInChildren<Button>(true), Is.EqualTo(new[] { revealButton }),
                    "Use the authored reveal control without generating extra buttons.");
                Assert.That(revealButton.interactable, Is.False);
                Assert.That(costView.activeSelf, Is.True);
                Assert.That(buttonText.gameObject.activeSelf, Is.False);
                Assert.That(costHeading.text, Is.EqualTo("특성 카드 공개"));
                Assert.That(costValue.text, Is.EqualTo("1"));
                Assert.That((Color32)costHeading.color, Is.EqualTo(new Color32(255, 120, 100, 255)));
                Assert.That((Color32)costValue.color, Is.EqualTo(new Color32(255, 120, 100, 255)));
                Assert.That(costIcon.sprite, Is.EqualTo(UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/DownLoadAssets/IconPack/2D Minimal-IconPack/Icons/128/Item_Book_01_Purple.png")));
                Assert.That(costIcon.raycastTarget, Is.False);
                Assert.That(pointsText.text, Is.EqualTo("0"));
                Assert.That(levelText.text, Is.EqualTo("특성 레벨 : 0"));
                Assert.That(canvas.transform.Find("IconLayout").Cast<Transform>().All(t => !t.gameObject.activeSelf), Is.True);
                canvas.RevealCards(); Assert.That(canvas.IsAnimating, Is.False);

                var request = new RunRequest("trait-card-test-" + Guid.NewGuid(), 20);
                var result = new RunResult(request, RunEndReason.TimeExpired, RunSettlementPolicy.Eligible,
                    20, 10, 0, 0, null, null, ProgressionLevelCurve.TotalExperienceForLevel(11));
                Assert.That(progression.GrantSettledRun(result), Is.True);
                Assert.That(progression.TraitCards.Points, Is.EqualTo(10));
                Assert.That(progression.GrantSettledRun(result), Is.True);
                Assert.That(progression.TraitCards.Points, Is.EqualTo(10), "Duplicate settlement must not grant levels again.");
                Assert.That(pointsText.text, Is.EqualTo("10"));
                Assert.That(revealButton.interactable, Is.True);
                Assert.That((Color32)costHeading.color, Is.EqualTo(new Color32(184, 245, 138, 255)));
                Assert.That((Color32)costValue.color, Is.EqualTo(new Color32(184, 245, 138, 255)));

                Time.timeScale = 0; // UI uses unscaled time.
                var eventSystem = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Single();
                Canvas.ForceUpdateCanvases();
                var buttonPointer = new PointerEventData(eventSystem) {
                    position = RectTransformUtility.WorldToScreenPoint(null, revealButton.transform.position) };
                var buttonHits = new List<RaycastResult>();
                eventSystem.RaycastAll(buttonPointer, buttonHits);
                Assert.That(buttonHits, Is.Not.Empty);
                Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(buttonHits[0].gameObject),
                    Is.EqualTo(revealButton.gameObject), "The authored button must receive pointer clicks.");
                revealButton.onClick.Invoke(); revealButton.onClick.Invoke();
                Assert.That(costView.activeSelf, Is.False);
                Assert.That(buttonText.gameObject.activeSelf, Is.True);
                Assert.That(buttonText.text, Is.EqualTo("공개 중…"));
                Assert.That(progression.TraitCards.Points, Is.EqualTo(9));
                Assert.That(pointsText.text, Is.EqualTo("9"));
                Assert.That(revealButton.interactable, Is.False);
                Assert.That(progression.TraitCards.Level, Is.Zero);
                Assert.That(slots.All(slot => !slot.IsSelectable), Is.True);
                Assert.That(slots.All(slot => slot.GetComponentInChildren<RawImage>(true).enabled), Is.True,
                    "All three cards flash together while revealing.");
                string[] draft = progression.TraitCards.CandidateIds.ToArray();
                menu.ShowResearch(); menu.ShowTalents(); yield return null;
                Assert.That(canvas.IsAnimating, Is.False);
                Assert.That(progression.TraitCards.CandidateIds, Is.EqualTo(draft));
                Assert.That(slots.All(slot => !slot.IsLocked && slot.IsSelectable), Is.True);
                float deadline = Time.realtimeSinceStartup + 5;
                while (slots.Any(slot => string.IsNullOrEmpty(slot.DisplayedTitle)) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(slots.All(slot => !string.IsNullOrEmpty(slot.DisplayedTitle) && !slot.DisplayedDescription.Contains("{")), Is.True);

                var hovered = slots[0];
                Vector3 originalScale = hovered.transform.localScale;
                hovered.OnPointerEnter(new PointerEventData(eventSystem));
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(hovered.transform.localScale.x, Is.GreaterThan(originalScale.x));
                Assert.That(hovered.GetComponentInChildren<RawImage>(true).enabled, Is.False,
                    "Hover only scales the card; it must not replay the flash.");
                hovered.OnPointerExit(new PointerEventData(eventSystem));
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(Vector3.Distance(hovered.transform.localScale, originalScale), Is.LessThan(.001f));

                var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
                for (int i = 0; i < 4; i++)
                {
                    if (i > 0)
                    {
                        Assert.That(costView.activeSelf, Is.True);
                        Assert.That(costValue.text, Is.EqualTo((i + 1).ToString()));
                        revealButton.onClick.Invoke();
                        yield return new WaitForSecondsRealtime(.8f);
                    }
                    var selected = slots.First(slot => slot.IsSelectable);
                    string id = selected.CardId;
                    Canvas.ForceUpdateCanvases();
                    var rootCanvas = ui.GetComponent<Canvas>();
                    pointer.position = RectTransformUtility.WorldToScreenPoint(
                        rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera,
                        selected.transform.position);
                    var hits = new List<RaycastResult>();
                    eventSystem.RaycastAll(pointer, hits);
                    Assert.That(hits, Is.Not.Empty);
                    Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),
                        Is.EqualTo(selected.gameObject), "The authored card must receive a real pointer hit.");
                    ExecuteEvents.Execute(selected.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    ExecuteEvents.Execute(selected.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    Assert.That(progression.TraitCards.Level, Is.EqualTo(i + 1));
                    Assert.That(levelText.text, Is.EqualTo("특성 레벨 : " + (i + 1)));
                    Assert.That(progression.TraitCards.OwnedIds, Does.Contain(id));
                    Assert.That(canvas.transform.Find("IconLayout/Owned_" + id), Is.Not.Null);
                    Assert.That(canvas.transform.Find("TraitIconFlight").gameObject.activeSelf, Is.True);
                    Assert.That(selected.GetComponentInChildren<RawImage>(true).enabled, Is.True,
                        "Selection flashes before the icon travels to the collection.");
                    if (i == 0)
                    {
                        menu.ShowResearch(); menu.ShowTalents(); yield return null;
                        Assert.That(canvas.transform.Find("TraitIconFlight").gameObject.activeSelf, Is.False);
                    }
                    else yield return new WaitForSecondsRealtime(.7f);
                    Assert.That(slots.Count(slot => slot.CardId != null), Is.EqualTo(Math.Min(3, 3 - i)));
                }
                Assert.That(progression.TraitCards.Complete, Is.True);
                Assert.That(progression.TraitCards.Points, Is.Zero);
                Assert.That(revealButton.interactable, Is.False);
                Assert.That(buttonText.text, Is.EqualTo("모든 특성 획득"));
                Assert.That(costView.activeSelf, Is.False);
                canvas.RevealCards(); Assert.That(canvas.IsAnimating, Is.False);
                Assert.That(TraitProgressionStore.TryLoad(out var saved), Is.True);
                Assert.That(saved.newTraitCards.ownedIds.Count, Is.EqualTo(4));
                Assert.That(saved.newTraitCards.candidateIds, Is.Empty);
                Assert.That(saved.newTraitCards.points, Is.Zero);
                Assert.That(saved.settledBossRunIds, Does.Contain(result.RunId.Value), "Run receipt must survive a restart.");
                Assert.That(saved.ownedTraitCardIds, Is.Empty, "New cards must not overwrite legacy card IDs.");
                // Recreate the new service from saved DTO, as on the next launch.
                var reloaded = new TraitCardProgress(saved.newTraitCards.ownedIds, saved.newTraitCards, _ => 0, _ => true);
                Assert.That(reloaded.Complete, Is.True);
            }
            finally
            {
                Time.timeScale = oldTimeScale;
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
        public IEnumerator CostAndColorsFollowAffordabilityAfterBuyingACard()
        {
#if UNITY_EDITOR
            string[] keys = { Key, Key + ".before-v6" };
            var previous = keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            Scene scene = default;
            try
            {
                foreach (string key in keys) PlayerPrefs.DeleteKey(key);
                Assert.That(TraitProgressionStore.Save(25, new long[] { 25 }, new string[0], null, null,
                    newTraitCards: new TraitCardProgressData { points = 1 }), Is.True);
                scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null; yield return null;
                var ui = scene.GetRootGameObjects().Single(root => root.name == "UICanvas");
                ui.GetComponent<ProgressionTopMenu>().ShowTalents(); yield return null;
                var canvas = ui.GetComponentInChildren<TraitCardCanvas>(true);
                var progression = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TraitScreenController>(true)).Single();
                var button = canvas.transform.Find("TraitCard_OpenButton").GetComponent<Button>();
                var costView = button.transform.Find("RevealCostView");
                var heading = costView.Find("RevealCostHeading").GetComponent<TMP_Text>();
                var value = costView.Find("RevealCostRow/RevealCostValue").GetComponent<TMP_Text>();
                Assert.That(value.text, Is.EqualTo("1"));
                Assert.That(button.interactable, Is.True);
                Assert.That((Color32)heading.color, Is.EqualTo(new Color32(184, 245, 138, 255)));
                Assert.That((Color32)value.color, Is.EqualTo(new Color32(184, 245, 138, 255)));

                button.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.8f);
                Assert.That(costView.gameObject.activeSelf, Is.False);
                Assert.That(button.transform.Find("Text").GetComponent<TMP_Text>().text, Is.EqualTo("카드를 선택하세요"));
                var selected = canvas.GetComponentsInChildren<TraitCardSlot>(true).First(slot => slot.IsSelectable);
                var eventSystem = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Single();
                selected.OnPointerClick(new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left });
                yield return new WaitForSecondsRealtime(.7f);
                Assert.That(costView.gameObject.activeSelf, Is.True);
                Assert.That(value.text, Is.EqualTo("2"));
                Assert.That(button.interactable, Is.False);
                Assert.That((Color32)heading.color, Is.EqualTo(new Color32(255, 120, 100, 255)));
                Assert.That((Color32)value.color, Is.EqualTo(new Color32(255, 120, 100, 255)));

                Assert.That(progression.GrantRunRewards(null, null, ProgressionLevelCurve.TotalExperienceForLevel(3)), Is.True);
                Assert.That(progression.TraitCards.Points, Is.EqualTo(2));
                Assert.That(button.interactable, Is.True, "Exactly enough points enables reveal.");
                Assert.That((Color32)heading.color, Is.EqualTo(new Color32(184, 245, 138, 255)));
                Assert.That((Color32)value.color, Is.EqualTo(new Color32(184, 245, 138, 255)));
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
    }
}
