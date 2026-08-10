using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TheWarden.Core;
using TheWarden.Core.Health;
using TheWarden.Core.Quarantine;
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
    private readonly FileSystemScanner _scanner;
    private readonly QuarantineManager _quarantineManager;
    private readonly HealthReportService _healthReportService;

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

    public MainViewModel()
    {
        var rulePack = RulePack.Default();
        _scanner = new FileSystemScanner(rulePack);

        var quarantineRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TheWarden",
            "Quarantine");
        _quarantineManager = new QuarantineManager(quarantineRoot);

        _healthReportService = new HealthReportService(new WmiSystemHealthProbe());

        RefreshQuarantineList();
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsBusy = true;
        StatusMessage = "Scanning known junk locations…";

        var results = await Task.Run(() => _scanner.Scan());

        Findings = new ObservableCollection<FileFinding>(results);

        var report = _healthReportService.Generate(results);
        HealthReport = report;
        HealthSummaryText = FormatHealthSummary(report);

        StatusMessage = results.Count == 0
            ? "No junk found. Your machine is tidy."
            : $"Found {results.Count} item(s) — nothing has been touched yet.";

        IsBusy = false;
    }

    [RelayCommand]
    private void Quarantine(FileFinding? finding)
    {
        if (finding is null)
        {
            return;
        }

        _quarantineManager.Quarantine(finding);
        Findings.Remove(finding);
        RefreshQuarantineList();
        StatusMessage = $"Moved '{Path.GetFileName(finding.FullPath)}' to quarantine — restorable any time.";
    }

    [RelayCommand]
    private void Restore(QuarantineRecord? record)
    {
        if (record is null)
        {
            return;
        }

        _quarantineManager.Restore(record.Id);
        RefreshQuarantineList();
        StatusMessage = $"Restored '{Path.GetFileName(record.OriginalPath)}' to its original location.";
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
