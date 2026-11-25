using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CursorHunter.App.Tests
{
    public sealed class BattleResultPlayModeTests
    {
        private Scene _scene;
        private HuntManager _hunt;
        private BattleResultPresentation _view;
        private string _profile;
        private float _timeScale;

        [UnitySetUp]
        public IEnumerator LoadMain()
        {
            _timeScale = Time.timeScale;
            _profile = PlayerPrefs.GetString("cursor_hunter.progression.v1", "missing");
#if UNITY_EDITOR
            _scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            yield return null;
            _hunt = Object.FindFirstObjectByType<HuntManager>();
            Set(_hunt, "playEntranceSequence", false); // This fixture exercises terminal presentation in isolation.
            _view = Object.FindFirstObjectByType<BattleResultPresentation>();
            Assert.That(_view, Is.Not.Null);
            Assert.That(_view.IsVisible, Is.False);
            Object.FindFirstObjectByType<CombatGemstoneList>(FindObjectsInactive.Include).Clear();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = _timeScale;
            if (_hunt != null) _hunt.ResetPrototypeRun();
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
            Assert.That(PlayerPrefs.GetString("cursor_hunter.progression.v1", "missing"), Is.EqualTo(_profile));
        }

        [UnityTest]
        public IEnumerator ExpiryPopsTimeoutThenRewardsAndBothButtonsNavigateOnce()
        {
            Set(_hunt, "connectTraitScreenToCombat", false);
            CombatBalanceSettings settings = _hunt.CaptureCombatBalanceSettings();
            settings.UsePlayerProgression = false;
            settings.AttackPower = 1L; settings.CriticalChancePercent = 0f; settings.DurationSeconds = 15f;
            foreach (SkillBalanceSettings skill in settings.Skills) skill.Enabled = false;
            foreach (MonsterBalanceSettings monster in settings.Monsters)
            {
                monster.Enabled = monster.MonsterId == "monster.01";
                monster.MaxHealth = 10000L; monster.PackSize = 1; monster.GarnetReward = 43L;
                monster.BonusDropCurrencyId = "gem.dragon"; monster.BonusDropAmount = 48L; monster.BonusDropChancePercent = 100f;
            }
            Assert.That(_hunt.TrySetCombatBalanceSettings(settings, out string error), Is.True, error);
            _hunt.BeginPrototypeRun();
            var combat = Object.FindFirstObjectByType<CombatRunController>();
            RunId first = combat.CurrentRunId;
            Assert.That(combat.TryApplySkillDamage(first, Object.FindFirstObjectByType<MonsterCombatTarget>(), 20000L), Is.True);
            combat.AdvanceTime(30f);
            Assert.That(_view.IsVisible, Is.True);
            Assert.That(Get<GameObject>(_view, "timeOutPanel").activeSelf, Is.True);
            Assert.That(Get<CanvasGroup>(_view, "resultUI").gameObject.activeSelf, Is.False);
            Assert.That(Get<CanvasGroup>(_view, "actions").interactable, Is.False);
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(0.45f);
            Assert.That(Get<RectTransform>(_view, "timeOutPopup").localScale.x, Is.EqualTo(1).Within(0.02));
            if (System.Environment.GetEnvironmentVariable("CURSOR_HUNTER_RESULT_CAPTURE") == "1")
                yield return CaptureResult(1920, 1080, "/tmp/cursor-hunter-timeout.png");
            yield return new WaitForSecondsRealtime(0.9f);
            Assert.That(Get<GameObject>(_view, "timeOutPanel").activeSelf, Is.False);
            Assert.That(Get<CanvasGroup>(_view, "resultUI").gameObject.activeSelf, Is.True);
            _view.CompleteReveal();
            yield return WaitForReady();
            Assert.That(_view.IsReady, Is.True);
            Assert.That(Amount("garnet"), Is.EqualTo("+43"));
            Assert.That(Amount("dragon"), Is.EqualTo("+48"));
            Assert.That(VisibleGems(), Is.EqualTo(new[] { "Reward_garnet", "Reward_dragon" }));
            Time.timeScale = _timeScale;
            Button retry = Get<Button>(_view, "retryButton");
            retry.onClick.Invoke(); retry.onClick.Invoke();
            Assert.That(combat.IsRunActive, Is.True);
            Assert.That(combat.CurrentRunId, Is.Not.EqualTo(first));
            Assert.That(_view.IsVisible, Is.False);
            Object.FindFirstObjectByType<PrototypeRunHud>().ShowResult(new RunResult(
                new RunRequest(first, 4, 1, RunMode.NormalField, "", 0, 15),
                RunEndReason.TimeExpired, RunSettlementPolicy.Eligible, 15, 1, 999, 999));
            Assert.That(_view.IsVisible, Is.False, "A stale result must not cover the new run.");
            combat.AdvanceTime(30f);
            yield return new WaitForSecondsRealtime(1.4f);
            _view.CompleteReveal();
            yield return WaitForReady();
            Get<Button>(_view, "upgradeButton").onClick.Invoke();
            Assert.That(Object.FindFirstObjectByType<AppUiRootController>().CurrentScreen, Is.EqualTo(UiScreenId.Trait));
            Assert.That(_view.IsVisible, Is.False);
            _hunt.BeginPrototypeRun();
            Assert.That(combat.IsRunActive, Is.True);
            _hunt.ResetPrototypeRun();
            Assert.That(_view.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator ResultPopsWithoutDimAndTransparentBackdropStillSkips()
        {
            Object.FindFirstObjectByType<AppUiRootController>().EnterCombat();
            Assert.That(Get<float>(_view, "resultPopupSeconds"), Is.GreaterThan(0f), "The Main scene needs a visible popup duration.");
            Set(_view, "popupSeconds", 0.05f);
            Set(_view, "holdSeconds", 0f);
            Set(_view, "dismissSeconds", 0.05f);
            Set(_view, "resultPopupSeconds", 0.6f);
            Set(_view, "countSeconds", 2f);
            Time.timeScale = 0f;
            _view.Show(new RunResult(new RunRequest("result-popup-without-dim", 15), RunEndReason.TimeExpired,
                RunSettlementPolicy.Eligible, 15, 1, 123, 42));

            CanvasGroup backdrop = Get<CanvasGroup>(_view, "backdrop");
            CanvasGroup result = Get<CanvasGroup>(_view, "resultUI");
            Assert.That(backdrop.alpha, Is.Zero, "The timeout must not dim the battlefield.");
            yield return WaitFor(() => result.gameObject.activeSelf);
            Assert.That(result.transform.localScale.x, Is.LessThan(1f));
            yield return WaitFor(() => result.transform.localScale.x > 0.87f);
            Assert.That(result.transform.localScale.x, Is.LessThan(1f), "The result should animate through intermediate scales.");
            Assert.That(backdrop.alpha, Is.Zero, "The result must not dim the battlefield.");

            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(5f, 5f),
                button = PointerEventData.InputButton.Left
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject, Is.EqualTo(backdrop.gameObject),
                "The transparent backdrop must still block the underlying HUD and accept skip clicks.");
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(result.transform.localScale, Is.EqualTo(Vector3.one), "Skipping must settle the popup immediately.");
            Assert.That(Amount("garnet"), Is.EqualTo("+123"));
            yield return WaitForReady();
            Assert.That(backdrop.alpha, Is.Zero);
            if (System.Environment.GetEnvironmentVariable("CURSOR_HUNTER_RESULT_CAPTURE") == "1")
                yield return CaptureResult(1920, 1080, "/tmp/cursor-result-no-dim.png");
        }

        [UnityTest]
        public IEnumerator EmptyResultStillPopsBeforeActions()
        {
            Object.FindFirstObjectByType<AppUiRootController>().EnterCombat();
            Set(_view, "popupSeconds", 0.05f);
            Set(_view, "holdSeconds", 0f);
            Set(_view, "dismissSeconds", 0.05f);
            Set(_view, "resultPopupSeconds", 0.4f);
            _view.Show(new RunResult(new RunRequest("empty-result-popup", 15), RunEndReason.TimeExpired,
                RunSettlementPolicy.Eligible, 15, 0, 0, 0));
            CanvasGroup result = Get<CanvasGroup>(_view, "resultUI");
            yield return WaitFor(() => result.gameObject.activeSelf);
            Assert.That(result.transform.localScale.x, Is.LessThan(1f));
            Assert.That(Get<CanvasGroup>(_view, "actions").interactable, Is.False);
            yield return WaitForReady();
            Assert.That(result.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Get<CanvasGroup>(_view, "backdrop").alpha, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SixCurrencyRevealIsExactAndCancellationNeverReopensOldResults()
        {
            Object.FindFirstObjectByType<AppUiRootController>().EnterCombat();
            string[] ids = { "garnet", "topaz", "amethyst", "sapphire", "diamond", "dragon" };
            long[] values = { 768864, 203136, 134592, 87552, 80064, 37632 };
            var rewards = ids.Select((id, i) => new ResourceRewardSnapshot("gem." + id, values[i])).ToArray();
            var result = new RunResult(new RunRequest("result-visual-test", 30), RunEndReason.TimeExpired,
                RunSettlementPolicy.Eligible, 30, 128, values[0], 4238050, rewards);
            _view.Show(result);
            _view.Show(result);
            yield return WaitForReady();
            Assert.That(_view.IsReady, Is.True);
            for (int i = 0; i < ids.Length; i++)
                Assert.That(Amount(ids[i]), Is.EqualTo("+" + values[i].ToString("N0", CultureInfo.InvariantCulture)));
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(x => x.name == "PrototypeResultPanel"), Is.False);
            if (System.Environment.GetEnvironmentVariable("CURSOR_HUNTER_RESULT_CAPTURE") == "1")
            {
                yield return CaptureResult(1920, 1080, "/tmp/cursor-hunter-battle-result.png");
                yield return CaptureResult(1280, 960, "/tmp/cursor-hunter-battle-result-4x3.png");
            }
            _view.Hide();
            _view.Show(new RunResult(new RunRequest("result-empty-test", 15), RunEndReason.UserExit,
                RunSettlementPolicy.Eligible, 2, 0, 0, 0));
            yield return WaitForReady();
            _view.CompleteReveal();
            Assert.That(VisibleGems(), Is.Empty, "Do not expose icons, names or slots for unearned gemstones.");
            Assert.That(Get<TMP_Text>(_view, "emptyRewards").gameObject.activeInHierarchy, Is.True);
            foreach (string id in ids) Assert.That(Row(id).gameObject.activeSelf, Is.False);
            Assert.That(_view.IsReady, Is.True, "An empty result must still allow navigation.");
            _view.Hide();
            _view.Show(result);
            _hunt.ReturnToMainMenu();
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(_view.IsVisible, Is.False);
            Assert.That(Get<GameObject>(_view, "timeOutPanel").activeSelf, Is.False);
            Assert.That(Get<CanvasGroup>(_view, "resultUI").gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator OnlyEarnedGemstonesAppearWithoutGapsOrHintsOfHiddenTypes()
        {
            Object.FindFirstObjectByType<AppUiRootController>().EnterCombat();
            _view.Show(new RunResult(new RunRequest("result-secret-test", 15), RunEndReason.TimeExpired,
                RunSettlementPolicy.Eligible, 15, 12, 0, 12540, new[] {
                    new ResourceRewardSnapshot("gem.dragon", 48),
                    new ResourceRewardSnapshot("gem.amethyst", 17) }));
            Assert.That(Row("garnet").gameObject.activeSelf, Is.False, "Hidden before the very first result frame.");
            yield return WaitForReady();
            Assert.That(_view.IsReady, Is.True);
            Assert.That(VisibleGems(), Is.EqualTo(new[] { "Reward_amethyst", "Reward_dragon" }));
            Assert.That(Amount("amethyst"), Is.EqualTo("+17"));
            Assert.That(Amount("dragon"), Is.EqualTo("+48"));
            Assert.That(Row("amethyst").anchoredPosition.y - Row("dragon").anchoredPosition.y,
                Is.EqualTo(Row("amethyst").rect.height + 6f).Within(0.1f), "Hidden types must not leave gaps.");
            Assert.That(Get<TMP_Text>(_view, "emptyRewards").gameObject.activeSelf, Is.False);
            if (System.Environment.GetEnvironmentVariable("CURSOR_HUNTER_RESULT_CAPTURE") == "1")
            {
                yield return CaptureResult(1920, 1080, "/tmp/cursor-hunter-result-plain.png");
                yield return CaptureResult(1280, 960, "/tmp/cursor-hunter-result-plain-4x3.png");
            }
            _view.Hide();
            _view.Show(new RunResult(new RunRequest("result-now-only-garnet", 15), RunEndReason.TimeExpired,
                RunSettlementPolicy.Eligible, 15, 1, 3, 42));
            yield return WaitForReady();
            Assert.That(VisibleGems(), Is.EqualTo(new[] { "Reward_garnet" }), "Previously earned types remain secret when absent in this run.");
            Assert.That(Amount("garnet"), Is.EqualTo("+3"));
            Assert.That(_view.IsReady, Is.True);
            if (System.Environment.GetEnvironmentVariable("CURSOR_HUNTER_RESULT_CAPTURE") == "1")
                yield return CaptureResult(1920, 1080, "/tmp/cursor-hunter-result-plain-single.png");
        }

        [UnityTest]
        public IEnumerator RewardsResizeForZeroToSixTypesAndAlwaysEndWithExperience()
        {
            Object.FindFirstObjectByType<AppUiRootController>().EnterCombat();
            Set(_view, "reduceMotion", true);
            Set(_view, "popupSeconds", 0.05f);
            Set(_view, "holdSeconds", 0f);
            Set(_view, "dismissSeconds", 0.05f);
            string oldLanguage = LocalizationCatalog.Language;
            LocalizationCatalog.SetLanguage("ko");
            string[] ids = { "garnet", "topaz", "amethyst", "sapphire", "diamond", "dragon" };
            long[] amounts = { 250, 203136, 134592, 87552, 80064, 37632 };
            float singleFont = 0f, singleIcon = 0f;
            try
            {
                for (int count = 0; count <= ids.Length; count++)
                {
                    var earned = ids.Take(count).Select((id, i) => new ResourceRewardSnapshot("gem." + id, amounts[i])).ToArray();
                    long experience = count == 0 ? 0L : count == 1 ? 120L : 1250L;
                    _view.Show(new RunResult(new RunRequest("result-adaptive-" + count, 30), RunEndReason.TimeExpired,
                        RunSettlementPolicy.Eligible, 30, 1, count == 0 ? 0L : amounts[0], 1L, earned, null, experience));
                    yield return WaitForReady();
                    Canvas.ForceUpdateCanvases();
                    RectTransform rows = Get<RectTransform>(_view, "rewardRows");
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rows);
                    Assert.That(VisibleGems(), Is.EqualTo(ids.Take(count).Select(id => "Reward_" + id).ToArray()));
                    Assert.That(Get<TMP_Text>(_view, "experienceAmount").text,
                        Is.EqualTo("+" + experience.ToString("N0", CultureInfo.InvariantCulture)));
                    Assert.That(Get<TMP_Text>(_view, "experienceLabel").text, Is.EqualTo("획득 경험치"));
                    Assert.That(Get<TMP_Text>(_view, "emptyRewards").gameObject.activeSelf, Is.EqualTo(count == 0));
                    Assert.That(Get<CanvasGroup>(_view, "resultUI").transform.Find("StatsHeading"), Is.Null);
                    RectTransform experienceIcon = Get<RectTransform>(_view, "experienceIcon");
                    Image experienceImage = experienceIcon.GetComponent<Image>();
                    Assert.That(experienceImage.sprite.name, Is.EqualTo("UI_Play_Exp_01_Purple"));
                    Assert.That(experienceImage.preserveAspect, Is.True);
                    Assert.That(experienceIcon.Find("BadgeText"), Is.Null,
                        "The EXP lettering is part of the supplied icon.");
                    Assert.That(experienceIcon.rect.height, Is.LessThanOrEqualTo(64f));
                    if (count > 0)
                    {
                        foreach (string id in ids.Take(count))
                        {
                            RectTransform gemstone = Row(id).Find("GemIcon").GetComponent<RectTransform>();
                            RectTransform gemstoneName = Row(id).Find("Name").GetComponent<RectTransform>();
                            RectTransform gemstoneAmount = Row(id).Find("Amount").GetComponent<RectTransform>();
                            Assert.That(gemstone.TransformPoint(gemstone.rect.center).x,
                                Is.EqualTo(experienceIcon.TransformPoint(experienceIcon.rect.center).x).Within(0.01f),
                                "All reward icons must share a column at every adaptive size.");
                            Assert.That(gemstoneName.TransformPoint(gemstoneName.rect.min).x,
                                Is.EqualTo(Get<TMP_Text>(_view, "experienceLabel").rectTransform.TransformPoint(
                                    Get<TMP_Text>(_view, "experienceLabel").rectTransform.rect.min).x).Within(0.01f),
                                "Reward and experience labels must share a column.");
                            Assert.That(gemstoneAmount.TransformPoint(gemstoneAmount.rect.max).x,
                                Is.EqualTo(Get<TMP_Text>(_view, "experienceAmount").rectTransform.TransformPoint(
                                    Get<TMP_Text>(_view, "experienceAmount").rectTransform.rect.max).x).Within(0.01f),
                                "Reward and experience amounts must share their right edge.");
                        }
                        var lastCorners = new Vector3[4];
                        var experienceCorners = new Vector3[4];
                        Row(ids[count - 1]).GetWorldCorners(lastCorners);
                        Get<TMP_Text>(_view, "experienceLabel").rectTransform.parent.GetComponent<RectTransform>().GetWorldCorners(experienceCorners);
                        Assert.That(lastCorners[0].y, Is.GreaterThan(experienceCorners[2].y),
                            "Experience must remain below every earned gemstone.");
                    }
                    if (count == 1)
                    {
                        singleFont = Row("garnet").Find("Name").GetComponent<TMP_Text>().fontSizeMax;
                        singleIcon = Row("garnet").Find("GemIcon").GetComponent<RectTransform>().rect.width;
                        if (System.Environment.GetEnvironmentVariable("CURSOR_HUNTER_RESULT_CAPTURE") == "1")
                        {
                            yield return new WaitForSecondsRealtime(0.2f);
                            yield return CaptureResult(1920, 1080, "/tmp/cursor-result-ui-one.png");
                        }
                    }
                    if (count == 6)
                    {
                        Assert.That(Row("garnet").Find("Name").GetComponent<TMP_Text>().fontSizeMax, Is.LessThan(singleFont));
                        Assert.That(Row("garnet").Find("GemIcon").GetComponent<RectTransform>().rect.width, Is.LessThan(singleIcon));
                        if (System.Environment.GetEnvironmentVariable("CURSOR_HUNTER_RESULT_CAPTURE") == "1")
                        {
                            yield return new WaitForSecondsRealtime(0.2f);
                            yield return CaptureResult(1920, 1080, "/tmp/cursor-result-ui-six.png");
                            yield return CaptureResult(1280, 960, "/tmp/cursor-result-ui-six-4x3.png");
                        }
                    }
                }

                LocalizationCatalog.SetLanguage("en");
                string[] englishNames = { "Garnet", "Topaz", "Amethyst", "Sapphire", "Diamond", "Dragon Gem" };
                yield return WaitFor(() => Get<TMP_Text>(_view, "rewardHeading").text == "Rewards Earned" &&
                    Get<TMP_Text>(_view, "experienceLabel").text == "Experience Earned" &&
                    Get<Button>(_view, "upgradeButton").GetComponentInChildren<TMP_Text>().text ==
                        (_hunt.IsTraitScreenConnectedToCombat ? "Upgrade" : "To Lobby") &&
                    ids.Select((id, i) => Row(id).Find("Name").GetComponent<TMP_Text>().text == englishNames[i]).All(x => x));
                Assert.That(Get<TMP_Text>(_view, "rewardHeading").text, Is.EqualTo("Rewards Earned"));
                Assert.That(Get<TMP_Text>(_view, "experienceLabel").text, Is.EqualTo("Experience Earned"));
                Assert.That(Row("dragon").Find("Name").GetComponent<TMP_Text>().text, Is.EqualTo("Dragon Gem"));
                if (System.Environment.GetEnvironmentVariable("CURSOR_HUNTER_RESULT_CAPTURE") == "1")
                    yield return CaptureResult(1280, 960, "/tmp/cursor-result-ui-six-en-4x3.png");

                var largest = new[] { new ResourceRewardSnapshot("gem.garnet", long.MaxValue) };
                _view.Show(new RunResult(new RunRequest("result-int64", 30), RunEndReason.TimeExpired,
                    RunSettlementPolicy.Eligible, 30, 1, long.MaxValue, 1L, largest, null, long.MaxValue));
                yield return WaitForReady();
                string maximum = "+" + long.MaxValue.ToString("N0", CultureInfo.InvariantCulture);
                Assert.That(Amount("garnet"), Is.EqualTo(maximum));
                Assert.That(Get<TMP_Text>(_view, "experienceAmount").text, Is.EqualTo(maximum));
                Canvas.ForceUpdateCanvases();
                foreach (TMP_Text value in new[] { Row("garnet").Find("Amount").GetComponent<TMP_Text>(), Get<TMP_Text>(_view, "experienceAmount") })
                {
                    value.ForceMeshUpdate();
                    Assert.That(value.isTextTruncated, Is.False, "The full Int64 value must remain readable.");
                }
                Assert.That(Get<TMP_Text>(_view, "title").font.HasCharacter('전', true, true), Is.True,
                    "The result font must support Korean glyphs.");
            }
            finally
            {
                LocalizationCatalog.SetLanguage(oldLanguage);
            }
        }

        [UnityTest]
        public IEnumerator ResultUsesStringTableAndFormatsBossStageAcrossLanguages()
        {
            Object.FindFirstObjectByType<AppUiRootController>().EnterCombat();
            Set(_view, "reduceMotion", true);
            Set(_view, "popupSeconds", 0.05f);
            Set(_view, "holdSeconds", 0f);
            Set(_view, "dismissSeconds", 0.05f);
            string previousLanguage = LocalizationCatalog.Language;
            UnityEngine.Localization.Tables.StringTableEntry heading = null;
            string previousHeading = null;
            try
            {
                LocalizationCatalog.SetLanguage("ko");
                var request = new RunRequest(new RunId("localized-boss-result"), 1, 1,
                    RunMode.Boss, "boss.test", 0, 30, 3);
                _view.Show(new RunResult(request, RunEndReason.BossDefeated,
                    RunSettlementPolicy.Eligible, 30, 1, 100, 42));
                yield return WaitForReady();
                yield return WaitFor(() => Get<TMP_Text>(_view, "title").text == "보스 3단계 · 토벌 성공");

                LocalizationCatalog.SetLanguage("en-US");
                yield return WaitFor(() => Get<TMP_Text>(_view, "title").text == "Boss Stage 3 · Victory");
                Assert.That(Get<TMP_Text>(_view, "experienceLabel").text, Is.EqualTo("Experience Earned"));

                LocalizationCatalog.SetLanguage("fr");
                yield return WaitFor(() => Get<TMP_Text>(_view, "title").text == "보스 3단계 · 토벌 성공");

                LocalizationCatalog.SetLanguage("en");
                var operation = LocalizationSettings.StringDatabase.GetTableAsync("ResultUI",
                    LocalizationSettings.AvailableLocales.GetLocale("en"));
                yield return operation;
                Assert.That(operation.Result, Is.Not.Null);
                heading = operation.Result.GetEntry("ui.result.rewards.heading");
                previousHeading = heading.Value;
                heading.Value = "Edited result table";
                _view.Show(new RunResult(new RunRequest("localized-table-edit", 15), RunEndReason.TimeExpired,
                    RunSettlementPolicy.Eligible, 15, 1, 20, 42));
                yield return WaitForReady();
                yield return WaitFor(() => Get<TMP_Text>(_view, "rewardHeading").text == "Edited result table");
                Assert.That(Get<TMP_Text>(_view, "experienceAmount").text, Is.EqualTo("+0"));
            }
            finally
            {
                if (heading != null) heading.Value = previousHeading;
                LocalizationCatalog.SetLanguage(previousLanguage);
            }
        }

        [UnityTest]
        public IEnumerator ExperienceOnlyCountsUpAndCanBeSkipped()
        {
            Object.FindFirstObjectByType<AppUiRootController>().EnterCombat();
            Set(_view, "popupSeconds", 0.05f);
            Set(_view, "holdSeconds", 0f);
            Set(_view, "dismissSeconds", 0.05f);
            Set(_view, "countSeconds", 2f);
            _view.Show(new RunResult(new RunRequest("experience-only-result", 15), RunEndReason.TimeExpired,
                RunSettlementPolicy.Eligible, 15, 0, 0, 0, null, null, 1250));
            yield return WaitFor(() => Get<CanvasGroup>(_view, "resultUI").gameObject.activeSelf);
            Assert.That(_view.IsReady, Is.False);
            Assert.That(Get<TMP_Text>(_view, "experienceAmount").text, Is.Not.EqualTo("+1,250"));
            _view.CompleteReveal();
            Assert.That(Get<TMP_Text>(_view, "experienceAmount").text, Is.EqualTo("+1,250"));
            yield return WaitForReady();
            Assert.That(Get<TMP_Text>(_view, "hint").gameObject.activeSelf, Is.False);
            _view.Show(new RunResult(new RunRequest("experience-only-result-next", 15), RunEndReason.TimeExpired,
                RunSettlementPolicy.Eligible, 15, 0, 0, 0, null, null, 1));
            Assert.That(Get<TMP_Text>(_view, "hint").gameObject.activeSelf, Is.True,
                "The skip hint must return for the next result.");
        }

        [UnityTest]
        public IEnumerator RelicDiscoveryStartsWithResultAndRemainsModalUntilConfirmed()
        {
            Object.FindFirstObjectByType<AppUiRootController>().EnterCombat();
            Set(_view, "popupSeconds", 0.05f);
            Set(_view, "holdSeconds", 0f);
            Set(_view, "dismissSeconds", 0.05f);
            Set(_view, "countSeconds", 0.8f);
            Time.timeScale = 0f;
            const string languagePreference = "CursorHunter.Language";
            bool hadLanguagePreference = PlayerPrefs.HasKey(languagePreference);
            string savedLanguagePreference = PlayerPrefs.GetString(languagePreference, "ko");
            string oldLanguage = LocalizationCatalog.Language;
            var oldLocale = LocalizationSettings.SelectedLocale;
            var korean = LocalizationSettings.AvailableLocales.GetLocale("ko");
            if (korean != null) LocalizationSettings.SelectedLocale = korean;
            LocalizationCatalog.SetLanguage("ko");
            _view.Show(new RunResult(new RunRequest("styled-relic-result", 15), RunEndReason.TimeExpired,
                RunSettlementPolicy.Eligible, 15, 1, 5, 1, null, new[] { "relic.red_potion" }, 7));
            GameObject overlay = Get<GameObject>(_view, "overlay");
            try
            {
                CanvasGroup result = Get<CanvasGroup>(_view, "resultUI");
                yield return WaitFor(() => result.gameObject.activeSelf);
                Transform celebration = overlay.transform.Find("RelicDiscoveryCelebration");
                Assert.That(celebration, Is.Not.Null, "Discovery must start in the same frame as the result.");
                Transform panel = celebration.Find("RelicDiscoveryPanel");
                Transform confirm = panel.Find("RelicDiscoveryConfirm");
                TMP_Text heading = panel.Find("RelicDiscoveryTitle").GetComponent<TMP_Text>();
                TMP_Text relicName = panel.Find("RelicAwardViewport/RelicAwardContent/RelicAward_relic.red_potion/Name")
                    .GetComponent<TMP_Text>();
#if UNITY_EDITOR
                TMP_FontAsset expectedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/CursorHunter/UI/font/NanumSquareRound-Bold SDF.asset");
                Assert.That(expectedFont, Is.Not.Null);
                Assert.That(heading.font, Is.EqualTo(expectedFont));
                Assert.That(relicName.font, Is.EqualTo(expectedFont));
#endif
                RectTransform rays = (RectTransform)panel.Find("RelicDiscoveryRays");
                Assert.That(rays.GetComponent<RelicDiscoveryRays>(), Is.Not.Null);
                Assert.That(panel.Find("RelicAwardViewport/RelicAwardContent/RelicAward_relic.red_potion/Icon"), Is.Not.Null);
                Assert.That(heading.text, Is.EqualTo("유물 발견!"));
                Assert.That(confirm.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("OK"));
                Assert.That(celebration.GetComponent<Image>().color.a, Is.GreaterThan(0.5f));
                Assert.That(result.alpha, Is.EqualTo(1f), "The result stays visible behind the discovery.");
                Assert.That(_view.IsReady, Is.False);
                Assert.That(Get<CanvasGroup>(_view, "actions").interactable, Is.False);
                Assert.That(Amount("garnet"), Is.Not.EqualTo("+5"), "Reward counting begins behind the modal.");
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(rays.localEulerAngles.z, Is.GreaterThan(0f), "The rays animate while game time is paused.");
                Canvas.ForceUpdateCanvases();
                var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(5f, 5f) };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits, Is.Not.Empty);
                Assert.That(hits[0].gameObject, Is.EqualTo(celebration.gameObject),
                    "The darkened discovery must block input to the result.");
                yield return WaitFor(() => Amount("garnet") == "+5");
                Assert.That(_view.IsReady, Is.False, "Finishing the count cannot dismiss the discovery.");
                if (System.Environment.GetEnvironmentVariable("CURSOR_HUNTER_RESULT_CAPTURE") == "1")
                {
                    yield return CaptureResult(1920, 1080, "/tmp/cursor-result-relic-1920.png");
                    yield return CaptureResult(1440, 1080, "/tmp/cursor-result-relic-1440.png");
                }
                var english = LocalizationSettings.AvailableLocales.GetLocale("en");
                Assert.That(english, Is.Not.Null);
                LocalizationSettings.SelectedLocale = english;
                yield return WaitFor(() => heading.text == "Relic Discovered!" && relicName.text == "Red Potion");
#if UNITY_EDITOR
                Assert.That(heading.font, Is.EqualTo(expectedFont));
                Assert.That(relicName.font, Is.EqualTo(expectedFont));
#endif
                if (System.Environment.GetEnvironmentVariable("CURSOR_HUNTER_RESULT_CAPTURE") == "1")
                    yield return CaptureResult(1920, 1080, "/tmp/cursor-result-relic-en.png");
                yield return null;
                Canvas.ForceUpdateCanvases();
                Canvas rootCanvas = confirm.GetComponentInParent<Canvas>().rootCanvas;
                Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
                pointer.position = RectTransformUtility.WorldToScreenPoint(eventCamera, confirm.position);
                hits.Clear();
                EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits, Is.Not.Empty, $"No hit at OK center {pointer.position} on {Screen.width}x{Screen.height}.");
                Assert.That(hits[0].gameObject, Is.EqualTo(confirm.gameObject), "OK must receive pointer input.");
                ExecuteEvents.Execute(confirm.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(celebration.gameObject.activeSelf, Is.False, "OK closes the modal immediately.");
                yield return WaitForReady();
                Assert.That(_view.IsReady, Is.True);
                Assert.That(result.alpha, Is.EqualTo(1f));
            }
            finally
            {
                if (oldLocale != null) LocalizationSettings.SelectedLocale = oldLocale;
                LocalizationCatalog.SetLanguage(oldLanguage);
                if (hadLanguagePreference) PlayerPrefs.SetString(languagePreference, savedLanguagePreference);
                else PlayerPrefs.DeleteKey(languagePreference);
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator RelicDiscoveryHandlesMultipleAwardsAndEarlyConfirmation()
        {
            Object.FindFirstObjectByType<AppUiRootController>().EnterCombat();
            Set(_view, "popupSeconds", 0.05f);
            Set(_view, "holdSeconds", 0f);
            Set(_view, "dismissSeconds", 0.05f);
            Set(_view, "countSeconds", 3f);
            _view.Show(new RunResult(new RunRequest("multiple-relic-result", 15), RunEndReason.TimeExpired,
                RunSettlementPolicy.Eligible, 15, 1, 5, 1, null,
                new[] { "relic.red_potion", "relic.blue_potion", "relic.monster_meat", "relic.thief_shoes" }, 7));
            GameObject overlay = Get<GameObject>(_view, "overlay");
            yield return WaitFor(() => Get<CanvasGroup>(_view, "resultUI").gameObject.activeSelf);
            Transform celebration = overlay.transform.Find("RelicDiscoveryCelebration");
            Transform panel = celebration.Find("RelicDiscoveryPanel");
            Transform viewport = panel.Find("RelicAwardViewport");
            Transform content = viewport.Find("RelicAwardContent");
            Assert.That(content.childCount, Is.EqualTo(4));
            ScrollRect scroller = viewport.GetComponent<ScrollRect>();
            Assert.That(scroller.horizontal, Is.True);
            Assert.That(panel.Find("RelicScrollHint"), Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            Assert.That(((RectTransform)content).rect.width, Is.GreaterThan(((RectTransform)viewport).rect.width));
            Assert.That(Amount("garnet"), Is.Not.EqualTo("+5"), "Confirm while counting is still in progress.");
            if (System.Environment.GetEnvironmentVariable("CURSOR_HUNTER_RESULT_CAPTURE") == "1")
            {
                yield return new WaitForSecondsRealtime(0.4f);
                yield return CaptureResult(1920, 1080, "/tmp/cursor-result-relic-multiple.png");
            }
            scroller.horizontalNormalizedPosition = 1f;
            yield return null;
            Assert.That(scroller.horizontalNormalizedPosition, Is.EqualTo(1f).Within(0.01f));
            panel.Find("RelicDiscoveryConfirm").GetComponent<Button>().onClick.Invoke();
            Assert.That(celebration.gameObject.activeSelf, Is.False);
            Assert.That(Amount("garnet"), Is.EqualTo("+5"));
            Assert.That(Get<AudioSource>(_view, "_countTickSource").isPlaying, Is.False);
            yield return WaitForReady();

            Set(_view, "reduceMotion", true);
            _view.Show(new RunResult(new RunRequest("reduced-motion-relic-result", 15), RunEndReason.TimeExpired,
                RunSettlementPolicy.Eligible, 15, 1, 5, 1, null, new[] { "relic.red_potion" }, 7));
            yield return WaitFor(() => Get<CanvasGroup>(_view, "resultUI").gameObject.activeSelf);
            celebration = overlay.transform.Find("RelicDiscoveryCelebration");
            Assert.That(celebration.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(Get<object>(_view, "_relicDiscoverySequence"), Is.Null,
                "Reduced motion must leave the rays static.");
            _view.Hide();
            Assert.That(celebration.gameObject.activeSelf, Is.False, "Hiding must disable the modal immediately.");

            _view.Show(new RunResult(new RunRequest("discarded-relic-result", 15), RunEndReason.TimeExpired,
                RunSettlementPolicy.Discard, 15, 1, 5, 1, null, new[] { "relic.red_potion" }, 7));
            yield return WaitFor(() => Get<CanvasGroup>(_view, "resultUI").gameObject.activeSelf);
            Assert.That(overlay.transform.Find("RelicDiscoveryCelebration"), Is.Null,
                "Discarded rewards must not show a relic discovery.");
        }

        [UnityTest]
        public IEnumerator RunRelicsAlignWithGarnetAndUseInspectorIconSize()
        {
            Set(_hunt, "connectTraitScreenToCombat", false);
            CombatBalanceSettings runSettings = _hunt.CaptureCombatBalanceSettings();
            runSettings.UsePlayerProgression = false;
            runSettings.DurationSeconds = 30f;
            foreach (SkillBalanceSettings skill in runSettings.Skills) skill.Enabled = false;
            foreach (MonsterBalanceSettings monster in runSettings.Monsters)
                monster.Enabled = monster.MonsterId == "monster.01";
            Assert.That(_hunt.TrySetCombatBalanceSettings(runSettings, out string error), Is.True, error);
            _hunt.BeginPrototypeRun();
            var combat = Object.FindFirstObjectByType<CombatRunController>();
            var spawner = Object.FindFirstObjectByType<MonsterSpawner>();
            var list = Object.FindFirstObjectByType<CombatGemstoneList>(FindObjectsInactive.Include);
            Assert.That(combat.IsRunActive, Is.True);

            Transform relicRoot = list.transform.Find("RunRelicCollection");
            Assert.That(relicRoot, Is.Not.Null, "The relic collection must be authored in the scene.");
            var settings = relicRoot.GetComponent<RunRelicCollectionSettings>();
            Assert.That(settings, Is.Not.Null);
            Set(settings, "iconSize", 76f);
            list.ShowRunning(combat, spawner);
            typeof(CombatGemstoneList).GetMethod("HandleRewardResolved", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(list, new object[] { new CombatRewardResolved(combat.CurrentRunId, 1, Vector3.zero,
                    false, 0L, string.Empty, 0L, "relic.red_potion", 0L) });
            Transform relicIcon = relicRoot.Find("RelicIcons/CollectedRelic_relic.red_potion");
            Assert.That(relicIcon, Is.Not.Null);
            yield return WaitFor(() => relicIcon.GetComponent<Image>().enabled);

            Canvas.ForceUpdateCanvases();
            list.ShowRunning(combat, spawner);
            var parent = (RectTransform)list.transform;
            var garnet = (RectTransform)list.transform.Find("GemStone_Garnet");
            var viewport = (RectTransform)relicRoot;
            Vector3 garnetRight = parent.InverseTransformPoint(garnet.TransformPoint(
                new Vector3(garnet.rect.xMax, garnet.rect.center.y, 0f)));
            Vector3 relicLeft = parent.InverseTransformPoint(viewport.TransformPoint(
                new Vector3(viewport.rect.xMin, viewport.rect.center.y, 0f)));
            Vector3 relicRight = parent.InverseTransformPoint(viewport.TransformPoint(
                new Vector3(viewport.rect.xMax, viewport.rect.center.y, 0f)));
            RectTransform timer = settings.RightBoundary;
            Assert.That(timer, Is.Not.Null);
            Vector3 timerLeft = parent.InverseTransformPoint(timer.TransformPoint(
                new Vector3(timer.rect.xMin, timer.rect.center.y, 0f)));
            Assert.That(relicLeft.y, Is.EqualTo(garnetRight.y).Within(0.1f));
            Assert.That(relicLeft.x, Is.GreaterThan(garnetRight.x));
            Assert.That(relicRight.x, Is.LessThan(timerLeft.x));
            Assert.That(((RectTransform)relicIcon).sizeDelta, Is.EqualTo(new Vector2(76f, 76f)));

            Set(settings, "iconSize", 96f);
            list.ShowRunning(combat, spawner);
            Assert.That(((RectTransform)relicIcon).sizeDelta, Is.EqualTo(new Vector2(96f, 96f)),
                "Changing the Inspector size must update existing icons as well as new ones.");
            if (System.Environment.GetEnvironmentVariable("CURSOR_HUNTER_RESULT_CAPTURE") == "1")
            {
                yield return CaptureResult(1920, 1080, "/tmp/cursor-relic-hud-1920.png");
                yield return CaptureResult(1440, 1080, "/tmp/cursor-relic-hud-1440.png");
            }
        }

        [UnityTest]
        public IEnumerator ButtonsPopInOrderAfterRewardsEvenWhileTimeIsPaused()
        {
            Object.FindFirstObjectByType<AppUiRootController>().EnterCombat();
            Set(_view, "holdSeconds", 0f);
            Set(_view, "countSeconds", 0.4f);
            Set(_view, "buttonPopupSeconds", 0.35f);
            Time.timeScale = 0f;
            Button upgrade = Get<Button>(_view, "upgradeButton");
            Button retry = Get<Button>(_view, "retryButton");
            CanvasGroup actions = Get<CanvasGroup>(_view, "actions");
            _view.Show(new RunResult(new RunRequest("result-button-order", 15), RunEndReason.TimeExpired,
                RunSettlementPolicy.Eligible, 15, 1, 123, 42));

            yield return WaitFor(() => Get<CanvasGroup>(_view, "resultUI").gameObject.activeSelf);
            Assert.That(actions.alpha, Is.Zero, "Both buttons stay hidden while the reward count animates.");
            Assert.That(upgrade.transform.localScale, Is.EqualTo(Vector3.zero));
            Assert.That(retry.transform.localScale, Is.EqualTo(Vector3.zero));
            AudioSource countTick = Get<AudioSource>(_view, "_countTickSource");
            yield return WaitFor(() => countTick.isPlaying);
            Assert.That(Amount("garnet"), Is.Not.EqualTo("+123"),
                "The reward count should still be increasing while its tick sound plays.");
            yield return WaitFor(() => upgrade.transform.localScale.x > 0f);
            Assert.That(Amount("garnet"), Is.EqualTo("+123"), "The main result must finish first.");
            Assert.That(countTick.isPlaying, Is.False,
                "The count sound should stop only after the reward has reached its exact final amount.");
            Assert.That(retry.transform.localScale, Is.EqualTo(Vector3.zero));
            Assert.That(actions.interactable, Is.False);
            Assert.That(upgrade.image.canvasRenderer.GetColor().a, Is.EqualTo(1f).Within(0.01f),
                "The first button must be opaque while it pops in and is not yet clickable.");
            Assert.That(_view.IsReady, Is.False);
            Vector3 upgradeScale = upgrade.transform.localScale;
            _view.CompleteReveal(); _view.CompleteReveal();
            Assert.That(upgrade.transform.localScale, Is.EqualTo(upgradeScale), "Extra clicks cannot skip or restart the button sequence.");
            upgrade.onClick.Invoke(); retry.onClick.Invoke();
            Assert.That(_view.IsVisible, Is.True, "Navigation remains locked throughout the button sequence.");

            yield return WaitFor(() => retry.transform.localScale.x > 0f);
            Assert.That(upgrade.transform.localScale, Is.EqualTo(Vector3.one), "Upgrade must fully settle before retry starts.");
            Assert.That(actions.interactable, Is.False);
            Assert.That(upgrade.image.canvasRenderer.GetColor().a, Is.EqualTo(1f).Within(0.01f));
            Assert.That(retry.image.canvasRenderer.GetColor().a, Is.EqualTo(1f).Within(0.01f),
                "The second button must also be opaque throughout its popup.");
            Assert.That(_view.IsReady, Is.False);
            yield return WaitForReady();
            Assert.That(upgrade.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(retry.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(actions.interactable, Is.True);
            Assert.That(Get<TMP_Text>(_view, "hint").gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator HidingDuringButtonPopupCancelsItAndReducedMotionStaysUsable()
        {
            Object.FindFirstObjectByType<AppUiRootController>().EnterCombat();
            Set(_view, "holdSeconds", 0f);
            Set(_view, "buttonPopupSeconds", 0.35f);
            _view.Show(new RunResult(new RunRequest("result-button-cancel", 15), RunEndReason.TimeExpired,
                RunSettlementPolicy.Eligible, 15, 1, 123, 42));
            yield return WaitFor(() => Get<Button>(_view, "upgradeButton").transform.localScale.x > 0f);
            _view.Hide();
            Assert.That(Get<object>(_view, "_buttonSequence"), Is.Null);
            Assert.That(Get<Button>(_view, "upgradeButton").transform.localScale, Is.EqualTo(Vector3.zero));
            Assert.That(Get<Button>(_view, "retryButton").transform.localScale, Is.EqualTo(Vector3.zero));

            Set(_view, "countSeconds", 2f);
            _view.Show(new RunResult(new RunRequest("result-after-cancel", 15), RunEndReason.TimeExpired,
                RunSettlementPolicy.Eligible, 15, 1, 7, 42));
            yield return new WaitForSecondsRealtime(0.9f);
            Assert.That(_view.IsReady, Is.False, "The old tween must not complete the new result.");
            Assert.That(Get<CanvasGroup>(_view, "actions").alpha, Is.Zero);
            Assert.That(Get<Button>(_view, "upgradeButton").transform.localScale, Is.EqualTo(Vector3.zero));
            Assert.That(Get<Button>(_view, "retryButton").transform.localScale, Is.EqualTo(Vector3.zero));
            _view.CompleteReveal();
            Assert.That(_view.IsReady, Is.False, "Skipping the count still plays the button popups.");
            yield return WaitForReady();
            Assert.That(Amount("garnet"), Is.EqualTo("+7"));

            Set(_view, "reduceMotion", true);
            _view.Show(new RunResult(new RunRequest("result-buttons-reduced-motion", 15), RunEndReason.TimeExpired,
                RunSettlementPolicy.Eligible, 15, 1, 9, 42));
            yield return WaitFor(() => Get<CanvasGroup>(_view, "resultUI").gameObject.activeSelf);
            Assert.That(Get<CanvasGroup>(_view, "resultUI").transform.localScale, Is.EqualTo(Vector3.one));
            yield return WaitForReady();
            Assert.That(Get<object>(_view, "_buttonSequence"), Is.Null);
            Assert.That(Get<Button>(_view, "upgradeButton").transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Get<Button>(_view, "retryButton").transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Get<CanvasGroup>(_view, "actions").interactable, Is.True);
            Assert.That(Amount("garnet"), Is.EqualTo("+9"));
        }

        private IEnumerator WaitForReady() => WaitFor(() => _view.IsReady);

        private static IEnumerator WaitFor(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Result animation did not reach its expected state within five seconds.");
        }

        private IEnumerator CaptureResult(int width, int height, string path)
        {
            Camera camera = Camera.main;
            Canvas canvas = Get<CanvasGroup>(_view, "resultUI").GetComponentInParent<Canvas>().rootCanvas;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousDistance = canvas.planeDistance;
            RenderTexture previousTarget = camera.targetTexture;
            var target = new RenderTexture(width, height, 24);
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (TMP_Text label in canvas.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture previousActive = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                RenderTexture.active = previousActive;
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                Object.Destroy(image);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = previousDistance;
                target.Release(); Object.Destroy(target);
            }
        }

        private RectTransform Row(string gem) => (RectTransform)Get<RectTransform>(_view, "rewardRows").Find("Reward_" + gem);
        private string Amount(string gem) => Row(gem).Find("Amount").GetComponent<TMP_Text>().text;
        private string[] VisibleGems() => Get<RectTransform>(_view, "rewardRows").Cast<Transform>()
            .Where(row => row.gameObject.activeInHierarchy).Select(row => row.name).ToArray();
        private static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    }
}
