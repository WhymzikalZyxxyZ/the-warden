using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TheWarden.App.ViewModels;
using TheWarden.Core.Quarantine;
using TheWarden.Core.Reputation;
using TheWarden.Core.Scanning;

namespace TheWarden.App;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        Title = "The Warden";
    }

    // x:Bind supports static/instance method calls directly, avoiding a converter
    // class for this one negation.
    public bool Not(bool value) => !value;

    private async void ScanButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.ScanCommand.ExecuteAsync(null);

    private void QuarantineButton_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is FileFinding finding)
        {
            ViewModel.QuarantineCommand.Execute(finding);
        }
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is QuarantineRecord record)
        {
            ViewModel.RestoreCommand.Execute(record);
        }
    }

    private void SaveApiKeyButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SaveApiKeyCommand.Execute(null);

    private void ClearApiKeyButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ClearApiKeyCommand.Execute(null);

    private async void CheckReputationButton_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is FileFinding finding)
        {
            await ViewModel.CheckReputationCommand.ExecuteAsync(finding);
        }
    }

    private void RelaunchElevatedButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.RelaunchElevatedCommand.Execute(null);

    private async void QuickMalwareScanButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.ScanForMalwareCommand.ExecuteAsync(MalwareScanScope.CommonLocations);

    // A full-drive scan is slow and touches far more of the disk than the default
    // scan — worth a confirmation with an honest expectation set, not a button that
    // silently kicks off something that might run for a long time.
    private async void FullDriveMalwareScanButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Scan the entire drive?",
            Content = "This walks every folder on the drive looking for executable-type files, then checks each one's reputation — rate-limited to protect your API key, so it can take a long time on a large drive. You can cancel at any point once it starts.",
            PrimaryButtonText = "Start Full Drive Scan",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.ScanForMalwareCommand.ExecuteAsync(MalwareScanScope.FullDrive);
        }
    }

    private void CancelMalwareScanButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.CancelMalwareScanCommand.Execute(null);

    private void QuarantineMalwareButton_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is MalwareFinding finding)
        {
            ViewModel.QuarantineMalwareFindingCommand.Execute(finding);
        }
    }

    // Bulk action, same reasoning as the full-drive-scan confirmation — moving
    // several files at once deserves an explicit "yes, all of them" rather than
    // one click doing something that size silently.
    private async void QuarantineAllFlaggedButton_Click(object sender, RoutedEventArgs e)
    {
        var count = ViewModel.MalwareFindings.Count;
        var dialog = new ContentDialog
        {
            Title = "Quarantine all flagged files?",
            Content = $"This moves all {count} flagged file(s) into quarantine. Nothing is deleted — every one stays restorable from the Quarantine list.",
            PrimaryButtonText = $"Quarantine All ({count})",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.QuarantineAllFlaggedMalwareCommand.Execute(null);
        }
    }
}
