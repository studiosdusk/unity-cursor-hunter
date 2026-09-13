using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Owns one normal-field run's combat clock, attack cooldown, collider
    /// resolution, damage accounting, and result creation. Cross-module
    /// lifecycle transitions are coordinated by App.RunCoordinator.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class CombatRunController : MonoBehaviour
    {
        [SerializeField] private LayerMask enemyLayers = -1;
        [SerializeField, Min(32)] private int overlapBufferCapacity = 256;

        private Collider2D[] _overlapBuffer;
        private readonly HashSet<WalkerStumpTarget> _uniqueTargets =
            new HashSet<WalkerStumpTarget>();

        private RunRequest _runRequest;
        private CombatSnapshot _combatSnapshot;
        private float _elapsedSeconds;
        private float _nextAttackAvailableAt;
        private int _defeatedCount;
        private long _garnetEarned;
        private long _effectiveDamage;
        private bool _isRunning;
        private bool _isPaused;
        private bool _completionRequested;
        private bool _abortRequested;

        public event Action<RunEndReason> CompletionRequested;
        public event Action<RunEndReason> AbortRequested;

        public bool IsRunning =>
            _isRunning && !_isPaused && !_completionRequested && !_abortRequested;
        public bool IsRunActive =>
            _isRunning && !_completionRequested && !_abortRequested;
        public bool IsPaused => _isPaused;
        public float ElapsedSeconds => _elapsedSeconds;
        public float RemainingSeconds =>
            _isRunning
                ? Mathf.Max(0f, _runRequest.DurationSeconds - _elapsedSeconds)
                : 0f;
        public int DefeatedCount => _defeatedCount;
        public long GarnetEarned => _garnetEarned;
        public long EffectiveDamage => _effectiveDamage;
        public CombatSnapshot CombatSnapshot => _combatSnapshot;

        private void Awake()
        {
            overlapBufferCapacity = Mathf.Max(32, overlapBufferCapacity);
            _overlapBuffer = new Collider2D[overlapBufferCapacity];
        }

        private void Update()
        {
            AdvanceTime(Time.deltaTime);
        }

        /// <summary>
        /// Advances the combat clock. Keeping this small seam public makes the
        /// duration boundary deterministic in tests without changing the
        /// production Update loop.
        /// </summary>
        public void AdvanceTime(float deltaSeconds)
        {
            if (!_isRunning || _isPaused || _completionRequested || _abortRequested ||
                deltaSeconds <= 0f || float.IsNaN(deltaSeconds) ||
                float.IsInfinity(deltaSeconds))
            {
                return;
            }

            _elapsedSeconds += deltaSeconds;

            if (_elapsedSeconds >= _runRequest.DurationSeconds)
            {
                _elapsedSeconds = _runRequest.DurationSeconds;
                _completionRequested = true;
                CompletionRequested?.Invoke(RunEndReason.TimeExpired);
            }
        }

        public bool StartRun(
            RunRequest request,
            CombatSnapshot combatSnapshot)
        {
            if (_isRunning || !request.IsValid)
            {
                return false;
            }

            _runRequest = request;
            _combatSnapshot = combatSnapshot;
            _elapsedSeconds = 0f;
            _nextAttackAvailableAt = 0f;
            _defeatedCount = 0;
            _garnetEarned = 0;
            _effectiveDamage = 0;
            _isRunning = true;
            _isPaused = false;
            _completionRequested = false;
            _abortRequested = false;
            return true;
        }

        public bool PauseRun()
        {
            if (!_isRunning || _isPaused || _completionRequested || _abortRequested)
            {
                return false;
            }

            _isPaused = true;
            return true;
        }

        public bool ResumeRun()
        {
            if (!_isRunning || !_isPaused || _completionRequested || _abortRequested)
            {
                return false;
            }

            _isPaused = false;
            return true;
        }

        /// <summary>
        /// Resolves one input attack against every distinct enemy Collider2D
        /// overlapping the cursor Collider2D.
        /// </summary>
        public bool TryAttack(Collider2D attackCollider)
        {
            if (!IsRunning || attackCollider == null || !attackCollider.enabled)
            {
                return false;
            }

            if (_elapsedSeconds >= _runRequest.DurationSeconds ||
                _elapsedSeconds < _nextAttackAvailableAt)
            {
                return false;
            }

            _nextAttackAvailableAt =
                _elapsedSeconds + _combatSnapshot.AttackCooldownSeconds;

            Physics2D.SyncTransforms();

            ContactFilter2D contactFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true
            };
            contactFilter.SetLayerMask(enemyLayers);

            int overlapCount = attackCollider.Overlap(
                contactFilter,
                _overlapBuffer);

            _uniqueTargets.Clear();

            for (int index = 0; index < overlapCount; index++)
            {
                Collider2D collider = _overlapBuffer[index];
                if (collider == null)
                {
                    continue;
                }

                WalkerStumpTarget target =
                    collider.GetComponentInParent<WalkerStumpTarget>();

                if (target != null && target.IsInitialized && !target.IsDead)
                {
                    _uniqueTargets.Add(target);
                }
            }

            foreach (WalkerStumpTarget target in _uniqueTargets)
            {
                if (!IsRunning)
                {
                    break;
                }

                ApplyBundle(target);
            }

            return true;
        }

        public bool TryCompleteRun(
            RunEndReason endReason,
            RunSettlementPolicy settlementPolicy,
            out RunResult result)
        {
            if (!_isRunning)
            {
                result = default;
                return false;
            }

            _isRunning = false;
            _isPaused = false;
            _completionRequested = false;
            _abortRequested = false;

            result = CreateResult(
                endReason,
                settlementPolicy);
            return true;
        }

        public bool TryAbortRun(
            RunEndReason endReason,
            RunSettlementPolicy settlementPolicy,
            out RunResult result)
        {
            if (!_isRunning)
            {
                result = default;
                return false;
            }

            _isRunning = false;
            _isPaused = false;
            _completionRequested = false;
            _abortRequested = false;

            result = CreateResult(
                endReason,
                settlementPolicy);
            return true;
        }

        private RunResult CreateResult(
            RunEndReason endReason,
            RunSettlementPolicy settlementPolicy)
        {
            return new RunResult(
                _runRequest,
                endReason,
                settlementPolicy,
                _elapsedSeconds,
                _defeatedCount,
                _garnetEarned,
                _effectiveDamage);
        }

        private void ApplyBundle(WalkerStumpTarget target)
        {
            for (int hitIndex = 0;
                 hitIndex < _combatSnapshot.HitsPerBundle;
                 hitIndex++)
            {
                bool applied = target.ApplyDamage(
                    _combatSnapshot.AttackPower,
                    out long effectiveDamage,
                    out bool killed);

                if (!applied)
                {
                    return;
                }

                if (!TryAddNonNegative(
                        _effectiveDamage,
                        effectiveDamage,
                        out long nextEffectiveDamage))
                {
                    RequestAbort(RunEndReason.NumericOverflow);
                    return;
                }

                _effectiveDamage = nextEffectiveDamage;

                if (!killed)
                {
                    continue;
                }

                if (_defeatedCount == int.MaxValue ||
                    !TryAddNonNegative(
                        _garnetEarned,
                        target.GarnetReward,
                        out long nextGarnetEarned))
                {
                    RequestAbort(RunEndReason.NumericOverflow);
                    return;
                }

                _defeatedCount++;
                _garnetEarned = nextGarnetEarned;
                return;
            }
        }

        private void RequestAbort(RunEndReason reason)
        {
            if (_abortRequested || !_isRunning)
            {
                return;
            }

            _abortRequested = true;
            AbortRequested?.Invoke(reason);
        }

        private static bool TryAddNonNegative(
            long current,
            long amount,
            out long result)
        {
            if (current < 0L || amount < 0L || long.MaxValue - current < amount)
            {
                result = 0L;
                return false;
            }

            result = current + amount;
            return true;
        }
    }
}
