using Microsoft.Win32;

namespace TheWarden.Core.Scanning;

/// <summary>
/// Reads the well-known Run keys (current user, local machine, and the Wow6432Node
/// mirror local machine uses for 32-bit entries on a 64-bit OS) and resolves each
/// command string down to the executable path it actually launches, so that path can
/// be hashed and reputation-checked the same way any other file is. Read-only, same
/// as every other scanner in this app — nothing here modifies the registry.
/// </summary>
public sealed class RegistryStartupScanner(IRegistryReader reader)
{
    public RegistryStartupScanner() : this(new RealRegistryReader())
    {
    }

    private static readonly (RegistryHive Hive, string SubKeyPath)[] RunKeyLocations =
    [
        (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run"),
        (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run"),
        (RegistryHive.LocalMachine, @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run"),
    ];

    public IReadOnlyList<RegistryStartupEntry> Scan()
    {
        var results = new List<RegistryStartupEntry>();

        foreach (var (hive, subKeyPath) in RunKeyLocations)
        {
            foreach (var (name, value) in reader.ReadStringValues(hive, subKeyPath))
            {
                results.Add(new RegistryStartupEntry(hive.ToString(), subKeyPath, name, value, ExtractExecutablePath(value)));
            }
        }

        return results;
    }

    /// <summary>
    /// Run-key values are full command lines, not bare paths — "C:\Program Files\App\app.exe"
    /// or "C:\Program Files\App\app.exe" --silent or, unquoted, C:\PF\app.exe --silent
    /// (ambiguous with spaces in the path, which real Windows resolves by trying
    /// progressively longer prefixes — a full command-line parser is more than this
    /// needs). Handles the two cases that cover the overwhelming majority of real
    /// entries: quoted paths, and unquoted paths up through the first recognized
    /// executable extension.
    /// </summary>
    public static string? ExtractExecutablePath(string command)
    {
        var trimmed = command.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed[0] == '"')
        {
            var closeQuote = trimmed.IndexOf('"', 1);
            return closeQuote > 1 ? trimmed[1..closeQuote] : null;
        }

        foreach (var extension in MalwareLocations.ExecutableExtensions)
        {
            var index = trimmed.IndexOf(extension, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                return trimmed[..(index + extension.Length)];
            }
        }

        // No recognized extension found at all — fall back to the whole string rather
        // than silently dropping the entry; the caller can still show it, just without
        // a resolvable path to hash.
        return trimmed;
    }
}
