namespace CursorHunter.Contracts
{
    public enum RunMode
    {
        NormalField = 0,
        Boss = 1
    }

    public enum RunEndReason
    {
        Unknown = 0,
        TimeExpired = 1,
        UserExit = 2,
        Reset = 3,
        StartFailed = 4,
        NumericOverflow = 5,
        BossDefeated = 6,
        ManualComplete = 7
    }

    public enum RunSettlementPolicy
    {
        Eligible = 0,
        Discard = 1
    }
}
