using System;
using CursorHunter.Contracts;

namespace CursorHunter.App
{
    public enum RunState
    {
        Idle = 0,
        Starting = 1,
        Running = 2,
        Paused = 3,
        Completing = 4,
        Completed = 5,
        Aborting = 6,
        Aborted = 7
    }

    /// <summary>
    /// Pure lifecycle state for one logical run. It deliberately has no
    /// Unity, Combat, Data, or save dependencies.
    /// </summary>
    public sealed class RunSession
    {
        private RunRequest _request;

        public RunState State { get; private set; } = RunState.Idle;

        public RunRequest Request => _request;

        public RunEndReason EndReason { get; private set; } = RunEndReason.Unknown;

        public RunSettlementPolicy SettlementPolicy { get; private set; } =
            RunSettlementPolicy.Discard;

        public bool IsActive =>
            State == RunState.Starting ||
            State == RunState.Running ||
            State == RunState.Paused;

        public bool IsRunning => State == RunState.Running;

        public event Action Started;
        public event Action Paused;
        public event Action Resumed;
        public event Action Completed;
        public event Action Aborted;

        /// <summary>
        /// Reserves the session identity and enters the preparation phase.
        /// CommitStart must be called only after all runtime participants are
        /// prepared successfully.
        /// </summary>
        public bool Start(RunRequest request)
        {
            if (State != RunState.Idle || !request.IsValid)
            {
                return false;
            }

            _request = request;
            EndReason = RunEndReason.Unknown;
            SettlementPolicy = RunSettlementPolicy.Discard;
            State = RunState.Starting;
            return true;
        }

        /// <summary>
        /// Commits the preparation phase. This is intentionally separate from
        /// Start so a failed Combat or Spawner setup cannot look like a live
        /// run.
        /// </summary>
        public bool CommitStart()
        {
            if (State != RunState.Starting)
            {
                return false;
            }

            State = RunState.Running;
            Started?.Invoke();
            return true;
        }

        public bool Pause()
        {
            if (State != RunState.Running)
            {
                return false;
            }

            State = RunState.Paused;
            Paused?.Invoke();
            return true;
        }

        public bool Resume()
        {
            if (State != RunState.Paused)
            {
                return false;
            }

            State = RunState.Running;
            Resumed?.Invoke();
            return true;
        }

        /// <summary>
        /// Completes the run exactly once. Completing and aborting are
        /// mutually exclusive terminal operations.
        /// </summary>
        public bool Complete(
            RunEndReason endReason,
            RunSettlementPolicy settlementPolicy)
        {
            if (State != RunState.Running && State != RunState.Paused)
            {
                return false;
            }

            State = RunState.Completing;
            EndReason = endReason;
            SettlementPolicy = settlementPolicy;
            State = RunState.Completed;
            Completed?.Invoke();
            return true;
        }

        /// <summary>
        /// Aborts the run exactly once. A reset or start failure can abort
        /// during Starting; user exits can abort a running or paused session.
        /// </summary>
        public bool Abort(
            RunEndReason endReason,
            RunSettlementPolicy settlementPolicy)
        {
            if (!IsActive)
            {
                return false;
            }

            State = RunState.Aborting;
            EndReason = endReason;
            SettlementPolicy = settlementPolicy;
            State = RunState.Aborted;
            Aborted?.Invoke();
            return true;
        }
    }
}
