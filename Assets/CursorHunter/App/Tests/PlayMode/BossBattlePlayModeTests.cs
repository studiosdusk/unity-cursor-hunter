using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CursorHunter.App.Tests
{
    public sealed class BossBattlePlayModeTests
    {
        private Scene _scene;
        private HuntManager _hunt;
        private CombatRunController _combat;
        private MonsterSpawner _spawner;
        private BattleResultPresentation _result;
        private string _profile;
        private bool _hadProfile;
        private string _language;
        private const string ProfileKey = "cursor_hunter.progression.v1";

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _language = LocalizationCatalog.Language;
            LocalizationCatalog.SetLanguage("ko");
            _hadProfile = PlayerPrefs.HasKey(ProfileKey);
            _profile = PlayerPrefs.GetString(ProfileKey);
            PlayerPrefs.DeleteKey(ProfileKey);
#if UNITY_EDITOR
            _scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            yield return null;
            _hunt = Object.FindFirstObjectByType<HuntManager>();
            _combat = Object.FindFirstObjectByType<CombatRunController>();
            _spawner = Object.FindFirstObjectByType<MonsterSpawner>();
            _result = Object.FindFirstObjectByType<BattleResultPresentation>();
            Set(_hunt, "connectTraitScreenToCombat", false);
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (_hunt != null) _hunt.ResetPrototypeRun();
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
            LocalizationCatalog.SetLanguage(_language);
            if (_hadProfile) PlayerPrefs.SetString(ProfileKey, _profile); else PlayerPrefs.DeleteKey(ProfileKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator ActiveBossTitleRefreshesWhenLanguageChangesAndFallsBackForMissingTranslation()
        {
            BossDefinition boss = Resources.Load<BossDefinition>("BossDefinitions/Boss01");
            string original = JsonUtility.ToJson(boss);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"localizedNames\":[{\"locale\":\"fr\",\"text\":\"Gardien de la foret\"}]}", boss);
                _hunt.BeginBossRun();
                TMP_Text title = Slider().GetComponentsInChildren<TMP_Text>(true).Single(x => x.name == "Text_Title");
                Assert.That(title.text, Is.EqualTo(boss.DefaultDisplayName));
                LocalizationCatalog.SetLanguage("fr-CA");
                Assert.That(title.text, Is.EqualTo("Gardien de la foret"));
                LocalizationCatalog.SetLanguage("ja");
                Assert.That(title.text, Is.EqualTo(boss.DefaultDisplayName));
                LocalizationCatalog.SetLanguage("ko");
                Assert.That(title.text, Is.EqualTo(boss.DefaultDisplayName));
                _hunt.ResetPrototypeRun();
                LocalizationCatalog.SetLanguage("fr");
                _hunt.BeginBossRun();
                Assert.That(title.text, Is.EqualTo("Gardien de la foret"));
                yield return null;
                LogAssert.NoUnexpectedReceived();
            }
            finally { JsonUtility.FromJsonOverwrite(original, boss); }
        }

        [UnityTest]
        public IEnumerator HealthTextPopsOnDamageSettlesWhilePausedAndResetsForANewRun()
        {
            foreach (MonoBehaviour skill in _combat.GetComponentsInChildren<MonoBehaviour>(true))
                if (skill is IRunSkillController) skill.enabled = false;
            var presenter = Object.FindFirstObjectByType<BossHealthPresenter>();
            TMP_Text value = Slider().GetComponentsInChildren<TMP_Text>(true).Single(x => x.name == "Text_Value");
            Vector3 baseScale = value.rectTransform.localScale;
            float peak = Get<float>(presenter, "healthHitScale");
            float duration = Get<float>(presenter, "healthHitDuration");
            float timeScale = Time.timeScale;
            try
            {
                _hunt.BeginBossRun();
                _combat.PauseRun();
                yield return null;
                Assert.That(value.rectTransform.localScale, Is.EqualTo(baseScale), "Showing full HP must not pop.");
                if (Environment.GetEnvironmentVariable("CURSOR_HUNTER_BOSS_HUD_CAPTURE") == "1")
                    yield return Capture("/tmp/cursor-hunter-boss-hud-rest.png");

                Time.timeScale = 0f;
                for (int hit = 0; hit < 3; hit++)
                {
                    _combat.ResumeRun();
                    Assert.That(_combat.TryApplySkillDamage(_combat.CurrentRunId, _spawner.BossTarget, 100), Is.True);
                    _combat.PauseRun();
                    yield return null;
                    Assert.That(value.rectTransform.localScale.x, Is.GreaterThan(baseScale.x));
                    Assert.That(value.rectTransform.localScale.x, Is.LessThanOrEqualTo(baseScale.x * peak + .0001f),
                        "Repeated hits must not accumulate scale.");
                }
                if (Environment.GetEnvironmentVariable("CURSOR_HUNTER_BOSS_HUD_CAPTURE") == "1")
                    yield return Capture("/tmp/cursor-hunter-boss-hud-hit.png");
                yield return new WaitForSecondsRealtime(duration + .05f);
                Assert.That(value.rectTransform.localScale, Is.EqualTo(baseScale), "The pop must settle with game time paused.");
                yield return null;
                Assert.That(value.rectTransform.localScale, Is.EqualTo(baseScale), "Unchanged HP must not retrigger it.");

                _combat.ResumeRun();
                Assert.That(_combat.TryApplySkillDamage(_combat.CurrentRunId, _spawner.BossTarget, 100), Is.True);
                _combat.PauseRun();
                yield return null;
                Assert.That(value.rectTransform.localScale.x, Is.GreaterThan(baseScale.x));
                _hunt.ResetPrototypeRun();
                Assert.That(value.rectTransform.localScale, Is.EqualTo(baseScale));
                _hunt.BeginBossRun();
                Assert.That(value.rectTransform.localScale, Is.EqualTo(baseScale));
                LogAssert.NoUnexpectedReceived();
            }
            finally { Time.timeScale = timeScale; }
        }

        [UnityTest]
        public IEnumerator BossButtonUsesAuthoredSliderAndFailureRetriesSameStageThenOpensUpgrades()
        {
            Slider slider = Slider();
            Assert.That(slider.gameObject.activeSelf, Is.False);
            Button challenge = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(x => x.name == "Button_BOSSHUNT");
            Assert.That(challenge.onClick.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(challenge.onClick.GetPersistentMethodName(0), Is.EqualTo("BeginBossRun"));
            challenge.onClick.Invoke();
            Assert.That(_combat.IsBossRun, Is.True);
            Assert.That(_combat.RemainingSeconds, Is.EqualTo(30).Within(.02));
            Assert.That(_spawner.ActiveSpawnedCount, Is.EqualTo(1));
            Assert.That(slider.gameObject.activeInHierarchy, Is.True);
            Assert.That(slider.interactable, Is.False);
            Assert.That(slider.value, Is.EqualTo(1));
            RunId first = _combat.CurrentRunId;
            Assert.That(_combat.TryApplySkillDamage(first, _spawner.BossTarget, 100), Is.True);
            yield return null;
            Assert.That(slider.value, Is.InRange(.01f, .99f));
            if (Environment.GetEnvironmentVariable("CURSOR_HUNTER_BOSS_CAPTURE") == "1")
                yield return Capture("/tmp/cursor-hunter-boss-combat.png");
            _combat.ResumeRun();
            _combat.AdvanceTime(30);
            Assert.That(_combat.IsRunActive, Is.False);
            Assert.That(slider.gameObject.activeSelf, Is.False);
            Assert.That(_hunt.HighestBossStageCleared, Is.Zero);
            Assert.That(Get<TMP_Text>(_result, "title").text, Does.Contain("토벌 실패"));
            Assert.That(Object.FindFirstObjectByType<GemstoneCollectionController>().ActiveCount, Is.Zero,
                "Failed boss attempts must not launch a clear-reward burst.");
            yield return Ready();
            Assert.That(Get<TMP_Text>(_result, "emptyRewards").gameObject.activeSelf, Is.True);
            Get<Button>(_result, "retryButton").onClick.Invoke();
            Assert.That(_combat.CurrentRunId, Is.Not.EqualTo(first));
            Assert.That(_spawner.BossTarget.MonsterId, Is.EqualTo("boss.01"));
            Assert.That(slider.value, Is.EqualTo(1));
            _combat.AdvanceTime(30);
            yield return Ready();
            Get<Button>(_result, "upgradeButton").onClick.Invoke();
            Assert.That(Object.FindFirstObjectByType<AppUiRootController>().CurrentScreen, Is.EqualTo(UiScreenId.Trait));
        }

        [UnityTest]
        public IEnumerator AllTwentyBossDeathsAnimateAlongsideLootAndDespawnEvenWithGameTimeStopped()
        {
            float timeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                var collection = Object.FindFirstObjectByType<GemstoneCollectionController>();
                for (int stage = 1; stage <= 20; stage++)
                {
                    _hunt.BeginBossStage(stage);
                    MonsterCombatTarget boss = _spawner.BossTarget;
                    boss.transform.position = Vector3.zero;
                    Animator animator = boss.GetComponentInChildren<Animator>();
                    RunId run = _combat.CurrentRunId;
                    KillBoss();
                    Assert.That(_combat.IsRunActive, Is.False, $"Stage {stage}: combat must end immediately.");
                    Assert.That(_spawner.ActiveSpawnedCount, Is.Zero);
                    Assert.That(boss.IsDead, Is.True);
                    Assert.That(boss.IsRegistered, Is.False);
                    Assert.That(boss.gameObject.activeInHierarchy, Is.True);
                    Assert.That(_hunt.IsBossDeathPlaying, Is.True);
                    Assert.That(boss.transform.Find("HitArea").GetComponent<Collider2D>().enabled, Is.False);
                    Assert.That(_combat.TryApplySkillDamage(run, boss, 1), Is.False);
                    Assert.That(collection.ActiveCount, Is.InRange(100, 128), "Loot must start with the death visual.");
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Dead"), Is.True, $"Stage {stage}");
                    float elapsed = _combat.ElapsedSeconds;
                    yield return new WaitForSecondsRealtime(.1f);
                    Assert.That(boss != null, Is.True, $"Stage {stage}: death cannot disappear on the next frame.");
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.InRange(.01f, .99f));
                    Assert.That(_combat.ElapsedSeconds, Is.EqualTo(elapsed));
                    Assert.That(_result.IsVisible, Is.False);
                    if (Environment.GetEnvironmentVariable("CURSOR_HUNTER_BOSS_DEATH_CAPTURE") == "1" &&
                        (stage == 1 || stage == 13))
                        yield return Capture($"/tmp/cursor-hunter-boss-death-loot-{stage:00}.png");
                    float until = Time.realtimeSinceStartup + 3f;
                    while (boss != null && Time.realtimeSinceStartup < until)
                    {
                        Assert.That(_result.IsVisible, Is.False, "The result must not cover an unfinished death animation.");
                        yield return null;
                    }
                    Assert.That(boss == null, Is.True, $"Stage {stage}: remove after the clip, not the normal revival grace period.");
                    Assert.That(_hunt.IsBossDeathPlaying, Is.False);
                }
                yield return Ready();
                LogAssert.NoUnexpectedReceived();
            }
            finally { Time.timeScale = timeScale; }
        }

        [UnityTest]
        public IEnumerator NextAttemptMenuResetAndTimeoutClearBossDeathVisuals()
        {
            _hunt.BeginBossRun();
            MonsterCombatTarget first = _spawner.BossTarget;
            KillBoss();
            _hunt.ContinueFromResult();
            _combat.PauseRun();
            Assert.That(_spawner.BossTarget.MonsterId, Is.EqualTo("boss.02"));
            Assert.That(_hunt.IsBossDeathPlaying, Is.False);
            yield return null;
            Assert.That(first == null, Is.True, "Starting a new attempt must clear the previous death visual.");

            MonsterCombatTarget second = _spawner.BossTarget;
            KillBoss();
            _hunt.ReturnToMainMenu();
            yield return null;
            Assert.That(second == null, Is.True);
            Assert.That(_hunt.IsBossDeathPlaying, Is.False);
            Assert.That(Object.FindFirstObjectByType<GemstoneCollectionController>().ActiveCount, Is.Zero);

            _hunt.BeginBossRun();
            MonsterCombatTarget timedOut = _spawner.BossTarget;
            _combat.ResumeRun();
            _combat.AdvanceTime(30f);
            Assert.That(_hunt.IsBossDeathPlaying, Is.False, "A living timed-out boss must not get a death animation.");
            yield return null;
            Assert.That(timedOut == null, Is.True);

            _hunt.BeginBossRun();
            MonsterCombatTarget reset = _spawner.BossTarget;
            KillBoss();
            _hunt.ResetBossProgress();
            yield return null;
            Assert.That(reset == null, Is.True);
            Assert.That(_hunt.HighestBossStageCleared, Is.Zero);
            Assert.That(_hunt.IsBossDeathPlaying, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ResultWaitsForDeathAfterLootFinishesAndMissingAnimatorHasBoundedCleanup()
        {
            _hunt.BeginBossRun();
            MonsterCombatTarget boss = _spawner.BossTarget;
            KillBoss();
            var collection = Object.FindFirstObjectByType<GemstoneCollectionController>();
            collection.AdvancePresentation(10f);
            Assert.That(collection.IsFinishing, Is.False);
            yield return null;
            Assert.That(_hunt.IsBossDeathPlaying, Is.True);
            Assert.That(_result.IsVisible, Is.False);
            yield return Ready();
            Assert.That(boss == null, Is.True);
            _hunt.ContinueFromResult();
            MonsterCombatTarget missingAnimator = _spawner.BossTarget;
            Object.DestroyImmediate(missingAnimator.GetComponentInChildren<Animator>());
            KillBoss();
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(missingAnimator == null, Is.True);
            Assert.That(_hunt.IsBossDeathPlaying, Is.False);
            yield return Ready();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ClearBurstsTogetherPaysOnceAndNextButtonEntersStageTwo()
        {
            Set(_hunt, "connectTraitScreenToCombat", true);
            _hunt.BeginBossRun();
            object progression = Get<object>(_hunt, "progressionController");
            long[] previousBalances = (long[])Get<long[]>(progression, "_gemstoneBalances").Clone();
            RunResult completed = default;
            Get<RunCoordinator>(_hunt, "_runCoordinator").Completed += value => completed = value;
            KillBoss();
            Assert.That(completed.BossDefeated, Is.True);
            Assert.That(completed.Rewards.Count, Is.EqualTo(3));
            Assert.That(completed.Rewards.Single(x => x.CurrencyId == "gem.topaz").Amount, Is.EqualTo(350));
            Assert.That(completed.Rewards.Single(x => x.CurrencyId == "gem.amethyst").Amount, Is.EqualTo(150));
            long[] balances = Get<long[]>(progression, "_gemstoneBalances");
            Assert.That(balances[1], Is.EqualTo(previousBalances[1] + 350));
            Assert.That(balances[2], Is.EqualTo(previousBalances[2] + 150));
            Assert.That(_hunt.HighestBossStageCleared, Is.EqualTo(1));
            string saved = PlayerPrefs.GetString(ProfileKey);
            Assert.That(saved, Does.Contain("highestBossStageCleared\":1"));
            Assert.That(progression.GetType().GetMethod("GrantBossClear").Invoke(progression, new object[] { completed }), Is.True);
            Assert.That(PlayerPrefs.GetString(ProfileKey), Is.EqualTo(saved), "Replaying the same receipt cannot pay twice.");
            var collection = Object.FindFirstObjectByType<GemstoneCollectionController>();
            Assert.That(collection.ActiveCount, Is.GreaterThanOrEqualTo(100), "Burst is simultaneous, not queued one gem per frame.");
            Assert.That(collection.ActiveCount, Is.LessThanOrEqualTo(128));
            Assert.That(_result.IsVisible, Is.False, "The result must not cover the burst.");
            if (Environment.GetEnvironmentVariable("CURSOR_HUNTER_BOSS_CAPTURE") == "1")
            {
                yield return new WaitForSecondsRealtime(.25f);
                yield return Capture("/tmp/cursor-hunter-boss-loot.png");
            }
            yield return Ready();
            Assert.That(collection.Shown("gem.garnet"), Is.EqualTo(completed.GarnetEarned));
            foreach (ResourceRewardSnapshot reward in completed.Rewards)
                Assert.That(collection.Shown(reward.CurrencyId), Is.EqualTo(reward.Amount));
            Assert.That(Get<Button>(_result, "retryButton").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("다음 단계 도전"));
            if (Environment.GetEnvironmentVariable("CURSOR_HUNTER_BOSS_CAPTURE") == "1")
                yield return Capture("/tmp/cursor-hunter-boss-success.png");
            Get<Button>(_result, "retryButton").onClick.Invoke();
            Assert.That(_spawner.BossTarget.MonsterId, Is.EqualTo("boss.02"));
            Assert.That(_combat.RemainingSeconds, Is.EqualTo(30));
            Assert.That(collection.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TwentyClearsStopAtTwentyAndNormalRunHidesBossHealth()
        {
            _hunt.BeginBossStage(2);
            Assert.That(_combat.IsRunActive, Is.False, "Locked stages cannot be entered.");
            _hunt.BeginBossRun();
            for (int stage = 1; stage <= 20; stage++)
            {
                Assert.That(_spawner.BossTarget.MonsterId, Is.EqualTo($"boss.{stage:00}"));
                Assert.That(_spawner.ActiveSpawnedCount, Is.EqualTo(1));
                KillBoss();
                Assert.That(_hunt.HighestBossStageCleared, Is.EqualTo(stage));
                if (stage == 20)
                    foreach (string id in new[] { "garnet", "topaz", "amethyst", "sapphire", "diamond", "dragon" })
                        Assert.That(_combat.GetResourceEarned("gem." + id), Is.GreaterThan(0));
                if (stage < 20) _hunt.ContinueFromResult();
            }
            if (Environment.GetEnvironmentVariable("CURSOR_HUNTER_BOSS_CAPTURE") == "1")
            {
                yield return new WaitForSecondsRealtime(.2f);
                yield return Capture("/tmp/cursor-hunter-boss-six-gems.png");
            }
            yield return Ready();
            var collection = Object.FindFirstObjectByType<GemstoneCollectionController>();
            foreach (string id in new[] { "garnet", "topaz", "amethyst", "sapphire", "diamond", "dragon" })
                Assert.That(collection.Shown("gem." + id), Is.EqualTo(_combat.GetResourceEarned("gem." + id)));
            Assert.That(Get<Button>(_result, "retryButton").GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("일반 전투"));
            _hunt.ContinueFromResult();
            Assert.That(_combat.IsRunActive, Is.True);
            Assert.That(_combat.IsBossRun, Is.False);
            Assert.That(Slider().gameObject.activeSelf, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResetBossProgressPreservesPurchasesWalletAndReceiptsAndRestartsAtStageOneAfterReload()
        {
            Set(_hunt, "connectTraitScreenToCombat", true);
            PurchaseAllTraitsInMemory();
            _hunt.BeginBossRun();
            RunResult firstClear = default;
            Get<RunCoordinator>(_hunt, "_runCoordinator").Completed += value =>
            {
                if (value.BossStage == 1) firstClear = value;
            };
            KillBoss();
            _hunt.ContinueFromResult();
            KillBoss();
            Assert.That(_hunt.HighestBossStageCleared, Is.EqualTo(2));
            string saved = PlayerPrefs.GetString(ProfileKey);
            _hunt.ResetBossProgress();
            Assert.That(_hunt.HighestBossStageCleared, Is.Zero);
            Assert.That(Get<int>(_hunt, "_sessionHighestBossStage"), Is.Zero);
            Assert.That(_result.IsVisible, Is.False);
            Assert.That(Slider().gameObject.activeSelf, Is.False);
            Assert.That(Object.FindFirstObjectByType<GemstoneCollectionController>().ActiveCount, Is.Zero);
            Assert.That(Object.FindFirstObjectByType<AppUiRootController>().CurrentScreen, Is.EqualTo(UiScreenId.MainMenu));
            string reset = PlayerPrefs.GetString(ProfileKey);
            Assert.That(reset, Is.EqualTo(saved.Replace("\"highestBossStageCleared\":2", "\"highestBossStageCleared\":0")),
                "Reset must change only boss progress, not wallet, purchases, or paid receipts.");
            object progression = Get<object>(_hunt, "progressionController");
            Assert.That(progression.GetType().GetMethod("GrantBossClear").Invoke(progression, new object[] { firstClear }), Is.True);
            Assert.That(_hunt.HighestBossStageCleared, Is.Zero, "An old paid result must not restore progress.");
            Assert.That(PlayerPrefs.GetString(ProfileKey), Is.EqualTo(reset));
            _hunt.ResetBossProgress();
            Assert.That(PlayerPrefs.GetString(ProfileKey), Is.EqualTo(reset), "Repeated resets are safe.");

            yield return SceneManager.UnloadSceneAsync(_scene);
#if UNITY_EDITOR
            _scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            yield return null;
            _hunt = Object.FindFirstObjectByType<HuntManager>();
            _combat = Object.FindFirstObjectByType<CombatRunController>();
            _spawner = Object.FindFirstObjectByType<MonsterSpawner>();
            _result = Object.FindFirstObjectByType<BattleResultPresentation>();
            Assert.That(_hunt.HighestBossStageCleared, Is.Zero);
            _hunt.BeginBossStage(2);
            Assert.That(_combat.IsRunActive, Is.False);
            _hunt.BeginBossRun();
            _combat.PauseRun();
            Assert.That(_spawner.BossTarget.MonsterId, Is.EqualTo("boss.01"));
            CapturePurchasedSkills();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ResetBossProgressKeepsActiveAttemptOnSaveFailureAndAbortsItAfterSuccessfulRetry()
        {
            Set(_hunt, "connectTraitScreenToCombat", true);
            _hunt.BeginBossRun();
            KillBoss();
            _hunt.ContinueFromResult();
            _combat.PauseRun();
            RunId run = _combat.CurrentRunId;
            string saved = PlayerPrefs.GetString(ProfileKey);
            PlayerPrefs.SetString(ProfileKey, "{\"version\":999}");
            LogAssert.Expect(LogType.Warning, "Trait progression changed in memory but could not be saved.");
            LogAssert.Expect(LogType.Warning, "보스 진행도 초기화를 저장하지 못했습니다. 다시 시도해 주세요.");
            _hunt.ResetBossProgress();
            Assert.That(_hunt.HighestBossStageCleared, Is.EqualTo(1));
            Assert.That(Get<int>(_hunt, "_sessionHighestBossStage"), Is.EqualTo(1));
            Assert.That(_combat.IsRunActive, Is.True);
            Assert.That(_combat.CurrentRunId, Is.EqualTo(run));
            Assert.That(PlayerPrefs.GetString(ProfileKey), Is.EqualTo("{\"version\":999}"));
            PlayerPrefs.SetString(ProfileKey, saved);
            _hunt.ResetBossProgress();
            Assert.That(_combat.IsRunActive, Is.False);
            Assert.That(_hunt.HighestBossStageCleared, Is.Zero);
            Assert.That(_spawner.ActiveSpawnedCount, Is.Zero);
            Assert.That(Get<int>(_hunt, "_lastBossStage"), Is.Zero);
            Assert.That(Get<bool>(_hunt, "_lastRunWasBoss"), Is.False);
            Assert.That(PlayerPrefs.GetString(ProfileKey), Is.EqualTo(saved.Replace("\"highestBossStageCleared\":1", "\"highestBossStageCleared\":0")));
            _hunt.BeginBossRun();
            _combat.PauseRun();
            Assert.That(_spawner.BossTarget.MonsterId, Is.EqualTo("boss.01"));
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ResetBossProgressSettlesPendingEarnedRewardsBeforeClearingTheStage()
        {
            Set(_hunt, "connectTraitScreenToCombat", true);
            _hunt.BeginBossRun();
            object progression = Get<object>(_hunt, "progressionController");
            long garnet = Get<long>(progression, "_garnetBalance");
            PlayerPrefs.SetString(ProfileKey, "{\"version\":999}");
            LogAssert.Expect(LogType.Warning, "Trait progression changed in memory but could not be saved.");
            LogAssert.Expect(LogType.Warning, $"Run '{_combat.CurrentRunId}' produced rewards, but progression settlement could not be saved.");
            KillBoss();
            RunResult pending = Get<RunResult>(_hunt, "_pendingBossSettlement");
            LogAssert.Expect(LogType.Warning, "Trait progression changed in memory but could not be saved.");
            LogAssert.Expect(LogType.Warning, $"Run '{pending.RunId}' produced rewards, but progression settlement could not be saved.");
            LogAssert.Expect(LogType.Warning, "보스 진행도 초기화를 저장하지 못했습니다. 다시 시도해 주세요.");
            _hunt.ResetBossProgress();
            Assert.That(Get<RunResult>(_hunt, "_pendingBossSettlement").RunId, Is.EqualTo(pending.RunId));
            Assert.That(Get<long>(progression, "_garnetBalance"), Is.EqualTo(garnet));
            PlayerPrefs.DeleteKey(ProfileKey);
            _hunt.ResetBossProgress();
            Assert.That(Get<RunResult>(_hunt, "_pendingBossSettlement").RunId.IsValid, Is.False);
            Assert.That(Get<long>(progression, "_garnetBalance"), Is.EqualTo(garnet + pending.GarnetEarned));
            Assert.That(_hunt.HighestBossStageCleared, Is.Zero);
            Assert.That(PlayerPrefs.GetString(ProfileKey), Does.Contain("\"highestBossStageCleared\":0"));
            _hunt.BeginBossRun();
            _combat.PauseRun();
            Assert.That(_spawner.BossTarget.MonsterId, Is.EqualTo("boss.01"));
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SaveFailureRollsBackPayoutAndNextActionRetriesTheSameReceipt()
        {
            Set(_hunt, "connectTraitScreenToCombat", true);
            _hunt.BeginBossRun();
            long initialGarnet = Get<long>(Get<object>(_hunt, "progressionController"), "_garnetBalance");
            PlayerPrefs.SetString(ProfileKey, "{\"version\":999}");
            KillBoss();
            Assert.That(_hunt.HighestBossStageCleared, Is.Zero);
            Assert.That(PlayerPrefs.GetString(ProfileKey), Is.EqualTo("{\"version\":999}"));
            PlayerPrefs.DeleteKey(ProfileKey);
            _hunt.ContinueFromResult();
            Assert.That(_hunt.HighestBossStageCleared, Is.EqualTo(1));
            Assert.That(_spawner.BossTarget.MonsterId, Is.EqualTo("boss.02"));
            Assert.That(PlayerPrefs.GetString(ProfileKey), Does.Contain("\"garnetBalance\":" + (initialGarnet + 1000)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FlameFinishesStageTwoAndResultCanStartStageThree()
        {
            var flame = _combat.GetComponentInChildren<FlameExhaustSkillController>(true);
            foreach (MonoBehaviour skill in _combat.GetComponentsInChildren<MonoBehaviour>(true))
                if (skill is IRunSkillController)
                {
                    skill.enabled = skill == flame;
                    Set(skill, "editorSkillAcquired", skill == flame);
                }
            Set(_hunt, "_sessionHighestBossStage", 1);
            _hunt.BeginBossStage(2);
            _combat.ResumeRun();
            MonsterCombatTarget boss = _spawner.BossTarget;
            RunId runId = _combat.CurrentRunId;
            Assert.That(_combat.TryApplySkillDamage(runId, boss, boss.CurrentHealth - 1), Is.True);
            Assert.That(boss.CurrentHealth, Is.EqualTo(1));
            Physics2D.SyncTransforms();
            CircleCollider2D cursor = Get<CircleCollider2D>(flame, "sourceCursorHitArea");
            Assert.That(boss.TryGetHitEffectBounds(out Bounds bounds), Is.True);
            boss.transform.position += cursor.bounds.center - bounds.center;
            Physics2D.SyncTransforms();
            typeof(FlameExhaustSkillController).GetMethod("LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flame, null);
            Assert.That(_combat.IsRunActive, Is.False);
            Assert.That(_hunt.HighestBossStageCleared, Is.EqualTo(2));
            Assert.That(flame.ActiveProjectileCount, Is.Zero);
            Assert.That(flame.ActiveHitEffectCount, Is.Zero);
            yield return Ready();
            Assert.That(Get<TMP_Text>(_result, "title").text, Does.Contain("토벌 성공"));
            Get<Button>(_result, "retryButton").onClick.Invoke();
            Assert.That(_spawner.BossTarget.MonsterId, Is.EqualTo("boss.03"));
            Assert.That(_combat.IsRunActive, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        private void KillBoss()
        {
            _combat.ResumeRun();
            Assert.That(_combat.TryApplySkillDamage(_combat.CurrentRunId, _spawner.BossTarget, 1000000000), Is.True);
        }

        [UnityTest]
        public IEnumerator PurchasedPlayerStatsAndAllSevenSkillsMatchBetweenNormalAndBossCombat()
        {
            Set(_hunt, "connectTraitScreenToCombat", true);
            ProgressionCombatSnapshot before = PlayerSnapshot();
            PurchaseAllTraitsInMemory();
            foreach (MonoBehaviour skill in _combat.GetComponentsInChildren<MonoBehaviour>(true))
                if (skill is IRunSkillController) Set(skill, "editorSkillAcquired", false);
            _hunt.BeginPrototypeRun();
            _combat.PauseRun();
            CombatSnapshot normal = _combat.CombatSnapshot;
            Assert.That(normal.AttackPower, Is.GreaterThan(before.Combat.AttackPower));
            Assert.That(normal.RangeMultiplier, Is.GreaterThan(before.Combat.RangeMultiplier));
            Assert.That(normal.BossDamageMultiplier, Is.GreaterThan(before.Combat.BossDamageMultiplier));
            string[] skills = CapturePurchasedSkills();
            _hunt.ReturnToMainMenu();
            _hunt.BeginBossRun();
            _combat.PauseRun();
            Assert.That(_combat.IsBossRun, Is.True);
            Assert.That(_combat.DurationSeconds, Is.EqualTo(30));
            Assert.That(_combat.CombatSnapshot, Is.EqualTo(normal));
            Assert.That(CapturePurchasedSkills(), Is.EqualTo(skills), "Purchased skill upgrades must match normal combat.");
            Assert.That(_hunt.IsCombatBalanceTestActive, Is.False);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BossBalanceUsesFreshPurchasesAndLiveEnemyEditsKeepPlayerSkillsAndDiscardRewards()
        {
            Set(_hunt, "connectTraitScreenToCombat", true);
            CombatBalanceSettings settings = _hunt.CaptureCombatBalanceSettings();
            Assert.That(settings.UsePlayerProgression, Is.True, "Enemy tuning must preserve purchases by default.");
            settings.AttackPower = 1;
            foreach (var skill in settings.Skills) skill.Enabled = true;
            settings.Bosses.Find(value => value.BossId == "boss.20").MaxHealth = 10000000;
            Assert.That(_hunt.TrySetCombatBalanceSettings(settings, out string error), Is.True, error);
            ProgressionCombatSnapshot locked = PlayerSnapshot();
            _hunt.BeginBossStage(20);
            _combat.PauseRun();
            foreach (SkillBalanceSettings skill in _combat.CaptureSkillBalanceSettings())
                Assert.That(skill.Enabled, Is.EqualTo(locked.Skills.Single(value => value.SkillId == skill.SkillId).Unlocked),
                    "The balance window must not unlock unpurchased skills: " + skill.SkillId);
            _hunt.ReturnToMainMenu();

            // The retained editor draft predates these purchases and must not freeze player values.
            PurchaseAllTraitsInMemory();
            ProgressionCombatSnapshot purchased = PlayerSnapshot();
            foreach (var skill in settings.Skills) skill.Enabled = false;
            Assert.That(_hunt.TrySetCombatBalanceSettings(settings, out error), Is.True, error);
            string profile = PlayerPrefs.GetString(ProfileKey, "missing");
            _hunt.BeginBossStage(20);
            _combat.PauseRun();
            Assert.That(_combat.CombatSnapshot, Is.EqualTo(purchased.Combat));
            Assert.That(_combat.DurationSeconds, Is.EqualTo(30));
            Assert.That(_hunt.IsCombatBalanceTestActive, Is.True);
            string[] skillValues = CapturePurchasedSkills();
            RunId run = _combat.CurrentRunId;
            float elapsed = _combat.ElapsedSeconds;
            MonsterCombatTarget boss = _spawner.BossTarget;
            settings.Bosses.Find(value => value.BossId == "boss.20").MaxHealth = 20000000;
            Assert.That(_hunt.TrySetCombatBalanceSettings(settings, out error), Is.True, error);
            Assert.That(_spawner.BossTarget, Is.SameAs(boss));
            Assert.That(boss.MaxHealth, Is.EqualTo(20000000));
            Assert.That(_combat.CurrentRunId, Is.EqualTo(run));
            Assert.That(_combat.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(_combat.CombatSnapshot, Is.EqualTo(purchased.Combat));
            Assert.That(CapturePurchasedSkills(), Is.EqualTo(skillValues));

            RunResult completed = default;
            Get<RunCoordinator>(_hunt, "_runCoordinator").Completed += value => completed = value;
            KillBoss();
            Assert.That(completed.BossDefeated, Is.True);
            Assert.That(completed.SettlementPolicy, Is.EqualTo(RunSettlementPolicy.Discard));
            Assert.That(_hunt.HighestBossStageCleared, Is.Zero);
            Assert.That(PlayerPrefs.GetString(ProfileKey, "missing"), Is.EqualTo(profile));
            yield return Ready();
            Assert.That(PlayerPrefs.GetString(ProfileKey, "missing"), Is.EqualTo(profile));
            LogAssert.NoUnexpectedReceived();
        }

        private ProgressionCombatSnapshot PlayerSnapshot()
        {
            object progression = Get<object>(_hunt, "progressionController");
            progression.GetType().GetMethod("SetCursorCombatStatDefaults").Invoke(progression,
                new object[] { _combat.PlayerCombatStatsRuntime.Defaults });
            return (ProgressionCombatSnapshot)progression.GetType().GetMethod("CreateCombatSnapshot").Invoke(progression, null);
        }

        private void PurchaseAllTraitsInMemory()
        {
            PlayerSnapshot();
            object progression = Get<object>(_hunt, "progressionController");
            var purchased = Get<HashSet<string>>(progression, "_purchased");
            object catalog = Get<object>(Get<object>(progression, "_document"), "progression");
            foreach (object category in Get<Array>(catalog, "categories"))
                foreach (object node in Get<Array>(category, "nodes"))
                    purchased.Add(Get<string>(node, "id"));
            Assert.That(purchased.Count, Is.GreaterThan(50));
        }

        private string[] CapturePurchasedSkills()
        {
            var skills = _combat.CaptureSkillBalanceSettings();
            Assert.That(skills.Count, Is.EqualTo(7));
            foreach (SkillBalanceSettings skill in skills) Assert.That(skill.Enabled, Is.True, skill.SkillId);
            return skills.Select(JsonUtility.ToJson).ToArray();
        }

        [UnityTest]
        public IEnumerator LiveBossBalancePreservesRunAndHealthRatioWithoutUnlockingOrPaying()
        {
            Set(_hunt, "connectTraitScreenToCombat", true);
            _hunt.BeginBossRun();
            _combat.PauseRun();
            string profile = PlayerPrefs.GetString(ProfileKey, "missing");
            MonsterCombatTarget boss = _spawner.BossTarget;
            RunId run = _combat.CurrentRunId;
            float elapsed = _combat.ElapsedSeconds;
            CombatBalanceSettings settings = _hunt.CaptureCombatBalanceSettings();
            settings.UsePlayerProgression = false;
            Assert.That(settings.Monsters.Any(value => value.Enabled), Is.True, "A boss must not disable the normal roster.");
            settings.AttackPower = 123;
            settings.CriticalChancePercent = 0;
            settings.BossDamageMultiplier = 2;
            settings.DurationSeconds = 17;
            foreach (var skill in settings.Skills) skill.Enabled = false;
            settings.Bosses.Find(value => value.BossId == "boss.01").MaxHealth = 1000;
            settings.Bosses.Find(value => value.BossId == "boss.02").MaxHealth = 4000;
            Assert.That(_hunt.TrySetCombatBalanceSettings(settings, out string error), Is.True, error);
            Assert.That(_hunt.IsCombatBalanceTestActive, Is.True);
            Assert.That(_combat.DurationSeconds, Is.EqualTo(30));
            Assert.That(_combat.CurrentRunId, Is.EqualTo(run));
            Assert.That(_combat.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(_combat.CombatSnapshot.AttackPower, Is.EqualTo(123));
            Assert.That(_combat.CombatSnapshot.BossDamageMultiplier, Is.EqualTo(2));
            Assert.That(boss.MaxHealth, Is.EqualTo(1000));
            _combat.ResumeRun();
            Assert.That(_combat.TryApplySkillDamage(run, boss, 100), Is.True);
            _combat.PauseRun();
            long remaining = boss.CurrentHealth;
            settings.Bosses[0].MaxHealth = 2000;
            settings.Bosses[0].MoveSpeed = 1.25f;
            Assert.That(_hunt.TrySetCombatBalanceSettings(settings, out error), Is.True, error);
            Assert.That(_spawner.BossTarget, Is.SameAs(boss));
            Assert.That(boss.CurrentHealth, Is.EqualTo(remaining * 2));
            Assert.That(_spawner.CurrentSpawnPlan.Entries[0].Snapshot.MoveSpeed, Is.EqualTo(1.25f));
            Assert.That(_spawner.CurrentSpawnPlan.GlobalAliveLimit, Is.EqualTo(1));
            Assert.That(_spawner.ActiveSpawnedCount, Is.EqualTo(1));
            CombatBalanceSettings invalid = settings.Clone();
            invalid.AttackPower = 999;
            invalid.Bosses[19].MaxHealth = 0;
            Assert.That(_hunt.TrySetCombatBalanceSettings(invalid, out _), Is.False);
            Assert.That(_combat.CombatSnapshot.AttackPower, Is.EqualTo(123));
            Assert.That(boss.CurrentHealth, Is.EqualTo(remaining * 2));
            yield return null;
            Assert.That(Slider().value, Is.EqualTo((float)remaining / 1000).Within(.001));
            Assert.That(Slider().GetComponentsInChildren<TMP_Text>(true).Single(x => x.name == "Text_Value").text,
                Does.EndWith(" / 2,000"));

            RunResult completed = default;
            Get<RunCoordinator>(_hunt, "_runCoordinator").Completed += value => completed = value;
            KillBoss();
            Assert.That(completed.BossDefeated, Is.True);
            Assert.That(completed.SettlementPolicy, Is.EqualTo(RunSettlementPolicy.Discard));
            Assert.That(_hunt.HighestBossStageCleared, Is.Zero);
            Assert.That(Get<int>(_hunt, "_sessionHighestBossStage"), Is.Zero);
            Assert.That(PlayerPrefs.GetString(ProfileKey, "missing"), Is.EqualTo(profile));
            var collection = Object.FindFirstObjectByType<GemstoneCollectionController>();
            Assert.That(collection.ActiveCount, Is.InRange(100, 128), "Balance tests must show the boss loot burst without paying it.");
            Assert.That(collection.IsFinishing, Is.True);
            Assert.That(_result.IsVisible, Is.False, "The result must wait until the test loot is collected.");
            if (Environment.GetEnvironmentVariable("CURSOR_HUNTER_BOSS_BALANCE_CAPTURE") == "1")
            {
                yield return new WaitForSecondsRealtime(.25f);
                yield return Capture("/tmp/cursor-hunter-boss-balance-loot.png");
            }
            yield return Ready();
            foreach (ResourceRewardSnapshot reward in completed.Rewards)
                Assert.That(collection.Shown(reward.CurrencyId), Is.EqualTo(reward.Amount));
            Assert.That(PlayerPrefs.GetString(ProfileKey, "missing"), Is.EqualTo(profile));
            Get<Button>(_result, "retryButton").onClick.Invoke();
            Assert.That(_spawner.BossTarget.MonsterId, Is.EqualTo("boss.02"));
            Assert.That(_spawner.BossTarget.MaxHealth, Is.EqualTo(4000));
            Assert.That(_combat.CombatSnapshot.AttackPower, Is.EqualTo(123));
            Assert.That(_hunt.IsCombatBalanceTestActive, Is.True);
            _hunt.ClearCombatBalanceSettings();
            Assert.That(_combat.IsRunActive, Is.False);
            Assert.That(Slider().gameObject.activeSelf, Is.False);
            Assert.That(PlayerPrefs.GetString(ProfileKey, "missing"), Is.EqualTo(profile));
        }

        [UnityTest]
        public IEnumerator PreparedBalanceCanTestAnyBossAndRetainsNormalSettingsWithProgressionConnected()
        {
            Set(_hunt, "connectTraitScreenToCombat", true);
            CombatBalanceSettings settings = _hunt.CaptureCombatBalanceSettings();
            settings.UsePlayerProgression = false;
            settings.AttackPower = 77;
            settings.DurationSeconds = 17;
            settings.GlobalAliveLimit = 33;
            settings.PerMonsterAliveLimit = 7;
            foreach (var skill in settings.Skills) skill.Enabled = false;
            foreach (var monster in settings.Monsters) monster.Enabled = monster.MonsterId == "monster.01";
            settings.Monsters.Find(value => value.MonsterId == "monster.01").MaxHealth = 99;
            settings.Bosses.Find(value => value.BossId == "boss.20").MaxHealth = 9999;
            Assert.That(_hunt.TrySetCombatBalanceSettings(settings, out string error), Is.True, error);
            _hunt.BeginBossStage(20);
            Assert.That(_hunt.IsCombatBalanceTestActive, Is.True);
            Assert.That(_spawner.BossTarget.MaxHealth, Is.EqualTo(9999));
            Assert.That(_combat.DurationSeconds, Is.EqualTo(30));
            Assert.That(_combat.CombatSnapshot.AttackPower, Is.EqualTo(77));
            CombatBalanceSettings captured = _hunt.AppliedCombatBalanceSettings;
            Assert.That(captured.DurationSeconds, Is.EqualTo(17));
            Assert.That(captured.GlobalAliveLimit, Is.EqualTo(33));
            Assert.That(captured.PerMonsterAliveLimit, Is.EqualTo(7));
            Assert.That(captured.Monsters.Find(value => value.MonsterId == "monster.01").MaxHealth, Is.EqualTo(99));
            _hunt.ReturnToMainMenu();
            _hunt.BeginPrototypeRun();
            Assert.That(_hunt.IsCombatBalanceTestActive, Is.True);
            Assert.That(_combat.IsBossRun, Is.False);
            Assert.That(_combat.DurationSeconds, Is.EqualTo(17));
            Assert.That(_combat.CombatSnapshot.AttackPower, Is.EqualTo(77));
            Assert.That(_spawner.CurrentSpawnPlan.GlobalAliveLimit, Is.EqualTo(33));
            Assert.That(_spawner.CurrentSpawnPlan.Entries.First(value => value.Snapshot.MonsterId == "monster.01").Snapshot.MaxHealth,
                Is.EqualTo(99));
            Assert.That(_hunt.AppliedCombatBalanceSettings.Bosses.Find(value => value.BossId == "boss.20").MaxHealth,
                Is.EqualTo(9999));
            _hunt.ClearCombatBalanceSettings();
            _hunt.BeginBossStage(20);
            Assert.That(_combat.IsRunActive, Is.False, "Clearing test overrides restores the actual boss lock.");
            _hunt.BeginBossRun();
            Assert.That(_hunt.IsCombatBalanceTestActive, Is.False);
            Assert.That(_spawner.BossTarget.MonsterId, Is.EqualTo("boss.01"));
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NewVisualsSpawnAnimateTakeDamageAndClearFromSixThroughTwenty()
        {
            foreach (MonoBehaviour skill in _combat.GetComponentsInChildren<MonoBehaviour>(true))
                if (skill is IRunSkillController) skill.enabled = false;
            Set(_hunt, "_sessionHighestBossStage", 5);
            for (int stage = 6; stage <= 20; stage++)
            {
                _hunt.BeginBossStage(stage);
                _combat.ResumeRun();
                MonsterCombatTarget target = _spawner.BossTarget;
                BossDefinition boss = Resources.Load<BossDefinition>($"BossDefinitions/Boss{stage:00}");
                Assert.That(target, Is.Not.Null, $"Stage {stage}");
                Assert.That(target.MonsterId, Is.EqualTo($"boss.{stage:00}"));
                var visual = target.transform.Find("VisualRoot").GetChild(0);
                Assert.That(visual.name, Is.EqualTo(boss.VisualPrefab.name));
                var animator = visual.GetComponentInChildren<Animator>();
                foreach (string state in new[] { "Idle", "Walk", "Hit", "Dead" })
                    Assert.That(animator.HasState(0, Animator.StringToHash(state)), Is.True, $"{stage}: {state}");
                yield return null;
                Assert.That(target.TryGetHitEffectBounds(out Bounds bounds), Is.True);
                Assert.That(bounds.size.x, Is.GreaterThan(0.1f));
                Assert.That(bounds.size.y, Is.GreaterThan(0.1f));
                Assert.That(visual.GetComponentsInChildren<SpriteRenderer>().Any(x => x.enabled && x.sprite != null), Is.True);
                TMP_Text title = Slider().GetComponentsInChildren<TMP_Text>(true).Single(x => x.name == "Text_Title");
                Assert.That(title.text, Is.EqualTo(boss.DefaultDisplayName));
                if (Environment.GetEnvironmentVariable("CURSOR_HUNTER_BOSS_ROSTER_CAPTURE") == "1")
                {
                    target.transform.position = Vector3.zero;
                    _combat.PauseRun();
                    yield return Capture($"/tmp/cursor-hunter-boss-stage-{stage:00}.png");
                }
                _combat.ResumeRun();
                long health = target.CurrentHealth;
                Assert.That(_combat.TryApplySkillDamage(_combat.CurrentRunId, target, 1), Is.True);
                Assert.That(target.CurrentHealth, Is.LessThan(health), "Critical hits may increase the requested damage.");
                KillBoss();
                Assert.That(_hunt.HighestBossStageCleared, Is.EqualTo(stage));
                Assert.That(_combat.IsRunActive, Is.False);
            }
            yield return Ready();
            LogAssert.NoUnexpectedReceived();
        }
        private IEnumerator Ready()
        {
            float until = Time.realtimeSinceStartup + 6;
            while (!_result.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(_result.IsReady, Is.True);
        }
        private static IEnumerator Capture(string path)
        {
            Camera camera = Camera.main;
            Canvas canvas = Object.FindFirstObjectByType<PrototypeRunHud>().GetComponent<Canvas>();
            if (canvas == null) canvas = GameObject.Find("UICanvas").GetComponent<Canvas>();
            RenderMode mode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            float distance = canvas.planeDistance;
            RenderTexture previous = camera.targetTexture;
            var target = new RenderTexture(1920, 1080, 24);
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
                camera.Render();
                RenderTexture oldActive = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
                RenderTexture.active = oldActive;
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                Object.Destroy(image);
            }
            finally
            {
                camera.targetTexture = previous;
                canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance;
                target.Release(); Object.Destroy(target);
            }
        }
        private static Slider Slider() => Object.FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(x => x.name == "BossSlider");
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
