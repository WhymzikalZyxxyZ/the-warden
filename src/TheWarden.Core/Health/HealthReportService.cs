using TheWarden.Core.Scanning;

namespace TheWarden.Core.Health;

/// <summary>
/// Turns raw disk/startup telemetry plus the latest scan findings into the "PC health"
/// summary shown on the dashboard — the framing that makes this a diagnostics tool
/// rather than just a delete button.
/// </summary>
public sealed class HealthReportService(ISystemHealthProbe probe, IClock clock)
{
    private readonly ISystemHealthProbe _probe = probe;
    private readonly IClock _clock = clock;

    public HealthReportService(ISystemHealthProbe probe) : this(probe, new SystemClock())
    {
    }

    public HealthReport Generate(IReadOnlyList<FileFinding> findings)
    {
        var disks = _probe.GetDiskUsage();
        var startupItemCount = _probe.GetStartupItemCount();

        return new HealthReport(
            disks,
            startupItemCount,
            findings.Count,
            findings.Sum(f => f.SizeBytes),
            _clock.UtcNow);
    }
}
