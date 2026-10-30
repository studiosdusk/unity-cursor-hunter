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
            foreach (string key in new[] { ProfileKey, ProfileKey + ".before-v5", ProfileKey + ".before-v6" })
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
            Hover("stat.cooldown.01");
            AssertTooltipVisible();
            Assert.That(Get<Button>(_trait, "_tooltipUpgradeButton").interactable, Is.True);
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
            Assert.That(Get<long>(_trait, "_garnetBalance"), Is.EqualTo(balance - 80));
            Assert.That(Get<string>(_trait, "_starterTutorialTargetId"), Is.EqualTo("stat.radius.02"));
            RightClick(upgrade);
            Assert.That(Get<long>(_trait, "_garnetBalance"), Is.EqualTo(balance - 80));

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
            Hover("monster.01.production.01");
            AssertTooltipVisible();
            Assert.That(Get<Button>(_trait, "_tooltipUpgradeButton").gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator StarterResearchLeadsToCombatSettlementAndTheNextProductionUpgrade()
        {
            var hunt = FindInScene<HuntManager>();
            var combat = FindInScene<CombatRunController>();
            var spawner = FindInScene<MonsterSpawner>();
            hunt.BeginPrototypeRun();
            Assert.That(combat.IsRunActive, Is.False, "A fresh profile cannot spawn monsters before research.");
            foreach (string id in new[] { "stat.attack.01", "stat.radius.01", "stat.cooldown.01",
                "stat.critical.01", "stat.fieldDuration.01", "stat.attack.02", "stat.radius.02",
                "skill.pet_baby_dragon", "monster.01" })
            {
                RightClick(id);
                yield return null;
            }
            Assert.That(Get<long>(_trait, "_garnetBalance"), Is.EqualTo(5L));
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
            for (int wave = 0; combat.GarnetEarned < 30L && wave < 12; wave++)
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
            Assert.That(combat.GarnetEarned, Is.GreaterThanOrEqualTo(30L));
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
        public IEnumerator MonsterProfilesExecuteVanishShieldTeleportHealingAndSingleRewardRevival()
        {
            TraitProgressionStore.CompleteStarterTutorial();
            _trait.GuideStarterResearch();
            Call(_trait, "UpgradeAllNodesForTesting");
            Purchased.RemoveWhere(id => id.StartsWith("skill."));
            FindInScene<MainCursorController>().enabled = false;
            _app.CloseTrait();
            var hunt = FindInScene<HuntManager>();
            var combat = FindInScene<CombatRunController>();
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
        public IEnumerator CurrencyExchangeWorksBeforeItsMonsterUnlockAndPersistsTheExactHundredToOnePayout()
        {
            TraitProgressionStore.CompleteStarterTutorial();
            _trait.GuideStarterResearch();
            RightClick("stat.attack.01");
            yield return null;
            RightClick("monster.01");
            _trait.SetProgressionSnapshot(1000L, new long[] { 1000, 0, 0, 0, 0, 0 }, 1);
            yield return null;
            var list = Get<RectTransform>(_trait, "_gemstoneList");
            var exchange = list.Find("Gemstone_gem.garnet/ExchangeToNextTier").gameObject;
            Assert.That(exchange.activeInHierarchy, Is.True);
            Assert.That(Purchased.Contains("monster.02"), Is.False);
            long before = Get<long>(_trait, "_garnetBalance");
            var pointer = new PointerEventData(_events) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(exchange, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(exchange, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(Get<long>(_trait, "_garnetBalance"), Is.EqualTo(before - 100L));
            Assert.That(Get<long[]>(_trait, "_gemstoneBalances")[1], Is.EqualTo(1L));
            Assert.That(TraitProgressionStore.TryLoad(out var save), Is.True);
            Assert.That(save.garnetBalance, Is.EqualTo(before - 100L));
            Assert.That(save.gemstoneBalances[1], Is.EqualTo(1L));
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
            Camera camera = FindInScene<Camera>();
            Canvas canvas = _events.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindInScene<Canvas>().rootCanvas;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousDistance = canvas.planeDistance;
            RenderTexture previousTarget = camera.targetTexture;
            var render = new RenderTexture(1920, 1080, 24);
            try
            {
                camera.targetTexture = render;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                yield return null;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture previousActive = RenderTexture.active;
                RenderTexture.active = render;
                var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                image.Apply();
                RenderTexture.active = previousActive;
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, label + ".png"), image.EncodeToPNG());
                Object.Destroy(image);
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

        private HashSet<string> Purchased => Get<HashSet<string>>(_trait, "_purchased");

        private RectTransform Node(string id) =>
            Get<Dictionary<string, RectTransform>>(_trait, "_boardNodeRectsById")[id];

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
            Assert.That(hits.Count, Is.GreaterThan(0));
            Transform hit = hits[0].gameObject.transform;
            Assert.That(hit == Node(id) || hit.IsChildOf(Node(id)), Is.True,
                "Tutorial dimming must allow pointer inspection of node " + id);
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
    }
}
