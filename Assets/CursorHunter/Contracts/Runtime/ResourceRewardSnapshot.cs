using System;

namespace CursorHunter.Contracts
{
    /// <summary>
    /// One currency payout recorded by a run. Currency IDs are data contracts
    /// such as <c>gem.garnet</c> or <c>gem.topaz</c>; settlement decides when
    /// the immutable result is committed to the profile.
    /// </summary>
    public readonly struct ResourceRewardSnapshot
    {
        public ResourceRewardSnapshot(string currencyId, long amount)
        {
            CurrencyId = currencyId ?? string.Empty;
            Amount = amount > 0L ? amount : 0L;
        }

        public string CurrencyId { get; }
        public long Amount { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(CurrencyId) && Amount > 0L;
    }

    /// <summary>
    /// Optional bonus drop attached to one monster spawn. It is copied into
    /// the run snapshot so Combat never reads a live progression asset.
    /// </summary>
    public readonly struct BonusDropSnapshot
    {
        public BonusDropSnapshot(
            string currencyId,
            long amount,
            float chancePercent)
        {
            CurrencyId = currencyId ?? string.Empty;
            Amount = amount > 0L ? amount : 0L;
            ChancePercent = SanitizePercent(chancePercent);
        }

        public string CurrencyId { get; }
        public long Amount { get; }
        public float ChancePercent { get; }

        public bool IsValid =>
            Amount == 0L ||
            (!string.IsNullOrWhiteSpace(CurrencyId) && ChancePercent > 0f);

        private static float SanitizePercent(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return value < 0f ? 0f : value > 100f ? 100f : value;
        }
    }
}
