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
    public class MonsterCombatTarget : MonoBehaviour, ICombatTarget
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

        [SerializeField] private Animator animator;
        [SerializeField] private Transform healthBarAnchor;

        private bool _isInitialized;
        private bool _isRegistered;
        private bool _isDead;
        private RunId _runId;
        private RunId _deathRunId;
        private float _destroyAt;
        private bool _destroyScheduled;
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

        public bool IsInitialized => _isInitialized;
        public bool IsRegistered => _isRegistered;
        public bool IsActive => _isInitialized && _isRegistered && !_isDead;
        public bool IsDead => _isDead;
        public RunId RunId => _runId;
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

        protected virtual void Update()
        {
            if (!_destroyScheduled || !_isDead || Time.time < _destroyAt)
            {
                return;
            }

            _destroyScheduled = false;
            if (_runId == _deathRunId)
            {
                DestroySelf();
            }
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

            if (animator == null)
            {
                return;
            }

            animator.Rebind();
            animator.Update(0f);
        }

        public bool ConfigureMovement(
            SpawnSnapshot snapshot,
            CombatRunController runController,
            Bounds movementBounds,
            Vector2 initialHeading)
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
                       initialHeading);
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
            if (_healthBar != null)
            {
                _healthBar.Hide();
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
            if (_healthBar != null)
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
                _deathRunId = _runId;
                killed = true;
                StartDeathAnimation();
                return true;
            }

            PlayHitAnimation();
            return true;
        }

        /// <summary>
        /// Kept for the existing Walker animation event. Generic prefabs do
        /// not need an event because the adapter schedules a guarded fallback.
        /// </summary>
        public void DestroySelf()
        {
            if (!_isDead || !_deathRunId.IsValid)
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

            bool hasAnimation = false;
            float animationDuration = 0f;

            if (TryPlayState(
                    DeadStateCandidates,
                    out float stateDuration))
            {
                hasAnimation = true;
                animationDuration = stateDuration;
            }

            if (!hasAnimation)
            {
                DestroySelf();
                return;
            }

            _destroyAt = Time.time +
                         Mathf.Max(0.25f, animationDuration);
            _destroyScheduled = true;
        }

        private void PlayHitAnimation()
        {
            if (animator == null)
            {
                return;
            }

            TryPlayState(HitStateCandidates, out _);
        }

        private bool TryPlayState(
            string[] stateCandidates,
            out float duration)
        {
            duration = 0f;
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
