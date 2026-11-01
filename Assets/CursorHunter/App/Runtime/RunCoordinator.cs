using System;
using System.Collections.Generic;
using CursorHunter.Combat;
using CursorHunter.Contracts;

namespace CursorHunter.App
{
    /// <summary>
    /// App-side composition boundary for one Combat/Spawner run. It is the
    /// only owner of the cross-module lifecycle; Combat only reports terminal
    /// requests and provides immutable facts.
    /// </summary>
    public sealed class RunCoordinator : IDisposable
    {
        private readonly CombatRunController _combatRunController;
        private readonly MonsterSpawner _monsterSpawner;
        private readonly RunIdRegistry _runIdRegistry = new RunIdRegistry();
        private RunSession _session;
        private bool _disposed;
        private bool _discardRewards;

        public RunCoordinator(
            CombatRunController combatRunController,
            MonsterSpawner monsterSpawner)
        {
            _combatRunController = combatRunController ??
                throw new ArgumentNullException(nameof(combatRunController));
            _monsterSpawner = monsterSpawner ??
                throw new ArgumentNullException(nameof(monsterSpawner));

            _combatRunController.CompletionRequested +=
                HandleCombatCompletionRequested;
            _combatRunController.AbortRequested += HandleCombatAbortRequested;
        }

        public event Action<RunResult> Completed;
        public event Action<RunResult> Aborted;

        public RunState State => _session != null
            ? _session.State
            : RunState.Idle;

        public bool IsActive => _session != null && _session.IsActive;

        public RunSession Session => _session;

        public bool MarkAsBalanceTest()
        {
            if (!IsActive) return false;
            _discardRewards = true;
            return true;
        }

        public bool StartBalanceTest(RunRequest request, CombatSnapshot combatSnapshot,
            SpawnPlan spawnPlan, IReadOnlyList<string> enabledMonsterIds,
            out string failureReason)
        {
            return StartInternal(request, combatSnapshot, null, null, null,
                default, spawnPlan, out failureReason, enabledMonsterIds, true);
        }

        public bool StartBalanceTest(RunRequest request, ProgressionCombatSnapshot progression,
            SpawnPlan spawnPlan, IReadOnlyList<string> enabledMonsterIds, out string failureReason)
        {
            if (progression == null || !progression.IsValid)
            {
                failureReason = "유효한 플레이어 특성 스냅샷이 필요합니다.";
                return false;
            }
            return StartInternal(request, progression.Combat, null, progression.Skills,
                progression.Relics, progression.RelicDropRules, spawnPlan, out failureReason,
                enabledMonsterIds, discardRewards: true, requireExactSkillMapping: true);
        }

        public bool Start(
            RunRequest request,
            CombatSnapshot combatSnapshot,
            SpawnPlan spawnPlan,
            out string failureReason)
        {
            return StartInternal(
                request,
                combatSnapshot,
                null,
                null,
                null,
                default,
                spawnPlan,
                out failureReason);
        }

        public bool Start(
            RunRequest request,
            CombatSnapshot combatSnapshot,
            IReadOnlyList<SkillCombatSnapshot> skills,
            SpawnPlan spawnPlan,
            out string failureReason)
        {
            return StartInternal(request, combatSnapshot, null, skills,
                null, default, spawnPlan, out failureReason);
        }

        public bool Start(
            RunRequest request,
            CombatSnapshot combatSnapshot,
            CursorCombatStatBonusesSnapshot cursorStatBonuses,
            SpawnPlan spawnPlan,
            out string failureReason)
        {
            return StartInternal(
                request,
                combatSnapshot,
                cursorStatBonuses,
                null,
                null,
                default,
                spawnPlan,
                out failureReason);
        }

        public bool Start(
            RunRequest request,
            CombatSnapshot combatSnapshot,
            CursorCombatStatBonusesSnapshot cursorStatBonuses,
            IReadOnlyList<SkillCombatSnapshot> skills,
            SpawnPlan spawnPlan,
            out string failureReason)
        {
            return StartInternal(request, combatSnapshot, cursorStatBonuses,
                skills, null, default, spawnPlan, out failureReason);
        }

        public bool Start(
            RunRequest request,
            CombatSnapshot combatSnapshot,
            CursorCombatStatBonusesSnapshot cursorStatBonuses,
            IReadOnlyList<SkillCombatSnapshot> skills,
            IReadOnlyList<RelicCombatSnapshot> relics,
            RelicDropRulesCombatSnapshot relicDropRules,
            SpawnPlan spawnPlan,
            out string failureReason)
        {
            return StartInternal(request, combatSnapshot, cursorStatBonuses,
                skills, relics, relicDropRules, spawnPlan, out failureReason);
        }

        /// <summary>
        /// Starts from a fully resolved progression snapshot. Combat stats
        /// already include the purchased trait deltas, so App must pass that
        /// value directly instead of applying a second, parallel bonus path.
        /// </summary>
        public bool Start(
            RunRequest request,
            CombatSnapshot combatSnapshot,
            IReadOnlyList<SkillCombatSnapshot> skills,
            IReadOnlyList<RelicCombatSnapshot> relics,
            RelicDropRulesCombatSnapshot relicDropRules,
            SpawnPlan spawnPlan,
            out string failureReason)
        {
            return StartInternal(request, combatSnapshot, null, skills,
                relics, relicDropRules, spawnPlan, out failureReason,
                requireExactSkillMapping: true);
        }

        private bool StartInternal(
            RunRequest request,
            CombatSnapshot combatSnapshot,
            CursorCombatStatBonusesSnapshot? cursorStatBonuses,
            IReadOnlyList<SkillCombatSnapshot> skills,
            IReadOnlyList<RelicCombatSnapshot> relics,
            RelicDropRulesCombatSnapshot relicDropRules,
            SpawnPlan spawnPlan,
            out string failureReason,
            IReadOnlyList<string> enabledMonsterIds = null,
            bool discardRewards = false,
            bool requireExactSkillMapping = false)
        {
            failureReason = string.Empty;

            if (_disposed)
            {
                failureReason = "RunCoordinator has been disposed.";
                return false;
            }

            if (IsActive)
            {
                failureReason = "A run is already active.";
                return false;
            }

            if (!request.IsValid)
            {
                failureReason = "RunRequest is invalid.";
                return false;
            }

            if (!_runIdRegistry.TryClaim(request.RunId))
            {
                failureReason =
                    $"RunId '{request.RunId}' has already been used. " +
                    "Create a new run for a new combat attempt.";
                return false;
            }

            RunSession session = new RunSession();
            _session = session;
            _discardRewards = discardRewards;

            if (!session.Start(request))
            {
                return FailStart(
                    session,
                    "RunSession rejected the start request.",
                    false,
                    false,
                    false,
                    out failureReason);
            }

            bool combatPrepared = false;
            bool combatStarted = false;
            try
            {
                RelicCombatModifiers relicModifiers =
                    RelicCombatModifiers.From(relics);
                combatSnapshot = relicModifiers.ApplyTo(combatSnapshot);
                bool prepared = cursorStatBonuses.HasValue
                    ? _combatRunController.PrepareRun(
                        request,
                        combatSnapshot,
                        cursorStatBonuses.Value)
                    : _combatRunController.PrepareRun(request, combatSnapshot);
                if (!prepared)
                {
                    return FailStart(
                        session,
                        "CombatRunController rejected run preparation.",
                        false,
                        false,
                        false,
                        out failureReason);
                }

                combatPrepared = true;

                if (!_combatRunController.ConfigureRelics(relics, relicDropRules))
                {
                    return FailStart(session,
                        "CombatRunController rejected relic preparation.",
                        true, false, false, out failureReason);
                }

                // Skill controllers capture the run's relic modifiers while
                // preparing their runtime stats, so relics must be configured
                // before they are enabled.
                if (!_combatRunController.ConfigureSkills(skills,
                    requireExactSkillMapping))
                {
                    return FailStart(session,
                        "CombatRunController rejected skill preparation.",
                        true, false, false, out failureReason);
                }

                SpawnStartResult spawnStartResult =
                    _monsterSpawner.StartRun(request, spawnPlan, enabledMonsterIds);
                if (!spawnStartResult.Succeeded)
                {
                    return FailStart(
                        session,
                        spawnStartResult.Message,
                        combatPrepared,
                        false,
                        spawnStartResult.Status !=
                        SpawnStartStatus.AlreadyRunning,
                        out failureReason);
                }

                if (!_combatRunController.CommitStart())
                {
                    return FailStart(
                        session,
                        "CombatRunController could not commit the start.",
                        combatPrepared,
                        false,
                        true,
                        out failureReason);
                }

                combatPrepared = false;
                combatStarted = true;

                if (!session.CommitStart())
                {
                    return FailStart(
                        session,
                        "RunSession could not commit the start.",
                        combatPrepared,
                        combatStarted,
                        true,
                        out failureReason);
                }

                return true;
            }
            catch (Exception exception)
            {
                return FailStart(
                    session,
                    $"Run start threw {exception.GetType().Name}: {exception.Message}",
                    combatPrepared,
                    combatStarted,
                    combatPrepared || combatStarted,
                    out failureReason);
            }
        }

        public bool Complete(
            RunEndReason endReason,
            RunSettlementPolicy settlementPolicy)
        {
            if (_discardRewards) settlementPolicy = RunSettlementPolicy.Discard;
            if (_disposed || _session == null || !_session.IsActive)
            {
                return false;
            }

            if (!_combatRunController.TryCompleteRun(
                    endReason,
                    settlementPolicy,
                    out RunResult result))
            {
                return false;
            }

            if (!_session.Complete(endReason, result.SettlementPolicy))
            {
                return false;
            }

            _monsterSpawner.StopRun(finishBossDeathAnimation: result.BossDefeated);
            Completed?.Invoke(result);
            return true;
        }

        public bool Pause()
        {
            if (_disposed || _session == null || !_session.IsRunning)
            {
                return false;
            }

            if (!_combatRunController.PauseRun())
            {
                return false;
            }

            if (!_session.Pause())
            {
                _combatRunController.ResumeRun();
                return false;
            }

            return true;
        }

        public bool Resume()
        {
            if (_disposed || _session == null || _session.State != RunState.Paused)
            {
                return false;
            }

            if (!_combatRunController.ResumeRun())
            {
                return false;
            }

            if (!_session.Resume())
            {
                _combatRunController.PauseRun();
                return false;
            }

            return true;
        }

        public bool Abort(
            RunEndReason endReason,
            RunSettlementPolicy settlementPolicy)
        {
            if (_discardRewards) settlementPolicy = RunSettlementPolicy.Discard;
            if (_disposed || _session == null || !_session.IsActive)
            {
                return false;
            }

            if (!_combatRunController.TryAbortRun(
                    endReason,
                    settlementPolicy,
                    out RunResult result))
            {
                return false;
            }

            if (!_session.Abort(endReason, result.SettlementPolicy))
            {
                return false;
            }

            _monsterSpawner.StopRun();
            Aborted?.Invoke(result);
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _combatRunController.CompletionRequested -=
                HandleCombatCompletionRequested;
            _combatRunController.AbortRequested -= HandleCombatAbortRequested;
            _disposed = true;
        }

        private bool FailStart(
            RunSession session,
            string reason,
            bool combatPrepared,
            bool combatStarted,
            bool cleanupSpawner,
            out string failureReason)
        {
            failureReason = reason;

            if (cleanupSpawner)
            {
                _monsterSpawner.StopRun();
            }

            if (combatStarted)
            {
                _combatRunController.TryAbortRun(
                    RunEndReason.StartFailed,
                    RunSettlementPolicy.Discard,
                    out _);
            }
            else if (combatPrepared)
            {
                _combatRunController.CancelPreparedRun();
            }

            session.Abort(
                RunEndReason.StartFailed,
                RunSettlementPolicy.Discard);
            return false;
        }

        private void HandleCombatCompletionRequested(RunEndReason endReason)
        {
            Complete(endReason, RunSettlementPolicy.Eligible);
        }

        private void HandleCombatAbortRequested(RunEndReason endReason)
        {
            Abort(endReason, RunSettlementPolicy.Discard);
        }
    }
}
