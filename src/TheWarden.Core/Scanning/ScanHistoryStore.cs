using System.Text.Json;
using TheWarden.Core;

namespace TheWarden.Core.Scanning;

/// <summary>
/// Persists the record of every malware scan that's run — same JSON-on-disk shape as
/// QuarantineManager's manifest, including the same crash-safety: atomic
/// write-to-temp-then-rename, and a corrupted file is treated as empty history rather
/// than crashing the app on the next launch.
/// </summary>
public sealed class ScanHistoryStore
{
    private const string HistoryFileName = "scan-history.json";
    private const int MaxEntriesKept = 100;

    private readonly string _historyPath;
    private readonly IClock _clock;
    private readonly List<ScanHistoryEntry> _entries;

    public ScanHistoryStore(string storageRoot, IClock clock)
    {
        Directory.CreateDirectory(storageRoot);
        _historyPath = Path.Combine(storageRoot, HistoryFileName);
        _clock = clock;
        _entries = Load();
    }

    public ScanHistoryStore(string storageRoot) : this(storageRoot, new SystemClock())
    {
    }

    /// <summary>Most recent first.</summary>
    public IReadOnlyList<ScanHistoryEntry> GetAll() =>
        _entries.OrderByDescending(e => e.TimestampUtc).ToList();

    public ScanHistoryEntry Add(MalwareScanScope scope, int filesFound, int filesChecked, int filesFlagged, ScanOutcome outcome)
    {
        var entry = new ScanHistoryEntry(Guid.NewGuid(), _clock.UtcNow, scope, filesFound, filesChecked, filesFlagged, outcome);
        _entries.Add(entry);

        // Unbounded history is a slow leak, not a feature — a scan a day for a year is
        // already 365 entries doing nothing but growing the file every launch has to
        // parse. Oldest entries drop off once the cap is hit.
        if (_entries.Count > MaxEntriesKept)
        {
            _entries.RemoveRange(0, _entries.Count - MaxEntriesKept);
        }

        Save();
        return entry;
    }

    private List<ScanHistoryEntry> Load()
    {
        if (!File.Exists(_historyPath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_historyPath);
            return JsonSerializer.Deserialize<List<ScanHistoryEntry>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
        var tempPath = _historyPath + $".{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _historyPath, overwrite: true);
    }
}
