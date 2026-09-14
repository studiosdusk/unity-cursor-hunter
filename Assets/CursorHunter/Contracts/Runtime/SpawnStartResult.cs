namespace CursorHunter.Contracts
{
    /// <summary>
    /// Explicit outcome of preparing the first spawn pack for a run.
    /// </summary>
    public enum SpawnStartStatus
    {
        Started = 0,
        InvalidRequest = 1,
        MissingPrefab = 2,
        MissingDefinition = 3,
        NoSpawnPosition = 4,
        InitialSpawnFailed = 5,
        AlreadyRunning = 6
    }

    public readonly struct SpawnStartResult
    {
        public SpawnStartResult(
            SpawnStartStatus status,
            string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public SpawnStartStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == SpawnStartStatus.Started;
    }
}
