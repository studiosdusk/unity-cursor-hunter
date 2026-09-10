namespace CursorHunter.Contracts
{
    /// <summary>
    /// Minimal request used by the prototype normal-field run.
    /// </summary>
    public readonly struct RunRequest
    {
        public RunRequest(string runId, float durationSeconds)
        {
            RunId = runId ?? string.Empty;
            DurationSeconds = durationSeconds > 0f ? durationSeconds : 1f;
        }

        public string RunId { get; }
        public float DurationSeconds { get; }
    }
}
