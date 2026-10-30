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
        [SerializeField] private GameObject bossRootPrefab;
        private GameObject _runRootPrefab;
        private bool _bossRun;
        private MonsterCombatTarget _bossDeathVisual;
        public MonsterCombatTarget BossTarget => _bossRun && _spawnedTargets.Count > 0 ? _spawnedTargets[0] : null;
        public bool IsBossDeathPlaying => _bossDeathVisual != null && _bossDeathVisual.gameObject.activeInHierarchy;
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
        private bool[] _enabledSpecies;
        private SpawnPlan _currentSpawnPlan;
        [SerializeField, Min(1)] private int globalAliveLimit = 80;
        private bool _isSpawning;
        private bool _spawnFailureLogged;

        public SpawnPlan CurrentSpawnPlan => _currentSpawnPlan;

        public bool IsSpeciesEnabled(string monsterId)
        {
            if (_entries == null || _enabledSpecies == null) return false;
            for (int index = 0; index < _entries.Count; index++)
                if (_entries[index].Snapshot.MonsterId == monsterId) return _enabledSpecies[index];
            return false;
        }

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

            if (_bossRun) return; // One boss per attempt, never respawn it.
            float elapsedSeconds = combatRunController.ElapsedSeconds;
            // Bounded work: at most one spawn opportunity per species per frame.
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!_enabledSpecies[i]) continue;
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
            return StartRun(request, spawnPlan, null);
        }

        public SpawnStartResult StartRun(
            RunRequest request,
            SpawnPlan spawnPlan,
            IReadOnlyList<string> enabledMonsterIds)
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

            _bossRun = request.Mode == RunMode.Boss;
            _runRootPrefab = _bossRun && bossRootPrefab != null ? bossRootPrefab : monsterRootPrefab;
            if (_bossRun && (spawnPlan.Entries.Count != 1 || spawnPlan.Entries[0].Snapshot.PackSize != 1 ||
                spawnPlan.Entries[0].Snapshot.MonsterId != request.BossId))
                return new SpawnStartResult(SpawnStartStatus.InvalidRequest, "Boss runs require exactly one matching boss.");
            if (!HasValidMonsterRootPrefab(out string rootError))
            {
                return new SpawnStartResult(
                    monsterRootPrefab == null
                        ? SpawnStartStatus.MissingPrefab
                        : SpawnStartStatus.InvalidRequest,
                    rootError);
            }

            if (!TryValidatePlan(spawnPlan, enabledMonsterIds,
                    out bool[] enabledSpecies, out string planError))
            {
                SpawnStartStatus status = SpawnStartStatus.InvalidRequest;
                foreach (SpawnPlanEntry entry in spawnPlan.Entries)
                    if (!entry.HasVisualPrefab) status = SpawnStartStatus.MissingPrefab;
                return new SpawnStartResult(status, planError);
            }
            bool anyEnabled = false;
            for (int index = 0; index < enabledSpecies.Length; index++)
                anyEnabled |= enabledSpecies[index];
            if (!anyEnabled)
            {
                return new SpawnStartResult(SpawnStartStatus.InvalidRequest,
                    "At least one monster species must be enabled to start a run.");
            }

            if (combatRunController == null)
            {
                Debug.LogWarning("MonsterSpawner requires a CombatRunController.", this);
                return new SpawnStartResult(
                    SpawnStartStatus.InvalidRequest,
                    "MonsterSpawner requires a CombatRunController.");
            }

            ClearBossDeathVisual();
            _runId = request.RunId;
            globalAliveLimit = spawnPlan.GlobalAliveLimit;
            _entries = spawnPlan.Entries;
            _currentSpawnPlan = spawnPlan;
            _enabledSpecies = enabledSpecies;
            _nextSpawnTimes = new float[_entries.Count];
            _random = new SeededRandom(request.Seed);
            _spawnFailureLogged = false;
            _isSpawning = true;
            for (int i = 0; i < _entries.Count; i++)
            {
                _nextSpawnTimes[i] = _entries[i].Snapshot.SpawnIntervalSeconds;
                if (!_enabledSpecies[i]) continue;
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

        /// <summary>Applies the complete candidate plan without restarting or reseeding this run.</summary>
        public bool TryApplyBalance(
            SpawnPlan plan,
            IReadOnlyList<string> enabledMonsterIds,
            out string error)
        {
            if (!TryPrepareBalance(plan, enabledMonsterIds, out bool[] enabledSpecies,
                    out Dictionary<string, int> candidateIndices, out Bounds[] bounds, out error))
                return false;

            float elapsed = combatRunController.ElapsedSeconds;
            var nextSpawnTimes = new float[plan.Entries.Count];
            for (int index = 0; index < plan.Entries.Count; index++)
                nextSpawnTimes[index] = elapsed + plan.Entries[index].Snapshot.SpawnIntervalSeconds;
            for (int previousIndex = 0; previousIndex < _entries.Count; previousIndex++)
            {
                int candidateIndex = candidateIndices[_entries[previousIndex].Snapshot.MonsterId];
                if (!_enabledSpecies[previousIndex] || !enabledSpecies[candidateIndex]) continue;
                float remainingRatio = Mathf.Clamp01(
                    (_nextSpawnTimes[previousIndex] - elapsed) /
                    _entries[previousIndex].Snapshot.SpawnIntervalSeconds);
                nextSpawnTimes[candidateIndex] = elapsed + remainingRatio *
                    plan.Entries[candidateIndex].Snapshot.SpawnIntervalSeconds;
            }

            // Validation has finished for every entry and body before any combat state changes.
            foreach (MonsterCombatTarget target in _spawnedTargets)
            {
                if (target == null || target.IsDead) continue;
                int index = candidateIndices[target.MonsterId];
                target.TryApplyBalance(plan.Entries[index].Snapshot, bounds[index], out _);
            }
            globalAliveLimit = plan.GlobalAliveLimit;
            _currentSpawnPlan = plan;
            _entries = plan.Entries;
            _enabledSpecies = enabledSpecies;
            _nextSpawnTimes = nextSpawnTimes;
            error = string.Empty;
            return true;
        }

        public bool CanApplyBalance(
            SpawnPlan plan,
            IReadOnlyList<string> enabledMonsterIds,
            out string error)
        {
            return TryPrepareBalance(plan, enabledMonsterIds, out _, out _, out _, out error);
        }

        private bool TryPrepareBalance(
            SpawnPlan plan,
            IReadOnlyList<string> enabledMonsterIds,
            out bool[] enabledSpecies,
            out Dictionary<string, int> candidateIndices,
            out Bounds[] bounds,
            out string error)
        {
            enabledSpecies = null;
            candidateIndices = null;
            bounds = null;
            if (!_isSpawning || _entries == null || combatRunController == null ||
                !combatRunController.IsRunActive)
            {
                error = "실행 중인 몬스터 전투가 없습니다.";
                return false;
            }
            if (!TryValidatePlan(plan, enabledMonsterIds, out enabledSpecies, out error))
                return false;
            if (_bossRun && (plan.Entries.Count != 1 || plan.GlobalAliveLimit != 1 ||
                plan.Entries[0].Snapshot.MonsterId != _entries[0].Snapshot.MonsterId ||
                plan.Entries[0].Snapshot.PackSize != 1 || plan.Entries[0].Snapshot.AliveLimit != 1 ||
                !enabledSpecies[0]))
            {
                error = "보스 전투는 현재 보스 한 마리를 유지해야 합니다.";
                return false;
            }

            candidateIndices = new Dictionary<string, int>(System.StringComparer.Ordinal);
            bounds = new Bounds[plan.Entries.Count];
            for (int index = 0; index < plan.Entries.Count; index++)
            {
                SpawnSnapshot snapshot = plan.Entries[index].Snapshot;
                candidateIndices.Add(snapshot.MonsterId, index);
                if (!TryGetMovementBounds(snapshot, out bounds[index]))
                {
                    error = snapshot.MonsterId + ": 피격 영역이 전투 필드보다 크거나 카메라가 없습니다.";
                    return false;
                }
            }
            foreach (SpawnPlanEntry previousEntry in _entries)
            {
                if (!candidateIndices.ContainsKey(previousEntry.Snapshot.MonsterId))
                {
                    error = "기존 종을 삭제하지 말고 생성 사용 여부를 꺼 주세요.";
                    return false;
                }
            }
            foreach (MonsterCombatTarget target in _spawnedTargets)
            {
                if (target == null || target.IsDead) continue;
                if (!candidateIndices.TryGetValue(target.MonsterId, out int candidateIndex))
                {
                    error = target.MonsterId + ": 현재 개체의 종이 생성 계획에 없습니다.";
                    return false;
                }
                if (!target.CanApplyBalance(plan.Entries[candidateIndex].Snapshot, out error)) return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidatePlan(
            SpawnPlan plan,
            IReadOnlyList<string> enabledMonsterIds,
            out bool[] enabledSpecies,
            out string error)
        {
            enabledSpecies = null;
            error = string.Empty;
            if (plan == null || !plan.HasEntries)
            {
                error = "몬스터 생성 계획이 없습니다.";
                return false;
            }
            var knownIds = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (SpawnPlanEntry entry in plan.Entries)
            {
                if (!entry.HasValidSnapshot || !entry.HasVisualPrefab ||
                    !knownIds.Add(entry.Snapshot.MonsterId))
                {
                    error = "각 몬스터에는 고유 ID, 유효한 전투값과 외형 프리팹이 필요합니다.";
                    return false;
                }
            }
            HashSet<string> enabledIds = null;
            if (enabledMonsterIds != null)
            {
                enabledIds = new HashSet<string>(System.StringComparer.Ordinal);
                foreach (string id in enabledMonsterIds)
                {
                    if (string.IsNullOrWhiteSpace(id) || !knownIds.Contains(id) || !enabledIds.Add(id))
                    {
                        error = "생성 사용 목록에 알 수 없거나 중복된 몬스터 ID가 있습니다.";
                        return false;
                    }
                }
            }
            enabledSpecies = new bool[plan.Entries.Count];
            for (int index = 0; index < plan.Entries.Count; index++)
                enabledSpecies[index] = enabledIds == null ||
                    enabledIds.Contains(plan.Entries[index].Snapshot.MonsterId);
            return true;
        }

        private bool HasValidMonsterRootPrefab(out string error)
        {
            error = string.Empty;
            if ((_runRootPrefab != null ? _runRootPrefab : monsterRootPrefab) == null)
            {
                error = "MonsterSpawner requires the shared MonsterRoot prefab.";
                return false;
            }

            Transform root = (_runRootPrefab != null ? _runRootPrefab : monsterRootPrefab).transform;
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

        public void StopRun() => StopRun(false);

        public void StopRun(bool finishBossDeathAnimation)
        {
            _isSpawning = false;
            ClearBossDeathVisual();

            for (int index = _spawnedTargets.Count - 1; index >= 0; index--)
            {
                MonsterCombatTarget target = _spawnedTargets[index];
                if (target == null)
                {
                    continue;
                }

                if (finishBossDeathAnimation && _bossRun && target.TryContinueDeathAfterRun(_runId))
                {
                    _bossDeathVisual = target;
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
            _enabledSpecies = null;
            _currentSpawnPlan = null;
        }

        private void ClearBossDeathVisual()
        {
            if (_bossDeathVisual == null) return;
            _bossDeathVisual.Unregister();
            Destroy(_bossDeathVisual.gameObject);
            _bossDeathVisual = null;
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

                target.ConfigureVisualSorting(worldCamera);

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

            if (_runRootPrefab == null || _spawnVisualPrefab == null)
            {
                LogSpawnFailure(
                    "MonsterSpawner requires both the shared MonsterRoot and a species visual prefab.");
                return false;
            }

            try
            {
                Transform cloneTransform = UnityEngine.Object.Instantiate(
                    _runRootPrefab.transform,
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
            return TryGetMovementBounds(_spawnSnapshot, out movementBounds);
        }

        private bool TryGetMovementBounds(SpawnSnapshot snapshot, out Bounds movementBounds)
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
            worldMinX += snapshot.HitAreaWidth * 0.5f;
            worldMaxX -= snapshot.HitAreaWidth * 0.5f;
            worldMinY += snapshot.HitAreaHeight * 0.5f;
            worldMaxY -= snapshot.HitAreaHeight * 0.5f;
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
