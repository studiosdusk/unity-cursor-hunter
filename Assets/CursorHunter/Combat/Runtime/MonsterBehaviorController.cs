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
        private const float ActionDurationSeconds = 2f;

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
        private MonsterSpecialAbility _specialAbility;
        private float _specialAbilityIntervalSeconds;
        private float _specialAbilityDurationSeconds;
        private float _specialAbilityRadius;
        private float _specialAbilityStrength;
        private float _nextSpecialAbilityAt;
        private float _specialEffectEndsAt;
        private float _lastElapsedSeconds;
        private float _nextActionAt;
        private float _hitStopUntil;
        private SeededRandom _actionRandom;
        private bool _isMoveAction;
        private bool _isConfigured;
        private bool _isVanished;
        private bool _isShielded;

        internal bool IsHitStopped =>
            _isConfigured && _runController != null &&
            _runController.ElapsedSeconds < _hitStopUntil;

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
                if (_target != null)
                {
                    _target.SetMoving(false);
                }
                return;
            }

            float elapsedSeconds = _runController.ElapsedSeconds;
            float deltaSeconds = elapsedSeconds - _lastElapsedSeconds;
            if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds) ||
                float.IsInfinity(deltaSeconds))
            {
                _target.SetMoving(false);
                return;
            }

            float segmentStart = _lastElapsedSeconds;
            bool movedInCurrentAction = false;
            while (segmentStart < elapsedSeconds)
            {
                float segmentEnd = Mathf.Min(elapsedSeconds, _nextActionAt);
                float movementStart = Mathf.Max(segmentStart, _hitStopUntil);
                if (_isMoveAction && movementStart < segmentEnd)
                {
                    movedInCurrentAction |= AdvanceMovement(segmentEnd - movementStart);
                }

                segmentStart = segmentEnd;
                if (segmentStart >= _nextActionAt)
                {
                    bool continuedMoving = _isMoveAction && movedInCurrentAction;
                    ChooseNextAction();
                    _nextActionAt += ActionDurationSeconds;
                    movedInCurrentAction = continuedMoving && _isMoveAction;
                }
            }

            _lastElapsedSeconds = elapsedSeconds;
            AdvanceSpecialAbility(elapsedSeconds);
            _target.SetMoving(_isMoveAction && movedInCurrentAction);
        }

        internal void StopForHit(float durationSeconds)
        {
            if (!_isConfigured || _target == null || !_target.IsActive ||
                _runController == null)
            {
                return;
            }

            _hitStopUntil = Mathf.Max(
                _hitStopUntil,
                _runController.ElapsedSeconds + Mathf.Max(0f, durationSeconds));
        }

        private bool AdvanceMovement(float deltaSeconds)
        {
            Vector3 previousPosition = transform.position;
            switch (_movementMode)
            {
                case MonsterMovementMode.BoundedWander:
                    AdvanceBoundedWander(deltaSeconds);
                    break;
                case MonsterMovementMode.Circular:
                    AdvanceCircular(deltaSeconds);
                    break;
            }

            Vector3 movement = transform.position - previousPosition;
            bool isMoving = movement.sqrMagnitude > 0f;
            if (isMoving)
            {
                FaceHorizontalDirection(movement.x);
            }

            return isMoving;
        }

        /// <summary>
        /// Applies scale and hitbox size before the target optionally lays out its health bar.
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
            ulong behaviorSeed)
        {
            _isConfigured = false;
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
            _nextActionAt = _lastElapsedSeconds + ActionDurationSeconds;
            _specialAbility = snapshot.SpecialAbility;
            _specialAbilityIntervalSeconds = snapshot.SpecialAbilityIntervalSeconds;
            _specialAbilityDurationSeconds = snapshot.SpecialAbilityDurationSeconds;
            _specialAbilityRadius = snapshot.SpecialAbilityRadius;
            _specialAbilityStrength = snapshot.SpecialAbilityStrength;
            _nextSpecialAbilityAt = _lastElapsedSeconds +
                _specialAbilityIntervalSeconds;
            _specialEffectEndsAt = 0f;
            _isVanished = false;
            _isShielded = false;
            target.SetVulnerable(true);
            target.SetShielded(false, 0f);
            _hitStopUntil = 0f;
            _actionRandom = new SeededRandom(behaviorSeed);
            _isConfigured = true;

            Vector3 initialPosition = transform.position;
            initialPosition.x = Mathf.Clamp(initialPosition.x, _boundsMin.x, _boundsMax.x);
            initialPosition.y = Mathf.Clamp(initialPosition.y, _boundsMin.y, _boundsMax.y);
            transform.position = initialPosition;
            _target.RefreshVisualSorting();

            _velocity = Vector2.zero;
            if (_movementMode == MonsterMovementMode.Circular)
            {
                _orbitCenter = transform.position;
                _orbitAngleDegrees = _actionRandom.NextFloat(0f, 360f);
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

            ChooseNextAction();
            _target.SetMoving(false);

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
            _target.RefreshVisualSorting();
        }

        private void AdvanceCircular(float deltaSeconds)
        {
            _orbitAngleDegrees = Mathf.Repeat(
                _orbitAngleDegrees + _orbitAngularSpeedDegrees * deltaSeconds,
                360f);
            SetRootPosition(
                _orbitCenter + DirectionFromDegrees(_orbitAngleDegrees) * _orbitRadius);
        }

        internal void ResetSpecialPresentation()
        {
            SetVanished(false);
            SetShielded(false);
            _specialEffectEndsAt = 0f;
        }

        internal void ResumeAfterRevive()
        {
            ResetSpecialPresentation();
            if (!_isConfigured || _runController == null)
            {
                return;
            }

            _lastElapsedSeconds = _runController.ElapsedSeconds;
            _nextActionAt = _lastElapsedSeconds + ActionDurationSeconds;
            _nextSpecialAbilityAt = _lastElapsedSeconds +
                _specialAbilityIntervalSeconds;
            _hitStopUntil = 0f;
            ChooseNextAction();
        }

        private void AdvanceSpecialAbility(float elapsedSeconds)
        {
            if (_specialEffectEndsAt > 0f && elapsedSeconds >= _specialEffectEndsAt)
            {
                SetVanished(false);
                SetShielded(false);
                _specialEffectEndsAt = 0f;
            }

            if (_specialAbility == MonsterSpecialAbility.None ||
                elapsedSeconds < _nextSpecialAbilityAt ||
                _target == null || !_target.IsActive)
            {
                return;
            }

            _nextSpecialAbilityAt = elapsedSeconds +
                _specialAbilityIntervalSeconds;
            switch (_specialAbility)
            {
                case MonsterSpecialAbility.Vanish:
                    SetVanished(true);
                    _specialEffectEndsAt = elapsedSeconds +
                        Mathf.Max(0.2f, _specialAbilityDurationSeconds);
                    break;
                case MonsterSpecialAbility.Shield:
                    SetShielded(true);
                    _specialEffectEndsAt = elapsedSeconds +
                        Mathf.Max(0.2f, _specialAbilityDurationSeconds);
                    break;
                case MonsterSpecialAbility.Teleport:
                    TeleportToNewPosition();
                    break;
                case MonsterSpecialAbility.HealNearby:
                    MonsterCombatTarget.HealNearbyAllies(
                        _target,
                        _specialAbilityRadius,
                        _specialAbilityStrength);
                    break;
                case MonsterSpecialAbility.ReviveNearby:
                    MonsterCombatTarget.ReviveNearbyAllies(
                        _target,
                        _specialAbilityRadius,
                        _specialAbilityStrength);
                    break;
            }
        }

        private void SetVanished(bool vanished)
        {
            if (_isVanished == vanished)
            {
                return;
            }

            _isVanished = vanished;
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(!vanished);
            }
            if (_target != null)
            {
                _target.SetVulnerable(!vanished);
            }
        }

        private void SetShielded(bool shielded)
        {
            if (_isShielded == shielded)
            {
                return;
            }

            _isShielded = shielded;
            if (_target != null)
            {
                _target.SetShielded(shielded, _specialAbilityStrength);
            }
        }

        private void TeleportToNewPosition()
        {
            if (_actionRandom == null)
            {
                return;
            }

            Vector2 previous = transform.position;
            Vector2 destination = previous;
            float minimumDistanceSquared = 1f;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                destination = new Vector2(
                    _actionRandom.NextFloat(_boundsMin.x, _boundsMax.x),
                    _actionRandom.NextFloat(_boundsMin.y, _boundsMax.y));
                if ((destination - previous).sqrMagnitude >= minimumDistanceSquared)
                {
                    break;
                }
            }

            SetRootPosition(destination);
        }

        private void ChooseNextAction()
        {
            _isMoveAction = _movementMode != MonsterMovementMode.Stationary &&
                _actionRandom.NextFloat(0f, 1f) < 0.5f;
            if (!_isMoveAction)
            {
                return;
            }

            if (_movementMode == MonsterMovementMode.BoundedWander)
            {
                float headingDegrees = _actionRandom.NextFloat(0f, 360f);
                _velocity = DirectionFromDegrees(headingDegrees) * _moveSpeed;
            }
            else if (_movementMode == MonsterMovementMode.Circular)
            {
                _orbitAngularSpeedDegrees = Mathf.Abs(_orbitAngularSpeedDegrees) *
                    (_actionRandom.NextFloat(0f, 1f) < 0.5f ? -1f : 1f);
            }
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
            _target?.RefreshVisualSorting();
        }

        private void FaceHorizontalDirection(float direction)
        {
            if (visualRoot == null || Mathf.Abs(direction) <= 0.000001f)
            {
                return;
            }

            Vector3 scale = visualRoot.localScale;
            // The Walker visuals face left at their authored positive X scale.
            float facingScale = Mathf.Abs(scale.x) * (direction > 0f ? -1f : 1f);
            if (scale.x != facingScale)
            {
                scale.x = facingScale;
                visualRoot.localScale = scale;
            }
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
