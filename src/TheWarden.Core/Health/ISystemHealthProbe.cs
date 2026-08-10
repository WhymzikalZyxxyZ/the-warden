namespace TheWarden.Core.Health;

/// <summary>
/// Abstracts the WMI-backed system queries behind the "PC health" dashboard, so the
/// reporting logic in <see cref="HealthReportService"/> is testable without a real
/// WMI provider (which may be unavailable or restricted in some CI/sandbox contexts).
/// </summary>
public interface ISystemHealthProbe
{
    IReadOnlyList<DiskUsage> GetDiskUsage();

    int GetStartupItemCount();
}
