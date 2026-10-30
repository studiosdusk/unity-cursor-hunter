using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Generic run-bound combat target. The visual prefab may use any hierarchy,
    /// Animator location, and supported animation state set.
    /// </summary>
    [DisallowMultipleComponent]
    public class MonsterCombatTarget : MonoBehaviour, ICombatTarget, IDamageTextAnchor, IHitEffectAnchor
    {
        private const float ReviveWindowSeconds = 6f;
        private const float HealPulseSeconds = 0.4f;
        private static readonly List<MonsterCombatTarget> RegisteredInstances =
            new List<MonsterCombatTarget>(128);

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
        [SerializeField] private Collider2D hitEffectCollider;
        [SerializeField] private bool showHealthBar;
        [SerializeField, Range(0.01f, 1f)]
        private float deathDespawnNormalizedTime = 1f;

        private bool _isInitialized;
        private bool _isRegistered;
        private bool _isDead;
        private bool _isVulnerable = true;
        private bool _isShielded;
        private bool _killSettlementClaimed;
        private float _healPulseUntil;
        private RunId _runId;
        private RunId _deathRunId;
        private float _destroyAt;
        private float _deathExpiresAt;
        private bool _destroyScheduled;
        private int _deathStateLayer = -1;
        private int _deathStateHash;
        private bool _deathAnimationFinished;
        private float _animatorSpeedBeforeDeath;
        private string _monsterId;
        private long _maxHealth;
        private long _currentHealth;
        private long _garnetReward;
        private string _bonusDropCurrencyId;
        private long _bonusDropAmount;
        private float _bonusDropChancePercent;
        private float _shieldDamageReduction;
        private MonsterBehaviorType _behaviorType;
        private SpriteRenderer[] _visualRenderers = Array.Empty<SpriteRenderer>();
        private SortingGroup _visualSortingGroup;
        private Camera _sortingCamera;
        private float _lastVisualSortingY;
        private bool _hasVisualSortingY;
        private Color[] _originalVisualColors = Array.Empty<Color>();
        private MonsterHealthBarView _healthBar;
        private MonsterBehaviorController _behaviorController;
        private CombatRunController _runController;
        private bool _hasIsMovingParameter;
        private bool _hasLocomotionStates;
        private bool _isMoving;
        private int _idleStateHash;
        private int _walkStateHash;

        public bool IsInitialized => _isInitialized;
        public bool IsRegistered => _isRegistered;
        public bool IsActive => _isInitialized && _isRegistered && !_isDead;
        public bool IsDead => _isDead;
        public bool IsVulnerable => _isVulnerable;
        public bool IsShielded => _isShielded;
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

        internal static void CollectActiveForRun(RunId runId, List<MonsterCombatTarget> results)
        {
            results.Clear();
            for (int i = 0; i < RegisteredInstances.Count; i++)
            {
                MonsterCombatTarget target = RegisteredInstances[i];
                if (target != null && target.gameObject.activeInHierarchy &&
                    target.RunId == runId && target.IsActive && target.IsVulnerable)
                    results.Add(target);
            }
        }

        protected virtual void Awake()
        {
            ResolveAnimator();
            ResolveHitEffectCollider();
        }

        protected virtual void Update()
        {
            if (_healPulseUntil > 0f && Time.time >= _healPulseUntil)
            {
                _healPulseUntil = 0f;
                ApplyStatusTint();
            }
        }

        protected virtual void OnDestroy()
        {
            RegisteredInstances.Remove(this);
        }

        private void ResolveHitEffectCollider()
        {
            if (hitEffectCollider != null) return;
            Transform hitArea = transform.Find("HitArea");
            hitEffectCollider = hitArea != null
                ? hitArea.GetComponent<Collider2D>()
                : GetComponentInChildren<Collider2D>(true);
        }

        public bool TryGetHitEffectBounds(out Bounds bounds)
        {
            // Renderers are cached at spawn; sample their current world bounds without
            // allocating or looking up components on each hit. Visual and hitbox scale
            // can differ, and health bar renderers are excluded from this cache.
            bounds = default;
            bool hasVisualBounds = false;
            for (int index = 0; index < _visualRenderers.Length; index++)
            {
                SpriteRenderer renderer = _visualRenderers[index];
                if (renderer == null || renderer.sprite == null || !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy) continue;
                if (!hasVisualBounds)
                {
                    bounds = renderer.bounds;
                    hasVisualBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            if (hasVisualBounds) return true;

            // Keep collider-only targets usable in sandboxes and legacy prefabs.
            bounds = hitEffectCollider != null ? hitEffectCollider.bounds : default;
            return hitEffectCollider != null && hitEffectCollider.enabled &&
                hitEffectCollider.gameObject.activeInHierarchy;
        }

        /// <summary>Returns the nearest point of this target's hit area to a pet.</summary>
        public Vector2 GetClosestHitPoint(Vector2 from)
        {
            if (hitEffectCollider != null && hitEffectCollider.enabled &&
                hitEffectCollider.gameObject.activeInHierarchy)
                return hitEffectCollider.ClosestPoint(from);
            return transform.position;
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
                if (!_deathAnimationFinished && state.normalizedTime >= 1f)
                {
                    HoldDeathAnimationOnLastFrame();
                }

                if ((!_deathAnimationFinished &&
                     state.normalizedTime < deathDespawnNormalizedTime) ||
                    HasDeathGraceRemaining())
                {
                    return;
                }
            }
            else if (HasDeathGraceRemaining())
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
            _deathExpiresAt = 0f;
            _runController = null;
            _deathStateLayer = -1;
            _deathStateHash = 0;
            RestoreDeathAnimationPlayback();
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
            _isVulnerable = true;
            _isShielded = false;
            _shieldDamageReduction = 0f;
            _healPulseUntil = 0f;
            _killSettlementClaimed = false;
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
            ResolveHitEffectCollider();
            CacheVisualRenderers();
            ConfigureVisualSorting(Camera.main);
            if (!RegisteredInstances.Contains(this))
            {
                RegisteredInstances.Add(this);
            }
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
            _runController = runController;
            return _behaviorController != null &&
                   _behaviorController.ConfigureMovement(
                       snapshot,
                       this,
                       runController,
                       movementBounds,
                       behaviorSeed);
        }

        public bool CanApplyBalance(SpawnSnapshot snapshot, out string error)
        {
            error = string.Empty;
            if (!_isInitialized || !snapshot.IsValid || snapshot.MonsterId != _monsterId)
            {
                error = "몬스터 전투 데이터 또는 대상 ID가 유효하지 않습니다.";
                return false;
            }
            if (_isDead) return true;
            ResolveBehaviorController();
            if (_behaviorController == null || !_behaviorController.CanApplyLiveBalance())
            {
                error = "몬스터의 VisualRoot 또는 HitArea가 없습니다.";
                return false;
            }
            return true;
        }

        /// <summary>Updates a living body without damage, revival, movement reset, or reward settlement.</summary>
        public bool TryApplyBalance(SpawnSnapshot snapshot, Bounds movementBounds, out string error)
        {
            if (!CanApplyBalance(snapshot, out error)) return false;
            if (_isDead) return true;
            _currentHealth = ScaleRemainingHealth(_currentHealth, _maxHealth, snapshot.MaxHealth);
            _maxHealth = snapshot.MaxHealth;
            _garnetReward = snapshot.GarnetReward;
            _bonusDropCurrencyId = snapshot.BonusDropCurrencyId;
            _bonusDropAmount = snapshot.BonusDropAmount;
            _bonusDropChancePercent = snapshot.BonusDropChancePercent;
            _behaviorController.ApplyLiveBalance(snapshot, movementBounds);
            if (showHealthBar)
            {
                EnsureHealthBar();
                _healthBar.Initialize(_maxHealth);
                _healthBar.SetHealth(_currentHealth);
                Bounds visualBounds = CalculateVisualBounds();
                if (healthBarAnchor != null)
                {
                    healthBarAnchor.position = new Vector3(visualBounds.center.x,
                        visualBounds.max.y + 0.16f, visualBounds.center.z);
                }
                _healthBar.Configure(visualBounds, healthBarAnchor != null);
            }
            return true;
        }

        private static long ScaleRemainingHealth(long health, long previousMaximum, long nextMaximum)
        {
            if (health == previousMaximum) return nextMaximum;
            if (nextMaximum == previousMaximum) return health;
            // Exact ceil(health * nextMaximum / previousMaximum). Long division
            // avoids both Int64 multiplication overflow and floating point loss
            // at very large HP values. The remainder always stays below divisor.
            ulong divisor = (ulong)previousMaximum;
            ulong multiplicand = (ulong)health;
            ulong multiplier = (ulong)nextMaximum;
            ulong quotient = 0UL;
            ulong remainder = 0UL;
            for (int bit = 62; bit >= 0; bit--)
            {
                quotient *= 2UL;
                if (remainder >= divisor - remainder)
                {
                    remainder -= divisor - remainder;
                    quotient++;
                }
                else remainder *= 2UL;
                if (((multiplier >> bit) & 1UL) == 0UL) continue;
                if (remainder >= divisor - multiplicand)
                {
                    remainder -= divisor - multiplicand;
                    quotient++;
                }
                else remainder += multiplicand;
            }
            return (long)(quotient + (remainder > 0UL ? 1UL : 0UL));
        }

        /// <summary>
        /// Removes this instance from the current run immediately. Unity's
        /// Destroy is deferred, so this guard is required before pooled or
        /// stale animation callbacks can run.
        /// </summary>
        public void Unregister()
        {
            _isRegistered = false;
            _isVulnerable = false;
            _destroyScheduled = false;
            if (_behaviorController != null)
            {
                _behaviorController.ResetSpecialPresentation();
            }
            SetShielded(false, 0f);
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
                if (hitEffectCollider == null) hitEffectCollider = existingCollider;
                return existingCollider;
            }

            BoxCollider2D generatedCollider = gameObject.AddComponent<BoxCollider2D>();
            hitEffectCollider = generatedCollider;
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
            return ApplyDamage(runId, damage, false,
                out effectiveDamage, out killed);
        }

        public bool ApplyDamage(
            RunId runId,
            long damage,
            bool bypassShield,
            out long effectiveDamage,
            out bool killed)
        {
            return ApplyDamage(runId, damage, bypassShield,
                out effectiveDamage, out killed, out _);
        }

        public bool ApplyDamage(
            RunId runId,
            long damage,
            bool bypassShield,
            out long effectiveDamage,
            out bool killed,
            out long displayDamage)
        {
            effectiveDamage = 0;
            killed = false;
            displayDamage = 0;

            if (!IsActive || !_isVulnerable || !BelongsTo(runId) ||
                damage <= 0 || _currentHealth <= 0)
            {
                return false;
            }

            if (!bypassShield && _isShielded && _shieldDamageReduction > 0f)
            {
                long blockedDamage = (long)Math.Ceiling(
                    damage * (double)_shieldDamageReduction);
                if (blockedDamage >= damage)
                {
                    blockedDamage = damage - 1L;
                }
                damage -= blockedDamage;
            }

            displayDamage = damage;
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
                _isVulnerable = false;
                if (_behaviorController != null)
                {
                    _behaviorController.ResetSpecialPresentation();
                }
                SetShielded(false, 0f);
                SetMoving(false);
                _deathRunId = _runId;
                killed = true;
                CombatAudioFeedback.PlayMonsterDeath(_monsterId);
                StartDeathAnimation();
                return true;
            }

            CombatAudioFeedback.PlayMonsterHit(_monsterId);
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

            bool hasDeathState = TryGetDeathState(out AnimatorStateInfo state);
            if (_destroyScheduled &&
                ((!hasDeathState ||
                  (!_deathAnimationFinished &&
                   state.normalizedTime < deathDespawnNormalizedTime)) ||
                 HasDeathGraceRemaining()))
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

        /// <summary>
        /// Prevents duplicate run rewards when a resurrected instance is
        /// defeated again. Each spawned body settles its drop at most once.
        /// </summary>
        internal bool TryClaimKillSettlement()
        {
            if (_killSettlementClaimed)
            {
                return false;
            }

            _killSettlementClaimed = true;
            return true;
        }

        internal void SetVulnerable(bool vulnerable)
        {
            _isVulnerable = vulnerable && !_isDead && _isRegistered;
        }

        internal void SetShielded(bool shielded, float damageReduction)
        {
            _isShielded = shielded && !_isDead;
            _shieldDamageReduction = _isShielded
                ? Mathf.Clamp01(damageReduction)
                : 0f;

            ApplyStatusTint();
        }

        private void ApplyStatusTint()
        {
            for (int index = 0; index < _visualRenderers.Length; index++)
            {
                SpriteRenderer renderer = _visualRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                Color original = _originalVisualColors[index];
                if (_isShielded)
                {
                    renderer.color = Color.Lerp(
                        original,
                        new Color(0.48f, 0.83f, 1f, original.a),
                        0.55f);
                }
                else if (_healPulseUntil > Time.time)
                {
                    renderer.color = Color.Lerp(
                        original,
                        new Color(0.36f, 1f, 0.57f, original.a),
                        0.62f);
                }
                else
                {
                    renderer.color = original;
                }
            }
        }

        private void ShowHealingPulse()
        {
            _healPulseUntil = Time.time + HealPulseSeconds;
            ApplyStatusTint();
        }

        internal static void HealNearbyAllies(
            MonsterCombatTarget source,
            float radius,
            float healthFraction)
        {
            if (source == null || !source.IsActive || !source.RunId.IsValid)
            {
                return;
            }

            float radiusSquared = Mathf.Max(0.1f, radius);
            radiusSquared *= radiusSquared;
            Vector2 sourcePosition = source.transform.position;
            for (int index = 0; index < RegisteredInstances.Count; index++)
            {
                MonsterCombatTarget ally = RegisteredInstances[index];
                if (ally == null || ally == source || !ally.IsActive ||
                    !ally.BelongsTo(source.RunId))
                {
                    continue;
                }

                Vector2 offset = (Vector2)ally.transform.position - sourcePosition;
                if (offset.sqrMagnitude <= radiusSquared)
                {
                    ally.HealByFraction(healthFraction);
                }
            }
        }

        internal static void ReviveNearbyAllies(
            MonsterCombatTarget source,
            float radius,
            float healthFraction)
        {
            if (source == null || !source.IsActive || !source.RunId.IsValid)
            {
                return;
            }

            float radiusSquared = Mathf.Max(0.1f, radius);
            radiusSquared *= radiusSquared;
            Vector2 sourcePosition = source.transform.position;
            for (int index = 0; index < RegisteredInstances.Count; index++)
            {
                MonsterCombatTarget ally = RegisteredInstances[index];
                if (ally == null || ally == source || !ally.IsDead ||
                    ally._runId != source.RunId || !ally._destroyScheduled)
                {
                    continue;
                }

                Vector2 offset = (Vector2)ally.transform.position - sourcePosition;
                if (offset.sqrMagnitude <= radiusSquared)
                {
                    ally.TryRevive(source.RunId, healthFraction);
                }
            }
        }

        private void HealByFraction(float healthFraction)
        {
            if (!IsActive || _currentHealth >= _maxHealth)
            {
                return;
            }

            long amount = (long)Math.Ceiling(
                _maxHealth * (double)Mathf.Clamp01(healthFraction));
            amount = Math.Max(1L, amount);
            long missingHealth = _maxHealth - _currentHealth;
            _currentHealth += Math.Min(missingHealth, amount);
            if (showHealthBar && _healthBar != null)
            {
                _healthBar.SetHealth(_currentHealth);
            }
            ShowHealingPulse();
        }

        private bool TryRevive(RunId runId, float healthFraction)
        {
            if (!_isInitialized || !_isDead || _runId != runId ||
                _deathRunId != runId || !_destroyScheduled ||
                !HasDeathGraceRemaining())
            {
                return false;
            }

            _destroyScheduled = false;
            _destroyAt = 0f;
            _deathExpiresAt = 0f;
            _deathRunId = default;
            _deathStateLayer = -1;
            _deathStateHash = 0;
            RestoreDeathAnimationPlayback();
            _isDead = false;
            _isRegistered = true;
            _isVulnerable = true;
            _currentHealth = Math.Max(
                1L,
                Math.Min(
                    _maxHealth,
                    (long)Math.Ceiling(
                        _maxHealth * (double)Mathf.Clamp01(healthFraction))));

            if (_behaviorController != null)
            {
                _behaviorController.ResumeAfterRevive();
            }
            SetShielded(false, 0f);
            if (_healthBar != null)
            {
                _healthBar.Initialize(_maxHealth);
                _healthBar.SetHealth(_currentHealth);
            }
            ShowHealingPulse();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Rebind();
                animator.Update(0f);
                InitializeMovementAnimation();
            }

            SetMoving(false);
            return true;
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

        private void CacheVisualRenderers()
        {
            SpriteRenderer[] discovered = GetComponentsInChildren<SpriteRenderer>(true);
            List<SpriteRenderer> renderers = new List<SpriteRenderer>(discovered.Length);
            List<Color> colors = new List<Color>(discovered.Length);
            for (int index = 0; index < discovered.Length; index++)
            {
                SpriteRenderer renderer = discovered[index];
                if (renderer == null ||
                    renderer.GetComponentInParent<MonsterHealthBarView>() != null)
                {
                    continue;
                }

                renderers.Add(renderer);
                colors.Add(renderer.color);
            }

            _visualRenderers = renderers.ToArray();
            _originalVisualColors = colors.ToArray();
        }

        public void ConfigureVisualSorting(Camera worldCamera)
        {
            _sortingCamera = worldCamera;
            _hasVisualSortingY = false;
            Transform visualRoot = transform.Find("VisualRoot");
            if (visualRoot == null) return;

            _visualSortingGroup = visualRoot.GetComponent<SortingGroup>();
            if (_visualSortingGroup == null)
                _visualSortingGroup = visualRoot.gameObject.AddComponent<SortingGroup>();

            RefreshVisualSorting();
        }

        internal void RefreshVisualSorting()
        {
            if (_visualSortingGroup == null || _sortingCamera == null) return;

            float worldY = transform.position.y;
            if (_hasVisualSortingY && _lastVisualSortingY == worldY) return;

            int order = ViewportVisualSorting.OrderAt(
                _sortingCamera, transform.position);
            if (_visualSortingGroup.sortingOrder != order)
                _visualSortingGroup.sortingOrder = order;

            _lastVisualSortingY = worldY;
            _hasVisualSortingY = true;
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
            _deathAnimationFinished = false;
            if (animator == null)
            {
                ScheduleDeathDespawn(ReviveWindowSeconds);
                return;
            }

            if (!TryPlayState(
                    DeadStateCandidates,
                    out float animationDuration,
                    out _deathStateLayer,
                    out _deathStateHash))
            {
                ScheduleDeathDespawn(ReviveWindowSeconds);
                return;
            }

            // Keep the corpse available long enough for the highest-tier
            // monster's resurrection ability, even if its death clip is short.
            ScheduleDeathDespawn(Mathf.Max(
                ReviveWindowSeconds,
                Mathf.Max(0.25f, animationDuration)));
        }

        private void ScheduleDeathDespawn(float delaySeconds)
        {
            float delay = Mathf.Max(0.25f, delaySeconds);
            _destroyAt = Time.time + delay;
            _deathExpiresAt = _runController != null &&
                _runController.CurrentRunId == _runId
                    ? _runController.ElapsedSeconds + delay
                    : float.NaN;
            _destroyScheduled = true;
        }

        private bool HasDeathGraceRemaining()
        {
            if (_runController != null && _runController.IsRunActive &&
                _runController.CurrentRunId == _runId &&
                !float.IsNaN(_deathExpiresAt) &&
                !float.IsInfinity(_deathExpiresAt))
            {
                return _runController.ElapsedSeconds < _deathExpiresAt;
            }

            return Time.time < _destroyAt;
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

        private void HoldDeathAnimationOnLastFrame()
        {
            // Many source clips loop, while corpses remain available for revival.
            // Sample the final pose before the next render and stop the Animator
            // without changing the source clip or the revival window.
            _animatorSpeedBeforeDeath = animator.speed;
            animator.Play(_deathStateHash, _deathStateLayer, 0.999f);
            animator.Update(0f);
            animator.speed = 0f;
            _deathAnimationFinished = true;
        }

        private void RestoreDeathAnimationPlayback()
        {
            if (_deathAnimationFinished && animator != null)
            {
                animator.speed = _animatorSpeedBeforeDeath;
            }

            _deathAnimationFinished = false;
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
