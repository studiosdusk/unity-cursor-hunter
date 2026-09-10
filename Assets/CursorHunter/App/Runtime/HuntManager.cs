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

        private bool _runStarted;

        private void Awake()
        {
            ResolveReferences();
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
            if (combatRunController != null)
            {
                combatRunController.Completed += HandleRunCompleted;
            }
        }

        /// <summary>
        /// Starts one prototype hunt from a UI Button. The prototype no longer
        /// enters combat automatically when the scene is loaded.
        /// </summary>
        public void BeginPrototypeRun()
        {
            ResolveReferences();

            if (_runStarted ||
                (combatRunController != null && combatRunController.IsRunning))
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

            CombatSnapshot combatSnapshot = new CombatSnapshot(
                attackPower,
                rangeMultiplier,
                attackCooldownSeconds,
                hitsPerBundle);
            SpawnSnapshot spawnSnapshot = CreateSpawnSnapshot();
            RunRequest runRequest = new RunRequest(
                $"prototype-{GetInstanceID()}",
                durationSeconds);

            cursorController.SetRangeMultiplier(combatSnapshot.RangeMultiplier);
            cursorController.ShowCursorImage();

            if (testPanelToggleController != null)
            {
                testPanelToggleController.HideTestPanel();
            }

            combatRunController.StartRun(runRequest, combatSnapshot);
            monsterSpawner.StartRun(spawnSnapshot);

            if (runHud != null)
            {
                runHud.Initialize();
                runHud.ShowRunning(
                    combatRunController.RemainingSeconds,
                    combatRunController.DefeatedCount,
                    combatRunController.GarnetEarned);
            }

            _runStarted = true;
        }

        /// <summary>
        /// Ends the current prototype run from the test panel.
        /// </summary>
        public void EndPrototypeRun()
        {
            if (combatRunController == null || !combatRunController.IsRunning)
            {
                return;
            }

            combatRunController.CompleteRun("manual_test");
        }

        /// <summary>
        /// Stops the current test run, removes spawned monsters, hides the
        /// cursor, and clears the prototype HUD without showing a result.
        /// </summary>
        public void ResetPrototypeRun()
        {
            _runStarted = false;

            if (combatRunController != null && combatRunController.IsRunning)
            {
                // Disable this controller's completion handling first so a
                // reset does not present a normal result screen.
                combatRunController.CompleteRun("manual_reset");
            }

            if (monsterSpawner != null)
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
            if (!_runStarted || combatRunController == null || !combatRunController.IsRunning)
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
            if (!_runStarted)
            {
                return;
            }

            _runStarted = false;
            monsterSpawner.StopRun();
            cursorController.HideCursorImage();

            if (runHud != null)
            {
                runHud.ShowResult(result);
            }
        }

        private void OnDisable()
        {
            if (combatRunController != null)
            {
                combatRunController.Completed -= HandleRunCompleted;
            }
        }

        private SpawnSnapshot CreateSpawnSnapshot()
        {
            if (monsterDefinition != null)
            {
                return monsterDefinition.CreateSnapshot(aliveLimit);
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
                return monsterDefinition.CreateSnapshot(aliveLimit);
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
                return monsterDefinition.CreateSnapshot(aliveLimit);
            }

            // Keep the scene playable if a manually edited scene reference was
            // lost while Unity was reloading assemblies. The authored SO is
            // still the normal path; these values mirror SlimeDefinition.asset.
            Debug.LogWarning(
                "MonsterDefinition reference is missing. Using the prototype Slime fallback snapshot.",
                this);
            return new SpawnSnapshot(
                "monster.slime",
                "walker_stump",
                30,
                1.5f,
                1,
                aliveLimit,
                3);
        }
    }
}
