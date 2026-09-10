using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Prototype normal-field spawner. It receives a run snapshot rather than
    /// reading Data assets, then creates the configured visual prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterSpawner : MonoBehaviour
    {
        [SerializeField] private CombatRunController combatRunController;
        [SerializeField] private GameObject walkerStumpPrefab;
        [SerializeField] private Transform spawnedEnemyRoot;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float spawnPlaneZ;
        [SerializeField, Min(0f)] private float horizontalPadding = 0.02f;
        [SerializeField, Min(0f)] private float bottomPadding = 0.09f;
        [SerializeField, Min(0f)] private float topPadding = 0.15f;

        private readonly List<WalkerStumpTarget> _spawnedTargets =
            new List<WalkerStumpTarget>();

        private SpawnSnapshot _spawnSnapshot;
        private float _nextSpawnAt;
        private bool _isSpawning;
        private bool _spawnFailureLogged;
        private GameObject _sceneSpawnTemplate;

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
                combatRunController = FindFirstObjectByType<CombatRunController>();
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

            TryResolveWalkerStumpPrefab();

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

            if (!combatRunController.IsRunning)
            {
                _isSpawning = false;
                return;
            }

            float elapsedSeconds = combatRunController.ElapsedSeconds;
            if (elapsedSeconds < _nextSpawnAt)
            {
                return;
            }

            if (ActiveSpawnedCount < _spawnSnapshot.AliveLimit)
            {
                if (!SpawnPack())
                {
                    _isSpawning = false;
                    return;
                }
            }

            // A missed spawn opportunity is intentionally not accumulated when
            // the alive limit is reached.
            _nextSpawnAt = elapsedSeconds + _spawnSnapshot.SpawnIntervalSeconds;
        }

        public void StartRun(SpawnSnapshot spawnSnapshot)
        {
            StopRun();

            if (combatRunController == null)
            {
                Debug.LogWarning("MonsterSpawner requires a CombatRunController.", this);
                return;
            }

            if (!TryResolveWalkerStumpPrefab())
            {
                Debug.LogWarning("MonsterSpawner requires a Walker_Stump prefab.", this);
                return;
            }

            _spawnSnapshot = spawnSnapshot;
            _nextSpawnAt = 0f;
            _spawnFailureLogged = false;
            _isSpawning = true;

            if (!SpawnPack())
            {
                _isSpawning = false;
                return;
            }

            _nextSpawnAt = spawnSnapshot.SpawnIntervalSeconds;
        }

        public void StopRun()
        {
            _isSpawning = false;

            for (int index = _spawnedTargets.Count - 1; index >= 0; index--)
            {
                WalkerStumpTarget target = _spawnedTargets[index];
                if (target != null && target.gameObject != _sceneSpawnTemplate)
                {
                    Destroy(target.gameObject);
                }
            }

            _spawnedTargets.Clear();
        }

        private bool SpawnPack()
        {
            int remainingCapacity =
                _spawnSnapshot.AliveLimit - ActiveSpawnedCount;
            int spawnCount = Mathf.Min(
                _spawnSnapshot.PackSize,
                Mathf.Max(0, remainingCapacity));

            for (int index = 0; index < spawnCount; index++)
            {
                if (!TryGetSpawnPosition(out Vector3 spawnPosition))
                {
                    continue;
                }

                if (!TryInstantiateWalkerStump(spawnPosition, out GameObject instance))
                {
                    return false;
                }

                WalkerStumpTarget target =
                    instance.GetComponent<WalkerStumpTarget>();

                if (target == null)
                {
                    target = instance.AddComponent<WalkerStumpTarget>();
                }

                target.Initialize(_spawnSnapshot);
                _spawnedTargets.Add(target);
            }

            return true;
        }

        private bool TryInstantiateWalkerStump(
            Vector3 spawnPosition,
            out GameObject instance)
        {
            instance = null;

            if (!TryResolveWalkerStumpPrefab())
            {
                LogSpawnFailure(
                    "MonsterSpawner could not create a Walker_Stump because the spawn source is missing.");
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
                    walkerStumpPrefab.transform,
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
                    $"MonsterSpawner failed to instantiate Walker_Stump from " +
                    $"'{walkerStumpPrefab.name}'. {exception.GetType().Name}: {exception.Message}");
                return false;
            }

            if (instance == null)
            {
                LogSpawnFailure(
                    "MonsterSpawner could not resolve the cloned Walker_Stump root GameObject. " +
                    $"Source type: {walkerStumpPrefab.GetType().Name}.");
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
                Random.Range(minX, maxX),
                Random.Range(minY, maxY),
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

        private bool TryResolveWalkerStumpPrefab()
        {
            if (IsUsableSpawnSource(walkerStumpPrefab))
            {
                if (walkerStumpPrefab.scene.IsValid())
                {
                    _sceneSpawnTemplate = walkerStumpPrefab;
                }

                return true;
            }

            // Prefer the imported prefab asset. This path repairs a stale
            // serialized reference without borrowing a scene object that may
            // later be removed by a reset or scene reload.
            GameObject loadedPrefab = FindLoadedWalkerStumpPrefab();
            if (IsUsableSpawnSource(loadedPrefab))
            {
                walkerStumpPrefab = loadedPrefab;
                _sceneSpawnTemplate = null;
                return true;
            }

            // Last-resort editor prototype fallback. The source is protected
            // from StopRun because it belongs to the scene rather than to the
            // current run's spawned-target list.
            GameObject sceneTemplate = FindSceneWalkerStumpTemplate();
            if (!IsUsableSpawnSource(sceneTemplate))
            {
                return false;
            }

            walkerStumpPrefab = sceneTemplate;
            _sceneSpawnTemplate = sceneTemplate;
            return true;
        }

        private bool IsUsableSpawnSource(GameObject candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            try
            {
                return candidate.transform != null;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        private GameObject FindLoadedWalkerStumpPrefab()
        {
            GameObject[] loadedObjects =
                Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject loadedObject in loadedObjects)
            {
                if (!IsUsableSpawnSource(loadedObject) ||
                    loadedObject.name != "Walker_Stump" ||
                    loadedObject.scene.IsValid() ||
                    loadedObject.transform.parent != null)
                {
                    continue;
                }

                return loadedObject;
            }

            return null;
        }

        private GameObject FindSceneWalkerStumpTemplate()
        {
            GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject sceneObject in sceneObjects)
            {
                if (sceneObject == null ||
                    sceneObject.name != "Walker_Stump" ||
                    !sceneObject.scene.IsValid())
                {
                    continue;
                }

                return sceneObject;
            }

            return null;
        }
    }
}
