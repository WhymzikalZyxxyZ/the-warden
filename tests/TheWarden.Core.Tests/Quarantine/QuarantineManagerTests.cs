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

    [Fact]
    public void Quarantine_throws_QuarantineOperationException_when_source_file_is_locked()
    {
        var sourcePath = CreateSourceFile("locked.tmp");
        var manager = new QuarantineManager(_quarantineDir);

        using var lockHandle = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None);

        var ex = Assert.Throws<QuarantineOperationException>(() => manager.Quarantine(FindingFor(sourcePath)));
        Assert.Contains("locked.tmp", ex.Message);
        Assert.IsType<IOException>(ex.InnerException);
        Assert.Empty(manager.ListActive());
    }

    [Fact]
    public void Restore_throws_QuarantineOperationException_when_something_already_occupies_the_original_path()
    {
        var sourcePath = CreateSourceFile("scratch.tmp");
        var manager = new QuarantineManager(_quarantineDir);
        var record = manager.Quarantine(FindingFor(sourcePath));

        // Something else now lives where the file used to be — a real scenario, not
        // just a locked handle (e.g. a new file happened to get created at that path
        // while this one sat in quarantine).
        File.WriteAllText(sourcePath, "someone else's file now");

        var ex = Assert.Throws<QuarantineOperationException>(() => manager.Restore(record.Id));
        Assert.Contains("scratch.tmp", ex.Message);
        Assert.IsType<IOException>(ex.InnerException);
        // The failed restore shouldn't have been marked restored or dropped from the active list.
        Assert.Single(manager.ListActive());
    }

    [Fact]
    public void PurgeOlderThan_skips_a_locked_file_but_still_purges_the_rest()
    {
        var lockedFilePath = CreateSourceFile("locked.tmp");
        var freeFilePath = CreateSourceFile("free.tmp");

        var clock = new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var manager = new QuarantineManager(_quarantineDir, clock);

        var lockedRecord = manager.Quarantine(FindingFor(lockedFilePath));
        var freeRecord = manager.Quarantine(FindingFor(freeFilePath));

        clock.UtcNow = clock.UtcNow.AddDays(10);

        using (new FileStream(lockedRecord.QuarantinedPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var purgedCount = manager.PurgeOlderThan(TimeSpan.Zero, "DELETE");

            Assert.Equal(1, purgedCount);
            Assert.False(File.Exists(freeRecord.QuarantinedPath));
            Assert.True(File.Exists(lockedRecord.QuarantinedPath));
        }

        // The locked record stays active — it was never actually purged.
        var stillActive = Assert.Single(manager.ListActive());
        Assert.Equal(lockedRecord.Id, stillActive.Id);
    }
}
