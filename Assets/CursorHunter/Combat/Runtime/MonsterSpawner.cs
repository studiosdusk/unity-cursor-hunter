using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Prototype normal-field spawner. It receives an immutable SpawnPlan
    /// rather than reading Data assets, then creates its authored prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterSpawner : MonoBehaviour
    {
        [SerializeField] private CombatRunController combatRunController;
        [SerializeField] private Transform spawnedEnemyRoot;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float spawnPlaneZ;
        [SerializeField, Min(0f)] private float horizontalPadding = 0.02f;
        [SerializeField, Min(0f)] private float bottomPadding = 0.09f;
        [SerializeField, Min(0f)] private float topPadding = 0.15f;

        private readonly List<WalkerStumpTarget> _spawnedTargets =
            new List<WalkerStumpTarget>();

        private RunId _runId;
        private SpawnSnapshot _spawnSnapshot;
        private GameObject _spawnPrefab;
        private SeededRandom _random;
        private IReadOnlyList<SpawnPlanEntry> _entries;
        private float[] _nextSpawnTimes;
        [SerializeField, Min(1)] private int globalAliveLimit = 80;
        private bool _isSpawning;
        private bool _spawnFailureLogged;

        public int ActiveSpawnedCount
        {
            get
            {
                PruneDestroyedTargets();

                int count = 0;
                foreach (WalkerStumpTarget target in _spawnedTargets)
                {
                    if (target != null && !target.IsDead)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void Awake()
        {
            if (combatRunController == null)
            {
                combatRunController = GetComponent<CombatRunController>();
            }

            if (combatRunController == null)
            {
                combatRunController = FindFirstObjectByType<CombatRunController>(
                    FindObjectsInactive.Include);
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (spawnedEnemyRoot == null)
            {
                Transform configuredRoot = transform.Find("SpawnedEnemies");
                spawnedEnemyRoot = configuredRoot != null
                    ? configuredRoot
                    : transform;
            }

            horizontalPadding = Mathf.Clamp01(horizontalPadding);
            bottomPadding = Mathf.Clamp01(bottomPadding);
            topPadding = Mathf.Clamp01(topPadding);
        }

        private void Update()
        {
            if (!_isSpawning || combatRunController == null)
            {
                return;
            }

            if (!combatRunController.IsRunActive)
            {
                _isSpawning = false;
                return;
            }

            if (combatRunController.IsPaused)
            {
                return;
            }

            float elapsedSeconds = combatRunController.ElapsedSeconds;
            // Bounded work: at most one spawn opportunity per species per frame.
            for (int i = 0; i < _entries.Count; i++)
            {
                if (elapsedSeconds < _nextSpawnTimes[i]) continue;
                _spawnSnapshot = _entries[i].Snapshot;
                _spawnPrefab = _entries[i].Prefab;
                if (ActiveSpawnedCount < globalAliveLimit && !SpawnPack(out _))
                {
                    _isSpawning = false;
                    return;
                }
                _nextSpawnTimes[i] = elapsedSeconds + _spawnSnapshot.SpawnIntervalSeconds;
            }
        }

        public SpawnStartResult StartRun(
            RunRequest request,
            SpawnPlan spawnPlan)
        {
            if (_isSpawning || _spawnedTargets.Count > 0)
            {
                Debug.LogWarning(
                    "MonsterSpawner cannot start while a previous spawn set is active.",
                    this);
                return new SpawnStartResult(
                    SpawnStartStatus.AlreadyRunning,
                    "MonsterSpawner already owns an active spawn set.");
            }

            if (!request.IsValid ||
                spawnPlan == null ||
                !spawnPlan.HasEntries ||
                !spawnPlan.Entries[0].HasValidSnapshot)
            {
                return new SpawnStartResult(
                    SpawnStartStatus.InvalidRequest,
                    "RunRequest or SpawnPlan is invalid.");
            }

            foreach (var entry in spawnPlan.Entries)
            {
                if (!entry.HasValidSnapshot || !entry.HasPrefab)
                    return new SpawnStartResult(entry.HasPrefab ? SpawnStartStatus.InvalidRequest : SpawnStartStatus.MissingPrefab,
                        "Every spawn entry requires valid data and a prefab.");
            }

            if (combatRunController == null)
            {
                Debug.LogWarning("MonsterSpawner requires a CombatRunController.", this);
                return new SpawnStartResult(
                    SpawnStartStatus.InvalidRequest,
                    "MonsterSpawner requires a CombatRunController.");
            }

            _runId = request.RunId;
            globalAliveLimit = spawnPlan.GlobalAliveLimit;
            _entries = spawnPlan.Entries;
            _nextSpawnTimes = new float[_entries.Count];
            _random = new SeededRandom(request.Seed);
            _spawnFailureLogged = false;
            _isSpawning = true;
            for (int i = 0; i < _entries.Count; i++)
            {
                _spawnSnapshot = _entries[i].Snapshot;
                _spawnPrefab = _entries[i].Prefab;
                if (!SpawnPack(out SpawnStartStatus failureStatus))
                {
                    StopRun();
                    return new SpawnStartResult(failureStatus, "Initial monster pack failed.");
                }
                _nextSpawnTimes[i] = _spawnSnapshot.SpawnIntervalSeconds;
            }
            return new SpawnStartResult(SpawnStartStatus.Started, "All monster packs prepared.");
        }

        public void StopRun()
        {
            _isSpawning = false;

            for (int index = _spawnedTargets.Count - 1; index >= 0; index--)
            {
                WalkerStumpTarget target = _spawnedTargets[index];
                if (target == null)
                {
                    continue;
                }

                target.Unregister();
                Destroy(target.gameObject);
            }

            _spawnedTargets.Clear();
            _runId = default;
            _spawnPrefab = null;
            _entries = null;
            _nextSpawnTimes = null;
        }

        private bool SpawnPack(out SpawnStartStatus failureStatus)
        {
            failureStatus = SpawnStartStatus.Started;

            int speciesCount = 0;
            foreach (var target in _spawnedTargets)
                if (target != null && !target.IsDead && target.MonsterId == _spawnSnapshot.MonsterId) speciesCount++;
            int remainingCapacity = Mathf.Min(_spawnSnapshot.AliveLimit - speciesCount,
                Mathf.Max(1, globalAliveLimit) - ActiveSpawnedCount);
            int spawnCount = Mathf.Min(
                _spawnSnapshot.PackSize,
                Mathf.Max(0, remainingCapacity));
            int firstNewTargetIndex = _spawnedTargets.Count;

            for (int index = 0; index < spawnCount; index++)
            {
                if (!TryGetSpawnPosition(out Vector3 spawnPosition))
                {
                    LogSpawnFailure(
                        "MonsterSpawner could not find a valid spawn position.");
                    failureStatus = SpawnStartStatus.NoSpawnPosition;
                    RollbackPack(firstNewTargetIndex);
                    return false;
                }

                if (!TryInstantiateSpawnPrefab(
                        spawnPosition,
                        out GameObject instance))
                {
                    failureStatus = SpawnStartStatus.InitialSpawnFailed;
                    RollbackPack(firstNewTargetIndex);
                    return false;
                }

                if (!TryInitializeTarget(
                        instance,
                        out WalkerStumpTarget target))
                {
                    failureStatus = SpawnStartStatus.InitialSpawnFailed;
                    if (target != null)
                    {
                        target.Unregister();
                    }

                    if (instance != null)
                    {
                        Destroy(instance);
                    }

                    RollbackPack(firstNewTargetIndex);
                    return false;
                }

                _spawnedTargets.Add(target);
            }

            return spawnCount == 0 ||
                   _spawnedTargets.Count - firstNewTargetIndex == spawnCount;
        }

        private bool TryInitializeTarget(
            GameObject instance,
            out WalkerStumpTarget target)
        {
            target = null;

            try
            {
                target = instance.GetComponent<WalkerStumpTarget>();
                if (target == null)
                {
                    target = instance.AddComponent<WalkerStumpTarget>();
                }

                target.Initialize(_spawnSnapshot, _runId);
                if (!target.IsActive)
                {
                    return false;
                }

                return target.EnsureCombatCollider() != null;
            }
            catch (System.Exception exception)
            {
                LogSpawnFailure(
                    $"MonsterSpawner failed to initialize a spawned target. " +
                    $"{exception.GetType().Name}: {exception.Message}");
                return false;
            }
        }

        private void RollbackPack(int firstNewTargetIndex)
        {
            for (int index = _spawnedTargets.Count - 1;
                 index >= firstNewTargetIndex;
                 index--)
            {
                WalkerStumpTarget target = _spawnedTargets[index];
                if (target == null)
                {
                    _spawnedTargets.RemoveAt(index);
                    continue;
                }

                target.Unregister();
                Destroy(target.gameObject);

                _spawnedTargets.RemoveAt(index);
            }
        }

        private bool TryInstantiateSpawnPrefab(
            Vector3 spawnPosition,
            out GameObject instance)
        {
            instance = null;

            if (_spawnPrefab == null)
            {
                LogSpawnFailure(
                    "MonsterSpawner could not create a target because the spawn prefab is missing.");
                return false;
            }

            try
            {
                // Clone the root Transform explicitly. Some Unity prefab
                // references resolve the cloned native object as a Transform
                // even though the serialized source field is a GameObject.
                // Keeping the requested type as Transform avoids the invalid
                // GameObject cast while preserving the complete prefab tree.
                Transform cloneTransform = UnityEngine.Object.Instantiate(
                    _spawnPrefab.transform,
                    spawnPosition,
                    Quaternion.identity,
                    spawnedEnemyRoot);
                instance = cloneTransform != null
                    ? cloneTransform.gameObject
                    : null;
            }
            catch (System.Exception exception)
            {
                LogSpawnFailure(
                    $"MonsterSpawner failed to instantiate spawn prefab " +
                    $"'{_spawnPrefab.name}'. {exception.GetType().Name}: {exception.Message}");
                return false;
            }

            if (instance == null)
            {
                LogSpawnFailure(
                    "MonsterSpawner could not resolve the cloned spawn prefab root GameObject. " +
                    $"Source type: {_spawnPrefab.GetType().Name}.");
                return false;
            }

            instance.SetActive(true);
            return true;
        }

        private void LogSpawnFailure(string message)
        {
            if (_spawnFailureLogged)
            {
                return;
            }

            _spawnFailureLogged = true;
            Debug.LogError(message, this);
        }

        private bool TryGetSpawnPosition(out Vector3 spawnPosition)
        {
            if (worldCamera == null)
            {
                spawnPosition = Vector3.zero;
                return false;
            }

            float minX = Mathf.Clamp01(horizontalPadding);
            float maxX = Mathf.Clamp01(1f - horizontalPadding);
            float minY = Mathf.Clamp01(bottomPadding);
            float maxY = Mathf.Clamp01(1f - topPadding);

            if (minX >= maxX || minY >= maxY)
            {
                spawnPosition = Vector3.zero;
                return false;
            }

            float cameraDistance = Mathf.Abs(
                spawnPlaneZ - worldCamera.transform.position.z);
            Vector3 viewportPosition = new Vector3(
                _random.NextFloat(minX, maxX),
                _random.NextFloat(minY, maxY),
                cameraDistance);

            spawnPosition = worldCamera.ViewportToWorldPoint(viewportPosition);
            spawnPosition.z = spawnPlaneZ;
            return true;
        }

        private void PruneDestroyedTargets()
        {
            for (int index = _spawnedTargets.Count - 1; index >= 0; index--)
            {
                if (_spawnedTargets[index] == null)
                {
                    _spawnedTargets.RemoveAt(index);
                }
            }
        }

        private void OnDisable()
        {
            StopRun();
        }
    }
}
