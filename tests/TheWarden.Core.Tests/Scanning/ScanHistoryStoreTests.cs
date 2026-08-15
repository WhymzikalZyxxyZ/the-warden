using TheWarden.Core;
using TheWarden.Core.Scanning;

namespace TheWarden.Core.Tests.Scanning;

public class ScanHistoryStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("warden-history-tests-").FullName;

    private sealed class FakeClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void GetAll_is_empty_for_a_fresh_store()
    {
        var store = new ScanHistoryStore(_root);

        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void Add_records_an_entry_that_GetAll_returns()
    {
        var store = new ScanHistoryStore(_root);

        var entry = store.Add(MalwareScanScope.CommonLocations, filesFound: 40, filesChecked: 40, filesFlagged: 2, ScanOutcome.Completed);

        var all = store.GetAll();
        var only = Assert.Single(all);
        Assert.Equal(entry.Id, only.Id);
        Assert.Equal(2, only.FilesFlagged);
        Assert.Equal(ScanOutcome.Completed, only.Outcome);
    }

    [Fact]
    public void GetAll_returns_most_recent_first()
    {
        var clock = new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var store = new ScanHistoryStore(_root, clock);

        store.Add(MalwareScanScope.CommonLocations, 1, 1, 0, ScanOutcome.Completed);
        clock.UtcNow = clock.UtcNow.AddMinutes(5);
        var second = store.Add(MalwareScanScope.CommonLocations, 2, 2, 0, ScanOutcome.Completed);

        Assert.Equal(second.Id, store.GetAll()[0].Id);
    }

    [Fact]
    public void History_survives_across_store_instances()
    {
        var first = new ScanHistoryStore(_root);
        first.Add(MalwareScanScope.FullDrive, 500, 500, 5, ScanOutcome.Completed);

        var second = new ScanHistoryStore(_root);

        Assert.Single(second.GetAll());
    }

    [Fact]
    public void Constructor_treats_a_corrupted_history_file_as_empty_instead_of_throwing()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "scan-history.json"), "{ not valid json truncated");

        var store = new ScanHistoryStore(_root);

        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void Add_never_leaves_a_temp_file_behind_after_a_successful_write()
    {
        var store = new ScanHistoryStore(_root);

        store.Add(MalwareScanScope.CommonLocations, 1, 1, 0, ScanOutcome.Completed);

        Assert.Empty(Directory.GetFiles(_root, "scan-history.json.*.tmp"));
    }

    [Fact]
    public void Add_caps_history_at_100_entries_dropping_the_oldest_first()
    {
        var store = new ScanHistoryStore(_root);

        ScanHistoryEntry? firstEntry = null;
        ScanHistoryEntry? lastEntry = null;
        for (var i = 0; i < 105; i++)
        {
            var entry = store.Add(MalwareScanScope.CommonLocations, i, i, 0, ScanOutcome.Completed);
            firstEntry ??= entry;
            lastEntry = entry;
        }

        var all = store.GetAll();
        Assert.Equal(100, all.Count);
        Assert.DoesNotContain(all, e => e.Id == firstEntry!.Id);
        Assert.Contains(all, e => e.Id == lastEntry!.Id);
    }
}
