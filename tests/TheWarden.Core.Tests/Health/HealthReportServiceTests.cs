using TheWarden.Core.Health;
using TheWarden.Core.Rules;
using TheWarden.Core.Scanning;

namespace TheWarden.Core.Tests.Health;

public class HealthReportServiceTests
{
    private sealed class FakeClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakeHealthProbe(IReadOnlyList<DiskUsage> disks, int startupItemCount) : ISystemHealthProbe
    {
        public IReadOnlyList<DiskUsage> GetDiskUsage() => disks;

        public int GetStartupItemCount() => startupItemCount;
    }

    private static FileFinding Finding(long sizeBytes) =>
        new(@"C:\Temp\a.tmp", JunkCategory.UserTemp, "user-temp", "test", sizeBytes, DateTime.UtcNow, RequiresElevation: false);

    [Fact]
    public void Generate_combines_probe_data_with_scan_findings()
    {
        var disks = new List<DiskUsage> { new("C:", 500_000_000_000, 100_000_000_000) };
        var probe = new FakeHealthProbe(disks, startupItemCount: 12);
        var utcNow = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
        var service = new HealthReportService(probe, new FakeClock(utcNow));

        var findings = new List<FileFinding> { Finding(1024), Finding(2048) };

        var report = service.Generate(findings);

        Assert.Equal(disks, report.Disks);
        Assert.Equal(12, report.StartupItemCount);
        Assert.Equal(2, report.JunkFileCount);
        Assert.Equal(3072, report.JunkBytesReclaimable);
        Assert.Equal(utcNow, report.GeneratedAtUtc);
    }

    [Fact]
    public void Generate_handles_no_findings()
    {
        var probe = new FakeHealthProbe([], startupItemCount: 0);
        var service = new HealthReportService(probe);

        var report = service.Generate([]);

        Assert.Equal(0, report.JunkFileCount);
        Assert.Equal(0, report.JunkBytesReclaimable);
    }
}
