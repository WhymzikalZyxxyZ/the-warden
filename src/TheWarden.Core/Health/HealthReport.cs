namespace TheWarden.Core.Health;

public sealed record HealthReport(
    IReadOnlyList<DiskUsage> Disks,
    int StartupItemCount,
    int JunkFileCount,
    long JunkBytesReclaimable,
    DateTime GeneratedAtUtc);
