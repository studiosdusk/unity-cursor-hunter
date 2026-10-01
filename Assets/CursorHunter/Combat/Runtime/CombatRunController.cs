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
        private readonly HashSet<ICombatTarget> _uniqueTargets =
            new HashSet<ICombatTarget>();
        private readonly Dictionary<string, long> _resourceRewards =
            new Dictionary<string, long>(StringComparer.Ordinal);

        private RunRequest _runRequest;
        private CombatSnapshot _combatSnapshot;
        private SkillCombatSnapshot[] _skills = Array.Empty<SkillCombatSnapshot>();
        private float[] _nextSkillAt = Array.Empty<float>();
        private SeededRandom _random;
        private float _elapsedSeconds;
        private float _nextAttackAvailableAt;
        private int _defeatedCount;
        private long _garnetEarned;
        private long _effectiveDamage;
        private bool _isPrepared;
        private bool _isRunning;
        private bool _isPaused;
        private bool _completionRequested;
        private bool _abortRequested;

        public event Action<RunEndReason> CompletionRequested;
        public event Action<RunEndReason> AbortRequested;
        /// <summary>
        /// Raised once for each logical critical hit. The HUD uses this event
        /// for an explicit visual callout; it carries no Unity object reference
        /// so listeners cannot mutate combat state or retain target objects.
        /// </summary>
        public event Action<long> CriticalHit;

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
            overlapBufferCapacity = Mathf.Max(512, overlapBufferCapacity);
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
            if (!PrepareRun(request, combatSnapshot))
            {
                return false;
            }

            if (CommitStart())
            {
                return true;
            }

            CancelPreparedRun();
            return false;
        }

        /// <summary>
        /// Captures the immutable run data without opening the attack clock.
        /// RunCoordinator commits this only after the initial spawn pack is
        /// ready, so a spawn failure cannot expose a half-started combat run.
        /// </summary>
        public bool PrepareRun(
            RunRequest request,
            CombatSnapshot combatSnapshot)
        {
            if (_isPrepared || _isRunning ||
                !request.IsValid || !combatSnapshot.IsValid)
            {
                return false;
            }

            _runRequest = request;
            _combatSnapshot = combatSnapshot;
            ConfigureSkills(null);
            _random = new SeededRandom(request.Seed ^ 0xC17C17UL);
            _elapsedSeconds = 0f;
            _nextAttackAvailableAt = 0f;
            _defeatedCount = 0;
            _garnetEarned = 0;
            _effectiveDamage = 0;
            _resourceRewards.Clear();
            _isPrepared = true;
            _isRunning = false;
            _isPaused = false;
            _completionRequested = false;
            _abortRequested = false;
            return true;
        }

        /// <summary>
        /// Opens the combat clock after all other run participants prepared.
        /// </summary>
        public bool CommitStart()
        {
            if (!_isPrepared || _isRunning)
            {
                return false;
            }

            _isPrepared = false;
            _isRunning = true;
            return true;
        }

        /// <summary>
        /// Clears a prepared-but-not-committed run during start rollback.
        /// </summary>
        public bool CancelPreparedRun()
        {
            if (!_isPrepared || _isRunning)
            {
                return false;
            }

            _isPrepared = false;
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

                WalkerStumpTarget targetAdapter =
                    collider.GetComponentInParent<WalkerStumpTarget>();

                if (targetAdapter != null &&
                    targetAdapter is ICombatTarget target &&
                    target.RunId == _runRequest.RunId &&
                    target.IsActive &&
                    target.IsRegistered)
                {
                    _uniqueTargets.Add(target);
                }
            }

            foreach (ICombatTarget target in _uniqueTargets)
            {
                if (!IsRunning)
                {
                    break;
                }

                ApplyBundle(target);
            }

            return true;
        }

        public void ConfigureSkills(IReadOnlyList<SkillCombatSnapshot> skills)
        {
            int count = skills == null ? 0 : skills.Count;
            _skills = new SkillCombatSnapshot[count];
            _nextSkillAt = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (!skills[i].IsValid) throw new ArgumentException("Invalid skill snapshot.");
                _skills[i] = skills[i];
                _nextSkillAt[i] = _elapsedSeconds + skills[i].CooldownSeconds;
            }
        }

        public void TryUseSkills(Vector2 center)
        {
            if (!IsRunning || _elapsedSeconds >= _runRequest.DurationSeconds) return;
            var filter = new ContactFilter2D { useLayerMask = true, useTriggers = true };
            filter.SetLayerMask(enemyLayers);
            bool transformsSynced = false;
            for (int i = 0; i < _skills.Length && IsRunning; i++)
            {
                var skill = _skills[i];
                if (!skill.Unlocked || _elapsedSeconds < _nextSkillAt[i]) continue;
                if (!transformsSynced) { Physics2D.SyncTransforms(); transformsSynced = true; }
                _nextSkillAt[i] = _elapsedSeconds + skill.CooldownSeconds;
                int count = Physics2D.OverlapCircle(center, skill.RadiusWorldUnits, filter, _overlapBuffer);
                _uniqueTargets.Clear();
                for (int j = 0; j < count; j++)
                {
                    var collider = _overlapBuffer[j];
                    var target = collider == null ? null : collider.GetComponentInParent<WalkerStumpTarget>();
                    if (target != null && target.RunId == _runRequest.RunId && target.IsActive && target.IsRegistered)
                        _uniqueTargets.Add(target);
                }
                foreach (var target in _uniqueTargets)
                {
                    if (!IsRunning) break;
                    ApplyBundle(target, skill.Damage);
                }
            }
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
                _effectiveDamage,
                CreateResourceRewards());
        }

        private void ApplyBundle(ICombatTarget target, long skillDamage = 0L)
        {
            bool wasCritical = RollCriticalHit();
            if (!TryGetHitDamage(wasCritical, out long hitDamage, skillDamage))
            {
                RequestAbort(RunEndReason.NumericOverflow);
                return;
            }

            bool applied = target.ApplyDamage(
                _runRequest.RunId,
                hitDamage,
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

            if (wasCritical && effectiveDamage > 0L)
            {
                CriticalHit?.Invoke(effectiveDamage);
            }

            if (!killed)
            {
                return;
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
            if (!TryRecordReward("gem.garnet", target.GarnetReward))
            {
                RequestAbort(RunEndReason.NumericOverflow);
                return;
            }

            if (!TryRecordBonusDrop(
                    target.BonusDropCurrencyId,
                    target.BonusDropAmount,
                    target.BonusDropChancePercent))
            {
                RequestAbort(RunEndReason.NumericOverflow);
                return;
            }
            return;
        }

        private bool RollCriticalHit()
        {
            return _combatSnapshot.CriticalChancePercent > 0f &&
                   _random != null &&
                   _random.NextFloat(0f, 100f) < _combatSnapshot.CriticalChancePercent;
        }

        private bool TryGetHitDamage(bool critical, out long damage, long skillDamage = 0L)
        {
            double scaled = skillDamage > 0 ? skillDamage : _combatSnapshot.AttackPower;
            if (_runRequest.Mode == RunMode.Boss)
            {
                scaled *= _combatSnapshot.BossDamageMultiplier;
            }

            if (critical)
            {
                scaled *= _combatSnapshot.CriticalDamageMultiplier;
            }

            if (double.IsNaN(scaled) || double.IsInfinity(scaled) || scaled <= 0d)
            {
                damage = 0L;
                return false;
            }

            if (scaled >= long.MaxValue)
            {
                damage = long.MaxValue;
                return true;
            }

            damage = Math.Max(1L, (long)Math.Round(scaled, MidpointRounding.AwayFromZero));
            return true;
        }

        private bool TryRecordBonusDrop(
            string currencyId,
            long amount,
            float chancePercent)
        {
            if (amount <= 0L || string.IsNullOrWhiteSpace(currencyId) ||
                chancePercent <= 0f || _random == null)
            {
                return true;
            }

            if (_random.NextFloat(0f, 100f) >= chancePercent)
            {
                return true;
            }

            return TryRecordReward(
                currencyId,
                amount);
        }

        private bool TryRecordReward(string currencyId, long amount)
        {
            if (string.IsNullOrWhiteSpace(currencyId) || amount <= 0L)
            {
                return true;
            }

            if (_resourceRewards.TryGetValue(currencyId, out long current))
            {
                if (!TryAddNonNegative(current, amount, out long next))
                {
                    return false;
                }

                _resourceRewards[currencyId] = next;
                return true;
            }

            _resourceRewards.Add(currencyId, amount);
            return true;
        }

        private ResourceRewardSnapshot[] CreateResourceRewards()
        {
            if (_resourceRewards.Count == 0)
            {
                return Array.Empty<ResourceRewardSnapshot>();
            }

            ResourceRewardSnapshot[] rewards =
                new ResourceRewardSnapshot[_resourceRewards.Count];
            int index = 0;
            foreach (KeyValuePair<string, long> pair in _resourceRewards)
            {
                rewards[index++] = new ResourceRewardSnapshot(pair.Key, pair.Value);
            }

            return rewards;
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
