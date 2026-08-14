using Microsoft.UI.Xaml;

namespace TheWarden.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    // Backstop only — MainViewModel's commands are expected to catch their own
    // known failure modes (QuarantineOperationException, locked/missing files) and
    // report them via StatusMessage instead of throwing. This exists for whatever
    // that specific handling doesn't anticipate, so an unexpected exception ends
    // the current operation rather than taking down the whole app.
    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
    }
}
