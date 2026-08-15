using TheWarden.Core.Reputation;
using TheWarden.Core.Scanning;

namespace TheWarden.Core.Tests.Reputation;

public class ReputationSweepServiceTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    public void Dispose() => File.Delete(_tempFile);

    private sealed class FakeReputationClient(Func<string, ReputationVerdict> respond) : IReputationClient
    {
        public List<string> RequestedHashes { get; } = [];

        public Task<ReputationVerdict> LookupAsync(string sha256, CancellationToken cancellationToken = default)
        {
            RequestedHashes.Add(sha256);
            return Task.FromResult(respond(sha256));
        }
    }

    [Fact]
    public async Task SweepAsync_yields_one_finding_per_file_with_the_client_s_verdict()
    {
        File.WriteAllText(_tempFile, "sample content");
        var client = new FakeReputationClient(hash => new ReputationVerdict(hash, ReputationStatus.Clean, 0, 70));
        var service = new ReputationSweepService(client, TimeSpan.Zero);
        var files = new List<FileEntry> { new(_tempFile, DateTime.UtcNow, 42) };

        var findings = new List<MalwareFinding>();
        await foreach (var finding in service.SweepAsync(files))
        {
            findings.Add(finding);
        }

        var found = Assert.Single(findings);
        Assert.Equal(_tempFile, found.FullPath);
        Assert.Equal(ReputationStatus.Clean, found.Verdict.Status);
        Assert.False(found.NeedsAttention);
    }

    [Fact]
    public async Task SweepAsync_marks_malicious_and_suspicious_verdicts_as_needing_attention()
    {
        File.WriteAllText(_tempFile, "sample content");
        var client = new FakeReputationClient(hash => new ReputationVerdict(hash, ReputationStatus.Malicious, 12, 70));
        var service = new ReputationSweepService(client, TimeSpan.Zero);
        var files = new List<FileEntry> { new(_tempFile, DateTime.UtcNow, 42) };

        MalwareFinding? finding = null;
        await foreach (var f in service.SweepAsync(files))
        {
            finding = f;
            break;
        }

        Assert.NotNull(finding);
        Assert.True(finding!.NeedsAttention);
    }

    [Fact]
    public async Task SweepAsync_reports_LookupFailed_for_a_file_that_no_longer_exists_instead_of_throwing()
    {
        var missingPath = _tempFile + ".gone";
        var client = new FakeReputationClient(_ => new ReputationVerdict("unused", ReputationStatus.Clean, 0, 70));
        var service = new ReputationSweepService(client, TimeSpan.Zero);
        var files = new List<FileEntry> { new(missingPath, DateTime.UtcNow, 0) };

        MalwareFinding? finding = null;
        await foreach (var f in service.SweepAsync(files))
        {
            finding = f;
            break;
        }

        Assert.NotNull(finding);
        Assert.Equal(ReputationStatus.LookupFailed, finding!.Verdict.Status);
        Assert.Empty(client.RequestedHashes);
    }

    [Fact]
    public async Task SweepAsync_waits_the_configured_delay_between_checks()
    {
        File.WriteAllText(_tempFile, "sample content");
        var client = new FakeReputationClient(hash => new ReputationVerdict(hash, ReputationStatus.Clean, 0, 70));
        var delay = TimeSpan.FromMilliseconds(150);
        var service = new ReputationSweepService(client, delay);
        var files = new List<FileEntry>
        {
            new(_tempFile, DateTime.UtcNow, 1),
            new(_tempFile, DateTime.UtcNow, 1),
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await foreach (var _ in service.SweepAsync(files))
        {
        }
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed >= delay, $"Expected at least {delay}, took {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task SweepAsync_does_not_wait_after_the_last_file()
    {
        File.WriteAllText(_tempFile, "sample content");
        var client = new FakeReputationClient(hash => new ReputationVerdict(hash, ReputationStatus.Clean, 0, 70));
        var service = new ReputationSweepService(client, TimeSpan.FromSeconds(30));
        var files = new List<FileEntry> { new(_tempFile, DateTime.UtcNow, 1) };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await foreach (var _ in service.SweepAsync(files))
        {
        }
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Should not have waited after the last file, took {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task SweepAsync_stops_when_cancelled()
    {
        File.WriteAllText(_tempFile, "sample content");
        var client = new FakeReputationClient(hash => new ReputationVerdict(hash, ReputationStatus.Clean, 0, 70));
        var service = new ReputationSweepService(client, TimeSpan.FromSeconds(30));
        var files = new List<FileEntry>
        {
            new(_tempFile, DateTime.UtcNow, 1),
            new(_tempFile, DateTime.UtcNow, 1),
        };

        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in service.SweepAsync(files, cts.Token))
            {
                cts.Cancel();
            }
        });
    }
}
