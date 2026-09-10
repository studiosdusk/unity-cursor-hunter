namespace CursorHunter.Contracts
{
    /// <summary>
    /// Result of a completed prototype combat run.
    /// </summary>
    public readonly struct RunResult
    {
        public RunResult(
            string runId,
            string endReason,
            float elapsedSeconds,
            int defeatedCount,
            long garnetEarned,
            long effectiveDamage)
        {
            RunId = runId ?? string.Empty;
            EndReason = endReason ?? string.Empty;
            ElapsedSeconds = elapsedSeconds >= 0f ? elapsedSeconds : 0f;
            DefeatedCount = defeatedCount >= 0 ? defeatedCount : 0;
            GarnetEarned = garnetEarned >= 0 ? garnetEarned : 0;
            EffectiveDamage = effectiveDamage >= 0 ? effectiveDamage : 0;
        }

        public string RunId { get; }
        public string EndReason { get; }
        public float ElapsedSeconds { get; }
        public int DefeatedCount { get; }
        public long GarnetEarned { get; }
        public long EffectiveDamage { get; }
    }
}
