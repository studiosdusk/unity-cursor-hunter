using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Applies the resolved species presentation and movement snapshot to the
    /// shared MonsterRoot. It owns no species data or progression state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterBehaviorController : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform hitArea;
        [SerializeField] private BoxCollider2D hitAreaCollider;

        private MonsterCombatTarget _target;
        private CombatRunController _runController;
        private RunId _runId;
        private MonsterMovementMode _movementMode;
        private Vector2 _boundsMin;
        private Vector2 _boundsMax;
        private Vector2 _velocity;
        private Vector2 _orbitCenter;
        private float _moveSpeed;
        private float _orbitRadius;
        private float _orbitAngleDegrees;
        private float _orbitAngularSpeedDegrees;
        private float _lastElapsedSeconds;
        private bool _isConfigured;

        private void Awake()
        {
            ResolveHierarchy();
        }

        private void Update()
        {
            if (!_isConfigured || _target == null || !_target.BelongsTo(_runId) ||
                _runController == null || !_runController.IsRunActive ||
                _runController.IsPaused)
            {
                return;
            }

            float elapsedSeconds = _runController.ElapsedSeconds;
            float deltaSeconds = elapsedSeconds - _lastElapsedSeconds;
            if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds) ||
                float.IsInfinity(deltaSeconds))
            {
                return;
            }

            _lastElapsedSeconds = elapsedSeconds;
            switch (_movementMode)
            {
                case MonsterMovementMode.BoundedWander:
                    AdvanceBoundedWander(deltaSeconds);
                    break;
                case MonsterMovementMode.Circular:
                    AdvanceCircular(deltaSeconds);
                    break;
            }
        }

        /// <summary>
        /// Applies scale and hitbox size before the target lays out its health bar.
        /// </summary>
        public bool ApplyPresentation(SpawnSnapshot snapshot)
        {
            ResolveHierarchy();
            if (visualRoot == null || hitArea == null || hitAreaCollider == null)
            {
                return false;
            }

            visualRoot.localScale = Vector3.one * snapshot.VisualScale;
            hitArea.localScale = Vector3.one;
            hitAreaCollider.size = new Vector2(
                snapshot.HitAreaWidth,
                snapshot.HitAreaHeight);
            return true;
        }

        /// <summary>
        /// Binds one movement instance to a run and its screen-space world bounds.
        /// </summary>
        public bool ConfigureMovement(
            SpawnSnapshot snapshot,
            MonsterCombatTarget target,
            CombatRunController runController,
            Bounds movementBounds,
            Vector2 initialHeading)
        {
            if (target == null || runController == null || !target.IsActive)
            {
                return false;
            }

            ResolveHierarchy();
            if (visualRoot == null || hitArea == null || hitAreaCollider == null ||
                !ApplyPresentation(snapshot))
            {
                return false;
            }

            _target = target;
            _runController = runController;
            _runId = target.RunId;
            _movementMode = snapshot.MovementMode;
            _moveSpeed = snapshot.MoveSpeed;
            _boundsMin = movementBounds.min;
            _boundsMax = movementBounds.max;
            _lastElapsedSeconds = runController.ElapsedSeconds;
            _isConfigured = true;

            Vector3 initialPosition = transform.position;
            initialPosition.x = Mathf.Clamp(initialPosition.x, _boundsMin.x, _boundsMax.x);
            initialPosition.y = Mathf.Clamp(initialPosition.y, _boundsMin.y, _boundsMax.y);
            transform.position = initialPosition;

            Vector2 direction = initialHeading.sqrMagnitude > 0.0001f
                ? initialHeading.normalized
                : Vector2.right;
            _velocity = direction * _moveSpeed;
            if (_movementMode == MonsterMovementMode.Circular)
            {
                _orbitCenter = transform.position;
                _orbitAngleDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                _orbitAngularSpeedDegrees = snapshot.OrbitAngularSpeedDegrees;
                float availableRadius = Mathf.Min(
                    Mathf.Min(_orbitCenter.x - _boundsMin.x, _boundsMax.x - _orbitCenter.x),
                    Mathf.Min(_orbitCenter.y - _boundsMin.y, _boundsMax.y - _orbitCenter.y));
                _orbitRadius = Mathf.Clamp(
                    snapshot.OrbitRadius,
                    0f,
                    Mathf.Max(0f, availableRadius));
                SetRootPosition(_orbitCenter + DirectionFromDegrees(_orbitAngleDegrees) * _orbitRadius);
            }

            return true;
        }

        private void AdvanceBoundedWander(float deltaSeconds)
        {
            Vector3 position = transform.position;
            float velocityX = _velocity.x;
            float velocityY = _velocity.y;
            position.x = AdvanceReflectedAxis(
                position.x,
                ref velocityX,
                _boundsMin.x,
                _boundsMax.x,
                deltaSeconds);
            position.y = AdvanceReflectedAxis(
                position.y,
                ref velocityY,
                _boundsMin.y,
                _boundsMax.y,
                deltaSeconds);
            _velocity = new Vector2(velocityX, velocityY);
            transform.position = position;
        }

        private void AdvanceCircular(float deltaSeconds)
        {
            _orbitAngleDegrees = Mathf.Repeat(
                _orbitAngleDegrees + _orbitAngularSpeedDegrees * deltaSeconds,
                360f);
            SetRootPosition(
                _orbitCenter + DirectionFromDegrees(_orbitAngleDegrees) * _orbitRadius);
        }

        private static float AdvanceReflectedAxis(
            float position,
            ref float velocity,
            float minimum,
            float maximum,
            float deltaSeconds)
        {
            float length = maximum - minimum;
            if (length <= 0.0001f)
            {
                velocity = 0f;
                return minimum;
            }

            float speed = Mathf.Abs(velocity);
            float offset = Mathf.Clamp(position - minimum, 0f, length);
            float phase = velocity >= 0f ? offset : (2f * length) - offset;
            phase = Mathf.Repeat(phase + speed * deltaSeconds, 2f * length);
            if (phase <= length)
            {
                velocity = speed;
                return minimum + phase;
            }

            velocity = -speed;
            return minimum + (2f * length) - phase;
        }

        private static Vector2 DirectionFromDegrees(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private void SetRootPosition(Vector2 position)
        {
            Vector3 rootPosition = transform.position;
            rootPosition.x = position.x;
            rootPosition.y = position.y;
            transform.position = rootPosition;
        }

        private void ResolveHierarchy()
        {
            if (visualRoot == null)
            {
                visualRoot = transform.Find("VisualRoot");
            }

            if (hitArea == null)
            {
                hitArea = transform.Find("HitArea");
            }

            if (hitAreaCollider == null && hitArea != null)
            {
                hitAreaCollider = hitArea.GetComponent<BoxCollider2D>();
            }
        }
    }
}
