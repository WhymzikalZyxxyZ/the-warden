namespace TheWarden.Core.Scanning;

/// <summary>
/// A single Run-key value — one of the most common real-world persistence mechanisms
/// malware actually uses, and something MalwareScanner's filesystem walk alone never
/// sees (a Downloads-folder scan doesn't know what's wired up to auto-launch at logon).
/// </summary>
public sealed record RegistryStartupEntry(
    string HiveName,
    string KeyPath,
    string ValueName,
    string RawCommand,
    string? ResolvedExecutablePath);
