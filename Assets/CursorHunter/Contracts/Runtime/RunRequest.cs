using System;
using System.Collections.Generic;

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
            float durationSeconds,
            int bossStage = 0,
            ResourceRewardSnapshot[] bossClearRewards = null)
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
            BossStage = mode == RunMode.Boss ? bossStage : 0;
            if (bossClearRewards != null && bossClearRewards.Length > 0)
            {
                if (mode != RunMode.Boss) throw new ArgumentException("Only boss runs have clear rewards.", nameof(bossClearRewards));
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (ResourceRewardSnapshot reward in bossClearRewards)
                    if (!reward.IsValid || !ids.Add(reward.CurrencyId))
                        throw new ArgumentException("Boss clear rewards require unique IDs and positive amounts.", nameof(bossClearRewards));
                _bossClearRewards = Array.AsReadOnly((ResourceRewardSnapshot[])bossClearRewards.Clone());
            }
            else _bossClearRewards = null;
            Seed = seed;
            DurationSeconds = mode == RunMode.NormalField ? System.Math.Min(30f, durationSeconds) : durationSeconds;
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
        public int BossStage { get; }
        private readonly IReadOnlyList<ResourceRewardSnapshot> _bossClearRewards;
        public IReadOnlyList<ResourceRewardSnapshot> BossClearRewards =>
            _bossClearRewards ?? Array.Empty<ResourceRewardSnapshot>();
        public ulong Seed { get; }
        public float DurationSeconds { get; }

        public bool IsValid =>
            RunId.IsValid &&
            SchemaVersion > 0 &&
            BalanceVersion > 0 &&
            DurationSeconds > 0f &&
            (Mode != RunMode.NormalField || DurationSeconds <= 30f) &&
            !float.IsNaN(DurationSeconds) &&
            !float.IsInfinity(DurationSeconds) &&
            (Mode != RunMode.Boss || !string.IsNullOrEmpty(BossId));
    }
}
