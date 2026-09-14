namespace CursorHunter.Contracts
{
    /// <summary>
    /// Immutable input captured before one run starts. The request is the
    /// identity and balance-version boundary for Combat.
    /// </summary>
    public readonly struct RunRequest
    {
        public RunRequest(
            RunId runId,
            int schemaVersion,
            int balanceVersion,
            RunMode mode,
            string bossId,
            ulong seed,
            float durationSeconds)
        {
            if (!runId.IsValid)
            {
                throw new System.ArgumentException("RunRequest requires a valid RunId.", nameof(runId));
            }

            if (schemaVersion <= 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(schemaVersion));
            }

            if (balanceVersion <= 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(balanceVersion));
            }

            if (mode == RunMode.Boss && string.IsNullOrWhiteSpace(bossId))
            {
                throw new System.ArgumentException(
                    "Boss runs require a bossId.",
                    nameof(bossId));
            }

            if (durationSeconds <= 0f ||
                float.IsNaN(durationSeconds) ||
                float.IsInfinity(durationSeconds))
            {
                throw new System.ArgumentOutOfRangeException(nameof(durationSeconds));
            }

            RunId = runId;
            SchemaVersion = schemaVersion;
            BalanceVersion = balanceVersion;
            Mode = mode;
            BossId = bossId ?? string.Empty;
            Seed = seed;
            DurationSeconds = durationSeconds;
        }

        /// <summary>
        /// Compatibility constructor for the old prototype call site. New
        /// production code must use the versioned constructor above.
        /// </summary>
        public RunRequest(string runId, float durationSeconds)
            : this(
                new RunId(runId),
                1,
                1,
                RunMode.NormalField,
                string.Empty,
                0UL,
                durationSeconds)
        {
        }

        public RunId RunId { get; }
        public int SchemaVersion { get; }
        public int BalanceVersion { get; }
        public RunMode Mode { get; }
        public string BossId { get; }
        public ulong Seed { get; }
        public float DurationSeconds { get; }

        public bool IsValid =>
            RunId.IsValid &&
            SchemaVersion > 0 &&
            BalanceVersion > 0 &&
            DurationSeconds > 0f &&
            !float.IsNaN(DurationSeconds) &&
            !float.IsInfinity(DurationSeconds) &&
            (Mode != RunMode.Boss || !string.IsNullOrEmpty(BossId));
    }
}
