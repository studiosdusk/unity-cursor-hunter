using System.Collections.Generic;
using CursorHunter.Contracts;

namespace CursorHunter.App
{
    /// <summary>
    /// Runtime guard that prevents one App lifetime from starting the same
    /// logical run twice. Settlement retries deliberately happen below this
    /// boundary with the original RunId.
    /// </summary>
    public sealed class RunIdRegistry
    {
        private readonly HashSet<RunId> _claimedRunIds = new HashSet<RunId>();

        public bool TryClaim(RunId runId)
        {
            return runId.IsValid && _claimedRunIds.Add(runId);
        }

        public bool Contains(RunId runId)
        {
            return _claimedRunIds.Contains(runId);
        }
    }
}
