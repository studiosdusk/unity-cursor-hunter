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
    public sealed partial class CombatRunController : MonoBehaviour
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
        private readonly Dictionary<string, double> _fractionalRewardRemainders =
            new Dictionary<string, double>(StringComparer.Ordinal);

        private RunRequest _runRequest;
        private readonly List<IRunSkillController> _skillControllers =
            new List<IRunSkillController>(7);
        private readonly Dictionary<string, IRunSkillController> _skillControllersById =
            new Dictionary<string, IRunSkillController>(StringComparer.Ordinal);
        private RelicCombatSnapshot[] _relicSnapshots = Array.Empty<RelicCombatSnapshot>();
        private IReadOnlyList<RelicCombatSnapshot> _relicSnapshotView =
            Array.AsReadOnly(Array.Empty<RelicCombatSnapshot>());
        private RelicDropRulesCombatSnapshot _relicDropRules;
        private RelicCombatModifiers _relicModifiers;
        private SeededRandom _random;
        private float _elapsedSeconds;
        private float _durationSeconds;
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
        private bool _hasInvalidSkillControllerSet;

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
                ? Mathf.Max(0f, _durationSeconds - _elapsedSeconds)
                : 0f;
        public int DefeatedCount => _defeatedCount;
        public long GarnetEarned => _garnetEarned;
        /// <summary>Current run earnings only; does not read or mutate the permanent wallet.</summary>
        public long GetResourceEarned(string currencyId)
        {
            return currencyId != null && _resourceRewards.TryGetValue(currencyId, out long amount)
                ? amount : 0L;
        }

        public long EffectiveDamage => _effectiveDamage;
        public LayerMask EnemyLayers => enemyLayers;
        public bool IsBossRun => _isRunning && _runRequest.Mode == RunMode.Boss;
        public CombatSnapshot CombatSnapshot => playerCombatStatsRuntime != null
            ? playerCombatStatsRuntime.Snapshot
            : default;
        public IReadOnlyList<RelicCombatSnapshot> RelicSnapshots => _relicSnapshotView;
        public RelicDropRulesCombatSnapshot RelicDropRules => _relicDropRules;
        public RelicCombatModifiers RelicModifiers => _relicModifiers;
        public float AreaSkillRadiusMultiplier =>
            _relicModifiers.AreaSkillRadiusMultiplier;

        public float ResolveAutoSkillCooldown(float baseCooldownSeconds)
        {
            return Mathf.Max(0.1f,
                baseCooldownSeconds * _relicModifiers.AutoSkillCooldownMultiplier);
        }
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
            RefreshSkillControllers();
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

            if (_elapsedSeconds >= _durationSeconds)
            {
                _elapsedSeconds = Mathf.Max(_elapsedSeconds - deltaSeconds, _durationSeconds);
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
            _durationSeconds = request.DurationSeconds;
            StopAllSkills();
            _random = new SeededRandom(request.Seed ^ 0xC17C17UL);
            _elapsedSeconds = 0f;
            _lastBasicHitAt.Clear();
            ResetAttackPath();
            _defeatedCount = 0;
            _garnetEarned = 0;
            _effectiveDamage = 0;
            _resourceRewards.Clear();
            _fractionalRewardRemainders.Clear();
            _relicSnapshots = Array.Empty<RelicCombatSnapshot>();
            _relicSnapshotView = Array.AsReadOnly(_relicSnapshots);
            _relicDropRules = default;
            _relicModifiers = default;
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
            _relicSnapshots = Array.Empty<RelicCombatSnapshot>();
            _relicSnapshotView = Array.AsReadOnly(_relicSnapshots);
            _relicDropRules = default;
            _relicModifiers = default;
            playerCombatStatsRuntime.SetRunActive(false);
            StopAllSkills();
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

            if (_elapsedSeconds >= _durationSeconds)
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

        public bool ConfigureSkills(IReadOnlyList<SkillCombatSnapshot> skills)
        {
            return ConfigureSkills(skills, false);
        }

        /// <summary>
        /// Configures skills from an immutable run snapshot. Main's Trait path
        /// requires every skill ID and every unlocked controller to match; it
        /// must not silently substitute editor toggles or omit broken skills.
        /// </summary>
        public bool ConfigureSkills(
            IReadOnlyList<SkillCombatSnapshot> skills,
            bool requireExactSnapshotMapping)
        {
            StopAllSkills();
            if (!_isPrepared && !_isRunning) return false;
            bool validControllerSet = RefreshSkillControllers();
            if (requireExactSnapshotMapping && !validControllerSet)
            {
                Debug.LogError(
                    "Trait skill mapping is incomplete: Main must have one valid controller per skill ID.",
                    this);
                return false;
            }

            if (requireExactSnapshotMapping && skills == null)
            {
                Debug.LogError("Trait skill snapshot is missing.", this);
                return false;
            }

            var snapshots = new Dictionary<string, SkillCombatSnapshot>(
                StringComparer.Ordinal);
            if (skills != null)
            {
                for (int i = 0; i < skills.Count; i++)
                {
                    SkillCombatSnapshot skill = skills[i];
                    if (!skill.IsValid)
                    {
                        if (requireExactSnapshotMapping)
                        {
                            Debug.LogError(
                                "Trait skill snapshot is invalid for '" + skill.SkillId + "'.",
                                this);
                            return false;
                        }
                        Debug.LogWarning(
                            "Ignoring invalid skill snapshot '" + skill.SkillId +
                            "'; other skills and the run will continue.", this);
                        continue;
                    }
                    if (snapshots.ContainsKey(skill.SkillId))
                    {
                        if (requireExactSnapshotMapping)
                        {
                            Debug.LogError(
                                "Trait skill snapshot contains duplicate ID '" + skill.SkillId + "'.",
                                this);
                            return false;
                        }
                        Debug.LogWarning(
                            "Ignoring duplicate skill snapshot '" + skill.SkillId +
                            "'; the first valid snapshot will be used.", this);
                        continue;
                    }
                    snapshots.Add(skill.SkillId, skill);
                }
            }
            if (requireExactSnapshotMapping &&
                (snapshots.Count != _skillControllersById.Count ||
                 snapshots.Count != skills.Count))
            {
                Debug.LogError(
                    "Trait skill mapping mismatch: snapshot and Main controller IDs must match exactly.",
                    this);
                return false;
            }

            if (requireExactSnapshotMapping)
            {
                foreach (string skillId in snapshots.Keys)
                {
                    if (_skillControllersById.ContainsKey(skillId)) continue;
                    Debug.LogError(
                        "Trait skill '" + skillId + "' has no Main scene controller.",
                        this);
                    return false;
                }
                if (_skillBalanceOverrides != null)
                    ClearSkillBalanceOverrides();
            }

            foreach (SkillCombatSnapshot skill in snapshots.Values)
            {
                if (skill.Unlocked && !_skillControllersById.ContainsKey(skill.SkillId))
                {
                    if (requireExactSnapshotMapping)
                    {
                        Debug.LogError(
                            "Unlocked Trait skill '" + skill.SkillId + "' has no scene controller.",
                            this);
                        return false;
                    }
                    Debug.LogWarning(
                        "Unlocked skill '" + skill.SkillId +
                        "' has no scene controller; continuing without it.", this);
                }
            }

            for (int i = 0; i < _skillControllers.Count; i++)
            {
                IRunSkillController controller = _skillControllers[i];
                SkillBalanceSettings balance = FindSkillBalance(controller.Id);
                if (balance != null && controller is ISkillBalanceController liveController)
                {
                    if (liveController.ApplyBalanceSettings(this, _runRequest.RunId,
                        _runRequest.Seed, balance)) continue;
                    StopAllSkills();
                    return false;
                }
                if (!snapshots.TryGetValue(controller.Id, out SkillCombatSnapshot skill))
                    skill = new SkillCombatSnapshot(controller.Id, false);
                bool acquired = requireExactSnapshotMapping
                    ? skill.Unlocked
                    : controller.IsAcquiredForRun(skill.Unlocked);
                if (!acquired) continue;

                MonoBehaviour component = (MonoBehaviour)controller;
                if (requireExactSnapshotMapping &&
                    (component == null || !component.isActiveAndEnabled))
                {
                    Debug.LogError(
                        "Unlocked Trait skill '" + controller.Id + "' has an inactive controller.",
                        this);
                    StopAllSkills();
                    return false;
                }
                try
                {
                    if (component.isActiveAndEnabled &&
                        controller.ConfigureForRun(this, _runRequest.RunId,
                            _runRequest.Seed, skill)) continue;
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "Skill '" + controller.Id +
                        "' threw during preparation. " +
                        exception, component);
                }
                if (requireExactSnapshotMapping)
                {
                    Debug.LogError(
                        "Unlocked Trait skill '" + controller.Id +
                        "' could not be prepared; refusing to start a mismatched run.",
                        component);
                    StopAllSkills();
                    return false;
                }
                Debug.LogWarning(
                    "Skill '" + controller.Id +
                    "' could not be prepared; continuing the run without it.",
                    component);
                StopSkillSafely(controller);
            }
            return true;
        }

        /// <summary>
        /// Captures relic definitions and ownership from the run's validated
        /// progression snapshot. Effects and drops are consumed by combat
        /// systems through this read-only view.
        /// </summary>
        public bool ConfigureRelics(
            IReadOnlyList<RelicCombatSnapshot> relics,
            RelicDropRulesCombatSnapshot dropRules)
        {
            if ((!_isPrepared && !_isRunning) || !dropRules.IsValid)
                return false;

            int count = relics == null ? 0 : relics.Count;
            if (count != 0 && count != 20)
                return false;

            var copied = new RelicCombatSnapshot[count];
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                RelicCombatSnapshot relic = relics[i];
                if (!relic.IsValid || !seenIds.Add(relic.RelicId))
                    return false;
                copied[i] = relic;
            }

            _relicSnapshots = copied;
            _relicSnapshotView = Array.AsReadOnly(_relicSnapshots);
            _relicDropRules = dropRules;
            try
            {
                _relicModifiers = RelicCombatModifiers.From(_relicSnapshotView);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError("CombatRunController rejected relic effects: " + exception.Message, this);
                _relicSnapshots = Array.Empty<RelicCombatSnapshot>();
                _relicSnapshotView = Array.AsReadOnly(_relicSnapshots);
                _relicDropRules = default;
                _relicModifiers = default;
                return false;
            }
            return true;
        }

        private bool RefreshSkillControllers()
        {
            _skillControllers.Clear();
            _skillControllersById.Clear();
            _hasInvalidSkillControllerSet = false;
            MonoBehaviour[] components = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (!(components[i] is IRunSkillController controller)) continue;
                if (!SkillStatUpgrade.IsKnownSkill(controller.Id))
                {
                    _hasInvalidSkillControllerSet = true;
                    Debug.LogWarning("Ignoring unknown skill controller: " +
                        controller.Id, components[i]);
                    continue;
                }
                if (_skillControllersById.ContainsKey(controller.Id))
                {
                    _hasInvalidSkillControllerSet = true;
                    Debug.LogWarning("Ignoring duplicate skill controller: " +
                        controller.Id + "; the first controller will be used.",
                        components[i]);
                    continue;
                }
                _skillControllersById.Add(controller.Id, controller);
                _skillControllers.Add(controller);
            }
            return !_hasInvalidSkillControllerSet;
        }

        private void StopAllSkills()
        {
            for (int i = 0; i < _skillControllers.Count; i++)
                StopSkillSafely(_skillControllers[i]);
        }

        private void StopSkillSafely(IRunSkillController controller)
        {
            if (!(controller is MonoBehaviour component) || component == null)
                return;
            try
            {
                controller.StopSkill();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "Skill '" + controller.Id +
                    "' threw while stopping; continuing run cleanup. " +
                    exception, component);
            }
        }

        public bool TryApplySkillDamage(RunId runId, ICombatTarget target, long rawDamage)
        {
            return IsRunning && _elapsedSeconds < _durationSeconds &&
                   runId == _runRequest.RunId && target != null &&
                   target.RunId == runId && target.IsActive && target.IsRegistered &&
                   rawDamage > 0L &&
                   ApplyBundle(target, rawDamage, CombatDamageSource.Skill);
        }

        public bool TryApplyPetDamage(RunId runId, ICombatTarget target, long rawDamage)
        {
            return IsRunning && _elapsedSeconds < _durationSeconds &&
                   runId == _runRequest.RunId && target != null &&
                   target.RunId == runId && target.IsActive && target.IsRegistered &&
                   rawDamage > 0L &&
                   ApplyBundle(target, rawDamage, CombatDamageSource.Skill, true);
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
            StopAllSkills();

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
            StopAllSkills();

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
            CombatDamageSource source = CombatDamageSource.CursorAttack,
            bool isPetDamage = false)
        {
            bool wasCritical = RollCriticalHit();
            bool execute = TryRollLowHealthExecute(target);
            if (!TryGetHitDamage(wasCritical, target, source, isPetDamage,
                    execute, out long hitDamage, skillDamage))
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
            bool applied;
            long effectiveDamage;
            long displayDamage = hitDamage;
            bool killed;
            if (target is MonsterCombatTarget damageTarget)
            {
                applied = damageTarget.ApplyDamage(
                    _runRequest.RunId, hitDamage, execute,
                    out effectiveDamage, out killed, out displayDamage);
            }
            else
            {
                applied = target.ApplyDamage(
                    _runRequest.RunId,
                    hitDamage,
                    out effectiveDamage,
                    out killed);
            }

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
                    Mathf.Max(hitBounds.size.x, hitBounds.size.y), displayDamage));
            }

            if (wasCritical && effectiveDamage > 0L)
            {
                CriticalHit?.Invoke(effectiveDamage);
            }

            if (!killed)
            {
                return true;
            }

            if (target is MonsterCombatTarget monsterTarget &&
                !monsterTarget.TryClaimKillSettlement())
            {
                return true;
            }

            long garnetReward = target.GarnetReward;
            bool normalMonsterKill = _runRequest.Mode == RunMode.NormalField &&
                target is MonsterCombatTarget;
            if (normalMonsterKill && !TryScaleReward(
                    "gem.garnet",
                    garnetReward,
                    _relicModifiers.NormalMonsterRewardMultiplier,
                    out garnetReward))
            {
                RequestAbort(RunEndReason.NumericOverflow);
                return true;
            }

            if (_defeatedCount == int.MaxValue ||
                !TryAddNonNegative(
                    _garnetEarned,
                    garnetReward,
                    out long nextGarnetEarned))
            {
                RequestAbort(RunEndReason.NumericOverflow);
                return true;
            }

            _defeatedCount++;
            _garnetEarned = nextGarnetEarned;
            if (!TryRecordReward("gem.garnet", garnetReward))
            {
                RequestAbort(RunEndReason.NumericOverflow);
                return true;
            }

            if (!TryRecordBonusDrop(
                    target.BonusDropCurrencyId,
                    target.BonusDropAmount,
                    target.BonusDropChancePercent,
                    normalMonsterKill))
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

        private bool TryGetHitDamage(
            bool critical,
            ICombatTarget target,
            CombatDamageSource source,
            bool isPetDamage,
            bool execute,
            out long damage,
            long skillDamage = 0L)
        {
            double scaled = skillDamage > 0 ? skillDamage : playerCombatStatsRuntime.AttackPower;

            if (source == CombatDamageSource.CursorAttack)
            {
                scaled *= _relicModifiers.CursorAttackDamageMultiplier;
            }
            else if (isPetDamage)
            {
                scaled *= _relicModifiers.PetDamageMultiplier;
                float doubleDamageChance = Mathf.Clamp(
                    _relicModifiers.PetDoubleDamageChancePercent, 0f, 100f);
                if (doubleDamageChance > 0f && _random != null &&
                    _random.NextFloat(0f, 100f) < doubleDamageChance)
                {
                    scaled *= 2d;
                }
            }
            else
            {
                scaled *= _relicModifiers.AutoSkillDamageMultiplier;
            }

            if (execute && target is MonsterCombatTarget executableTarget)
            {
                scaled = executableTarget.MaxHealth;
            }
            else if (_runRequest.Mode == RunMode.NormalField &&
                target is MonsterCombatTarget fullHealthTarget &&
                fullHealthTarget.CurrentHealth == fullHealthTarget.MaxHealth)
            {
                scaled *= 1d + _relicModifiers.FullHealthFirstHitDamagePercent / 100d;
            }

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

        private bool TryRollLowHealthExecute(ICombatTarget target)
        {
            if (_runRequest.Mode != RunMode.NormalField ||
                !(target is MonsterCombatTarget monster) ||
                monster.MaxHealth <= 0L ||
                monster.CurrentHealth <= 0L ||
                _relicModifiers.LowHealthExecuteChancePercent <= 0f ||
                _relicModifiers.LowHealthExecuteThresholdPercent <= 0f ||
                _random == null)
            {
                return false;
            }

            double healthPercent = monster.CurrentHealth * 100d / monster.MaxHealth;
            return healthPercent <= _relicModifiers.LowHealthExecuteThresholdPercent &&
                   _random.NextFloat(0f, 100f) < Mathf.Clamp(
                       _relicModifiers.LowHealthExecuteChancePercent, 0f, 100f);
        }

        private bool TryRecordBonusDrop(
            string currencyId,
            long amount,
            float chancePercent,
            bool isNormalMonsterKill)
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

            long resolvedAmount = amount;
            if (isNormalMonsterKill && !TryScaleReward(
                    currencyId,
                    resolvedAmount,
                    _relicModifiers.AllNormalMonsterRewardMultiplier,
                    out resolvedAmount))
            {
                return false;
            }

            if (isNormalMonsterKill && currencyId.StartsWith("gem.", StringComparison.Ordinal) &&
                _relicModifiers.BonusGemDropChancePercent > 0f &&
                _random.NextFloat(0f, 100f) < Mathf.Clamp(
                    _relicModifiers.BonusGemDropChancePercent, 0f, 100f))
            {
                if (resolvedAmount == long.MaxValue) return false;
                resolvedAmount++;
            }

            return TryRecordReward(
                currencyId,
                resolvedAmount);
        }

        private bool TryScaleReward(
            string currencyId,
            long amount,
            float multiplier,
            out long scaledAmount)
        {
            if (amount <= 0L || multiplier <= 1f)
            {
                scaledAmount = amount;
                return true;
            }

            double result = amount * (double)multiplier;
            if (_fractionalRewardRemainders.TryGetValue(currencyId, out double remainder))
                result += remainder;
            if (double.IsNaN(result) || double.IsInfinity(result) ||
                result < 0d || result > long.MaxValue)
            {
                scaledAmount = 0L;
                return false;
            }

            scaledAmount = (long)Math.Floor(result);
            double nextRemainder = result - scaledAmount;
            if (nextRemainder <= 0d)
                _fractionalRewardRemainders.Remove(currencyId);
            else
                _fractionalRewardRemainders[currencyId] = nextRemainder;
            return true;
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
