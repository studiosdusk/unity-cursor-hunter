using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using NUnit.Framework;
using TMPro;
using UnityEngine;
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
            Assert.That(Object.FindFirstObjectByType<AppUiRootController>().CurrentScreen, Is.EqualTo(UiScreenId.MainMenu));
            Assert.That(_view.IsVisible, Is.False);
            _hunt.BeginPrototypeRun();
            Assert.That(combat.IsRunActive, Is.True);
            _hunt.ResetPrototypeRun();
            Assert.That(_view.IsVisible, Is.False);
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
                Is.EqualTo(56f).Within(0.1f), "Hidden types must not leave gaps.");
            Assert.That(((RectTransform)Get<CanvasGroup>(_view, "resultUI").transform).rect.height, Is.EqualTo(350f));
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
            yield return WaitFor(() => upgrade.transform.localScale.x > 0f);
            Assert.That(Amount("garnet"), Is.EqualTo("+123"), "The main result must finish first.");
            Assert.That(retry.transform.localScale, Is.EqualTo(Vector3.zero));
            Assert.That(actions.interactable, Is.False);
            Assert.That(_view.IsReady, Is.False);
            Vector3 upgradeScale = upgrade.transform.localScale;
            _view.CompleteReveal(); _view.CompleteReveal();
            Assert.That(upgrade.transform.localScale, Is.EqualTo(upgradeScale), "Extra clicks cannot skip or restart the button sequence.");
            upgrade.onClick.Invoke(); retry.onClick.Invoke();
            Assert.That(_view.IsVisible, Is.True, "Navigation remains locked throughout the button sequence.");

            yield return WaitFor(() => retry.transform.localScale.x > 0f);
            Assert.That(upgrade.transform.localScale, Is.EqualTo(Vector3.one), "Upgrade must fully settle before retry starts.");
            Assert.That(actions.interactable, Is.False);
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
