using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Owns one normal-field run's combat clock, attack cooldown, collider
    /// resolution, damage accounting, and result creation.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class CombatRunController : MonoBehaviour
    {
        private const string TimeExpiredReason = "time_expired";

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

        public event Action<RunResult> Completed;

        public bool IsRunning => _isRunning;
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
            if (!_isRunning)
            {
                return;
            }

            _elapsedSeconds += Time.deltaTime;

            if (_elapsedSeconds >= _runRequest.DurationSeconds)
            {
                _elapsedSeconds = _runRequest.DurationSeconds;
                CompleteRun(TimeExpiredReason);
            }
        }

        public void StartRun(RunRequest request, CombatSnapshot combatSnapshot)
        {
            _runRequest = request;
            _combatSnapshot = combatSnapshot;
            _elapsedSeconds = 0f;
            _nextAttackAvailableAt = 0f;
            _defeatedCount = 0;
            _garnetEarned = 0;
            _effectiveDamage = 0;
            _isRunning = true;
        }

        /// <summary>
        /// Resolves one input attack against every distinct enemy Collider2D
        /// overlapping the cursor Collider2D.
        /// </summary>
        public bool TryAttack(Collider2D attackCollider)
        {
            if (!_isRunning || attackCollider == null || !attackCollider.enabled)
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
                ApplyBundle(target);
            }

            return true;
        }

        public void CompleteRun(string endReason)
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;

            RunResult result = new RunResult(
                _runRequest.RunId,
                endReason,
                _elapsedSeconds,
                _defeatedCount,
                _garnetEarned,
                _effectiveDamage);

            Completed?.Invoke(result);
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

                _effectiveDamage += effectiveDamage;

                if (!killed)
                {
                    continue;
                }

                _defeatedCount++;
                _garnetEarned += target.GarnetReward;
                return;
            }
        }
    }
}
