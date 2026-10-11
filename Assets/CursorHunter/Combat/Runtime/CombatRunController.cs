using System;
using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Owns one normal-field run's combat clock, per-target attack cooldowns,
    /// collider resolution, damage accounting, and result creation. Cross-module
    /// lifecycle transitions are coordinated by App.RunCoordinator.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class CombatRunController : MonoBehaviour
    {
        [SerializeField] private LayerMask enemyLayers = -1;
        [SerializeField, Min(32)] private int overlapBufferCapacity = 256;
        [SerializeField] private PlayerCombatStatsRuntime playerCombatStatsRuntime;

        private Collider2D[] _overlapBuffer;
        private readonly HashSet<ICombatTarget> _uniqueTargets =
            new HashSet<ICombatTarget>();
        private readonly Dictionary<ICombatTarget, float> _lastBasicHitAt =
            new Dictionary<ICombatTarget, float>();
        private readonly List<RaycastHit2D> _sweepHits =
            new List<RaycastHit2D>(128);
        private readonly List<Collider2D> _attackOverlaps =
            new List<Collider2D>(128);
        private readonly List<ICombatTarget> _attackTargets =
            new List<ICombatTarget>(128);
        private readonly Dictionary<string, long> _resourceRewards =
            new Dictionary<string, long>(StringComparer.Ordinal);

        private RunRequest _runRequest;
        private SkillCombatSnapshot[] _skills = Array.Empty<SkillCombatSnapshot>();
        private float[] _nextSkillAt = Array.Empty<float>();
        private SeededRandom _random;
        private float _elapsedSeconds;
        private Vector2 _previousAttackCenter;
        private float _previousAttackRadius;
        private bool _hasPreviousAttackCenter;
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
        /// Retained for non-text critical feedback consumers.
        /// </summary>
        public event Action<long> CriticalHit;
        public event Action<CombatDamageApplied> DamageApplied;
        public event Action PresentationReset;

        public RunId CurrentRunId => _isRunning ? _runRequest.RunId : default;

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
        public CombatSnapshot CombatSnapshot => playerCombatStatsRuntime != null
            ? playerCombatStatsRuntime.Snapshot
            : default;
        public PlayerCombatStatsRuntime PlayerCombatStatsRuntime
        {
            get
            {
                ResolvePlayerCombatStatsRuntime();
                return playerCombatStatsRuntime;
            }
        }

        private void Awake()
        {
            overlapBufferCapacity = Mathf.Max(512, overlapBufferCapacity);
            _overlapBuffer = new Collider2D[overlapBufferCapacity];
            ResolvePlayerCombatStatsRuntime();
        }

        private void ResolvePlayerCombatStatsRuntime()
        {
            if (playerCombatStatsRuntime == null)
            {
                playerCombatStatsRuntime = GetComponentInChildren<PlayerCombatStatsRuntime>(true);
            }

            if (playerCombatStatsRuntime == null)
            {
                playerCombatStatsRuntime = gameObject.AddComponent<PlayerCombatStatsRuntime>();
            }
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
            return PrepareRun(request, combatSnapshot, null);
        }

        public bool PrepareRun(
            RunRequest request,
            CombatSnapshot combatSnapshot,
            CursorCombatStatBonusesSnapshot bonuses)
        {
            return PrepareRun(request, combatSnapshot, (CursorCombatStatBonusesSnapshot?)bonuses);
        }

        private bool PrepareRun(
            RunRequest request,
            CombatSnapshot combatSnapshot,
            CursorCombatStatBonusesSnapshot? bonuses)
        {
            ResolvePlayerCombatStatsRuntime();
            if (_isPrepared || _isRunning ||
                !request.IsValid || !combatSnapshot.IsValid)
            {
                return false;
            }

            bool capturedStats = bonuses.HasValue
                ? playerCombatStatsRuntime.TryCaptureSnapshot(bonuses.Value)
                : playerCombatStatsRuntime.TryCaptureSnapshot(combatSnapshot);
            if (!capturedStats)
            {
                return false;
            }

            _runRequest = request;
            ConfigureSkills(null);
            _random = new SeededRandom(request.Seed ^ 0xC17C17UL);
            _elapsedSeconds = 0f;
            _lastBasicHitAt.Clear();
            ResetAttackPath();
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
            playerCombatStatsRuntime.SetRunActive(true);
            PresentationReset?.Invoke();
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
            _lastBasicHitAt.Clear();
            ResetAttackPath();
            playerCombatStatsRuntime.SetRunActive(false);
            PresentationReset?.Invoke();
            return true;
        }

        public bool PauseRun()
        {
            if (!_isRunning || _isPaused || _completionRequested || _abortRequested)
            {
                return false;
            }

            _isPaused = true;
            ResetAttackPath();
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
        /// Drops the cursor's previous position when input cannot attack. The
        /// next valid sample then checks its endpoint without bridging the gap.
        /// </summary>
        public void ResetAttackPath()
        {
            _hasPreviousAttackCenter = false;
        }

        /// <summary>
        /// Resolves basic attacks along the cursor's movement since its last
        /// valid sample. Each target uses its own last successful hit time.
        /// </summary>
        public bool TryAttack(Collider2D attackCollider, bool includeMovementPath = true)
        {
            if (!IsRunning || attackCollider == null || !attackCollider.enabled)
            {
                ResetAttackPath();
                return false;
            }

            if (_elapsedSeconds >= _runRequest.DurationSeconds)
            {
                ResetAttackPath();
                return false;
            }

            Physics2D.SyncTransforms();

            ContactFilter2D contactFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true
            };
            contactFilter.SetLayerMask(enemyLayers);

            _uniqueTargets.Clear();
            _attackTargets.Clear();

            if (attackCollider is CircleCollider2D)
            {
                Bounds bounds = attackCollider.bounds;
                Vector2 center = bounds.center;
                float radius = Mathf.Max(bounds.extents.x, bounds.extents.y);
                if (radius <= 0f)
                {
                    ResetAttackPath();
                    return false;
                }

                _sweepHits.Clear();
                Vector2 movement = center - _previousAttackCenter;
                if (includeMovementPath && _hasPreviousAttackCenter &&
                    Mathf.Approximately(radius, _previousAttackRadius) &&
                    movement.sqrMagnitude > 0f)
                {
                    float distance = movement.magnitude;
                    Physics2D.CircleCast(
                        _previousAttackCenter,
                        radius,
                        movement / distance,
                        contactFilter,
                        _sweepHits,
                        distance);
                    for (int index = 0; index < _sweepHits.Count; index++)
                    {
                        AddAttackTarget(_sweepHits[index].collider);
                    }
                }

                _attackOverlaps.Clear();
                Physics2D.OverlapCircle(center, radius, contactFilter, _attackOverlaps);
                for (int index = 0; index < _attackOverlaps.Count; index++)
                {
                    AddAttackTarget(_attackOverlaps[index]);
                }

                _previousAttackCenter = center;
                _previousAttackRadius = radius;
                _hasPreviousAttackCenter = includeMovementPath;
            }
            else
            {
                // Other collider shapes retain point-only overlap behavior.
                ResetAttackPath();
                int overlapCount = attackCollider.Overlap(contactFilter, _overlapBuffer);
                for (int index = 0; index < overlapCount; index++)
                {
                    AddAttackTarget(_overlapBuffer[index]);
                }
            }

            float cooldown = playerCombatStatsRuntime.AutoAttackIntervalSeconds;
            for (int index = 0; index < _attackTargets.Count; index++)
            {
                if (!IsRunning)
                {
                    break;
                }

                ICombatTarget target = _attackTargets[index];
                if (!target.IsActive || !target.IsRegistered ||
                    target.RunId != _runRequest.RunId ||
                    (_lastBasicHitAt.TryGetValue(target, out float lastHitAt) &&
                     _elapsedSeconds - lastHitAt < cooldown))
                {
                    continue;
                }

                if (ApplyBundle(target) && target.IsActive && IsRunning)
                {
                    _lastBasicHitAt[target] = _elapsedSeconds;
                }
            }

            return true;
        }

        private void AddAttackTarget(Collider2D collider)
        {
            if (collider == null)
            {
                return;
            }

            MonsterCombatTarget targetAdapter =
                collider.GetComponentInParent<MonsterCombatTarget>();
            if (targetAdapter != null &&
                targetAdapter is ICombatTarget target &&
                target.RunId == _runRequest.RunId &&
                target.IsActive &&
                target.IsRegistered &&
                _uniqueTargets.Add(target))
            {
                _attackTargets.Add(target);
            }
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
                    var target = collider == null ? null : collider.GetComponentInParent<MonsterCombatTarget>();
                    if (target != null && target.RunId == _runRequest.RunId && target.IsActive && target.IsRegistered)
                        _uniqueTargets.Add(target);
                }
                foreach (var target in _uniqueTargets)
                {
                    if (!IsRunning) break;
                    ApplyBundle(target, skill.Damage, CombatDamageSource.Skill);
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
            _lastBasicHitAt.Clear();
            ResetAttackPath();
            playerCombatStatsRuntime.SetRunActive(false);

            PresentationReset?.Invoke();
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
            _lastBasicHitAt.Clear();
            ResetAttackPath();
            playerCombatStatsRuntime.SetRunActive(false);

            PresentationReset?.Invoke();
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

        private bool ApplyBundle(ICombatTarget target, long skillDamage = 0L,
            CombatDamageSource source = CombatDamageSource.CursorAttack)
        {
            bool wasCritical = RollCriticalHit();
            if (!TryGetHitDamage(wasCritical, out long hitDamage, skillDamage))
            {
                RequestAbort(RunEndReason.NumericOverflow);
                return false;
            }

            IDamageTextAnchor anchor = target as IDamageTextAnchor;
            Vector3 hitPosition = DamageApplied != null && anchor != null
                ? anchor.DamageTextPosition : default;
            Bounds hitBounds = default;
            bool hasHitBounds = DamageApplied != null &&
                source == CombatDamageSource.CursorAttack &&
                target is IHitEffectAnchor hitAnchor &&
                hitAnchor.TryGetHitEffectBounds(out hitBounds);
            bool applied = target.ApplyDamage(
                _runRequest.RunId,
                hitDamage,
                out long effectiveDamage,
                out bool killed);

            if (!applied)
            {
                return false;
            }

            if (killed)
            {
                _lastBasicHitAt.Remove(target);
            }

            if (!TryAddNonNegative(
                    _effectiveDamage,
                    effectiveDamage,
                    out long nextEffectiveDamage))
            {
                RequestAbort(RunEndReason.NumericOverflow);
                return true;
            }

            _effectiveDamage = nextEffectiveDamage;

            if (effectiveDamage > 0L)
            {
                DamageApplied?.Invoke(new CombatDamageApplied(
                    _runRequest.RunId, target, effectiveDamage, wasCritical,
                    hitPosition, anchor != null, source, hitBounds.center, hasHitBounds,
                    Mathf.Max(hitBounds.size.x, hitBounds.size.y)));
            }

            if (wasCritical && effectiveDamage > 0L)
            {
                CriticalHit?.Invoke(effectiveDamage);
            }

            if (!killed)
            {
                return true;
            }

            if (_defeatedCount == int.MaxValue ||
                !TryAddNonNegative(
                    _garnetEarned,
                    target.GarnetReward,
                    out long nextGarnetEarned))
            {
                RequestAbort(RunEndReason.NumericOverflow);
                return true;
            }

            _defeatedCount++;
            _garnetEarned = nextGarnetEarned;
            if (!TryRecordReward("gem.garnet", target.GarnetReward))
            {
                RequestAbort(RunEndReason.NumericOverflow);
                return true;
            }

            if (!TryRecordBonusDrop(
                    target.BonusDropCurrencyId,
                    target.BonusDropAmount,
                    target.BonusDropChancePercent))
            {
                RequestAbort(RunEndReason.NumericOverflow);
                return true;
            }
            return true;
        }

        private bool RollCriticalHit()
        {
            return playerCombatStatsRuntime.CriticalChancePercent > 0f &&
                   _random != null &&
                   _random.NextFloat(0f, 100f) < playerCombatStatsRuntime.CriticalChancePercent;
        }

        private bool TryGetHitDamage(bool critical, out long damage, long skillDamage = 0L)
        {
            double scaled = skillDamage > 0 ? skillDamage : playerCombatStatsRuntime.AttackPower;
            if (_runRequest.Mode == RunMode.Boss)
            {
                scaled *= playerCombatStatsRuntime.BossDamageMultiplier;
            }

            if (critical)
            {
                scaled *= playerCombatStatsRuntime.CriticalDamageMultiplier;
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
