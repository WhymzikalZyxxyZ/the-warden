using System.ComponentModel;
using System.Diagnostics;

namespace TheWarden.App;

/// <summary>
/// Starts a new, elevated instance of this same executable via the standard UAC
/// "runas" verb. The app itself always launches asInvoker (see app.manifest) —
/// this is the opt-in path for the specific operations that actually need more,
/// rather than the whole app demanding Administrator every time it opens.
/// </summary>
public static class ElevationRelauncher
{
    /// <summary>
    /// Returns true if the elevated process was launched (the caller should exit
    /// the current, non-elevated instance right after). Returns false if the user
    /// declined the UAC prompt or elevation otherwise isn't available — the current
    /// instance should keep running as if nothing happened.
    /// </summary>
    public static bool TryRelaunchElevated()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas",
            });
            return true;
        }
        catch (Win32Exception)
        {
            // ERROR_CANCELLED (1223): the user clicked "No" on the UAC prompt.
            // Any other Win32Exception here means elevation genuinely isn't
            // available on this machine — either way, staying non-elevated is
            // the only option, so this is the same outcome for the caller.
            return false;
        }
    }
}
