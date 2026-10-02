using System;
using System.Collections.Generic;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;
using CursorHunter.Progression;
using UnityEngine;

namespace CursorHunter.App
{
    /// <summary>
    /// Hunt composition root for the first normal-field prototype run.
    /// It converts authored Data into immutable run snapshots and connects the
    /// cursor, combat session, spawner, and result HUD.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class HuntManager : MonoBehaviour
    {
        public const float BossFieldDurationSeconds = 60f;

        private const string PrototypeSlimeResourcePath =
            "MonsterDefinitions/SlimeDefinition";

        [Header("Runtime systems")]
        [SerializeField] private MainCursorController cursorController;
        [SerializeField] private CombatRunController combatRunController;
        [SerializeField] private MonsterSpawner monsterSpawner;
        [SerializeField] private PrototypeRunHud runHud;
        [SerializeField] private TestPanelToggleController testPanelToggleController;
        [SerializeField] private AppUiRootController uiRootController;
        [SerializeField] private TraitScreenController progressionController;

        [Header("Hunt run")]
        [SerializeField] private MonsterDefinition monsterDefinition;
        [SerializeField, Min(1)] private int aliveLimit = 80;
        [SerializeField, Min(1f)] private float durationSeconds = 15f;
        [SerializeField, Min(1)] private int schemaVersion = 1;
        [SerializeField, Min(1)] private int balanceVersion = 1;
        [Tooltip("Test-only escape hatch. Production runs must fail when the authored definition is missing.")]
        [SerializeField] private bool allowPrototypeFallback;
        [SerializeField] private GameObject prototypeFallbackPrefab;

        private RunCoordinator _runCoordinator;
        private bool _coordinatorEventsSubscribed;
        private bool _pausedForInspection;
        private readonly HashSet<RunId> _settledRunIds =
            new HashSet<RunId>();

        private void Awake()
        {
            ResolveReferences();
            SyncCursorCombatStatDefaults();
            EnsureRunCoordinator();
        }

        private void ResolveReferences()
        {
            if (cursorController == null)
            {
                cursorController = GetComponent<MainCursorController>();
            }

            if (cursorController == null)
            {
                cursorController = FindFirstObjectByType<MainCursorController>(
                    FindObjectsInactive.Include);
            }

            if (combatRunController == null)
            {
                combatRunController = GetComponent<CombatRunController>();
            }

            if (combatRunController == null)
            {
                combatRunController = FindFirstObjectByType<CombatRunController>(
                    FindObjectsInactive.Include);
            }

            if (monsterSpawner == null)
            {
                monsterSpawner = GetComponent<MonsterSpawner>();
            }

            if (monsterSpawner == null)
            {
                monsterSpawner = FindFirstObjectByType<MonsterSpawner>(
                    FindObjectsInactive.Include);
            }

            if (runHud == null)
            {
                runHud = GetComponent<PrototypeRunHud>();
            }

            if (runHud == null)
            {
                runHud = FindFirstObjectByType<PrototypeRunHud>(
                    FindObjectsInactive.Include);
            }

            if (testPanelToggleController == null)
            {
                testPanelToggleController =
                    FindFirstObjectByType<TestPanelToggleController>(
                        FindObjectsInactive.Include);
            }

            if (uiRootController == null)
            {
                uiRootController = FindFirstObjectByType<AppUiRootController>(
                    FindObjectsInactive.Include);
            }

            if (progressionController == null)
            {
                progressionController = FindFirstObjectByType<TraitScreenController>(
                    FindObjectsInactive.Include);
            }
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureRunCoordinator();
            SubscribeToCoordinator();
        }

        /// <summary>
        /// Starts one prototype hunt from a UI Button. The prototype no longer
        /// enters combat automatically when the scene is loaded.
        /// </summary>
        public void BeginPrototypeRun()
        {
            ResolveReferences();
            EnsureRunCoordinator();
            SubscribeToCoordinator();

            if (_runCoordinator == null || _runCoordinator.IsActive)
            {
                return;
            }

            if (cursorController == null ||
                combatRunController == null ||
                monsterSpawner == null)
            {
                Debug.LogWarning(
                    "HuntManager requires cursor, combat, and spawner references.",
                    this);
                return;
            }

            PlayerCombatStatsRuntime runtimeStats =
                combatRunController.PlayerCombatStatsRuntime;
            CursorCombatStatBonusesSnapshot cursorStatBonuses = default;
            ProgressionCombatSnapshot progressionSnapshot = null;
            if (progressionController != null)
            {
                progressionController.SetCursorCombatStatDefaults(runtimeStats.Defaults);
                string json;
                try { json = progressionController.CreateGameInformationJson(); }
                catch (Exception exception)
                {
                    Debug.LogError("Cannot prepare current player combat information: " + exception.Message, this);
                    return;
                }
                if (!GameInformationJson.TryDeserialize(json, out var information, out string error))
                {
                    Debug.LogError("Combat JSON rejected: " + error, this);
                    return;
                }
                cursorStatBonuses =
                    progressionController.CreateCursorCombatStatBonusesSnapshot();
                progressionSnapshot = information.ToCombatSnapshot(
                    json,
                    cursorStatBonuses);
                schemaVersion = information.schemaVersion;
                balanceVersion = information.balanceVersion;
            }
            else if (!allowPrototypeFallback)
            {
                Debug.LogError("Game information producer is required.", this);
                return;
            }
            CombatSnapshot combatSnapshot = progressionSnapshot == null
                ? runtimeStats.DefaultsCombatSnapshot
                : progressionSnapshot.Combat;

            if (!combatSnapshot.IsValid)
            {
                Debug.LogWarning(
                    "Progression produced an invalid combat snapshot; using the prototype fallback.",
                    this);
                combatSnapshot = runtimeStats.DefaultsCombatSnapshot;
            }

            aliveLimit = Mathf.Max(1, aliveLimit);
            durationSeconds = GetRunDuration(
                RunMode.NormalField,
                progressionSnapshot,
                durationSeconds);
            schemaVersion = Mathf.Max(1, schemaVersion);
            balanceVersion = Mathf.Max(1, balanceVersion);

            if (!TryCreateSpawnPlan(
                progressionSnapshot,
                out SpawnPlan spawnPlan,
                out SpawnStartResult spawnPlanStartResult))
            {
                Debug.LogError(
                    $"HuntManager could not prepare the monster definition: " +
                    spawnPlanStartResult.Message,
                    this);
                return;
            }

            RunRequest runRequest = new RunRequest(
                RunId.Create(),
                schemaVersion,
                balanceVersion,
                RunMode.NormalField,
                string.Empty,
                CreateRunSeed(),
                durationSeconds);

            if (!_runCoordinator.Start(
                    runRequest,
                    combatSnapshot,
                    cursorStatBonuses,
                    spawnPlan,
                    out string failureReason))
            {
                Debug.LogWarning(
                    $"HuntManager could not start run '{runRequest.RunId}': " +
                    failureReason,
                    this);
                return;
            }

            // Keep the exact progression copy used by Combat available to
            // the Field HUD's read-only summary. The UI never re-reads live
            // trait dictionaries while a run is active.
            if (progressionController != null)
            {
                progressionController.SetActiveRunSnapshot(progressionSnapshot);
            }

            if (uiRootController != null)
            {
                uiRootController.EnterCombat();
            }

            combatRunController.ConfigureSkills(progressionSnapshot == null ? null : progressionSnapshot.Skills);
            cursorController.SetRangeMultiplier(
                combatRunController.PlayerCombatStatsRuntime.AttackRangeMultiplier);
            cursorController.ShowCursorImage();

            if (testPanelToggleController != null)
            {
                testPanelToggleController.HideTestPanel();
            }

            if (runHud != null)
            {
                runHud.Initialize();
                runHud.ShowRunning(
                    combatRunController.RemainingSeconds,
                    combatRunController.DefeatedCount,
                    combatRunController.GarnetEarned);
            }
        }

        private void SyncCursorCombatStatDefaults()
        {
            if (progressionController == null || combatRunController == null)
            {
                return;
            }

            progressionController.SetCursorCombatStatDefaults(
                combatRunController.PlayerCombatStatsRuntime.Defaults);
        }

        /// <summary>
        /// Resolves the duration at the App boundary. Normal fields use the
        /// progression snapshot (15 seconds at the start, +5 seconds per
        /// purchased node, capped at 30). Boss duration comes from JSON rules
        /// (60 seconds by default); legacy callers keep the 60-second fallback.
        /// </summary>
        public static float GetRunDuration(
            RunMode mode,
            ProgressionCombatSnapshot progressionSnapshot,
            float fallbackNormalDurationSeconds = 15f)
        {
            if (mode == RunMode.Boss)
            {
                return progressionSnapshot == null ? BossFieldDurationSeconds : progressionSnapshot.BossFieldDurationSeconds;
            }

            if (progressionSnapshot != null)
            {
                return Mathf.Clamp(
                    progressionSnapshot.NormalFieldDurationSeconds,
                    15f,
                    30f);
            }

            if (float.IsNaN(fallbackNormalDurationSeconds) ||
                float.IsInfinity(fallbackNormalDurationSeconds))
            {
                return 15f;
            }

            return Mathf.Clamp(fallbackNormalDurationSeconds, 15f, 30f);
        }

        /// <summary>
        /// Ends the current prototype run from the test panel.
        /// </summary>
        public void EndPrototypeRun()
        {
            if (_runCoordinator == null || !_runCoordinator.IsActive)
            {
                return;
            }

            _runCoordinator.Abort(
                RunEndReason.UserExit,
                RunSettlementPolicy.Eligible);
        }

        /// <summary>
        /// Stops the current test run, removes spawned monsters, hides the
        /// cursor, and clears the prototype HUD without showing a result.
        /// </summary>
        public void ResetPrototypeRun()
        {
            if (_runCoordinator != null && _runCoordinator.IsActive)
            {
                _runCoordinator.Abort(
                    RunEndReason.Reset,
                    RunSettlementPolicy.Discard);
            }
            else if (monsterSpawner != null)
            {
                monsterSpawner.StopRun();
            }

            if (cursorController != null)
            {
                cursorController.HideCursorImage();
            }

            if (runHud != null)
            {
                runHud.Reset();
            }

            if (progressionController != null)
            {
                progressionController.ClearActiveRunSnapshot();
            }
        }

        /// <summary>
        /// Assign this parameterless method to the main-menu return button.
        /// It aborts an active run without settlement, clears the prototype
        /// HUD, and restores the main menu button group.
        /// </summary>
        public void ReturnToMainMenu()
        {
            ResetPrototypeRun();

            if (testPanelToggleController != null)
            {
                testPanelToggleController.HideTestPanel();
            }

            if (uiRootController != null)
            {
                uiRootController.ShowMainMenu();
            }
        }

        private void Update()
        {
            ReconcileInspectionPause();
            if (_runCoordinator == null ||
                !_runCoordinator.IsActive ||
                combatRunController == null ||
                !combatRunController.IsRunActive)
            {
                return;
            }

            if (runHud != null)
            {
                runHud.ShowRunning(
                    combatRunController.RemainingSeconds,
                    combatRunController.DefeatedCount,
                    combatRunController.GarnetEarned);
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            ReconcileInspectionPause();
        }

        private void ReconcileInspectionPause()
        {
            if (_runCoordinator == null || !_runCoordinator.IsActive)
            {
                _pausedForInspection = false;
                return;
            }
            bool shouldPause = !Application.isFocused ||
                (progressionController != null && progressionController.IsSummaryOpen);
            if (shouldPause && _runCoordinator.State == RunState.Running)
                _pausedForInspection = _runCoordinator.Pause();
            else if (!shouldPause && _pausedForInspection)
            {
                _runCoordinator.Resume();
                _pausedForInspection = false;
            }
        }

        private void HandleRunCompleted(RunResult result)
        {
            SettleRunRewards(result);

            if (cursorController != null)
            {
                cursorController.HideCursorImage();
            }

            if (runHud != null)
            {
                runHud.ShowResult(result);
            }
        }

        private void HandleRunAborted(RunResult result)
        {
            SettleRunRewards(result);

            if (cursorController != null)
            {
                cursorController.HideCursorImage();
            }

            if (runHud != null &&
                result.SettlementPolicy == RunSettlementPolicy.Eligible)
            {
                runHud.ShowResult(result);
            }
        }

        private void SettleRunRewards(RunResult result)
        {
            if (result.SettlementPolicy != RunSettlementPolicy.Eligible ||
                result.Rewards == null ||
                result.Rewards.Count == 0 ||
                progressionController == null ||
                _settledRunIds.Contains(result.RunId))
            {
                return;
            }

            if (progressionController.GrantResourceRewards(result.Rewards))
            {
                _settledRunIds.Add(result.RunId);
            }
            else
            {
                Debug.LogWarning(
                    $"Run '{result.RunId}' produced rewards, but progression settlement could not be saved.",
                    this);
            }
        }

        private void OnDisable()
        {
            if (_runCoordinator != null && _runCoordinator.IsActive)
            {
                _runCoordinator.Abort(
                    RunEndReason.Reset,
                    RunSettlementPolicy.Discard);
            }

            if (_coordinatorEventsSubscribed && _runCoordinator != null)
            {
                _runCoordinator.Completed -= HandleRunCompleted;
                _runCoordinator.Aborted -= HandleRunAborted;
                _coordinatorEventsSubscribed = false;
            }

            if (_runCoordinator != null)
            {
                _runCoordinator.Dispose();
                _runCoordinator = null;
            }

            if (progressionController != null)
            {
                progressionController.ClearActiveRunSnapshot();
            }
        }

        private void EnsureRunCoordinator()
        {
            if (_runCoordinator == null &&
                combatRunController != null &&
                monsterSpawner != null)
            {
                _runCoordinator = new RunCoordinator(
                    combatRunController,
                    monsterSpawner);
            }
        }

        private void SubscribeToCoordinator()
        {
            if (_runCoordinator == null || _coordinatorEventsSubscribed)
            {
                return;
            }

            _runCoordinator.Completed += HandleRunCompleted;
            _runCoordinator.Aborted += HandleRunAborted;
            _coordinatorEventsSubscribed = true;
        }

        private static ulong CreateRunSeed()
        {
            unchecked
            {
                return (ulong)DateTime.UtcNow.Ticks ^
                       ((ulong)Time.frameCount << 32);
            }
        }

        private bool TryCreateSpawnPlan(
            ProgressionCombatSnapshot progressionSnapshot,
            out SpawnPlan spawnPlan,
            out SpawnStartResult failureResult)
        {
            spawnPlan = null;
            failureResult = new SpawnStartResult(
                SpawnStartStatus.Started,
                "Monster definition snapshot is ready.");

            if (progressionSnapshot != null && !string.IsNullOrEmpty(progressionSnapshot.SourceJson))
            {
                var definitions = Resources.LoadAll<MonsterDefinition>("MonsterDefinitions");
                var entries = new List<SpawnPlanEntry>();
                foreach (var monster in progressionSnapshot.Monsters)
                {
                    if (!monster.Unlocked) continue;
                    MonsterDefinition definition = null;
                    foreach (var candidate in definitions)
                        if (candidate != null && candidate.MonsterId == monster.MonsterId) { definition = candidate; break; }
                    if (definition == null || definition.VisualPrefab == null)
                    {
                        failureResult = new SpawnStartResult(SpawnStartStatus.MissingDefinition,
                            "Missing visual definition: " + monster.MonsterId);
                        return false;
                    }
                    // Definition BaseStats + composable species profiles are
                    // authoritative for combat values. Progression contributes
                    // unlock state and the resolved production count only.
                    SpawnSnapshot snapshot = definition.CreateSnapshotWithProductionBonus(
                        progressionSnapshot.PerMonsterAliveLimit,
                        monster.ProductionBonusCount);
                    if (!snapshot.IsValid)
                    {
                        failureResult = new SpawnStartResult(
                            SpawnStartStatus.InvalidRequest,
                            "Monster definition produced invalid final stats: " + monster.MonsterId);
                        return false;
                    }
                    entries.Add(new SpawnPlanEntry(snapshot, definition.VisualPrefab));
                }
                if (entries.Count == 0)
                {
                    failureResult = new SpawnStartResult(SpawnStartStatus.InvalidRequest, "No enabled monster.");
                    return false;
                }
                spawnPlan = new SpawnPlan(entries, progressionSnapshot.GlobalAliveLimit);
                return true;
            }

            if (monsterDefinition != null)
            {
                return TryCreateSpawnPlan(
                    monsterDefinition,
                    progressionSnapshot,
                    out spawnPlan,
                    out failureResult);
            }

            // Scene references can be temporarily empty while Unity reloads
            // assemblies or when this controller is copied to a fresh scene.
            // Load the authored prototype definition explicitly so a Player
            // build does not depend on an already-loaded asset instance.
            MonsterDefinition resourceDefinition =
                Resources.Load<MonsterDefinition>(PrototypeSlimeResourcePath);
            if (resourceDefinition != null &&
                resourceDefinition.MonsterId == "monster.slime")
            {
                monsterDefinition = resourceDefinition;
                return TryCreateSpawnPlan(
                    monsterDefinition,
                    progressionSnapshot,
                    out spawnPlan,
                    out failureResult);
            }

            MonsterDefinition[] loadedDefinitions =
                Resources.FindObjectsOfTypeAll<MonsterDefinition>();
            foreach (MonsterDefinition loadedDefinition in loadedDefinitions)
            {
                if (loadedDefinition == null ||
                    loadedDefinition.MonsterId != "monster.slime")
                {
                    continue;
                }

                monsterDefinition = loadedDefinition;
                return TryCreateSpawnPlan(
                    monsterDefinition,
                    progressionSnapshot,
                    out spawnPlan,
                    out failureResult);
            }

            if (!allowPrototypeFallback)
            {
                failureResult = new SpawnStartResult(
                    SpawnStartStatus.MissingDefinition,
                    "MonsterDefinition reference is missing. " +
                    "Assign an authored definition or explicitly enable the test-only fallback.");
                return false;
            }

            // This branch is intentionally opt-in for tests only. A missing
            // authored definition must never become a successful production
            // run with silently invented balance values.
            Debug.LogWarning(
                "MonsterDefinition reference is missing. Using the explicitly enabled prototype Slime fallback snapshot.",
                this);
            SpawnSnapshot fallbackSnapshot = new SpawnSnapshot(
                "monster.slime",
                "walker_stump",
                30,
                1.5f,
                1,
                aliveLimit,
                3);
            if (!fallbackSnapshot.IsValid)
            {
                failureResult = new SpawnStartResult(
                    SpawnStartStatus.MissingDefinition,
                    "The explicitly enabled prototype fallback snapshot is invalid.");
                return false;
            }

            spawnPlan = new SpawnPlan(
                ApplyProgressionToSpawnSnapshot(fallbackSnapshot, progressionSnapshot),
                prototypeFallbackPrefab);
            return true;
        }

        private bool TryCreateSpawnPlan(
            MonsterDefinition definition,
            ProgressionCombatSnapshot progressionSnapshot,
            out SpawnPlan spawnPlan,
            out SpawnStartResult failureResult)
        {
            spawnPlan = null;
            SpawnSnapshot snapshot = ApplyProgressionToSpawnSnapshot(
                definition.CreateSnapshot(aliveLimit),
                progressionSnapshot);
            if (!snapshot.IsValid)
            {
                failureResult = new SpawnStartResult(
                    SpawnStartStatus.MissingDefinition,
                    $"MonsterDefinition '{definition.name}' produced an invalid snapshot.");
                return false;
            }

            spawnPlan = new SpawnPlan(snapshot, definition.VisualPrefab);
            failureResult = new SpawnStartResult(
                SpawnStartStatus.Started,
                "Monster definition snapshot and prefab are ready.");
            return true;
        }

        private static SpawnSnapshot ApplyProgressionToSpawnSnapshot(
            SpawnSnapshot authored,
            ProgressionCombatSnapshot progressionSnapshot)
        {
            if (progressionSnapshot == null ||
                progressionSnapshot.Monsters == null ||
                progressionSnapshot.Monsters.Count == 0)
            {
                return authored;
            }

            // The prototype spawner consumes one entry. Select the matching
            // progression species and add its production bonus to the
            // definition-owned BaseStats and behavior-profile contributions.
            for (int i = 0; i < progressionSnapshot.Monsters.Count; i++)
            {
                MonsterCombatSnapshot monster = progressionSnapshot.Monsters[i];
                bool matchesAuthored = string.Equals(
                    monster.MonsterId,
                    authored.MonsterId,
                    StringComparison.Ordinal) ||
                    (authored.MonsterId == "monster.slime" &&
                     monster.MonsterId == "monster.01");
                if (!monster.Unlocked || !matchesAuthored)
                {
                    continue;
                }

                return new SpawnSnapshot(
                    authored.MonsterId,
                    authored.PrefabKey,
                    authored.MaxHealth,
                    authored.SpawnIntervalSeconds,
                    AddProductionBonus(
                        authored.PackSize,
                        monster.ProductionBonusCount),
                    authored.AliveLimit,
                    authored.GarnetReward,
                    authored.BonusDropCurrencyId,
                    authored.BonusDropAmount,
                    authored.BonusDropChancePercent,
                    authored.BehaviorType,
                    authored.MovementMode,
                    authored.MoveSpeed,
                    authored.VisualScale,
                    authored.HitAreaWidth,
                    authored.HitAreaHeight,
                    authored.OrbitRadius,
                    authored.OrbitAngularSpeedDegrees);
            }

            return authored;
        }

        private static int AddProductionBonus(int basePackSize, int bonusCount)
        {
            long result = (long)Mathf.Max(1, basePackSize) + Mathf.Max(0, bonusCount);
            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }
    }
}
