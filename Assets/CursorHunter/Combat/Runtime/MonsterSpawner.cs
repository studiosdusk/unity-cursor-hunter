using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Normal-field spawner. It receives an immutable SpawnPlan rather than
    /// reading Data assets, then composes the shared root with each species visual.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterSpawner : MonoBehaviour
    {
        [SerializeField] private CombatRunController combatRunController;
        [SerializeField] private Transform spawnedEnemyRoot;
        [SerializeField] private Camera worldCamera;
        [SerializeField, Tooltip("Shared combat root used by every monster species.")]
        private GameObject monsterRootPrefab;
        [SerializeField] private float spawnPlaneZ;
        [SerializeField, Min(0f)] private float horizontalPadding = 0.02f;
        [SerializeField, Min(0f)] private float bottomPadding = 0.09f;
        [SerializeField, Min(0f)] private float topPadding = 0.15f;

        private readonly List<MonsterCombatTarget> _spawnedTargets =
            new List<MonsterCombatTarget>();

        private RunId _runId;
        private SpawnSnapshot _spawnSnapshot;
        private GameObject _spawnVisualPrefab;
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
                foreach (MonsterCombatTarget target in _spawnedTargets)
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
                _spawnVisualPrefab = _entries[i].VisualPrefab;
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

            if (!HasValidMonsterRootPrefab(out string rootError))
            {
                return new SpawnStartResult(
                    monsterRootPrefab == null
                        ? SpawnStartStatus.MissingPrefab
                        : SpawnStartStatus.InvalidRequest,
                    rootError);
            }

            foreach (var entry in spawnPlan.Entries)
            {
                if (!entry.HasValidSnapshot || !entry.HasVisualPrefab)
                {
                    return new SpawnStartResult(
                        entry.HasVisualPrefab
                            ? SpawnStartStatus.InvalidRequest
                            : SpawnStartStatus.MissingPrefab,
                        "Every spawn entry requires valid data and a species visual prefab.");
                }
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
                _spawnVisualPrefab = _entries[i].VisualPrefab;
                if (!SpawnPack(out SpawnStartStatus failureStatus))
                {
                    StopRun();
                    return new SpawnStartResult(failureStatus, "Initial monster pack failed.");
                }
                _nextSpawnTimes[i] = _spawnSnapshot.SpawnIntervalSeconds;
            }
            return new SpawnStartResult(SpawnStartStatus.Started, "All monster packs prepared.");
        }

        private bool HasValidMonsterRootPrefab(out string error)
        {
            error = string.Empty;
            if (monsterRootPrefab == null)
            {
                error = "MonsterSpawner requires the shared MonsterRoot prefab.";
                return false;
            }

            Transform root = monsterRootPrefab.transform;
            Transform hitArea = root.Find("HitArea");
            Transform healthBarAnchor = root.Find("HealthBarAnchor");
            if (root.GetComponent<MonsterCombatTarget>() == null ||
                root.GetComponent<MonsterBehaviorController>() == null ||
                root.Find("VisualRoot") == null ||
                hitArea == null || hitArea.GetComponent<BoxCollider2D>() == null ||
                healthBarAnchor == null ||
                healthBarAnchor.GetComponent<MonsterHealthBarView>() == null)
            {
                error = "MonsterRoot must contain MonsterCombatTarget, " +
                        "MonsterBehaviorController, HitArea/BoxCollider2D, " +
                        "HealthBarAnchor/MonsterHealthBarView, " +
                        "and VisualRoot direct children.";
                return false;
            }

            return true;
        }

        public void StopRun()
        {
            _isSpawning = false;

            for (int index = _spawnedTargets.Count - 1; index >= 0; index--)
            {
                MonsterCombatTarget target = _spawnedTargets[index];
                if (target == null)
                {
                    continue;
                }

                target.Unregister();
                Destroy(target.gameObject);
            }

            _spawnedTargets.Clear();
            _runId = default;
            _spawnVisualPrefab = null;
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
                        out MonsterCombatTarget target))
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
            out MonsterCombatTarget target)
        {
            target = null;

            try
            {
                target = instance.GetComponent<MonsterCombatTarget>();
                if (target == null)
                {
                    target = instance.AddComponent<MonsterCombatTarget>();
                }

                target.Initialize(_spawnSnapshot, _runId);
                if (!target.IsActive)
                {
                    return false;
                }

                if (target.EnsureCombatCollider() == null ||
                    !TryGetMovementBounds(out Bounds movementBounds))
                {
                    return false;
                }

                return target.ConfigureMovement(
                    _spawnSnapshot,
                    combatRunController,
                    movementBounds,
                    _random.NextUInt64());
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
                MonsterCombatTarget target = _spawnedTargets[index];
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

            if (monsterRootPrefab == null || _spawnVisualPrefab == null)
            {
                LogSpawnFailure(
                    "MonsterSpawner requires both the shared MonsterRoot and a species visual prefab.");
                return false;
            }

            try
            {
                Transform cloneTransform = UnityEngine.Object.Instantiate(
                    monsterRootPrefab.transform,
                    spawnPosition,
                    Quaternion.identity,
                    spawnedEnemyRoot);
                instance = cloneTransform != null
                    ? cloneTransform.gameObject
                    : null;

                if (instance == null)
                {
                    LogSpawnFailure(
                        "MonsterSpawner could not resolve the shared MonsterRoot clone.");
                    return false;
                }

                Transform visualRoot = instance.transform.Find("VisualRoot");
                if (visualRoot == null)
                {
                    LogSpawnFailure(
                        "The shared MonsterRoot prefab must contain a direct child named VisualRoot.");
                    Destroy(instance);
                    instance = null;
                    return false;
                }

                Transform visualClone = UnityEngine.Object.Instantiate(
                    _spawnVisualPrefab.transform,
                    visualRoot,
                    false);
                if (visualClone == null)
                {
                    LogSpawnFailure(
                        $"MonsterSpawner could not create visual '{_spawnVisualPrefab.name}'.");
                    Destroy(instance);
                    instance = null;
                    return false;
                }

                visualClone.name = _spawnVisualPrefab.name;
                visualClone.localPosition = Vector3.zero;
                visualClone.localRotation = Quaternion.identity;
                visualClone.localScale = _spawnVisualPrefab.transform.localScale;
                visualClone.gameObject.SetActive(true);

                // Supplier prefabs are visual-only under the shared root.
                // Hit testing belongs to MonsterRoot/HitArea so vendor collider
                // shapes cannot change combat reach from one species to another.
                Collider2D[] visualColliders =
                    visualClone.GetComponentsInChildren<Collider2D>(true);
                foreach (Collider2D visualCollider in visualColliders)
                {
                    if (visualCollider != null)
                    {
                        visualCollider.enabled = false;
                    }
                }
            }
            catch (System.Exception exception)
            {
                LogSpawnFailure(
                    $"MonsterSpawner failed to instantiate the shared MonsterRoot or " +
                    $"visual '{_spawnVisualPrefab.name}'. " +
                    $"{exception.GetType().Name}: {exception.Message}");
                if (instance != null)
                {
                    Destroy(instance);
                    instance = null;
                }
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

        private bool TryGetMovementBounds(out Bounds movementBounds)
        {
            movementBounds = default;
            if (worldCamera == null)
            {
                return false;
            }

            float minX = Mathf.Clamp01(horizontalPadding);
            float maxX = Mathf.Clamp01(1f - horizontalPadding);
            float minY = Mathf.Clamp01(bottomPadding);
            float maxY = Mathf.Clamp01(1f - topPadding);
            if (minX >= maxX || minY >= maxY)
            {
                return false;
            }

            float cameraDistance = Mathf.Abs(
                spawnPlaneZ - worldCamera.transform.position.z);
            Vector3 bottomLeft = worldCamera.ViewportToWorldPoint(
                new Vector3(minX, minY, cameraDistance));
            Vector3 topRight = worldCamera.ViewportToWorldPoint(
                new Vector3(maxX, maxY, cameraDistance));
            float worldMinX = Mathf.Min(bottomLeft.x, topRight.x);
            float worldMaxX = Mathf.Max(bottomLeft.x, topRight.x);
            float worldMinY = Mathf.Min(bottomLeft.y, topRight.y);
            float worldMaxY = Mathf.Max(bottomLeft.y, topRight.y);
            worldMinX += _spawnSnapshot.HitAreaWidth * 0.5f;
            worldMaxX -= _spawnSnapshot.HitAreaWidth * 0.5f;
            worldMinY += _spawnSnapshot.HitAreaHeight * 0.5f;
            worldMaxY -= _spawnSnapshot.HitAreaHeight * 0.5f;
            if (worldMaxX <= worldMinX || worldMaxY <= worldMinY)
            {
                return false;
            }

            movementBounds = new Bounds(
                new Vector3(
                    (worldMinX + worldMaxX) * 0.5f,
                    (worldMinY + worldMaxY) * 0.5f,
                    spawnPlaneZ),
                new Vector3(
                    worldMaxX - worldMinX,
                    worldMaxY - worldMinY,
                    0f));
            return worldMaxX > worldMinX && worldMaxY > worldMinY;
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
