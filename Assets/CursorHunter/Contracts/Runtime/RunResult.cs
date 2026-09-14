namespace CursorHunter.Contracts
{
    /// <summary>
    /// Immutable facts produced when a run reaches a terminal state. It does
    /// not perform settlement or mutate a profile.
    /// </summary>
    public readonly struct RunResult
    {
        public RunResult(
            RunRequest request,
            RunEndReason endReason,
            RunSettlementPolicy settlementPolicy,
            float elapsedSeconds,
            int defeatedCount,
            long garnetEarned,
            long effectiveDamage)
        {
            if (!request.IsValid)
            {
                throw new System.ArgumentException(
                    "RunResult requires a valid RunRequest.",
                    nameof(request));
            }

            if (elapsedSeconds < 0f ||
                float.IsNaN(elapsedSeconds) ||
                float.IsInfinity(elapsedSeconds))
            {
                throw new System.ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            if (defeatedCount < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(defeatedCount));
            }

            if (garnetEarned < 0L)
            {
                throw new System.ArgumentOutOfRangeException(nameof(garnetEarned));
            }

            if (effectiveDamage < 0L)
            {
                throw new System.ArgumentOutOfRangeException(nameof(effectiveDamage));
            }

            RunId = request.RunId;
            SchemaVersion = request.SchemaVersion;
            BalanceVersion = request.BalanceVersion;
            Mode = request.Mode;
            EndReason = endReason;
            SettlementPolicy = settlementPolicy;
            ElapsedSeconds = elapsedSeconds;
            DefeatedCount = defeatedCount;
            GarnetEarned = garnetEarned;
            EffectiveDamage = effectiveDamage;
        }

        public RunId RunId { get; }
        public int SchemaVersion { get; }
        public int BalanceVersion { get; }
        public RunMode Mode { get; }
        public RunEndReason EndReason { get; }
        public RunSettlementPolicy SettlementPolicy { get; }
        public float ElapsedSeconds { get; }
        public int DefeatedCount { get; }
        public long GarnetEarned { get; }
        public long EffectiveDamage { get; }
    }
}
