using TheWarden.Core.Rules;

namespace TheWarden.Core.Quarantine;

/// <summary>
/// A single quarantined file. Nothing about this record is ever silently deleted —
/// <see cref="QuarantineManager.PurgeOlderThan"/> is the only path that permanently
/// removes a quarantined file, and it requires an explicit typed confirmation.
/// </summary>
public sealed record QuarantineRecord(
    Guid Id,
    string OriginalPath,
    string QuarantinedPath,
    JunkCategory Category,
    string Reason,
    long SizeBytes,
    DateTime QuarantinedAtUtc)
{
    public bool Restored { get; init; }
    public DateTime? RestoredAtUtc { get; init; }
}
