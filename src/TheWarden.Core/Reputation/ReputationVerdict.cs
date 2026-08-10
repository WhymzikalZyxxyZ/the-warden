namespace TheWarden.Core.Reputation;

public enum ReputationStatus
{
    /// <summary>No API key configured, or the lookup wasn't attempted. Never treated as "safe" — just "unchecked".</summary>
    NotChecked,
    Clean,
    Suspicious,
    Malicious,
    LookupFailed,
}

/// <summary>
/// A reputation opinion for a single file hash. This is presented to the user as a
/// flag for their own judgment (see ADR-0002) — the app never auto-deletes based on
/// this verdict alone.
/// </summary>
public sealed record ReputationVerdict(
    string Sha256,
    ReputationStatus Status,
    int MaliciousEngineCount,
    int TotalEngineCount)
{
    public static ReputationVerdict NotChecked(string sha256) => new(sha256, ReputationStatus.NotChecked, 0, 0);
}
