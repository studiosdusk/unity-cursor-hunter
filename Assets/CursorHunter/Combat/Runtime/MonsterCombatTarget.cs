using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Generic run-bound combat target. The visual prefab may use any hierarchy,
    /// Animator location, and supported animation state set.
    /// </summary>
    [DisallowMultipleComponent]
    public class MonsterCombatTarget : MonoBehaviour, ICombatTarget, IDamageTextAnchor
    {
        private static readonly string[] HitStateCandidates =
        {
            "Hit",
            "Hurt",
            "Damage",
            "hit"
        };

        private static readonly string[] DeadStateCandidates =
        {
            "Dead",
            "Death",
            "dead",
            "death"
        };

        private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
        private static readonly int IdleHash = Animator.StringToHash("Idle");
        private static readonly int WalkHash = Animator.StringToHash("Walk");

        [SerializeField] private Animator animator;
        [SerializeField] private Transform healthBarAnchor;
        [SerializeField] private bool showHealthBar;
        [SerializeField, Range(0.01f, 1f)]
        private float deathDespawnNormalizedTime = 1f;

        private bool _isInitialized;
        private bool _isRegistered;
        private bool _isDead;
        private RunId _runId;
        private RunId _deathRunId;
        private float _destroyAt;
        private bool _destroyScheduled;
        private int _deathStateLayer = -1;
        private int _deathStateHash;
        private string _monsterId;
        private long _maxHealth;
        private long _currentHealth;
        private long _garnetReward;
        private string _bonusDropCurrencyId;
        private long _bonusDropAmount;
        private float _bonusDropChancePercent;
        private MonsterBehaviorType _behaviorType;
        private MonsterHealthBarView _healthBar;
        private MonsterBehaviorController _behaviorController;
        private bool _hasIsMovingParameter;
        private bool _hasLocomotionStates;
        private bool _isMoving;
        private int _idleStateHash;
        private int _walkStateHash;

        public bool IsInitialized => _isInitialized;
        public bool IsRegistered => _isRegistered;
        public bool IsActive => _isInitialized && _isRegistered && !_isDead;
        public bool IsDead => _isDead;
        public RunId RunId => _runId;
        public Vector3 DamageTextPosition => healthBarAnchor != null
            ? healthBarAnchor.position
            : transform.position + Vector3.up;
        public string MonsterId => _monsterId;
        public long MaxHealth => _maxHealth;
        public long CurrentHealth => _currentHealth;
        public long GarnetReward => _garnetReward;
        public string BonusDropCurrencyId => _bonusDropCurrencyId ?? string.Empty;
        public long BonusDropAmount => _bonusDropAmount;
        public float BonusDropChancePercent => _bonusDropChancePercent;
        public MonsterBehaviorType BehaviorType => _behaviorType;

        protected virtual void Awake()
        {
            ResolveAnimator();
        }

        protected virtual void LateUpdate()
        {
            if (!_destroyScheduled || !_isDead)
            {
                return;
            }

            if (_runId != _deathRunId)
            {
                _destroyScheduled = false;
                return;
            }

            if (TryGetDeathState(out AnimatorStateInfo state))
            {
                if (state.normalizedTime < deathDespawnNormalizedTime)
                {
                    return;
                }
            }
            else if (Time.time < _destroyAt)
            {
                return;
            }

            _destroyScheduled = false;
            DestroySelf();
        }

        /// <summary>
        /// Initializes one spawned instance from the immutable run snapshot.
        /// </summary>
        public void Initialize(
            SpawnSnapshot snapshot,
            RunId runId)
        {
            if (!runId.IsValid || !snapshot.IsValid)
            {
                Unregister();
                Debug.LogError(
                    "MonsterCombatTarget requires a valid RunId and SpawnSnapshot.",
                    this);
                return;
            }

            _destroyScheduled = false;
            _deathRunId = default;
            _deathStateLayer = -1;
            _deathStateHash = 0;
            _runId = runId;
            _monsterId = snapshot.MonsterId;
            _maxHealth = snapshot.MaxHealth;
            _currentHealth = snapshot.MaxHealth;
            _garnetReward = snapshot.GarnetReward;
            _bonusDropCurrencyId = snapshot.BonusDropCurrencyId;
            _bonusDropAmount = snapshot.BonusDropAmount;
            _bonusDropChancePercent = snapshot.BonusDropChancePercent;
            _behaviorType = snapshot.BehaviorType;
            _isDead = false;
            _isInitialized = true;
            _isRegistered = true;

            ResolveBehaviorController();
            if (_behaviorController != null &&
                !_behaviorController.ApplyPresentation(snapshot))
            {
                Unregister();
                Debug.LogError(
                    "MonsterRoot behavior controller could not apply its VisualRoot/HitArea presentation.",
                    this);
                return;
            }

            // MonsterRoot is instantiated before its species visual is attached,
            // so resolve the Animator again after visual composition is complete.
            ResolveAnimator();
            AttachAnimationEventRelays();
            if (showHealthBar)
            {
                EnsureHealthBar();
                _healthBar.Initialize(_maxHealth);
                Bounds visualBounds = CalculateVisualBounds();
                if (healthBarAnchor != null)
                {
                    healthBarAnchor.position = new Vector3(
                        visualBounds.center.x,
                        visualBounds.max.y + 0.16f,
                        visualBounds.center.z);
                    _healthBar.Configure(visualBounds, true);
                }
                else
                {
                    _healthBar.Configure(visualBounds);
                }
            }
            else if (_healthBar != null)
            {
                _healthBar.Hide();
            }

            if (animator == null)
            {
                return;
            }

            animator.Rebind();
            animator.Update(0f);
            InitializeMovementAnimation();
        }

        public bool ConfigureMovement(
            SpawnSnapshot snapshot,
            CombatRunController runController,
            Bounds movementBounds,
            ulong behaviorSeed)
        {
            if (!IsActive)
            {
                return false;
            }

            ResolveBehaviorController();
            return _behaviorController != null &&
                   _behaviorController.ConfigureMovement(
                       snapshot,
                       this,
                       runController,
                       movementBounds,
                       behaviorSeed);
        }

        /// <summary>
        /// Removes this instance from the current run immediately. Unity's
        /// Destroy is deferred, so this guard is required before pooled or
        /// stale animation callbacks can run.
        /// </summary>
        public void Unregister()
        {
            _isRegistered = false;
            _destroyScheduled = false;
            SetMoving(false);
            if (_healthBar != null)
            {
                _healthBar.Hide();
            }
        }

        internal void SetMoving(bool moving)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            bool shouldMove = moving && IsActive;
            if (_isMoving != shouldMove)
            {
                _isMoving = shouldMove;
                if (_hasIsMovingParameter)
                {
                    animator.SetBool(IsMovingHash, shouldMove);
                }
            }

            // Death can be requested in LateUpdate, after the Animator has
            // evaluated for this frame. Until its next evaluation the reported
            // state may still be Walk; never replace the pending Dead playback.
            if (_isDead)
            {
                return;
            }

            // Some visual controllers have Idle/Walk clips but no transition.
            // Only switch between locomotion states so Hit and Dead can finish.
            if (!_hasLocomotionStates ||
                (_behaviorController != null && _behaviorController.IsHitStopped))
            {
                return;
            }

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
                if (!shouldMove && nextState.shortNameHash == WalkHash &&
                    (state.shortNameHash == IdleHash ||
                     state.shortNameHash == WalkHash))
                {
                    animator.Play(_idleStateHash, 0, 0f);
                }
                return;
            }

            if (shouldMove && state.shortNameHash == IdleHash)
            {
                animator.CrossFade(_walkStateHash, 0.1f, 0);
            }
            else if (!shouldMove && state.shortNameHash == WalkHash)
            {
                animator.Play(_idleStateHash, 0, 0f);
            }
        }

        public bool BelongsTo(RunId runId)
        {
            return _isRegistered && _runId == runId;
        }

        /// <summary>
        /// Creates a fallback collider only when the authored prefab has no
        /// Collider2D. Existing child colliders are preserved.
        /// </summary>
        public Collider2D EnsureCombatCollider()
        {
            Collider2D existingCollider =
                GetComponentInChildren<Collider2D>(true);
            if (existingCollider != null)
            {
                return existingCollider;
            }

            BoxCollider2D generatedCollider = gameObject.AddComponent<BoxCollider2D>();
            SpriteRenderer[] allRenderers =
                GetComponentsInChildren<SpriteRenderer>(true);
            List<SpriteRenderer> visualRenderers =
                new List<SpriteRenderer>(allRenderers.Length);
            foreach (SpriteRenderer renderer in allRenderers)
            {
                if (renderer == null ||
                    renderer.GetComponentInParent<MonsterHealthBarView>() != null)
                {
                    continue;
                }

                visualRenderers.Add(renderer);
            }

            SpriteRenderer[] renderers = visualRenderers.ToArray();

            if (renderers.Length == 0)
            {
                generatedCollider.size = Vector2.one;
                return generatedCollider;
            }

            Bounds worldBounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                worldBounds.Encapsulate(renderers[index].bounds);
            }

            Vector3 localMin = transform.InverseTransformPoint(worldBounds.min);
            Vector3 localMax = transform.InverseTransformPoint(worldBounds.max);
            Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
            generatedCollider.offset = new Vector2(localCenter.x, localCenter.y);
            generatedCollider.size = new Vector2(
                Mathf.Max(0.1f, Mathf.Abs(localMax.x - localMin.x)),
                Mathf.Max(0.1f, Mathf.Abs(localMax.y - localMin.y)));
            return generatedCollider;
        }

        /// <summary>
        /// Applies one logical hit. Damage is accepted while the hit animation
        /// is playing; animation timing never gates health changes.
        /// </summary>
        public bool ApplyDamage(
            RunId runId,
            long damage,
            out long effectiveDamage,
            out bool killed)
        {
            effectiveDamage = 0;
            killed = false;

            if (!IsActive || !BelongsTo(runId) ||
                damage <= 0 || _currentHealth <= 0)
            {
                return false;
            }

            effectiveDamage = damage < _currentHealth ? damage : _currentHealth;
            _currentHealth -= effectiveDamage;
            if (showHealthBar && _healthBar != null)
            {
                _healthBar.SetHealth(_currentHealth);
            }

            if (_currentHealth <= 0)
            {
                _currentHealth = 0;
                if (_healthBar != null)
                {
                    _healthBar.Hide();
                }
                _isDead = true;
                _isRegistered = false;
                SetMoving(false);
                _deathRunId = _runId;
                killed = true;
                StartDeathAnimation();
                return true;
            }

            PlayHitAnimation();
            return true;
        }

        /// <summary>
        /// Kept for existing animation events. An event cannot remove a monster
        /// before its configured death visual has finished.
        /// </summary>
        public void DestroySelf()
        {
            if (!_isDead || !_deathRunId.IsValid || _runId != _deathRunId)
            {
                return;
            }

            if (_destroyScheduled &&
                (!TryGetDeathState(out AnimatorStateInfo state) ||
                 state.normalizedTime < deathDespawnNormalizedTime))
            {
                return;
            }

            _destroyScheduled = false;
            Unregister();
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        private void ResolveAnimator()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
        }

        private void ResolveBehaviorController()
        {
            if (_behaviorController == null)
            {
                _behaviorController = GetComponent<MonsterBehaviorController>();
            }
        }

        private void InitializeMovementAnimation()
        {
            _isMoving = false;
            _hasIsMovingParameter = false;
            _hasLocomotionStates = false;
            if (animator == null || animator.runtimeAnimatorController == null ||
                animator.layerCount == 0)
            {
                return;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == IsMovingHash &&
                    parameter.type == AnimatorControllerParameterType.Bool)
                {
                    _hasIsMovingParameter = true;
                    animator.SetBool(IsMovingHash, false);
                    break;
                }
            }

            string layerName = animator.GetLayerName(0);
            _idleStateHash = Animator.StringToHash(layerName + ".Idle");
            _walkStateHash = Animator.StringToHash(layerName + ".Walk");
            if (!animator.HasState(0, _idleStateHash))
            {
                _idleStateHash = IdleHash;
            }
            if (!animator.HasState(0, _walkStateHash))
            {
                _walkStateHash = WalkHash;
            }
            _hasLocomotionStates =
                animator.HasState(0, _idleStateHash) &&
                animator.HasState(0, _walkStateHash);
        }

        private void AttachAnimationEventRelays()
        {
            Animator[] animators = GetComponentsInChildren<Animator>(true);
            foreach (Animator childAnimator in animators)
            {
                if (childAnimator == null || childAnimator.gameObject == gameObject)
                {
                    continue;
                }

                MonsterAnimationEventRelay relay =
                    childAnimator.GetComponent<MonsterAnimationEventRelay>();
                if (relay == null)
                {
                    relay = childAnimator.gameObject
                        .AddComponent<MonsterAnimationEventRelay>();
                }

                relay.Initialize(this);
            }
        }

        private void EnsureHealthBar()
        {
            if (healthBarAnchor == null)
            {
                Transform configuredAnchor = transform.Find("HealthBarAnchor");
                if (configuredAnchor != null)
                {
                    healthBarAnchor = configuredAnchor;
                }
            }

            if (_healthBar == null)
            {
                if (healthBarAnchor != null)
                {
                    _healthBar = healthBarAnchor.GetComponent<MonsterHealthBarView>();
                }

                if (_healthBar == null)
                {
                    _healthBar = GetComponentInChildren<MonsterHealthBarView>(true);
                }
            }

            if (_healthBar == null)
            {
                GameObject healthBarObject = new GameObject("MonsterHealthBar");
                healthBarObject.transform.SetParent(
                    healthBarAnchor != null ? healthBarAnchor : transform,
                    false);
                _healthBar = healthBarObject.AddComponent<MonsterHealthBarView>();
            }
        }

        private Bounds CalculateVisualBounds()
        {
            SpriteRenderer[] renderers =
                GetComponentsInChildren<SpriteRenderer>(true);
            bool hasBounds = false;
            Bounds bounds = default;
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null ||
                    renderer.GetComponentInParent<MonsterHealthBarView>() != null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds
                ? bounds
                : new Bounds(transform.position, Vector3.one);
        }

        private void StartDeathAnimation()
        {
            if (animator == null)
            {
                DestroySelf();
                return;
            }

            if (!TryPlayState(
                    DeadStateCandidates,
                    out float animationDuration,
                    out _deathStateLayer,
                    out _deathStateHash))
            {
                DestroySelf();
                return;
            }

            // The clock only cleans up a missing/interrupted Animator state.
            // Normal removal follows the Animator's actual playback progress.
            _destroyAt = Time.time +
                         Mathf.Max(0.25f, animationDuration);
            _destroyScheduled = true;
        }

        private bool TryGetDeathState(out AnimatorStateInfo state)
        {
            state = default;
            if (animator == null || !animator.isActiveAndEnabled ||
                animator.runtimeAnimatorController == null ||
                _deathStateLayer < 0 || _deathStateLayer >= animator.layerCount)
            {
                return false;
            }

            state = animator.GetCurrentAnimatorStateInfo(_deathStateLayer);
            return state.fullPathHash == _deathStateHash;
        }

        private void PlayHitAnimation()
        {
            SetMoving(false);

            float hitDuration = 0.25f;
            if (animator != null &&
                TryPlayState(HitStateCandidates, out float stateDuration))
            {
                if (stateDuration > 0f)
                {
                    hitDuration = stateDuration;
                }
            }

            ResolveBehaviorController();
            if (_behaviorController != null)
            {
                _behaviorController.StopForHit(hitDuration);
            }
        }

        private bool TryPlayState(
            string[] stateCandidates,
            out float duration)
        {
            return TryPlayState(stateCandidates, out duration, out _, out _);
        }

        private bool TryPlayState(
            string[] stateCandidates,
            out float duration,
            out int playedLayer,
            out int playedStateHash)
        {
            duration = 0f;
            playedLayer = -1;
            playedStateHash = 0;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return false;
            }

            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                string layerName = animator.GetLayerName(layer);
                foreach (string stateCandidate in stateCandidates)
                {
                    int stateHash = Animator.StringToHash(
                        layerName + "." + stateCandidate);
                    if (!animator.HasState(layer, stateHash))
                    {
                        continue;
                    }

                    animator.Play(stateHash, layer, 0f);
                    duration = FindClipLength(stateCandidate);
                    playedLayer = layer;
                    playedStateHash = stateHash;
                    return true;
                }
            }

            return false;
        }

        private float FindClipLength(string stateName)
        {
            RuntimeAnimatorController controller =
                animator.runtimeAnimatorController;
            if (controller == null)
            {
                return 0.25f;
            }

            foreach (AnimationClip clip in controller.animationClips)
            {
                if (clip != null &&
                    string.Equals(
                        clip.name,
                        stateName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return clip.length;
                }
            }

            return 0.25f;
        }
    }
}
