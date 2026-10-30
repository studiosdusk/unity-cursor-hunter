using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Progression;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CursorHunter.App.Tests
{
    public sealed class TraitTooltipPlayModeTests
    {
        private const string ProfileKey = "cursor_hunter.progression.v1";
        private readonly Dictionary<string, string> _savedStrings = new Dictionary<string, string>();
        private readonly Dictionary<string, int?> _savedFlags = new Dictionary<string, int?>();
        private Scene _scene;
        private AppUiRootController _app;
        private TraitScreenController _trait;
        private EventSystem _events;
        private float _listenerVolume;

        [UnitySetUp]
        public IEnumerator LoadNewProfile()
        {
            _listenerVolume = AudioListener.volume;
            AudioListener.volume = 0f;
            foreach (string key in new[]
                { ProfileKey, ProfileKey + ".before-v5", ProfileKey + ".before-v6" })
            {
                _savedStrings[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
                PlayerPrefs.DeleteKey(key);
            }
            foreach (string key in new[]
                { ProfileKey + ".starter-tutorial-complete", ProfileKey + ".starter-tutorial-started" })
            {
                _savedFlags[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
                PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
#if UNITY_EDITOR
            _scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            yield return null;
            _app = FindInScene<AppUiRootController>();
            _trait = FindInScene<TraitScreenController>();
            _events = FindInScene<EventSystem>();
            Assert.That(_app.CurrentScreen, Is.EqualTo(UiScreenId.MainMenu));
            _app.ShowTrait();
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator RestoreProfile()
        {
            if (_scene.IsValid() && _scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_scene);
            foreach (var entry in _savedStrings)
            {
                if (entry.Value == null) PlayerPrefs.DeleteKey(entry.Key);
                else PlayerPrefs.SetString(entry.Key, entry.Value);
            }
            foreach (var entry in _savedFlags)
            {
                if (!entry.Value.HasValue) PlayerPrefs.DeleteKey(entry.Key);
                else PlayerPrefs.SetInt(entry.Key, entry.Value.Value);
            }
            PlayerPrefs.Save();
            AudioListener.volume = _listenerVolume;
        }

        [UnityTest]
        public IEnumerator TraitBackButtonUsesTheFieldBackButtonPrefabGeometryAndArtwork()
        {
            yield return null;
            RectTransform runtimeRoot = Get<RectTransform>(_trait, "_runtimeRoot");
            Button traitBack = runtimeRoot.Find("BackButton").GetComponent<Button>();
            Button fieldBack = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Button>(true))
                .FirstOrDefault(button => button.gameObject.name == "Button_Side01_Demo");

            Assert.That(fieldBack, Is.Not.Null);
            Assert.That(traitBack, Is.Not.Null);
            Assert.That(traitBack.GetComponent<Image>().sprite,
                Is.SameAs(fieldBack.GetComponent<Image>().sprite));

            RectTransform fieldRect = fieldBack.transform as RectTransform;
            RectTransform traitRect = traitBack.transform as RectTransform;
            Assert.That(traitRect.anchorMin, Is.EqualTo(fieldRect.anchorMin));
            Assert.That(traitRect.anchorMax, Is.EqualTo(fieldRect.anchorMax));
            Assert.That(traitRect.pivot, Is.EqualTo(fieldRect.pivot));
            Assert.That(traitRect.sizeDelta, Is.EqualTo(fieldRect.sizeDelta));
            Assert.That(traitRect.anchoredPosition.x,
                Is.EqualTo(fieldRect.anchoredPosition.x).Within(0.01f));
            Assert.That(traitRect.anchoredPosition.y,
                Is.EqualTo(fieldRect.anchoredPosition.y).Within(0.01f));

            Image traitIcon = traitBack.transform.Find("Icon").GetComponent<Image>();
            Image fieldIcon = fieldBack.transform.Find("Icon").GetComponent<Image>();
            Assert.That(traitIcon.sprite, Is.SameAs(fieldIcon.sprite));
            RectTransform traitIconRect = traitIcon.transform as RectTransform;
            RectTransform fieldIconRect = fieldIcon.transform as RectTransform;
            Assert.That(traitIconRect.sizeDelta, Is.EqualTo(fieldIconRect.sizeDelta));
            Assert.That(traitIconRect.anchoredPosition.x,
                Is.EqualTo(fieldIconRect.anchoredPosition.x).Within(0.01f));
            Assert.That(traitIconRect.anchoredPosition.y,
                Is.EqualTo(fieldIconRect.anchoredPosition.y).Within(0.01f));

            Shadow traitShadow = traitBack.GetComponent<Shadow>();
            Shadow fieldShadow = fieldBack.GetComponent<Shadow>();
            Assert.That(traitShadow.effectColor, Is.EqualTo(fieldShadow.effectColor));
            Assert.That(traitShadow.effectDistance, Is.EqualTo(fieldShadow.effectDistance));
            Assert.That(traitBack.colors.normalColor, Is.EqualTo(fieldBack.colors.normalColor));
            Assert.That(traitBack.colors.highlightedColor, Is.EqualTo(fieldBack.colors.highlightedColor));
            Assert.That(traitBack.colors.pressedColor, Is.EqualTo(fieldBack.colors.pressedColor));

            TraitProgressionStore.CompleteStarterTutorial();
            traitBack.onClick.Invoke();
            Assert.That(_app.CurrentScreen, Is.EqualTo(UiScreenId.MainMenu));
        }

        [UnityTest]
        public IEnumerator FinalUpgradeKeepsRainbowFrameBeforeAndAfterPurchase()
        {
            Call(_trait, "ToggleUnlockAllConditionsForTesting");
            yield return null;

            RectTransform availableFinalNode = Node("stat.attack.10");
            Assert.That(availableFinalNode, Is.Not.Null);
            AssertRainbowFrame(availableFinalNode);
            Assert.That(availableFinalNode.Find("StateBadge"), Is.Null,
                "The final node is shown as available, not purchased, before purchase.");

            Call(_trait, "UpgradeAllNodesForTesting");
            yield return null;

            RectTransform purchasedFinalNode = Node("stat.attack.10");
            Assert.That(purchasedFinalNode.Find("StateBadge"), Is.Not.Null);
            AssertRainbowFrame(purchasedFinalNode);
        }

        [UnityTest]
        public IEnumerator GuideCardsStayAboveOrUpperLeftOfTheirFocusedControls()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Guide resolution/raycast validation requires a GPU; run this fixture without -nographics.");
            foreach (Vector2Int size in new[]
                { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1280, 960) })
            {
                foreach (int phase in new[] { 3, 1 })
                {
                    typeof(AppUiRootController).GetField("_starterGuidePhase",
                        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_app, phase);
                    _app.ShowMainMenu();
                    yield return CheckGuidePlacement(
                        Get<RectTransform>(_app, "_starterGuideOverlay"),
                        Get<Button>(_app, "_starterGuideButton").transform as RectTransform,
                        Get<RectTransform>(_app, "_starterGuideCard"),
                        () => typeof(AppUiRootController).GetMethod("UpdateStarterGuideSpotlight",
                            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_app, null),
                        size, "menu-" + phase);
                }
                _app.ShowTrait();
                yield return null;
                yield return CheckGuidePlacement(
                    Get<GameObject>(_trait, "_starterTutorialOverlay").transform as RectTransform,
                    Get<RectTransform>(_trait, "_starterTutorialTarget"),
                    Get<RectTransform>(_trait, "_starterTutorialCard"),
                    () => typeof(TraitScreenController).GetMethod("UpdateStarterTutorialSpotlightLayout",
                        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_trait, null),
                    size, "research");
            }
        }

        private IEnumerator CheckGuidePlacement(
            RectTransform overlay, RectTransform focus, RectTransform popup,
            System.Action refresh, Vector2Int size, string label)
        {
            Assert.That(overlay, Is.Not.Null);
            Assert.That(focus, Is.Not.Null);
            Camera camera = FindInScene<Camera>();
            Canvas canvas = overlay.GetComponentInParent<Canvas>().rootCanvas;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousDistance = canvas.planeDistance;
            RenderTexture previousTarget = camera.targetTexture;
            var render = new RenderTexture(size.x, size.y, 24);
            try
            {
                camera.targetTexture = render;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                refresh();
                foreach (TMP_Text text in popup.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();

                string directory = System.Environment.GetEnvironmentVariable("CURSOR_HUNTER_GUIDE_CAPTURE");
                if (!string.IsNullOrEmpty(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                    camera.Render();
                    RenderTexture previousActive = RenderTexture.active;
                    RenderTexture.active = render;
                    var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                    image.Apply();
                    RenderTexture.active = previousActive;
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,
                        label + "-" + size.x + "x" + size.y + ".png"), image.EncodeToPNG());
                    Object.Destroy(image);
                }

                Rect target = RelativeRect(overlay, focus);
                Rect card = RelativeRect(overlay, popup);
                Rect bounds = overlay.rect;
                bool above = card.yMin >= target.yMax + 10f &&
                    card.xMax > target.xMin && card.xMin < target.xMax;
                bool upperLeft = card.xMax <= target.xMin - 10f && card.center.y >= target.center.y;
                Assert.That(above || upperLeft, Is.True,
                    label + " " + size + ": guide must stay above/upper-left. Target=" + target + " card=" + card);
                Assert.That(card.Overlaps(target), Is.False);
                Assert.That(card.xMin, Is.GreaterThanOrEqualTo(bounds.xMin + 8f));
                Assert.That(card.xMax, Is.LessThanOrEqualTo(bounds.xMax - 8f));
                Assert.That(card.yMin, Is.GreaterThanOrEqualTo(bounds.yMin + 8f));
                Assert.That(card.yMax, Is.LessThanOrEqualTo(bounds.yMax - 8f));
                var pointer = new PointerEventData(_events)
                {
                    position = RectTransformUtility.WorldToScreenPoint(
                        camera, focus.TransformPoint(focus.rect.center))
                };
                var hits = new List<RaycastResult>();
                _events.RaycastAll(pointer, hits);
                Assert.That(hits.Count, Is.GreaterThan(0));
                Transform hit = hits[0].gameObject.transform;
                Assert.That(hit == focus || hit.IsChildOf(focus), Is.True,
                    "The full-screen spotlight must leave the guided control clickable.");
                foreach (TMP_Text text in popup.GetComponentsInChildren<TMP_Text>())
                {
                    Assert.That(text.rectTransform.rect.width, Is.GreaterThan(0f), text.name);
                    Assert.That(text.isTextOverflowing, Is.False, text.name + " must remain readable.");
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance;
                render.Release();
                Object.Destroy(render);
            }
        }

        private static Rect RelativeRect(RectTransform space, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 point = space.InverseTransformPoint(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [UnityTest]
        public IEnumerator HoverShowsDescriptionsAboveTheGuideAndKeepsPurchaseOrder()
        {
            Assert.That(Get<RectTransform>(_trait, "_nodeContent").localScale.x,
                Is.EqualTo(1.48f).Within(0.01f));
            Hover("stat.attack.01");
            AssertTooltipVisible();
            Assert.That(Get<RectTransform>(_trait, "_starterTutorialCard").gameObject.activeSelf, Is.False);
            Exit("stat.attack.01");
            yield return new WaitForSecondsRealtime(0.36f);
            Assert.That(Get<RectTransform>(_trait, "_tooltipRoot").gameObject.activeSelf, Is.False);
            Assert.That(Get<RectTransform>(_trait, "_starterTutorialCard").gameObject.activeSelf, Is.True);

            RightClick("stat.attack.01");
            yield return null;
            Hover("stat.cooldown.01"); // Visible, but radius is the current guide step.
            AssertTooltipVisible();
            Assert.That(Get<Button>(_trait, "_tooltipUpgradeButton").gameObject.activeSelf, Is.False);
            RightClick("stat.cooldown.01");
            Assert.That(Purchased.Contains("stat.cooldown.01"), Is.False);

            TraitProgressionStore.CompleteStarterTutorial();
            _trait.GuideStarterResearch();
            Hover("stat.attack.02");
            AssertTooltipVisible();
            Assert.That(Get<Button>(_trait, "_tooltipUpgradeButton").interactable, Is.True);
            string preview = Get<TextMeshProUGUI>(_trait, "_tooltipPreview").text;
            Assert.That(preview, Does.Contain("→"));
            Assert.That(preview, Does.Contain("강화 후").Or.Contain("현재"));
            yield return CaptureTooltipAtResolutions("tooltip-stat-upgrade");
        }

        [UnityTest]
        public IEnumerator FirstAttackUpgradeTeachesRightClickWithoutButtonBypasses()
        {
            foreach (string id in new[]
                { "stat.attack.01", "stat.radius.01", "stat.cooldown.01", "stat.critical.01", "stat.fieldDuration.01" })
            {
                RightClick(id);
                yield return null;
            }

            const string upgrade = "stat.attack.02";
            long balance = Get<long>(_trait, "_garnetBalance");
            Assert.That(Get<TextMeshProUGUI>(_trait, "_starterTutorialMessage").text, Does.Contain("우클릭"));
            Node(upgrade).GetComponent<Button>().onClick.Invoke();
            Get<Button>(_trait, "_starterTutorialActionButton").onClick.Invoke();
            Get<Button>(_trait, "_tooltipUpgradeButton").onClick.Invoke();
            Assert.That(Purchased.Contains(upgrade), Is.False);
            Assert.That(Get<long>(_trait, "_garnetBalance"), Is.EqualTo(balance));
            AssertTooltipVisible();
            Assert.That(Get<Button>(_trait, "_tooltipUpgradeButton").interactable, Is.False);

            RightClick(upgrade);
            yield return null;
            Assert.That(Purchased.Contains(upgrade), Is.True);
            Assert.That(Get<long>(_trait, "_garnetBalance"),
                Is.EqualTo(balance - ResearchCost(upgrade)));
            Assert.That(Get<string>(_trait, "_starterTutorialTargetId"), Is.EqualTo("stat.radius.02"));
            RightClick(upgrade);
            Assert.That(Get<long>(_trait, "_garnetBalance"),
                Is.EqualTo(balance - ResearchCost(upgrade)));

            foreach (string id in new[] { "stat.radius.02", "skill.pet_baby_dragon", "monster.01" })
            {
                RightClick(id);
                yield return null;
            }
            _app.CloseTrait();
            Assert.That(TraitProgressionStore.HasCompletedStarterTutorial, Is.True);
            _app.ShowTrait();
            yield return null;
            yield return null;
            Assert.That(Get<bool>(_trait, "_starterTutorialActive"), Is.False,
                "Reopening Trait after onboarding must restore normal research controls.");
            Hover("skill.pet_baby_dragon");
            AssertTooltipVisible();
            yield return CaptureTooltipAtResolutions("tooltip-pet-skill");
            string petValue = Get<TextMeshProUGUI>(_trait, "_tooltipValue").text;
            Assert.That(petValue, Does.Contain("동료 배우기"));
            Assert.That(petValue, Does.Not.Contain("Learn"));
            Assert.That(Get<TextMeshProUGUI>(_trait, "_tooltipCost").text, Is.EqualTo("배움 완료"));
            Assert.That(Get<Image>(_trait, "_tooltipCurrencyIcon").gameObject.activeSelf, Is.False);
            Assert.That(Get<TextMeshProUGUI>(_trait, "_tooltipStatus").text, Is.EqualTo("전투에서 사용 중"));

            Hover("monster.01.production.01");
            AssertTooltipVisible();
            yield return CaptureTooltipAtResolutions("tooltip-monster-production");
            Assert.That(Get<Button>(_trait, "_tooltipUpgradeButton").gameObject.activeSelf, Is.True);
            string productionValue = Get<TextMeshProUGUI>(_trait, "_tooltipValue").text;
            Assert.That(productionValue, Does.Contain("한 번에 3마리"));
            Assert.That(productionValue, Does.Not.Contain("Spawn"));
            string productionDescription = Get<TextMeshProUGUI>(_trait, "_tooltipDescription").text;
            Assert.That(productionDescription, Does.Contain("특별한 능력 없이 기본 방식으로"));
            Assert.That(productionDescription, Does.Contain("처치 보상"));
            Assert.That(productionDescription, Does.Not.Contain("한 번에 3마리 등장"));
            Assert.That(Get<TextMeshProUGUI>(_trait, "_tooltipCost").text, Does.Contain("필요 100"));
            Assert.That(Get<TextMeshProUGUI>(_trait, "_tooltipStatus").text, Is.EqualTo("젬스톤이 부족해요"));
            Assert.That(Get<TextMeshProUGUI>(_trait, "_tooltipUpgradeLabel").text, Is.EqualTo("강화하기"));
        }

        [UnityTest]
        public IEnumerator EveryResearchTooltipFitsOnTheSmallestSupportedScreen()
        {
            TraitProgressionStore.CompleteStarterTutorial();
            _trait.GuideStarterResearch();
            Call(_trait, "UpgradeAllNodesForTesting");
            yield return null;
            var catalog = Get<TraitCatalog>(_trait, "_activeCatalog");
            var categories = catalog.StatCategories
                .Concat(catalog.SkillCategories)
                .Concat(catalog.MonsterCategories);
            string[] nodeIds = categories
                .Where(category => category != null && category.Nodes != null)
                .SelectMany(category => category.Nodes)
                .Where(node => node != null && !string.IsNullOrEmpty(node.Id))
                .Select(node => node.Id)
                .ToArray();
            Assert.That(nodeIds, Has.Length.EqualTo(103),
                "The tooltip audit must cover every configured research node.");

            string directory = System.Environment.GetEnvironmentVariable("CURSOR_HUNTER_GUIDE_CAPTURE");
            var resolution = new Vector2Int(1280, 720);
            foreach (string nodeId in nodeIds)
            {
                Call(_trait, "ShowNodeTooltip", FindResearch(nodeId));
                AssertTooltipVisible();
                string safeId = nodeId.Replace('.', '_');
                yield return RenderCurrentFrame("tooltip-node-" + safeId,
                    resolution, directory, true);
            }
        }

        [UnityTest]
        public IEnumerator StarterResearchLeadsToCombatSettlementAndTheNextProductionUpgrade()
        {
            long startingBalance = Get<long>(_trait, "_garnetBalance");
            string[] starterPurchases = { "stat.attack.01", "stat.radius.01", "stat.cooldown.01",
                "stat.critical.01", "stat.fieldDuration.01", "stat.attack.02", "stat.radius.02",
                "skill.pet_baby_dragon", "monster.01" };
            long expectedGarnetSpend = starterPurchases.Sum(id =>
                ResearchCurrency(id) == "gem.garnet" ? ResearchCost(id) : 0L);
            var hunt = FindInScene<HuntManager>();
            var combat = FindInScene<CombatRunController>();
            var spawner = FindInScene<MonsterSpawner>();
            hunt.BeginPrototypeRun();
            Assert.That(combat.IsRunActive, Is.False, "A fresh profile cannot spawn monsters before research.");
            foreach (string id in starterPurchases)
            {
                RightClick(id);
                yield return null;
            }
            Assert.That(Get<long>(_trait, "_garnetBalance"),
                Is.EqualTo(startingBalance - expectedGarnetSpend));
            _app.CloseTrait();
            Assert.That(_app.CurrentScreen, Is.EqualTo(UiScreenId.MainMenu));
            Get<Button>(_app, "_starterGuideButton").onClick.Invoke();
            Assert.That(_app.CurrentScreen, Is.EqualTo(UiScreenId.FieldSelect));
            ProgressionCombatSnapshot expected = _trait.CreateCombatSnapshot();
            Get<Button>(_app, "_starterGuideButton").onClick.Invoke();
            Assert.That(_app.CurrentScreen, Is.EqualTo(UiScreenId.Combat));
            Assert.That(combat.IsRunActive, Is.True);
            Assert.That(combat.CombatSnapshot, Is.EqualTo(expected.Combat));
            Assert.That(spawner.CurrentSpawnPlan.Entries, Has.Count.EqualTo(1));
            SpawnSnapshot stump = spawner.CurrentSpawnPlan.Entries[0].Snapshot;
            Assert.That(stump.MonsterId, Is.EqualTo("monster.01"));
            Assert.That(stump.PackSize, Is.EqualTo(2));
            Assert.That(stump.SpawnIntervalSeconds, Is.EqualTo(.75f));
            yield return null; // Pet presentation starts in LateUpdate after run commit.
            Assert.That(FindInScene<PetSkillController>().IsPetVisible, Is.True);
            yield return CaptureCurrentFrame("starter-combat");
            yield return new WaitForSecondsRealtime(.8f);
            AssertAmbientMusic(false);

            // Real targets and the real damage/reward pipeline; no direct wallet grants.
            long wallet = Get<long>(_trait, "_garnetBalance");
            long productionCost = ResearchCost("monster.01.production.01");
            long requiredReward = productionCost -
                (startingBalance - expectedGarnetSpend);
            if (requiredReward < 30L) requiredReward = 30L;
            for (int wave = 0; combat.GarnetEarned < requiredReward && wave < 19; wave++)
            {
                // Batch editors are unfocused; resume before advancing the
                // actual combat clock and spawner deterministically.
                if (combat.IsPaused) Get<RunCoordinator>(hunt, "_runCoordinator").Resume();
                Call(spawner, "Update");
                foreach (var victim in Object.FindObjectsByType<MonsterCombatTarget>(FindObjectsSortMode.None)
                    .Where(target => target.IsActive && !target.IsDead).ToArray())
                    Assert.That(combat.TryApplySkillDamage(combat.CurrentRunId, victim, victim.CurrentHealth * 4L), Is.True);
                combat.AdvanceTime(.76f);
                yield return null;
            }
            Assert.That(combat.GarnetEarned, Is.GreaterThanOrEqualTo(requiredReward));
            Assert.That(Get<long>(_trait, "_garnetBalance"), Is.EqualTo(wallet),
                "Combat rewards must wait for settlement.");
            long earned = combat.GarnetEarned;
            if (combat.IsPaused) Get<RunCoordinator>(hunt, "_runCoordinator").Resume();
            combat.AdvanceTime(combat.DurationSeconds);
            Assert.That(combat.IsRunActive, Is.False);
            Assert.That(Get<long>(_trait, "_garnetBalance"), Is.EqualTo(wallet + earned));
            combat.AdvanceTime(1f);
            Assert.That(Get<long>(_trait, "_garnetBalance"), Is.EqualTo(wallet + earned));
            Assert.That(TraitProgressionStore.TryLoad(out var saved), Is.True);
            Assert.That(saved.garnetBalance, Is.EqualTo(wallet + earned));

            var result = FindInScene<BattleResultPresentation>();
            yield return new WaitForSecondsRealtime(1.4f);
            result.CompleteReveal();
            float readyDeadline = Time.realtimeSinceStartup + 5f;
            while (!result.IsReady && Time.realtimeSinceStartup < readyDeadline) yield return null;
            Assert.That(result.IsReady, Is.True);
            Assert.That(Get<Button>(result, "upgradeButton").GetComponentInChildren<TMP_Text>().text,
                Is.EqualTo("특성 강화"));
            yield return CaptureCurrentFrame("starter-result");
            Get<Button>(result, "upgradeButton").onClick.Invoke();
            Assert.That(_app.CurrentScreen, Is.EqualTo(UiScreenId.Trait));
            yield return null;
            RightClick("monster.01.production.01");
            yield return null;
            Assert.That(Purchased.Contains("monster.01.production.01"), Is.True);
            long previousHp = stump.MaxHealth;
            _app.CloseTrait();
            hunt.BeginPrototypeRun();
            Assert.That(combat.IsRunActive, Is.True);
            Assert.That(spawner.CurrentSpawnPlan.Entries[0].Snapshot.PackSize, Is.EqualTo(3));
            Assert.That(spawner.CurrentSpawnPlan.Entries[0].Snapshot.MaxHealth, Is.EqualTo(previousHp),
                "Production upgrades must not secretly raise HP.");
            hunt.ReturnToMainMenu();
            yield return new WaitForSecondsRealtime(.8f);
            AssertAmbientMusic(true);
        }

        [UnityTest]
        public IEnumerator BossMilestonesRevealOneSkillEachAndPreserveExistingNodePositions()
        {
            TraitProgressionStore.CompleteStarterTutorial();
            _trait.GuideStarterResearch();
            RightClick("stat.attack.01");
            yield return null;
            string[] skills = { "skill.pet_baby_dragon", "skill.tornado", "skill.thunder",
                "skill.flame_exhaust", "skill.magic_ball", "skill.laser" };
            for (int tier = 0; tier <= 5; tier++)
            {
                _trait.SetProgressionSnapshot(100000L, new long[] { 100000, 100000, 100000, 100000, 100000, 100000 }, tier);
                yield return null;
                var rects = Get<Dictionary<string, RectTransform>>(_trait, "_boardNodeRectsById");
                Assert.That(skills.Where(rects.ContainsKey), Is.EquivalentTo(skills.Take(tier + 1)));
            }
            var before = Get<Dictionary<string, RectTransform>>(_trait, "_boardNodeRectsById")
                .ToDictionary(pair => pair.Key, pair => pair.Value.anchoredPosition);
            RightClick("stat.attack.02");
            yield return null;
            foreach (var pair in before)
                Assert.That(Node(pair.Key).anchoredPosition, Is.EqualTo(pair.Value), pair.Key + " moved after research.");

            Call(_trait, "UpgradeAllNodesForTesting");
            yield return null;
            var expected = _trait.CreateCombatSnapshot();
            var hunt = FindInScene<HuntManager>();
            var combat = FindInScene<CombatRunController>();
            _app.CloseTrait();
            hunt.BeginPrototypeRun();
            Assert.That(combat.IsRunActive, Is.True, "All research must still allow Main combat to start.");
            Assert.That(combat.CombatSnapshot, Is.EqualTo(expected.Combat));
            Assert.That(FindInScene<MonsterSpawner>().CurrentSpawnPlan.Entries, Has.Count.EqualTo(10));
            Assert.That(combat.CaptureSkillBalanceSettings().Count(skill => skill.Enabled), Is.EqualTo(6));
            yield return new WaitForSecondsRealtime(1f);
            yield return CaptureCurrentFrame("all-research-combat");
            hunt.ReturnToMainMenu();
        }

        [UnityTest]
        public IEnumerator SettledRelicAndGemstonePersistIntoTheNextCombatSnapshot()
        {
            Call(_trait, "UpgradeAllNodesForTesting");
            var hunt = FindInScene<HuntManager>();
            hunt.BeginPrototypeRun();
            var mainCombat = FindInScene<CombatRunController>();
            Assert.That(mainCombat.IsRunActive, Is.True);

            long before = Get<long>(_trait, "_garnetBalance");
            var request = new RunRequest(mainCombat.CurrentRunId, 1, 1,
                RunMode.NormalField, string.Empty, 0UL, 30f);
            var result = new RunResult(request, RunEndReason.UserExit,
                RunSettlementPolicy.Eligible, 0f, 1, 17L, 1L,
                new[] { new ResourceRewardSnapshot("gem.garnet", 17L) },
                new[] { "relic.red_potion" });
            Call(hunt, "SettleRunRewards", result);

            Assert.That(Get<long>(_trait, "_garnetBalance"), Is.EqualTo(before + 17L));
            Assert.That(TraitProgressionStore.TryLoad(out TraitProgressionStore.SaveData saved), Is.True);
            Assert.That(saved.garnetBalance, Is.EqualTo(before + 17L));
            CollectionAssert.Contains(saved.ownedRelicIds, "relic.red_potion");

            Call(hunt, "SettleRunRewards", result);
            Assert.That(Get<long>(_trait, "_garnetBalance"), Is.EqualTo(before + 17L),
                "The same eligible run must not pay twice.");
            hunt.ReturnToMainMenu();

            ProgressionCombatSnapshot progression = _trait.CreateCombatSnapshot();
            RelicCombatSnapshot redPotion = progression.Relics.Single(
                relic => relic.RelicId == "relic.red_potion");
            Assert.That(redPotion.Owned, Is.True,
                "The saved relic must be owned in the next progression snapshot.");
            RelicCombatModifiers modifiers = RelicCombatModifiers.From(progression.Relics);
            Assert.That(modifiers.CursorAttackDamageMultiplier, Is.GreaterThan(1f));

            var combatObject = new GameObject("Next relic-enabled combat");
            try
            {
                var combat = combatObject.AddComponent<CombatRunController>();
                Assert.That(combat.StartRun(new RunRequest("next-relic-run", 30f),
                    modifiers.ApplyTo(progression.Combat)), Is.True);
                Assert.That(combat.ConfigureRelics(progression.Relics,
                    progression.RelicDropRules), Is.True);
                Assert.That(combat.RelicSnapshots.Single(
                    relic => relic.RelicId == "relic.red_potion").Owned, Is.True);
                Assert.That(combat.RelicModifiers.CursorAttackDamageMultiplier,
                    Is.GreaterThan(1f));
            }
            finally
            {
                Object.DestroyImmediate(combatObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator AllTwentyRelicsHaveArtAndCloseThroughTheRealButtonOrBackdropRaycast()
        {
            TraitProgressionStore.CompleteStarterTutorial();
            _trait.GuideStarterResearch();
            Call(_trait, "ShowRelicCollection", false);
            yield return null;
            var content = Get<RectTransform>(_trait, "_nodeContent");
            Vector3 originalScale = content.localScale;
            Get<RectTransform>(_trait, "_nodeViewport").GetComponent<TraitGraphScrollRect>()
                .OnScroll(new PointerEventData(_events) { position = Vector2.one * 300f, scrollDelta = Vector2.up * 8f });
            Assert.That(content.localScale, Is.EqualTo(originalScale), "Relic grid cannot zoom.");
            Assert.That(content.Cast<Transform>().Count(child => child.name.StartsWith("RelicSlot_")), Is.EqualTo(20));
            for (int index = 0; index < 20; index++)
            {
                Call(_trait, "SetTestRelicObtained", index, true);
                yield return null;
                yield return new WaitForSecondsRealtime(.5f);
                var overlay = Get<GameObject>(_trait, "_relicRevealOverlay");
                Assert.That(overlay, Is.Not.Null, "Missing reveal for relic " + index);
                Assert.That(overlay.transform.Find("RelicRevealFocus/RevealTitle").GetComponent<TMP_Text>().text,
                    Does.Not.StartWith("유물 "));
                Assert.That(overlay.transform.Find("RelicRevealFocus/RevealIcon").GetComponent<Image>().sprite,
                    Is.Not.Null);
                Assert.That(overlay.transform.Find("RelicRevealFocus/RevealDescription").GetComponent<TMP_Text>().text,
                    Does.Not.Contain("준비 중"));
                if (index == 0) yield return CaptureCurrentFrame("relic-reveal");
                var target = index % 2 == 0
                    ? overlay.transform.Find("RelicRevealFocus/RelicRevealConfirm") as RectTransform
                    : overlay.transform as RectTransform;
                Vector2 localPoint = index % 2 == 0 ? target.rect.center : target.rect.min + Vector2.one * 12f;
                RaycastClick(target, localPoint);
                Assert.That(Get<GameObject>(_trait, "_relicRevealOverlay"), Is.Null,
                    "The reveal must close on the same click, relic " + index);
                yield return null;
            }
            Assert.That(_trait.CreateCombatSnapshot().Relics.All(relic => relic.Owned), Is.True);
            yield return CaptureCurrentFrame("relic-collection");
            Call(_trait, "UpgradeAllNodesForTesting");
            var expected = _trait.CreateCombatSnapshot();
            var modifiers = RelicCombatModifiers.From(expected.Relics);
            var hunt = FindInScene<HuntManager>();
            var combat = FindInScene<CombatRunController>();
            _app.CloseTrait();
            hunt.BeginPrototypeRun();
            Assert.That(combat.IsRunActive, Is.True);
            Assert.That(combat.RelicSnapshots.Count(relic => relic.Owned), Is.EqualTo(20));
            Assert.That(combat.CombatSnapshot, Is.EqualTo(modifiers.ApplyTo(expected.Combat)),
                "Owned relics must modify the actual combat snapshot once.");
            Assert.That(combat.RelicModifiers.PetDamageMultiplier, Is.GreaterThan(1f));
            Assert.That(combat.RelicModifiers.CursorAttackDamageMultiplier, Is.GreaterThan(1f));
            hunt.ReturnToMainMenu();
        }

        private void RaycastClick(RectTransform target, Vector2 localPoint)
        {
            Canvas.ForceUpdateCanvases();
            Canvas canvas = target.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(_events) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, target.TransformPoint(localPoint)) };
            var hits = new List<RaycastResult>();
            _events.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Transform hit = hits[0].gameObject.transform;
            Assert.That(hit == target || hit.IsChildOf(target), Is.True, "A graphic intercepted " + target.name);
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        [UnityTest]
        public IEnumerator EstablishedProfileWithCoreAndStumpDoesNotRequireTheNewTutorialPurchases()
        {
            Purchased.Add("stat.attack.01");
            Purchased.Add("monster.01");
            Call(_trait, "SaveProgressionState");
            PlayerPrefs.DeleteKey(ProfileKey + ".starter-tutorial-started");
            PlayerPrefs.DeleteKey(ProfileKey + ".starter-tutorial-complete");
            Assert.That(TraitProgressionStore.ShouldShowStarterTutorial, Is.False);
            _trait.GuideStarterResearch();
            _app.CloseTrait();
            FindInScene<HuntManager>().BeginPrototypeRun();
            Assert.That(FindInScene<CombatRunController>().IsRunActive, Is.True,
                "Existing saves must not be blocked by a newly added onboarding purchase.");
            Assert.That(Get<bool>(_trait, "_starterTutorialActive"), Is.False);
            FindInScene<HuntManager>().ReturnToMainMenu();
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryMonsterDefinitionResolvesItsAuthoredCombatPrefab()
        {
            ScriptableObject[] definitions = Resources.LoadAll<ScriptableObject>("MonsterDefinitions");
            Sprite[] traitIcons = Get<Sprite[]>(_trait, "monsterIcons");
            Assert.That(traitIcons, Has.Length.EqualTo(10),
                "Trait icon order must contain one icon for every authored monster.");
            for (int index = 0; index < 10; index++)
            {
                string id = "monster." + (index + 1).ToString("00");
                ScriptableObject definition = definitions.SingleOrDefault(candidate =>
                    candidate != null && candidate.GetType().Name == "MonsterDefinition" &&
                    (string)candidate.GetType().GetProperty("MonsterId")?.GetValue(candidate) == id);
                Assert.That(definition, Is.Not.Null, "Missing Resources MonsterDefinition for " + id);
                GameObject visualPrefab = (GameObject)definition.GetType()
                    .GetProperty("VisualPrefab")?.GetValue(definition);
                Assert.That(visualPrefab != null, Is.True,
                    id + " has an unresolved visualPrefab reference.");
                Assert.That((string)definition.GetType().GetProperty("DisplayName")?.GetValue(definition),
                    Is.Not.Empty, id + " must have a player-facing name.");
                Sprite traitIcon = traitIcons[index];
                Assert.That(traitIcon, Is.Not.Null, id + " has no Trait icon.");

                GameObject visual = Object.Instantiate(visualPrefab);
                try
                {
                    Sprite[] battleSprites = visual.GetComponentsInChildren<SpriteRenderer>(true)
                        .Select(renderer => renderer.sprite)
                        .Where(sprite => sprite != null)
                        .ToArray();
                    Assert.That(battleSprites, Does.Contain(traitIcon),
                        id + " combat visual must use the same authored sprite as its Trait icon " +
                        traitIcon.name + ".");

                    if (index == 1)
                    {
                        Assert.That((string)definition.GetType().GetProperty("DisplayName")
                            ?.GetValue(definition), Is.EqualTo("여왕벌"));
                        Assert.That((string)definition.GetType().GetProperty("PrefabKey")
                            ?.GetValue(definition), Is.EqualTo("Bee/Bee_Honey"));
                        Assert.That(traitIcon.name, Is.EqualTo("Body"),
                            "The queen bee icon must be the Bee_Honey body, not a bat/fly sprite.");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(visual);
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryMonsterTooltipShowsItsNameTraitAndKillRewards()
        {
            TraitProgressionStore.CompleteStarterTutorial();
            _trait.GuideStarterResearch();
            Call(_trait, "UpgradeAllNodesForTesting");

            string[] names =
            {
                "그루터기", "여왕벌", "독버섯", "가면유령", "갑옷병사",
                "눈요정", "벌집", "골렘", "봉오리", "판도라 상자"
            };
            string[] traits =
            {
                "특별한 능력 없이 기본 방식으로 움직입니다.",
                "몸집과 피격 범위가 작습니다.",
                "빠르게 이동합니다.",
                "4초마다 1.2초 동안 모습을 감춰 공격을 피합니다.",
                "4.5초마다 1.7초 동안 받는 피해가 65% 감소합니다.",
                "3.5초마다 순간이동해 위치를 바꿉니다.",
                "체력이 높아 오래 버팁니다.",
                "몸집이 크고 피격 범위가 넓습니다.",
                "3.5초마다 반경 2.75 안의 아군 체력을 최대 체력의 20%만큼 회복합니다.",
                "4초마다 반경 3.5 안의 쓰러진 몬스터를 최대 체력의 30%로 되살립니다."
            };
            string[] rewardGems =
            {
                string.Empty, "토파즈", "토파즈", "자수정", "자수정",
                "사파이어", "사파이어", "다이아몬드", "드래곤 스톤", "드래곤 스톤"
            };
            int[] garnetRewards = { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            int[] bonusGemRewards = { 0, 3, 4, 3, 3, 5, 5, 2, 1, 1 };

            for (int index = 0; index < names.Length; index++)
            {
                string monsterId = "monster." + (index + 1).ToString("00");
                TraitNodeDefinition unlock = FindResearch(monsterId);
                Call(_trait, "ShowNodeTooltip", unlock);
                AssertTooltipVisible();
                Assert.That(Get<TextMeshProUGUI>(_trait, "_tooltipType").text,
                    Is.EqualTo("몬스터 해금"), monsterId);
                Assert.That(Get<TextMeshProUGUI>(_trait, "_tooltipTitle").text,
                    Is.EqualTo(names[index]), monsterId);

                string description = Get<TextMeshProUGUI>(_trait, "_tooltipDescription").text;
                Assert.That(description, Does.Contain(traits[index]), monsterId);
                Assert.That(description, Does.Contain("처치 보상"), monsterId);
                Assert.That(description, Does.Contain("가넷 " + garnetRewards[index] + "개"), monsterId);
                Assert.That(description, Does.Not.Contain("생성 간격"), monsterId);
                Assert.That(description, Does.Not.Contain("Spawn"), monsterId);
                Assert.That(description, Does.Not.Contain("추가 잼스톤"), monsterId);
                if (bonusGemRewards[index] > 0)
                {
                    Assert.That(description, Does.Contain(
                        rewardGems[index] + " " + bonusGemRewards[index] + "개"), monsterId);
                }

                TraitNodeDefinition production = FindResearch(monsterId + ".production.01");
                Call(_trait, "ShowNodeTooltip", production);
                AssertTooltipVisible();
                Assert.That(Get<TextMeshProUGUI>(_trait, "_tooltipType").text,
                    Is.EqualTo("몬스터 생산량 증가"), monsterId);
                Assert.That(Get<TextMeshProUGUI>(_trait, "_tooltipTitle").text,
                    Does.StartWith(names[index]), monsterId);
                description = Get<TextMeshProUGUI>(_trait, "_tooltipDescription").text;
                Assert.That(description, Does.Contain(traits[index]), monsterId);
                Assert.That(description, Does.Contain("처치 보상"), monsterId);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator MonsterProfilesExecuteVanishShieldTeleportHealingAndSingleRewardRevival()
        {
            TraitProgressionStore.CompleteStarterTutorial();
            _trait.GuideStarterResearch();
            Call(_trait, "UpgradeAllNodesForTesting");
            Purchased.RemoveWhere(id => id.StartsWith("skill."));
            FindInScene<MainCursorController>().enabled = false;
            _app.CloseTrait();
            var combat = FindInScene<CombatRunController>();
            var hunt = FindInScene<HuntManager>();
            hunt.BeginPrototypeRun();
            Assert.That(combat.IsRunActive, Is.True);
            var monsters = Object.FindObjectsByType<MonsterCombatTarget>(FindObjectsSortMode.None)
                .Where(monster => monster.IsActive && monster.RunId == combat.CurrentRunId)
                .GroupBy(monster => monster.MonsterId).ToDictionary(group => group.Key, group => group.First());
            Assert.That(monsters, Has.Count.EqualTo(10));
            var plan = FindInScene<MonsterSpawner>().CurrentSpawnPlan.Entries
                .ToDictionary(entry => entry.Snapshot.MonsterId, entry => entry.Snapshot);
            Assert.That(plan["monster.02"].VisualScale, Is.LessThan(plan["monster.01"].VisualScale));
            Assert.That(plan["monster.03"].MoveSpeed, Is.GreaterThan(plan["monster.01"].MoveSpeed));
            Assert.That(plan["monster.07"].MaxHealth, Is.GreaterThan(plan["monster.01"].MaxHealth));
            Assert.That(plan["monster.08"].VisualScale, Is.GreaterThan(plan["monster.01"].VisualScale));
            foreach (var pair in monsters)
            {
                Assert.That(pair.Value.GarnetReward, Is.EqualTo(plan[pair.Key].GarnetReward));
                Assert.That(pair.Value.BonusDropCurrencyId, Is.EqualTo(plan[pair.Key].BonusDropCurrencyId));
            }

            combat.AdvanceTime(4.6f);
            Call(monsters["monster.04"].GetComponent<MonsterBehaviorController>(), "Update");
            Assert.That(monsters["monster.04"].IsVulnerable, Is.False);
            Assert.That(combat.TryApplySkillDamage(combat.CurrentRunId, monsters["monster.04"], 100L), Is.False);
            Call(monsters["monster.05"].GetComponent<MonsterBehaviorController>(), "Update");
            Assert.That(monsters["monster.05"].IsShielded, Is.True);
            long shieldHp = monsters["monster.05"].CurrentHealth;
            Assert.That(monsters["monster.05"].ApplyDamage(combat.CurrentRunId, 10L, out _, out _), Is.True);
            Assert.That(shieldHp - monsters["monster.05"].CurrentHealth, Is.InRange(1L, 4L));
            Vector3 oldPosition = monsters["monster.06"].transform.position;
            Call(monsters["monster.06"].GetComponent<MonsterBehaviorController>(), "Update");
            Assert.That(Vector3.Distance(monsters["monster.06"].transform.position, oldPosition), Is.GreaterThan(.1f));

            MonsterCombatTarget stump = monsters["monster.01"];
            stump.transform.position = Vector3.zero;
            Assert.That(stump.ApplyDamage(combat.CurrentRunId, 5L, out _, out _), Is.True);
            long damagedHp = stump.CurrentHealth;
            monsters["monster.09"].transform.position = Vector3.zero;
            Call(monsters["monster.09"].GetComponent<MonsterBehaviorController>(), "Update");
            Assert.That(stump.CurrentHealth, Is.GreaterThan(damagedHp));
            Assert.That(combat.TryApplySkillDamage(combat.CurrentRunId, stump, 10000L), Is.True);
            Assert.That(stump.IsDead, Is.True);
            long firstReward = combat.GarnetEarned;
            Assert.That(firstReward, Is.GreaterThan(0L));
            monsters["monster.10"].transform.position = Vector3.zero;
            Call(monsters["monster.10"].GetComponent<MonsterBehaviorController>(), "Update");
            Assert.That(stump.IsActive, Is.True, "Nearby revival must restore the actual combat target.");
            Assert.That(stump.CurrentHealth, Is.LessThan(stump.MaxHealth));
            Assert.That(combat.TryApplySkillDamage(combat.CurrentRunId, stump, 10000L), Is.True);
            Assert.That(combat.GarnetEarned, Is.EqualTo(firstReward), "A revived body must not duplicate rewards.");
            combat.AdvanceTime(1.3f);
            Call(monsters["monster.04"].GetComponent<MonsterBehaviorController>(), "Update");
            Assert.That(monsters["monster.04"].IsVulnerable, Is.True);
            hunt.ReturnToMainMenu();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResearchWheelAndDragAreBoundedAndDoNotJumpAcrossTheBoard()
        {
            TraitProgressionStore.CompleteStarterTutorial();
            _trait.GuideStarterResearch();
            RightClick("stat.attack.01");
            yield return null;
            var viewport = Get<RectTransform>(_trait, "_nodeViewport");
            var content = Get<RectTransform>(_trait, "_nodeContent");
            var scroll = viewport.GetComponent<TraitGraphScrollRect>();
            Canvas canvas = viewport.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(camera, viewport.TransformPoint(viewport.rect.center));
            scroll.OnScroll(new PointerEventData(_events) { position = center, scrollDelta = Vector2.up * 100f });
            Assert.That(content.localScale.x, Is.EqualTo(2.1f).Within(.001f));
            Canvas.ForceUpdateCanvases();
            var drag = new PointerEventData(_events) { button = PointerEventData.InputButton.Left, position = center };
            scroll.OnInitializePotentialDrag(drag);
            scroll.OnBeginDrag(drag);
            Vector2 before = content.anchoredPosition;
            drag.position += new Vector2(90f, 60f);
            drag.delta = new Vector2(90f, 60f);
            scroll.OnDrag(drag);
            float distance = Vector2.Distance(content.anchoredPosition, before);
            Assert.That(distance, Is.GreaterThan(1f));
            Assert.That(distance, Is.LessThan(200f / canvas.scaleFactor));
            scroll.OnEndDrag(drag);
            scroll.StopMovement();
            scroll.OnScroll(new PointerEventData(_events) { position = center, scrollDelta = Vector2.down * 100f });
            Assert.That(content.localScale.x, Is.EqualTo(.28f).Within(.001f));
        }

        [UnityTest]
        public IEnumerator CurrencyExchangeWorksWithoutItsMonsterAndPersistsTheExactHundredToOnePayout()
        {
            TraitProgressionStore.CompleteStarterTutorial();
            _trait.GuideStarterResearch();
            RightClick("stat.attack.01");
            yield return null;
            RightClick("monster.01");
            _trait.SetProgressionSnapshot(1000L, new long[] { 1000, 0, 0, 0, 0, 0 }, 1);
            yield return null;
            var panel = Get<RectTransform>(_trait, "_gemstonePanel");
            Assert.That(panel, Is.Not.Null);
            Assert.That(Purchased.Contains("monster.02"), Is.False,
                "The next-tier exchange must not require its supply monster to be unlocked.");
            var exchange = panel.Find("GemstoneList/Gemstone_gem.garnet/ExchangeToNextTier")
                .GetComponent<TraitGemstoneExchangeHoldInput>();
            Assert.That(exchange, Is.Not.Null);
            long before = Get<long>(_trait, "_garnetBalance");
            exchange.OnPointerDown(new PointerEventData(_events)
                { button = PointerEventData.InputButton.Left });
            exchange.OnPointerUp(new PointerEventData(_events)
                { button = PointerEventData.InputButton.Left });
            Assert.That(Get<long>(_trait, "_garnetBalance"), Is.EqualTo(before - 100L));
            Assert.That(Get<long[]>(_trait, "_gemstoneBalances")[1], Is.EqualTo(1L));
            Assert.That(TraitProgressionStore.TryLoad(out var save), Is.True);
            Assert.That(save.garnetBalance, Is.EqualTo(before - 100L));
            Assert.That(save.gemstoneBalances[1], Is.EqualTo(1L));
        }

        [UnityTest]
        public IEnumerator CurrencyExchangeRepeatsWhileHeldAndStopsOnRelease()
        {
            TraitProgressionStore.CompleteStarterTutorial();
            _trait.GuideStarterResearch();
            RightClick("stat.attack.01");
            yield return null;
            _trait.SetProgressionSnapshot(1000L, new long[] { 1000, 0, 0, 0, 0, 0 }, 1);
            yield return null;

            var panel = Get<RectTransform>(_trait, "_gemstonePanel");
            var exchange = panel.Find("GemstoneList/Gemstone_gem.garnet/ExchangeToNextTier")
                .GetComponent<TraitGemstoneExchangeHoldInput>();
            long garnetBefore = Get<long>(_trait, "_garnetBalance");
            long topazBefore = Get<long[]>(_trait, "_gemstoneBalances")[1];
            var pointer = new PointerEventData(_events)
                { button = PointerEventData.InputButton.Left };

            exchange.OnPointerDown(pointer);
            yield return new WaitForSecondsRealtime(.52f);
            exchange.OnPointerUp(pointer);

            long garnetAfterRelease = Get<long>(_trait, "_garnetBalance");
            long topazAfterRelease = Get<long[]>(_trait, "_gemstoneBalances")[1];
            Assert.That(garnetAfterRelease, Is.LessThanOrEqualTo(garnetBefore - 200L),
                "A sustained press should exchange repeatedly after the initial payout.");
            Assert.That(topazAfterRelease, Is.GreaterThanOrEqualTo(topazBefore + 2L));

            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(Get<long>(_trait, "_garnetBalance"), Is.EqualTo(garnetAfterRelease),
                "Releasing the pointer must stop all further exchange ticks.");
            Assert.That(Get<long[]>(_trait, "_gemstoneBalances")[1], Is.EqualTo(topazAfterRelease));
            Assert.That(TraitProgressionStore.TryLoad(out var save), Is.True);
            Assert.That(save.garnetBalance, Is.EqualTo(garnetAfterRelease));
            Assert.That(save.gemstoneBalances[1], Is.EqualTo(topazAfterRelease));
        }

        [UnityTest]
        public IEnumerator RelicPresentationFollowsStableIdsWhenGameDataOrderChanges()
        {
            object document = Get<object>(_trait, "_document");
            GameInformation information = ReadField<GameInformation>(document, "information");
            var entries = Get<IList>(_trait, "relicEntries");
            RelicInformation[] originalDefinitions = information.relics;
            object[] originalEntries = entries.Cast<object>().ToArray();
            RelicCollectionCatalog catalog = Resources.Load<RelicCollectionCatalog>(
                "RelicCollectionCatalog");

            try
            {
                entries.Clear();
                foreach (object entry in originalEntries.Reverse())
                    entries.Add(entry);
                information.relics = originalDefinitions.Reverse().ToArray();

                Call(_trait, "ApplyRelicPresentationDefaults");

                Assert.That(catalog.Entries.Count, Is.EqualTo(20));
                Assert.That(entries.Count, Is.EqualTo(information.relics.Length));
                for (int i = 0; i < information.relics.Length; i++)
                {
                    string relicId = information.relics[i].id;
                    object entry = entries[i];
                    RelicCollectionCatalogEntry expected = catalog.FindById(relicId);
                    Assert.That(expected, Is.Not.Null, "Missing catalog ID " + relicId);
                    Assert.That(ReadProperty<string>(entry, "Id"), Is.EqualTo(relicId));
                    Assert.That(ReadProperty<string>(entry, "Title"), Is.EqualTo(expected.Title));
                    Assert.That(ReadProperty<Sprite>(entry, "Icon"), Is.EqualTo(expected.Icon));
                }
            }
            finally
            {
                information.relics = originalDefinitions;
                entries.Clear();
                foreach (object entry in originalEntries)
                    entries.Add(entry);
                Call(_trait, "ApplyRelicPresentationDefaults");
            }

            yield return null;
        }

        private static void Call(object target, string method, params object[] args) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic, null,
                args.Select(arg => arg.GetType()).ToArray(), null).Invoke(target, args);

        private void AssertAmbientMusic(bool playing)
        {
            AudioSource music = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(source => source.clip != null && source.clip.name == "PeacefulFantasyLoop");
            Assert.That(music, Is.Not.Null);
            Assert.That(music.isPlaying && music.volume > .01f, Is.EqualTo(playing));
        }

        private IEnumerator CaptureCurrentFrame(string label)
        {
            string directory = System.Environment.GetEnvironmentVariable("CURSOR_HUNTER_GUIDE_CAPTURE");
            if (string.IsNullOrEmpty(directory)) yield break;
            yield return RenderCurrentFrame(label, new Vector2Int(1920, 1080), directory, false);
        }

        private IEnumerator CaptureTooltipAtResolutions(string label)
        {
            string directory = System.Environment.GetEnvironmentVariable("CURSOR_HUNTER_GUIDE_CAPTURE");
            foreach (Vector2Int resolution in new[]
                { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1280, 960) })
            {
                string suffix = resolution.x + "x" + resolution.y;
                yield return RenderCurrentFrame(label + "-" + suffix, resolution, directory, true);
            }
        }

        private IEnumerator RenderCurrentFrame(
            string label, Vector2Int resolution, string directory, bool verifyTooltip)
        {
            Camera camera = FindInScene<Camera>();
            Canvas canvas = _events.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindInScene<Canvas>().rootCanvas;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousDistance = canvas.planeDistance;
            RenderTexture previousTarget = camera.targetTexture;
            var render = new RenderTexture(resolution.x, resolution.y, 24);
            try
            {
                camera.targetTexture = render;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                yield return null;
                yield return null;
                yield return new WaitForSecondsRealtime(0.16f);
                Canvas.ForceUpdateCanvases();
                if (verifyTooltip)
                {
                    typeof(TraitScreenController).GetMethod("RefreshNodeTooltipPosition",
                        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_trait, null);
                    Canvas.ForceUpdateCanvases();
                }
                if (verifyTooltip) AssertTooltipFitsResolution(camera, resolution, label);

                if (!string.IsNullOrEmpty(directory))
                {
                    camera.Render();
                    RenderTexture previousActive = RenderTexture.active;
                    RenderTexture.active = render;
                    var image = new Texture2D(resolution.x, resolution.y, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0);
                    image.Apply();
                    RenderTexture.active = previousActive;
                    System.IO.Directory.CreateDirectory(directory);
                    System.IO.File.WriteAllBytes(
                        System.IO.Path.Combine(directory, label + ".png"), image.EncodeToPNG());
                    Object.Destroy(image);
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance;
                render.Release();
                Object.Destroy(render);
            }
        }

        private void AssertTooltipFitsResolution(Camera camera, Vector2Int resolution, string label)
        {
            RectTransform tooltip = Get<RectTransform>(_trait, "_tooltipRoot");
            Assert.That(tooltip.gameObject.activeInHierarchy, Is.True);
            var corners = new Vector3[4];
            tooltip.GetWorldCorners(corners);
            Rect bounds = camera.pixelRect;
            foreach (Vector3 corner in corners)
            {
                Vector3 screenPoint = camera.WorldToScreenPoint(corner);
                Assert.That(screenPoint.x, Is.InRange(bounds.xMin - 1f, bounds.xMax + 1f),
                    label + ": tooltip must remain inside the " + resolution + " render area.");
                Assert.That(screenPoint.y, Is.InRange(bounds.yMin - 1f, bounds.yMax + 1f),
                    label + ": tooltip must remain inside the " + resolution + " render area.");
            }

            foreach (TMP_Text text in tooltip.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!text.isActiveAndEnabled) continue;
                text.ForceMeshUpdate();
                Assert.That(ContainsLatinLetter(text.text), Is.False,
                    "Tooltip text " + text.name + " ('" + text.text +
                    "') must not expose English source data at " + resolution + ".");
                Assert.That(text.isTextOverflowing, Is.False,
                    "Tooltip text " + text.name + " ('" + text.text + "', " +
                    text.rectTransform.rect + ", font " + text.fontSize.ToString("0.##") +
                    ", overflow " + text.overflowMode + ") must not overflow at " + resolution + ".");
            }
        }

        private static bool ContainsLatinLetter(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            bool insideRichTextTag = false;
            foreach (char character in value)
            {
                if (character == '<')
                {
                    insideRichTextTag = true;
                    continue;
                }

                if (insideRichTextTag)
                {
                    if (character == '>') insideRichTextTag = false;
                    continue;
                }

                if ((character >= 'A' && character <= 'Z') ||
                    (character >= 'a' && character <= 'z'))
                {
                    return true;
                }
            }
            return false;
        }

        private HashSet<string> Purchased => Get<HashSet<string>>(_trait, "_purchased");

        private TraitNodeDefinition FindResearch(string id)
        {
            TraitCatalog catalog = Get<TraitCatalog>(_trait, "_activeCatalog");
            return catalog.StatCategories.Concat(catalog.SkillCategories)
                .Concat(catalog.MonsterCategories).SelectMany(category => category.Nodes)
                .Single(node => node.Id == id);
        }

        private int ResearchCost(string id) => FindResearch(id).Cost;

        private string ResearchCurrency(string id) => FindResearch(id).CostGemstoneId;

        private RectTransform Node(string id) =>
            Get<Dictionary<string, RectTransform>>(_trait, "_boardNodeRectsById")[id];

        private static void AssertRainbowFrame(RectTransform node)
        {
            AssertRainbowSide(node, "FrameTop", new Color(1f, 0.25f, 0.36f, 1f));
            AssertRainbowSide(node, "FrameRight", new Color(1f, 0.75f, 0.20f, 1f));
            AssertRainbowSide(node, "FrameBottom", new Color(0.20f, 0.82f, 1f, 1f));
            AssertRainbowSide(node, "FrameLeft", new Color(0.70f, 0.40f, 1f, 1f));
        }

        private static void AssertRainbowSide(RectTransform node, string side, Color expected)
        {
            Image frame = node.GetComponentsInChildren<Image>(true)
                .Where(image => image.gameObject.name == side)
                .OrderByDescending(image => image.color.a)
                .FirstOrDefault();
            Assert.That(frame, Is.Not.Null, "Missing final-tier frame side " + side + ".");
            Assert.That(frame.color, Is.EqualTo(expected), side);
        }

        private PointerEventData Pointer(string id)
        {
            RectTransform rect = Node(id);
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return new PointerEventData(_events)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center))
            };
        }

        private void Hover(string id)
        {
            Canvas.ForceUpdateCanvases();
            PointerEventData pointer = Pointer(id);
            var hits = new List<RaycastResult>();
            _events.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), "No pointer hit for node " + id);
            Transform hit = hits[0].gameObject.transform;
            Assert.That(hit == Node(id) || hit.IsChildOf(Node(id)), Is.True,
                "Pointer inspection must reach node " + id);
            ExecuteEvents.Execute(Node(id).gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        }

        private void Exit(string id) => ExecuteEvents.Execute(
            Node(id).gameObject, Pointer(id), ExecuteEvents.pointerExitHandler);

        private void RightClick(string id)
        {
            PointerEventData pointer = Pointer(id);
            pointer.button = PointerEventData.InputButton.Right;
            ExecuteEvents.Execute(Node(id).gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private void AssertTooltipVisible()
        {
            RectTransform popup = Get<RectTransform>(_trait, "_tooltipRoot");
            Assert.That(popup.gameObject.activeInHierarchy, Is.True);
            Assert.That(Get<TextMeshProUGUI>(_trait, "_tooltipTitle").text, Is.Not.Empty);
            Assert.That(popup.GetSiblingIndex(),
                Is.GreaterThan(Get<GameObject>(_trait, "_starterTutorialOverlay").transform.GetSiblingIndex()));
        }

        private T FindInScene<T>() where T : Component
        {
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            Assert.Fail("Missing scene component " + typeof(T).Name);
            return null;
        }

        private static T Get<T>(object target, string field) => (T)target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static T ReadProperty<T>(object target, string property) => (T)target.GetType()
            .GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .GetValue(target);

        private static T ReadField<T>(object target, string field) => (T)target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .GetValue(target);
    }
}
