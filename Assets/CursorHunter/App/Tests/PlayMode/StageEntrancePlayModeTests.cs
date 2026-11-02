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
    public sealed class StageEntrancePlayModeTests
    {
        private Scene _scene;
        private HuntManager _hunt;
        private CombatRunController _combat;
        private MonsterSpawner _spawner;
        private BattleResultPresentation _presentation;
        private string _profile, _language;
        private bool _hadProfile;
        private float _timeScale;
        private const string ProfileKey = "cursor_hunter.progression.v1";

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _timeScale = Time.timeScale;
            Time.timeScale = 1;
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
            _presentation = Object.FindFirstObjectByType<BattleResultPresentation>();
            Set(_hunt, "connectTraitScreenToCombat", false);
            Set(_hunt, "_ignoreFocusPauseForAutomatedPlayModeTest", true);
            Assert.That(Get<bool>(_hunt, "playEntranceSequence"), Is.True, "Main must enable entrance by default.");
            Assert.That(Get<BattleResultPresentation>(_hunt, "battlePresentation"), Is.EqualTo(_presentation));
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            Time.timeScale = _timeScale;
            _hunt?.ResetPrototypeRun();
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
            LocalizationCatalog.SetLanguage(_language);
            if (_hadProfile) PlayerPrefs.SetString(ProfileKey, _profile); else PlayerPrefs.DeleteKey(ProfileKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator EmptyFieldThenStaggeredPopupsThenAuthoredStartPopupGateAllCombat()
        {
            BeginNormal();
            RunId run = _combat.CurrentRunId;
            MonsterCombatTarget[] targets = Targets();
            Assert.That(targets.Length, Is.EqualTo(6));
            MonsterSpawnPresentation[] popups = targets.Select(t => t.GetComponent<MonsterSpawnPresentation>()).ToArray();
            Assert.That(popups.All(p => p.Scale == 0f), Is.True, "The first rendered field must be empty.");
            Assert.That(_combat.IsEntering, Is.True);
            Assert.That(_combat.IsRunning, Is.False);
            Assert.That(Get<RunCoordinator>(_hunt, "_runCoordinator").State, Is.EqualTo(RunState.Starting));
            Assert.That(Get<Button>(_hunt, "retireButton").interactable, Is.False);
            _hunt.EndPrototypeRun();
            Assert.That(_combat.IsEntering, Is.True, "Retirement is locked during setup.");
            _combat.AdvanceTime(50);
            Assert.That(_combat.ElapsedSeconds, Is.Zero);
            Assert.That(_combat.TryApplySkillDamage(run, targets[0], 100), Is.False);
            Vector3[] positions = targets.Select(t => t.transform.position).ToArray();
            Vector2[] colliders = targets.Select(t => (Vector2)t.EnsureCombatCollider().bounds.size).ToArray();
            yield return Until(() => popups.Any(p => p.Scale > 0f));
            Assert.That(popups.Any(p => p.Scale == 0f), Is.True, "The initial pack must not pop all at once.");
            float[] scales = popups.Select(p => p.Scale).ToArray();
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(0.2f);
            CollectionAssert.AreEqual(scales, popups.Select(p => p.Scale).ToArray());
            Time.timeScale = 1;
            yield return Until(() => _presentation.IsShowingStart);
            Assert.That(popups.All(p => p.IsFinished && p.Scale == 1f), Is.True);
            Assert.That(_combat.ElapsedSeconds, Is.Zero);
            Assert.That(_combat.RemainingSeconds, Is.EqualTo(17));
            CollectionAssert.AreEqual(positions, targets.Select(t => t.transform.position).ToArray());
            CollectionAssert.AreEqual(colliders, targets.Select(t => (Vector2)t.EnsureCombatCollider().bounds.size).ToArray());
            Assert.That(Get<TMP_Text>(_presentation, "timeOutTitle").text, Is.EqualTo("전투 시작!"));
            Assert.That(Get<GameObject>(_presentation, "timeOutPanel").activeSelf, Is.True);
            Assert.That(Get<CanvasGroup>(_presentation, "resultUI").gameObject.activeSelf, Is.False);
            LocalizationCatalog.SetLanguage("en");
            Assert.That(Get<TMP_Text>(_presentation, "timeOutTitle").text, Is.EqualTo("Battle Start!"));
            LocalizationCatalog.SetLanguage("ko");
            Get<Button>(_presentation, "skipButton").onClick.Invoke();
            _presentation.CompleteReveal();
            Assert.That(_presentation.IsShowingStart, Is.True, "A click must not skip the start beat.");
            Time.timeScale = 0;
            float popupClock = Get<float>(_presentation, "_clock");
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(Get<float>(_presentation, "_clock"), Is.EqualTo(popupClock));
            Time.timeScale = 1;
            yield return Capture("start-popup");
            yield return Until(() => _combat.IsRunning);
            Assert.That(_combat.CurrentRunId, Is.EqualTo(run));
            Assert.That(_combat.ElapsedSeconds, Is.LessThan(0.3f));
            Assert.That(_presentation.IsVisible, Is.False);
            Assert.That(Get<Button>(_hunt, "retireButton").interactable, Is.True);
            Assert.That(Get<RunCoordinator>(_hunt, "_runCoordinator").State, Is.EqualTo(RunState.Running));
        }

        [UnityTest]
        public IEnumerator EightyInitialMonstersFinishInsideTheSameSetupWindow()
        {
            BeginNormal(40);
            Assert.That(Targets().Length, Is.EqualTo(80));
            float started = Time.realtimeSinceStartup;
            yield return Until(() => _presentation.IsShowingStart);
            Assert.That(Time.realtimeSinceStartup - started, Is.LessThan(1.5f));
            Assert.That(Targets().All(t => t.GetComponent<MonsterSpawnPresentation>().IsFinished), Is.True);
            Assert.That(_combat.ElapsedSeconds, Is.Zero);
            Assert.That(_spawner.ActiveSpawnedCount, Is.EqualTo(80));
        }

        [UnityTest]
        public IEnumerator ReinforcementsPopWithoutChangingHitboxesOrAddingAnotherSetupDelay()
        {
            BeginNormal();
            yield return Until(() => _combat.IsRunning);
            MonsterCombatTarget[] initial = Targets();
            _combat.AdvanceTime(1.1f);
            yield return Until(() => Targets().Length > initial.Length);
            MonsterCombatTarget target = Targets().First(t => !initial.Contains(t));
            var popup = target.GetComponent<MonsterSpawnPresentation>();
            Assert.That(popup.IsFinished, Is.False);
            Assert.That(popup.Scale, Is.LessThan(1.11f));
            Vector3 colliderSize = target.EnsureCombatCollider().bounds.size;
            Assert.That(_hunt.IsPlayingEntrance, Is.False);
            Assert.That(_combat.IsRunning, Is.True);
            yield return Until(() => popup.IsFinished);
            Assert.That(popup.Scale, Is.EqualTo(1f));
            Assert.That(target.EnsureCombatCollider().bounds.size, Is.EqualTo(colliderSize));
            Assert.That(_presentation.IsVisible, Is.False);
            // Death during a new popup must restore the visual scale before its death presentation.
            _combat.AdvanceTime(1.1f);
            yield return Until(() => Targets().Any(t => !t.GetComponent<MonsterSpawnPresentation>().IsFinished));
            target = Targets().First(t => !t.GetComponent<MonsterSpawnPresentation>().IsFinished);
            Assert.That(_combat.TryApplySkillDamage(_combat.CurrentRunId, target, 1000000), Is.True);
            yield return null;
            Assert.That(target.GetComponent<MonsterSpawnPresentation>().Scale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator ResetAndMenuExitCancelBothSetupAndAnnouncementWithoutStartingStaleRuns()
        {
            BeginNormal();
            RunId first = _combat.CurrentRunId;
            yield return new WaitForSecondsRealtime(0.2f);
            _hunt.ResetPrototypeRun();
            Assert.That(_spawner.ActiveSpawnedCount, Is.Zero);
            Assert.That(_combat.IsRunActive, Is.False);
            BeginNormal();
            Assert.That(_combat.CurrentRunId, Is.Not.EqualTo(first));
            yield return Until(() => _presentation.IsShowingStart);
            Object.FindFirstObjectByType<AppUiRootController>().ShowMainMenu();
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(_combat.IsRunActive, Is.False);
            Assert.That(_spawner.ActiveSpawnedCount, Is.Zero);
            Assert.That(_presentation.IsVisible, Is.False);
            BeginNormal();
            yield return Until(() => _combat.IsRunning);
            Assert.That(_presentation.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator BossEntranceKeepsAllPurchasedSkillsPreparedAndThirtySecondsUntouched()
        {
            Set(_hunt, "connectTraitScreenToCombat", true);
            object progression = Get<object>(_hunt, "progressionController");
            progression.GetType().GetMethod("CreateCombatSnapshot").Invoke(progression, null);
            var purchased = Get<HashSet<string>>(progression, "_purchased");
            object config = Get<object>(Get<object>(progression, "_document"), "progression");
            foreach (object category in Get<Array>(config, "categories"))
                foreach (object node in Get<Array>(category, "nodes")) purchased.Add(Get<string>(node, "id"));
            _hunt.BeginBossRun();
            Assert.That(_combat.IsEntering, Is.True);
            Assert.That(_spawner.ActiveSpawnedCount, Is.EqualTo(1));
            yield return Until(() => _presentation.IsShowingStart);
            Assert.That(_combat.RemainingSeconds, Is.EqualTo(30));
            Assert.That(_spawner.BossTarget.CurrentHealth, Is.EqualTo(_spawner.BossTarget.MaxHealth));
            MonoBehaviour[] skills = _combat.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(s => s is IRunSkillController).ToArray();
            Assert.That(skills.Length, Is.EqualTo(7));
            foreach (MonoBehaviour skill in skills)
                Assert.That(Get<bool>(skill, "_configured"), Is.True, skill.name + " lost its prepared run during entrance.");
            Assert.That(_combat.GarnetEarned, Is.Zero);
            _hunt.ResetPrototypeRun();
        }

        private void BeginNormal(int packSize = 3)
        {
            CombatBalanceSettings settings = _hunt.CaptureCombatBalanceSettings();
            settings.UsePlayerProgression = false;
            settings.AutoAttackEnabled = false;
            settings.AttackPower = 1;
            settings.CriticalChancePercent = 0;
            settings.DurationSeconds = 17;
            settings.GlobalAliveLimit = 80;
            settings.PerMonsterAliveLimit = 80;
            foreach (SkillBalanceSettings skill in settings.Skills) skill.Enabled = false;
            foreach (MonsterBalanceSettings monster in settings.Monsters)
            {
                monster.Enabled = monster.MonsterId == "monster.01" || monster.MonsterId == "monster.02";
                monster.MaxHealth = 10000;
                monster.PackSize = packSize;
                monster.SpawnIntervalSeconds = 1;
            }
            Assert.That(_hunt.TrySetCombatBalanceSettings(settings, out string error), Is.True, error);
            _hunt.BeginPrototypeRun();
        }

        private MonsterCombatTarget[] Targets() => Object.FindObjectsByType<MonsterCombatTarget>(FindObjectsSortMode.None)
            .Where(t => t.IsActive && t.RunId == _combat.CurrentRunId).ToArray();

        private static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 6;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Stage entrance timed out.");
        }

        private IEnumerator Capture(string name)
        {
            if (Environment.GetEnvironmentVariable("CURSOR_HUNTER_ENTRANCE_CAPTURE") != "1") yield break;
            Camera camera = Camera.main;
            Canvas canvas = Get<CanvasGroup>(_presentation, "resultUI").GetComponentInParent<Canvas>().rootCanvas;
            RenderMode mode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            float distance = canvas.planeDistance;
            int sortingOrder = canvas.sortingOrder;
            RenderTexture previous = camera.targetTexture;
            var texture = new RenderTexture(1920, 1080, 24);
            try
            {
                camera.targetTexture = texture;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                // ScreenSpaceOverlay normally renders above every world SortingGroup.
                canvas.sortingOrder = 1000;
                yield return null;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture old = RenderTexture.active;
                RenderTexture.active = texture;
                var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
                RenderTexture.active = old;
                System.IO.File.WriteAllBytes("/private/tmp/cursor-hunter-" + name + ".png", pixels.EncodeToPNG());
                Object.Destroy(pixels);
            }
            finally
            {
                camera.targetTexture = previous;
                canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance;
                canvas.sortingOrder = sortingOrder;
                texture.Release(); Object.Destroy(texture);
            }
        }

        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
