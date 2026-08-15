using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using TheWarden.Core;
using TheWarden.Core.Health;
using TheWarden.Core.Quarantine;
using TheWarden.Core.Reputation;
using TheWarden.Core.Rules;
using TheWarden.Core.Scanning;

namespace TheWarden.App.ViewModels;

/// <summary>
/// Backs the main window. Deliberately thin — every real decision (what counts as
/// junk, what "removing" a file actually does) lives in TheWarden.Core, which is
/// independently unit tested. This class only coordinates and formats for display.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    // Shared across every reputation lookup rather than one-per-check — a fresh
    // HttpClient per call is the classic way to quietly exhaust sockets under load.
    private static readonly HttpClient HttpClient = new();

    private readonly FileSystemScanner _scanner;
    private readonly QuarantineManager _quarantineManager;
    private readonly HealthReportService _healthReportService;
    private readonly IApiKeyStore _apiKeyStore;

    [ObservableProperty]
    private ObservableCollection<FileFinding> findings = [];

    [ObservableProperty]
    private ObservableCollection<QuarantineRecord> quarantinedItems = [];

    [ObservableProperty]
    private HealthReport? healthReport;

    [ObservableProperty]
    private string healthSummaryText = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Ready to scan.";

    [ObservableProperty]
    private bool hasApiKey;

    // A plain property rather than binding Visibility to Not(ViewModel.HasApiKey)
    // in XAML: x:Bind's generated code hits a name collision (a local named
    // "obj" shadowing itself) whenever a method-call binding target is also
    // implicitly converted bool -> Visibility. Exposing the negation as its
    // own property sidesteps the bug entirely — OnHasApiKeyChanged keeps it
    // in sync any time HasApiKey changes.
    public bool NeedsApiKey => !HasApiKey;

    partial void OnHasApiKeyChanged(bool value) => OnPropertyChanged(nameof(NeedsApiKey));

    // Not persisted as typed — only ever written to the encrypted store via
    // SaveApiKeyCommand, then cleared from here immediately after.
    [ObservableProperty]
    private string apiKeyInput = string.Empty;

    // Set true only when the most recent Quarantine/Restore failure was
    // specifically an elevation problem — lets the UI offer "Relaunch as
    // Administrator" instead of a dead-end error message. Reset on every
    // command invocation so a stale flag can't linger past whatever
    // actually caused it.
    [ObservableProperty]
    private bool lastOperationNeedsElevation;

    public MainViewModel()
    {
        var rulePack = RulePack.Default();
        _scanner = new FileSystemScanner(rulePack);

        var appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TheWarden");

        _quarantineManager = new QuarantineManager(Path.Combine(appDataRoot, "Quarantine"));
        _apiKeyStore = new DpapiApiKeyStore(Path.Combine(appDataRoot, "vt-apikey.bin"));
        HasApiKey = _apiKeyStore.GetApiKey() is not null;

        _healthReportService = new HealthReportService(new WmiSystemHealthProbe());

        RefreshQuarantineList();
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsBusy = true;
        StatusMessage = "Scanning known junk locations…";

        try
        {
            var results = await Task.Run(() => _scanner.Scan());

            Findings = new ObservableCollection<FileFinding>(results);

            var report = _healthReportService.Generate(results);
            HealthReport = report;
            HealthSummaryText = FormatHealthSummary(report);

            StatusMessage = results.Count == 0
                ? "No junk found. Your machine is tidy."
                : $"Found {results.Count} item(s) — nothing has been touched yet.";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            StatusMessage = $"Scan couldn't finish — {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Quarantine(FileFinding? finding)
    {
        if (finding is null)
        {
            return;
        }

        LastOperationNeedsElevation = false;

        try
        {
            _quarantineManager.Quarantine(finding);
            Findings.Remove(finding);
            RefreshQuarantineList();
            StatusMessage = $"Moved '{Path.GetFileName(finding.FullPath)}' to quarantine — restorable any time.";
        }
        catch (QuarantineOperationException ex)
        {
            StatusMessage = ex.Message;
            LastOperationNeedsElevation = ex.RequiresElevation;
        }
        catch (FileNotFoundException)
        {
            StatusMessage = $"'{Path.GetFileName(finding.FullPath)}' is already gone — someone else removed it first.";
            Findings.Remove(finding);
        }
    }

    [RelayCommand]
    private void Restore(QuarantineRecord? record)
    {
        if (record is null)
        {
            return;
        }

        LastOperationNeedsElevation = false;

        try
        {
            _quarantineManager.Restore(record.Id);
            RefreshQuarantineList();
            StatusMessage = $"Restored '{Path.GetFileName(record.OriginalPath)}' to its original location.";
        }
        catch (QuarantineOperationException ex)
        {
            StatusMessage = ex.Message;
            LastOperationNeedsElevation = ex.RequiresElevation;
        }
        catch (DirectoryNotFoundException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void RelaunchElevated()
    {
        if (ElevationRelauncher.TryRelaunchElevated())
        {
            Application.Current.Exit();
        }
        else
        {
            StatusMessage = "Elevation was cancelled — try again if you still want to remove that item.";
        }
    }

    [RelayCommand]
    private void SaveApiKey()
    {
        var trimmed = ApiKeyInput.Trim();
        if (trimmed.Length == 0)
        {
            StatusMessage = "Enter a VirusTotal API key first.";
            return;
        }

        _apiKeyStore.SetApiKey(trimmed);
        ApiKeyInput = string.Empty;
        HasApiKey = true;
        StatusMessage = "VirusTotal API key saved — reputation checks are now available.";
    }

    [RelayCommand]
    private void ClearApiKey()
    {
        _apiKeyStore.ClearApiKey();
        HasApiKey = false;
        StatusMessage = "VirusTotal API key removed.";
    }

    [RelayCommand]
    private async Task CheckReputationAsync(FileFinding? finding)
    {
        if (finding is null)
        {
            return;
        }

        var apiKey = _apiKeyStore.GetApiKey();
        if (apiKey is null)
        {
            StatusMessage = "Set a VirusTotal API key first to check file reputation.";
            return;
        }

        var fileName = Path.GetFileName(finding.FullPath);
        StatusMessage = $"Checking reputation for '{fileName}'…";

        string sha256;
        try
        {
            sha256 = await Task.Run(() => FileHasher.ComputeSha256Hex(finding.FullPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Couldn't read '{fileName}' to check its reputation — {ex.Message}";
            return;
        }

        var client = new VirusTotalReputationClient(HttpClient, apiKey);
        var verdict = await client.LookupAsync(sha256);

        StatusMessage = verdict.Status switch
        {
            ReputationStatus.Malicious =>
                $"'{fileName}': flagged malicious by {verdict.MaliciousEngineCount}/{verdict.TotalEngineCount} engines on VirusTotal.",
            ReputationStatus.Suspicious =>
                $"'{fileName}': flagged suspicious on VirusTotal ({verdict.MaliciousEngineCount}/{verdict.TotalEngineCount} engines).",
            ReputationStatus.Clean =>
                $"'{fileName}': clean across {verdict.TotalEngineCount} VirusTotal engines.",
            ReputationStatus.NotChecked =>
                $"'{fileName}': VirusTotal has no record of this file — absence of data isn't the same as clean.",
            _ => $"'{fileName}': reputation lookup failed — try again in a moment.",
        };
    }

    private void RefreshQuarantineList()
    {
        QuarantinedItems = new ObservableCollection<QuarantineRecord>(_quarantineManager.ListActive());
    }

    private static string FormatHealthSummary(HealthReport report)
    {
        var diskSummary = report.Disks.Count == 0
            ? "disk data unavailable"
            : string.Join(", ", report.Disks.Select(d => $"{d.DriveLetter} {d.UsedFraction:P0} used"));

        var reclaimableMb = report.JunkBytesReclaimable / 1024.0 / 1024.0;
        return $"{diskSummary} · {report.StartupItemCount} startup item(s) · {reclaimableMb:0.0} MB reclaimable";
    }
}
