using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace TheWarden.App;

public partial class App : Application
{
    // Named Mutex rather than nothing: two instances each loading their own in-memory
    // copy of the quarantine manifest and independently saving it is a real split-brain
    // bug, not a hypothetical one — whichever instance saves last silently overwrites the
    // other's changes, and a file that got physically quarantined by the "losing" instance
    // ends up with no manifest entry at all, invisible to Restore forever after.
    private const string SingleInstanceMutexName = "TheWarden.SingleInstance.Mutex";

    private Mutex? _singleInstanceMutex;
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            BringExistingInstanceToForeground();
            Environment.Exit(0);
            return;
        }

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SwRestore = 9;

    // Best-effort — if the existing instance's window can't be found (title changed,
    // minimized to tray in some future version, etc.) this just silently does nothing
    // and the duplicate launch exits quietly, which is still correct: no second
    // instance touching the manifest, even if the UX isn't as polished as it could be.
    private static void BringExistingInstanceToForeground()
    {
        var handle = FindWindow(null, "The Warden");
        if (handle == IntPtr.Zero)
        {
            return;
        }

        ShowWindow(handle, SwRestore);
        SetForegroundWindow(handle);
    }
}
