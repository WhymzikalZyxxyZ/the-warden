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
    // Explicit Timeout rather than the 100s default: a single hung request shouldn't
    // sit that long before VirusTotalReputationClient's timeout handling can mark it
    // LookupFailed and let the sweep move on.
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(20) };

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

    // VirusTotal's public API is rate-limited (4 requests/minute on the free tier);
    // 16s of headroom over the strict 15s minimum protects the user's key from
    // tripping that limit if a request runs slightly long.
    private static readonly TimeSpan ReputationCheckDelay = TimeSpan.FromSeconds(16);

    private readonly MalwareScanner _malwareScanner = new();
    private CancellationTokenSource? _malwareScanCts;

    [ObservableProperty]
    private ObservableCollection<MalwareFinding> malwareFindings = [];

    [ObservableProperty]
    private ObservableCollection<FileTypeCount> fileTypeBreakdown = [];

    [ObservableProperty]
    private bool isScanningForMalware;

    [ObservableProperty]
    private string malwareScanStatusText = "Not scanned yet.";

    [ObservableProperty]
    private double malwareScanProgress;

    [ObservableProperty]
    private int malwareFilesFoundCount;

    // Same reasoning as HasFlaggedMalware/NeedsApiKey — a plain bool the Visibility
    // binding can read directly, instead of comparing MalwareFilesFoundCount to zero
    // via a converter or method call in XAML.
    public bool HasScannedFiles => MalwareFilesFoundCount > 0;

    partial void OnMalwareFilesFoundCountChanged(int value) => OnPropertyChanged(nameof(HasScannedFiles));

    [ObservableProperty]
    private int malwareFilesCheckedCount;

    [ObservableProperty]
    private int malwareFilesFlaggedCount;

    // Kept in sync via MalwareFindings.CollectionChanged (wired in the constructor)
    // rather than computed inline in XAML — same reasoning as NeedsApiKey: a plain
    // property sidesteps the x:Bind Visibility/method-call compiler bug instead of
    // running into it again.
    [ObservableProperty]
    private bool hasFlaggedMalware;

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

        MalwareFindings.CollectionChanged += (_, _) => HasFlaggedMalware = MalwareFindings.Count > 0;

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

    [RelayCommand]
    private async Task ScanForMalwareAsync(MalwareScanScope scope)
    {
        var apiKey = _apiKeyStore.GetApiKey();
        if (apiKey is null)
        {
            StatusMessage = "Set a VirusTotal API key first to scan for malware.";
            return;
        }

        _malwareScanCts?.Cancel();
        var cts = new CancellationTokenSource();
        _malwareScanCts = cts;

        IsScanningForMalware = true;
        MalwareFindings.Clear();
        FileTypeBreakdown.Clear();
        MalwareFilesFoundCount = 0;
        MalwareFilesCheckedCount = 0;
        MalwareFilesFlaggedCount = 0;
        MalwareScanProgress = 0.0;
        MalwareScanStatusText = scope == MalwareScanScope.FullDrive
            ? "Scanning the entire drive for files — this can take a while…"
            : "Scanning Downloads, Desktop, and Startup folders…";

        try
        {
            var driveRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
            var files = await Task.Run(() => _malwareScanner.Scan(scope, driveRoot, cts.Token), cts.Token);

            MalwareFilesFoundCount = files.Count;
            UpdateFileTypeBreakdown(files);

            if (files.Count == 0)
            {
                MalwareScanStatusText = "No executable-type files found in scope.";
                return;
            }

            MalwareScanStatusText = $"Checking reputation — 0 of {files.Count}…";

            var sweep = new ReputationSweepService(new VirusTotalReputationClient(HttpClient, apiKey), ReputationCheckDelay);
            await foreach (var finding in sweep.SweepAsync(files, cts.Token))
            {
                MalwareFilesCheckedCount++;
                MalwareScanProgress = (double)MalwareFilesCheckedCount / files.Count;

                if (finding.NeedsAttention)
                {
                    MalwareFindings.Add(finding);
                    MalwareFilesFlaggedCount++;
                }

                MalwareScanStatusText = $"Checking reputation — {MalwareFilesCheckedCount} of {files.Count} — {MalwareFilesFlaggedCount} flagged";
            }

            MalwareScanStatusText = MalwareFilesFlaggedCount == 0
                ? $"Scan complete — {files.Count} file(s) checked, nothing flagged."
                : $"Scan complete — {MalwareFilesFlaggedCount} of {files.Count} file(s) flagged for review.";
        }
        catch (OperationCanceledException)
        {
            MalwareScanStatusText = $"Scan cancelled — {MalwareFilesCheckedCount} of {MalwareFilesFoundCount} checked before stopping.";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            MalwareScanStatusText = $"Scan couldn't finish — {ex.Message}";
        }
        finally
        {
            IsScanningForMalware = false;
            if (_malwareScanCts == cts)
            {
                _malwareScanCts = null;
            }
        }
    }

    [RelayCommand]
    private void CancelMalwareScan()
    {
        _malwareScanCts?.Cancel();
    }

    [RelayCommand]
    private void QuarantineMalwareFinding(MalwareFinding? finding)
    {
        if (finding is null)
        {
            return;
        }

        QuarantineMalwareFindingCore(finding);
    }

    [RelayCommand]
    private void QuarantineAllFlaggedMalware()
    {
        // Snapshot first — QuarantineMalwareFindingCore mutates MalwareFindings as it
        // goes (removing each item it successfully quarantines), so iterating the live
        // collection directly would skip entries or throw on a modified-during-enumeration.
        foreach (var finding in MalwareFindings.ToList())
        {
            QuarantineMalwareFindingCore(finding);
        }
    }

    private void QuarantineMalwareFindingCore(MalwareFinding finding)
    {
        LastOperationNeedsElevation = false;

        // Reuses the exact same QuarantineManager/QuarantineOperationException path
        // Quarantine(FileFinding) already uses — a malware finding just needs to be
        // expressed as one, there's no separate quarantine mechanism to build or test.
        var syntheticFinding = new FileFinding(
            finding.FullPath,
            JunkCategory.SuspiciousExecutable,
            "malware-scan",
            $"VirusTotal: {finding.Verdict.Status} ({finding.Verdict.MaliciousEngineCount}/{finding.Verdict.TotalEngineCount} engines)",
            finding.SizeBytes,
            DateTime.UtcNow,
            RequiresElevation: false);

        try
        {
            _quarantineManager.Quarantine(syntheticFinding);
            MalwareFindings.Remove(finding);
            MalwareFilesFlaggedCount = Math.Max(0, MalwareFilesFlaggedCount - 1);
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
            MalwareFindings.Remove(finding);
        }
    }

    private const double FileTypeBarMaxWidth = 220.0;

    private void UpdateFileTypeBreakdown(IReadOnlyList<FileEntry> files)
    {
        var total = files.Count;
        var counts = files
            .GroupBy(f => Path.GetExtension(f.FullPath).ToLowerInvariant())
            .Select(g => new FileTypeCount(g.Key.Length == 0 ? "(no extension)" : g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .Select(c =>
            {
                var fraction = total == 0 ? 0 : (double)c.Count / total;
                return c with { FractionOfTotal = fraction, BarWidthPixels = fraction * FileTypeBarMaxWidth };
            });

        FileTypeBreakdown = new ObservableCollection<FileTypeCount>(counts);
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
