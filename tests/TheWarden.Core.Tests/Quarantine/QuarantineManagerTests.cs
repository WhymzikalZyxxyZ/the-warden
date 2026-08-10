using TheWarden.Core.Quarantine;
using TheWarden.Core.Rules;
using TheWarden.Core.Scanning;

namespace TheWarden.Core.Tests.Quarantine;

public class QuarantineManagerTests : IDisposable
{
    private readonly string _sourceDir;
    private readonly string _quarantineDir;

    public QuarantineManagerTests()
    {
        var root = Directory.CreateTempSubdirectory("warden-tests-");
        _sourceDir = Directory.CreateDirectory(Path.Combine(root.FullName, "source")).FullName;
        _quarantineDir = Path.Combine(root.FullName, "quarantine");
    }

    public void Dispose()
    {
        var root = Directory.GetParent(_sourceDir)!.FullName;
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private string CreateSourceFile(string name, string content = "junk")
    {
        var path = Path.Combine(_sourceDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static FileFinding FindingFor(string path) =>
        new(path, JunkCategory.UserTemp, "user-temp", "test rule", SizeBytes: 1024, DateTime.UtcNow, RequiresElevation: false);

    [Fact]
    public void Quarantine_moves_file_out_of_its_original_location()
    {
        var sourcePath = CreateSourceFile("scratch.tmp");
        var manager = new QuarantineManager(_quarantineDir);

        var record = manager.Quarantine(FindingFor(sourcePath));

        Assert.False(File.Exists(sourcePath));
        Assert.True(File.Exists(record.QuarantinedPath));
        Assert.Single(manager.ListActive());
    }

    [Fact]
    public void Restore_moves_the_file_back_to_its_original_path()
    {
        var sourcePath = CreateSourceFile("scratch.tmp");
        var manager = new QuarantineManager(_quarantineDir);
        var record = manager.Quarantine(FindingFor(sourcePath));

        var restored = manager.Restore(record.Id);

        Assert.True(File.Exists(sourcePath));
        Assert.True(restored.Restored);
        Assert.Empty(manager.ListActive());
    }

    [Fact]
    public void Manifest_survives_across_manager_instances()
    {
        var sourcePath = CreateSourceFile("scratch.tmp");
        var first = new QuarantineManager(_quarantineDir);
        var record = first.Quarantine(FindingFor(sourcePath));

        var second = new QuarantineManager(_quarantineDir);

        var reloaded = Assert.Single(second.ListActive());
        Assert.Equal(record.Id, reloaded.Id);
    }

    [Fact]
    public void PurgeOlderThan_throws_when_confirmation_phrase_is_wrong()
    {
        var manager = new QuarantineManager(_quarantineDir);

        Assert.Throws<InvalidOperationException>(() => manager.PurgeOlderThan(TimeSpan.Zero, "delete"));
        Assert.Throws<InvalidOperationException>(() => manager.PurgeOlderThan(TimeSpan.Zero, ""));
    }

    [Fact]
    public void PurgeOlderThan_only_removes_records_past_retention_and_permanently_deletes_the_file()
    {
        var oldFilePath = CreateSourceFile("old.tmp");
        var newFilePath = CreateSourceFile("new.tmp");

        var clock = new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var manager = new QuarantineManager(_quarantineDir, clock);

        var oldRecord = manager.Quarantine(FindingFor(oldFilePath));

        clock.UtcNow = clock.UtcNow.AddDays(10);
        var newRecord = manager.Quarantine(FindingFor(newFilePath));

        clock.UtcNow = clock.UtcNow.AddDays(1);
        var purgedCount = manager.PurgeOlderThan(TimeSpan.FromDays(7), "DELETE");

        Assert.Equal(1, purgedCount);
        Assert.False(File.Exists(oldRecord.QuarantinedPath));
        Assert.True(File.Exists(newRecord.QuarantinedPath));
        Assert.Single(manager.ListActive());
    }

    [Fact]
    public void Quarantine_throws_when_source_file_no_longer_exists()
    {
        var manager = new QuarantineManager(_quarantineDir);
        var missingPath = Path.Combine(_sourceDir, "gone.tmp");

        Assert.Throws<FileNotFoundException>(() => manager.Quarantine(FindingFor(missingPath)));
    }
}
