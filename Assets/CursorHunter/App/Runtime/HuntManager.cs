using System;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;
using UnityEngine;

namespace CursorHunter.App
{
    /// <summary>
    /// Hunt composition root for the first normal-field prototype run.
    /// It converts authored Data into immutable run snapshots and connects the
    /// cursor, combat session, spawner, and result HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HuntManager : MonoBehaviour
    {
        private const string PrototypeSlimeResourcePath =
            "MonsterDefinitions/SlimeDefinition";

        [Header("Runtime systems")]
        [SerializeField] private MainCursorController cursorController;
        [SerializeField] private CombatRunController combatRunController;
        [SerializeField] private MonsterSpawner monsterSpawner;
        [SerializeField] private PrototypeRunHud runHud;
        [SerializeField] private TestPanelToggleController testPanelToggleController;

        [Header("Hunt combat parameters")]
        [SerializeField, Min(1)] private long attackPower = 10;
        [SerializeField, Min(0.01f)] private float rangeMultiplier = 1f;
        [SerializeField, Min(0f)] private float attackCooldownSeconds = 0.5f;
        [SerializeField, Min(1)] private int hitsPerBundle = 1;

        [Header("Hunt run")]
        [SerializeField] private MonsterDefinition monsterDefinition;
        [SerializeField, Min(1)] private int aliveLimit = 80;
        [SerializeField, Min(1f)] private float durationSeconds = 60f;
        [SerializeField, Min(1)] private int schemaVersion = 1;
        [SerializeField, Min(1)] private int balanceVersion = 1;
        [Tooltip("Test-only escape hatch. Production runs must fail when the authored definition is missing.")]
        [SerializeField] private bool allowPrototypeFallback;
        [SerializeField] private GameObject prototypeFallbackPrefab;

        private RunCoordinator _runCoordinator;
        private bool _coordinatorEventsSubscribed;

        private void Awake()
        {
            ResolveReferences();
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

            if (attackPower < 1L)
            {
                attackPower = 1L;
            }

            rangeMultiplier = Mathf.Max(0.01f, rangeMultiplier);
            attackCooldownSeconds = Mathf.Max(0f, attackCooldownSeconds);
            hitsPerBundle = Mathf.Max(1, hitsPerBundle);
            aliveLimit = Mathf.Max(1, aliveLimit);
            durationSeconds = Mathf.Max(1f, durationSeconds);
            schemaVersion = Mathf.Max(1, schemaVersion);
            balanceVersion = Mathf.Max(1, balanceVersion);

            CombatSnapshot combatSnapshot = new CombatSnapshot(
                attackPower,
                rangeMultiplier,
                attackCooldownSeconds,
                hitsPerBundle);
            if (!TryCreateSpawnPlan(
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
                    spawnPlan,
                    out string failureReason))
            {
                Debug.LogWarning(
                    $"HuntManager could not start run '{runRequest.RunId}': " +
                    failureReason,
                    this);
                return;
            }

            cursorController.SetRangeMultiplier(combatSnapshot.RangeMultiplier);
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
        }

        private void Update()
        {
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

        private void HandleRunCompleted(RunResult result)
        {
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
            out SpawnPlan spawnPlan,
            out SpawnStartResult failureResult)
        {
            spawnPlan = null;
            failureResult = new SpawnStartResult(
                SpawnStartStatus.Started,
                "Monster definition snapshot is ready.");

            if (monsterDefinition != null)
            {
                return TryCreateSpawnPlan(
                    monsterDefinition,
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
                fallbackSnapshot,
                prototypeFallbackPrefab);
            return true;
        }

        private bool TryCreateSpawnPlan(
            MonsterDefinition definition,
            out SpawnPlan spawnPlan,
            out SpawnStartResult failureResult)
        {
            spawnPlan = null;
            SpawnSnapshot snapshot = definition.CreateSnapshot(aliveLimit);
            if (!snapshot.IsValid)
            {
                failureResult = new SpawnStartResult(
                    SpawnStartStatus.MissingDefinition,
                    $"MonsterDefinition '{definition.name}' produced an invalid snapshot.");
                return false;
            }

            spawnPlan = new SpawnPlan(snapshot, definition.Prefab);
            failureResult = new SpawnStartResult(
                SpawnStartStatus.Started,
                "Monster definition snapshot and prefab are ready.");
            return true;
        }
    }
}
