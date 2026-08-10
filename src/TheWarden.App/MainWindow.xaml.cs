using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TheWarden.App.ViewModels;
using TheWarden.Core.Quarantine;
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
}
