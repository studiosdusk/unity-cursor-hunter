using System;
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

        public bool Start(
            RunRequest request,
            CombatSnapshot combatSnapshot,
            SpawnSnapshot spawnSnapshot,
            out string failureReason)
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

            if (!session.Start(request))
            {
                return FailStart(
                    session,
                    "RunSession rejected the start request.",
                    false,
                    out failureReason);
            }

            bool combatStarted = false;
            try
            {
                if (!_combatRunController.StartRun(request, combatSnapshot))
                {
                    return FailStart(
                        session,
                        "CombatRunController rejected the start request.",
                        false,
                        out failureReason);
                }

                combatStarted = true;

                if (!_monsterSpawner.StartRun(request, spawnSnapshot))
                {
                    return FailStart(
                        session,
                        "MonsterSpawner could not start the initial spawn.",
                        combatStarted,
                        out failureReason);
                }

                if (!session.CommitStart())
                {
                    return FailStart(
                        session,
                        "RunSession could not commit the start.",
                        combatStarted,
                        out failureReason);
                }

                return true;
            }
            catch (Exception exception)
            {
                return FailStart(
                    session,
                    $"Run start threw {exception.GetType().Name}: {exception.Message}",
                    combatStarted,
                    out failureReason);
            }
        }

        public bool Complete(
            RunEndReason endReason,
            RunSettlementPolicy settlementPolicy)
        {
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

            if (!_session.Complete(endReason, settlementPolicy))
            {
                return false;
            }

            _monsterSpawner.StopRun();
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

            if (!_session.Abort(endReason, settlementPolicy))
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
            bool combatStarted,
            out string failureReason)
        {
            failureReason = reason;

            _monsterSpawner.StopRun();

            if (combatStarted)
            {
                _combatRunController.TryAbortRun(
                    RunEndReason.StartFailed,
                    RunSettlementPolicy.Discard,
                    out _);
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
