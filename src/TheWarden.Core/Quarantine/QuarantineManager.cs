using System.Text.Json;
using TheWarden.Core.Scanning;

namespace TheWarden.Core.Quarantine;

/// <summary>
/// Owns the quarantine directory: every "removal" the app performs is really a move
/// into this folder plus a manifest entry, never a direct delete (see ADR-0002).
/// Restoring puts a file back exactly where it came from. Permanently deleting a
/// quarantined file requires <see cref="PurgeOlderThan"/>, which only acts on items
/// already past a caller-supplied retention window and only when the caller passes
/// the literal confirmation phrase "DELETE".
/// </summary>
public sealed class QuarantineManager
{
    private const string ConfirmationPhrase = "DELETE";
    private const string ManifestFileName = "manifest.json";

    private readonly string _quarantineRoot;
    private readonly IClock _clock;
    private readonly string _manifestPath;
    private readonly List<QuarantineRecord> _records;

    public QuarantineManager(string quarantineRoot, IClock clock)
    {
        _quarantineRoot = quarantineRoot;
        _clock = clock;
        Directory.CreateDirectory(_quarantineRoot);
        _manifestPath = Path.Combine(_quarantineRoot, ManifestFileName);
        _records = LoadManifest();
    }

    public QuarantineManager(string quarantineRoot) : this(quarantineRoot, new SystemClock())
    {
    }

    /// <summary>All quarantined items that have neither been restored nor purged.</summary>
    public IReadOnlyList<QuarantineRecord> ListActive() =>
        _records.Where(r => !r.Restored).ToList();

    public QuarantineRecord Quarantine(FileFinding finding)
    {
        if (!File.Exists(finding.FullPath))
        {
            throw new FileNotFoundException("Cannot quarantine a file that no longer exists.", finding.FullPath);
        }

        var id = Guid.NewGuid();
        var quarantinedPath = Path.Combine(_quarantineRoot, $"{id:N}_{Path.GetFileName(finding.FullPath)}");
        var fileName = Path.GetFileName(finding.FullPath);

        try
        {
            File.Move(finding.FullPath, quarantinedPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new QuarantineOperationException(
                $"Couldn't quarantine '{fileName}' — access denied. " +
                (finding.RequiresElevation
                    ? "This location requires running The Warden as Administrator."
                    : "Check that the file isn't protected by another program."),
                ex);
        }
        catch (IOException ex)
        {
            throw new QuarantineOperationException(
                $"Couldn't quarantine '{fileName}' — it may be open in another program right now.",
                ex);
        }

        var record = new QuarantineRecord(
            id,
            finding.FullPath,
            quarantinedPath,
            finding.Category,
            finding.RuleDescription,
            finding.SizeBytes,
            _clock.UtcNow);

        _records.Add(record);
        SaveManifest();
        return record;
    }

    /// <summary>Moves a quarantined file back to its original location. Throws if the original directory no longer exists.</summary>
    public QuarantineRecord Restore(Guid id)
    {
        var index = _records.FindIndex(r => r.Id == id && !r.Restored);
        if (index < 0)
        {
            throw new InvalidOperationException($"No active quarantine record with id {id}.");
        }

        var record = _records[index];
        var originalDirectory = Path.GetDirectoryName(record.OriginalPath)
            ?? throw new InvalidOperationException($"Quarantine record {id} has no directory component.");

        if (!Directory.Exists(originalDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Cannot restore '{record.OriginalPath}' — its original directory no longer exists.");
        }

        var fileName = Path.GetFileName(record.OriginalPath);

        try
        {
            File.Move(record.QuarantinedPath, record.OriginalPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new QuarantineOperationException(
                $"Couldn't restore '{fileName}' — access denied restoring to its original location.",
                ex);
        }
        catch (IOException ex)
        {
            throw new QuarantineOperationException(
                $"Couldn't restore '{fileName}' — a file already exists at its original location, or it's open in another program.",
                ex);
        }

        var restored = record with { Restored = true, RestoredAtUtc = _clock.UtcNow };
        _records[index] = restored;
        SaveManifest();
        return restored;
    }

    /// <summary>
    /// Permanently deletes quarantined files older than <paramref name="retention"/>.
    /// Requires the literal phrase "DELETE" as an explicit, non-defaultable acknowledgement
    /// that this step — unlike everything else in the app — cannot be undone.
    /// </summary>
    public int PurgeOlderThan(TimeSpan retention, string typedConfirmation)
    {
        if (typedConfirmation != ConfirmationPhrase)
        {
            throw new InvalidOperationException(
                $"Purge requires typing '{ConfirmationPhrase}' exactly to confirm this action is irreversible.");
        }

        var cutoff = _clock.UtcNow - retention;
        var toPurge = _records
            .Where(r => !r.Restored && r.QuarantinedAtUtc <= cutoff)
            .ToList();

        // A file locked by another process shouldn't abort the whole batch — everything
        // still deletable gets purged, and the manifest is only saved for what actually
        // succeeded, so a mid-batch failure can never desync the manifest from disk.
        var purgedCount = 0;
        foreach (var record in toPurge)
        {
            try
            {
                if (File.Exists(record.QuarantinedPath))
                {
                    File.Delete(record.QuarantinedPath);
                }
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            _records.Remove(record);
            purgedCount++;
        }

        if (purgedCount > 0)
        {
            SaveManifest();
        }

        return purgedCount;
    }

    private List<QuarantineRecord> LoadManifest()
    {
        if (!File.Exists(_manifestPath))
        {
            return [];
        }

        var json = File.ReadAllText(_manifestPath);
        return JsonSerializer.Deserialize<List<QuarantineRecord>>(json) ?? [];
    }

    private void SaveManifest()
    {
        var json = JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_manifestPath, json);
    }
}
