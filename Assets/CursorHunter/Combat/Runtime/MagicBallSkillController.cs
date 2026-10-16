using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>Owns one magic ball for the whole run.</summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class MagicBallSkillController : MonoBehaviour
    {
        public const string SkillId = "skill.magic_ball";

        [Header("Editor Play Mode test")]
        [SerializeField, InspectorName("Skill Acquired"),
         Tooltip("Treat Magic Ball as acquired in Editor Play Mode without changing saved progress.")]
        private bool editorSkillAcquired;

        [Header("Prefab and field")]
        [SerializeField] private MagicBallObject ballPrefab;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float spawnPlaneZ;
        [SerializeField, Range(0f, 0.4f)] private float horizontalViewportPadding = 0.02f;
        [SerializeField, Range(0f, 0.4f)] private float bottomViewportPadding = 0.09f;
        [SerializeField, Range(0f, 0.4f)] private float topViewportPadding = 0.15f;
        [SerializeField, Min(0f)] private float fieldEdgePaddingWorldUnits;

        [Header("Base balance")]
        [SerializeField] private MagicBallBaseStat baseStat = new MagicBallBaseStat
        {
            FirstCastDelaySeconds = 0f,
            MoveSpeedWorldUnitsPerSecond = 7f,
            SizeMultiplier = 1f,
            DamageMultiplier = 1f,
            SameTargetHitCooldownSeconds = 0.5f
        };

        [Header("Eight starting directions")]
        [SerializeField] private Vector2[] startingDirections =
        {
            new Vector2(0.92388f, 0.38268f),
            new Vector2(0.38268f, 0.92388f),
            new Vector2(-0.38268f, 0.92388f),
            new Vector2(-0.92388f, 0.38268f),
            new Vector2(-0.92388f, -0.38268f),
            new Vector2(-0.38268f, -0.92388f),
            new Vector2(0.38268f, -0.92388f),
            new Vector2(0.92388f, -0.38268f)
        };

        private CombatRunController _combat;
        private MagicBallObject _ball;
        private SeededRandom _random;
        private RunId _runId;
        private MagicBallRuntimeStat _runtime;
        private ContactFilter2D _enemyFilter;
        private float _castAt;
        private bool _configured;
        private bool _visualsPaused;

        public bool BallIsActive => _ball != null && _ball.IsActive;
        internal bool IsAcquiredForRun(bool savedAcquired) =>
            savedAcquired || Application.isEditor && editorSkillAcquired;

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
        }

        internal bool ConfigureForRun(CombatRunController combat, RunId runId,
            ulong seed, SkillCombatSnapshot skill = default)
        {
            StopSkill();
            if (combat == null || !runId.IsValid || ballPrefab == null ||
                !ballPrefab.HasHitbox || startingDirections == null ||
                startingDirections.Length != 8) return false;
            if (!string.IsNullOrEmpty(skill.SkillId) &&
                (skill.SkillId != SkillId || !skill.IsValid)) return false;
            for (int i = 0; i < startingDirections.Length; i++)
            {
                Vector2 direction = startingDirections[i];
                if (direction.sqrMagnitude < 0.0001f ||
                    float.IsNaN(direction.x) || float.IsInfinity(direction.x) ||
                    float.IsNaN(direction.y) || float.IsInfinity(direction.y))
                {
                    Debug.LogError("Magic Ball needs eight nonzero finite starting directions.", this);
                    return false;
                }
            }
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null)
            {
                Debug.LogError("Magic Ball needs a world camera.", this);
                return false;
            }

            _combat = combat;
            _runId = runId;
            _random = new SeededRandom(seed ^ 0x4D4147494342414CUL);
            _runtime = new MagicBallRuntimeStat(baseStat, skill,
                combat.CombatSnapshot.AttackPower);
            _castAt = combat.ElapsedSeconds + _runtime.FirstCastDelaySeconds;
            _enemyFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true
            };
            _enemyFilter.SetLayerMask(combat.EnemyLayers);
            if (_ball == null)
            {
                _ball = Instantiate(ballPrefab, transform);
                _ball.ReturnToPool();
            }
            _configured = true;
            _visualsPaused = false;
            return true;
        }

        private void LateUpdate()
        {
            if (!_configured) return;
            if (_combat == null || !_combat.IsRunActive ||
                _combat.CurrentRunId != _runId)
            {
                StopSkill();
                return;
            }
            if (_combat.IsPaused)
            {
                if (!_visualsPaused && BallIsActive)
                {
                    _visualsPaused = true;
                    _ball.SetVisualPaused(true);
                }
                return;
            }
            if (!_combat.IsRunning) return;
            if (_visualsPaused)
            {
                _visualsPaused = false;
                _ball.SetVisualPaused(false);
            }

            float now = _combat.ElapsedSeconds;
            if (!TryGetFieldBounds(out Rect fieldBounds)) return;
            if (!BallIsActive)
            {
                if (now < _castAt) return;
                int directionIndex = (int)(_random.NextUInt64() % 8UL);
                Vector2 spawnFraction = new Vector2(
                    _random.NextFloat(0f, 1f), _random.NextFloat(0f, 1f));
                if (!_ball.Activate(this, _runId, fieldBounds, spawnFraction,
                    spawnPlaneZ, startingDirections[directionIndex].normalized,
                    now, _runtime.SizeMultiplier)) return;
            }
            Physics2D.SyncTransforms();
            _ball.Tick(now, fieldBounds, _enemyFilter,
                _runtime.MoveSpeedWorldUnitsPerSecond,
                _runtime.SameTargetHitCooldownSeconds);
        }

        private bool TryGetFieldBounds(out Rect bounds)
        {
            bounds = default;
            if (worldCamera == null) return false;
            float distance = Mathf.Abs(spawnPlaneZ - worldCamera.transform.position.z);
            Vector3 bottomLeft = worldCamera.ViewportToWorldPoint(
                new Vector3(horizontalViewportPadding, bottomViewportPadding, distance));
            Vector3 topRight = worldCamera.ViewportToWorldPoint(
                new Vector3(1f - horizontalViewportPadding,
                    1f - topViewportPadding, distance));
            float minX = Mathf.Min(bottomLeft.x, topRight.x) + fieldEdgePaddingWorldUnits;
            float maxX = Mathf.Max(bottomLeft.x, topRight.x) - fieldEdgePaddingWorldUnits;
            float minY = Mathf.Min(bottomLeft.y, topRight.y) + fieldEdgePaddingWorldUnits;
            float maxY = Mathf.Max(bottomLeft.y, topRight.y) - fieldEdgePaddingWorldUnits;
            if (maxX <= minX || maxY <= minY) return false;
            bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        internal bool IsCurrentRun(RunId runId) =>
            _configured && _combat != null && _combat.IsRunning &&
            _combat.CurrentRunId == runId && _runId == runId;

        internal bool Hit(RunId runId, MonsterCombatTarget target)
        {
            return IsCurrentRun(runId) && target != null &&
                _combat.TryApplySkillDamage(runId, target, _runtime.Damage);
        }

        internal void StopSkill()
        {
            if (_ball != null) _ball.ReturnToPool();
            _combat = null;
            _random = null;
            _runId = default;
            _runtime = default;
            _configured = false;
            _visualsPaused = false;
        }

        private void OnDisable() => StopSkill();
    }
}
