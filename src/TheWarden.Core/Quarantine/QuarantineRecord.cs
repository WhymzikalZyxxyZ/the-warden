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

    /// <summary>
    /// Carried over from the originating FileFinding at quarantine time so a later
    /// Restore failure can tell the user "relaunch as Administrator" is actually
    /// worth trying, instead of guessing from the restore failure alone. Defaults
    /// false for manifests written before this field existed.
    /// </summary>
    public bool RequiresElevation { get; init; }
}
