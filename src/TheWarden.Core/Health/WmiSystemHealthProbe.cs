using System.Diagnostics.CodeAnalysis;
using System.Management;

namespace TheWarden.Core.Health;

/// <summary>
/// Real WMI-backed implementation. Every query is defensive: WMI can be unavailable
/// or access-restricted in locked-down or containerized environments, and a health
/// dashboard failing to render is a much worse outcome than a health dashboard
/// reporting fewer numbers, so failures degrade to empty results rather than throwing.
/// Excluded from coverage: this is a thin, real-WMI integration shim exercised via
/// <see cref="ISystemHealthProbe"/> fakes in <c>HealthReportServiceTests</c> instead.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class WmiSystemHealthProbe : ISystemHealthProbe
{
    public IReadOnlyList<DiskUsage> GetDiskUsage()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType = 3");

            var results = new List<DiskUsage>();
            foreach (var disk in searcher.Get())
            {
                var deviceId = disk["DeviceID"]?.ToString() ?? "?";
                var total = Convert.ToInt64(disk["Size"] ?? 0L);
                var free = Convert.ToInt64(disk["FreeSpace"] ?? 0L);
                results.Add(new DiskUsage(deviceId, total, free));
            }

            return results;
        }
        catch (ManagementException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    public int GetStartupItemCount()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Command FROM Win32_StartupCommand");
            return searcher.Get().Count;
        }
        catch (ManagementException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
