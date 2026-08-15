namespace TheWarden.Core.Scanning;

public enum ScanOutcome
{
    Completed,
    Cancelled,
    Error,
}

/// <summary>A record of one completed, cancelled, or failed malware scan — what was checked, when, and what came of it.</summary>
public sealed record ScanHistoryEntry(
    Guid Id,
    DateTime TimestampUtc,
    MalwareScanScope Scope,
    int FilesFound,
    int FilesChecked,
    int FilesFlagged,
    ScanOutcome Outcome);
