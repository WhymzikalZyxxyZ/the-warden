namespace TheWarden.Core.Rules;

/// <summary>
/// A versioned, user-editable set of <see cref="JunkRule"/>s. Shipping the built-in
/// rules as data (loadable from JSON) rather than hard-coded logic means a wrong
/// classification is fixable without a code change or a new release.
/// </summary>
public sealed class RulePack
{
    public required string Version { get; init; }
    public required IReadOnlyList<JunkRule> Rules { get; init; }

    /// <summary>
    /// The built-in rule set covering the well-known, low-risk Windows/browser junk
    /// locations described in ADR-0002. Deliberately conservative — every entry here
    /// is a location Windows itself already treats as disposable.
    /// </summary>
    public static RulePack Default() => new()
    {
        Version = "1.0.0",
        Rules =
        [
            new JunkRule
            {
                Id = "system-temp",
                Category = JunkCategory.SystemTemp,
                Description = "Windows system temp directory",
                DirectoryPattern = @"%SystemRoot%\Temp",
                FilePattern = "*",
                MinAgeHours = 24,
                RequiresElevation = true,
            },
            new JunkRule
            {
                Id = "user-temp",
                Category = JunkCategory.UserTemp,
                Description = "Per-user temp directory",
                DirectoryPattern = @"%TEMP%",
                FilePattern = "*",
                MinAgeHours = 24,
            },
            new JunkRule
            {
                Id = "chrome-cache",
                Category = JunkCategory.BrowserCache,
                Description = "Chrome/Chromium HTTP cache",
                DirectoryPattern = @"%LOCALAPPDATA%\Google\Chrome\User Data\Default\Cache",
                FilePattern = "*",
                MinAgeHours = 0,
            },
            new JunkRule
            {
                Id = "edge-cache",
                Category = JunkCategory.BrowserCache,
                Description = "Edge (Chromium) HTTP cache",
                DirectoryPattern = @"%LOCALAPPDATA%\Microsoft\Edge\User Data\Default\Cache",
                FilePattern = "*",
                MinAgeHours = 0,
            },
            new JunkRule
            {
                Id = "thumbnail-cache",
                Category = JunkCategory.ThumbnailCache,
                Description = "Windows Explorer thumbnail cache",
                DirectoryPattern = @"%LOCALAPPDATA%\Microsoft\Windows\Explorer",
                FilePattern = "thumbcache_*.db",
                MinAgeHours = 24,
            },
            new JunkRule
            {
                Id = "windows-update-leftover",
                Category = JunkCategory.WindowsUpdateLeftover,
                Description = "SoftwareDistribution download cache left behind after Windows Update installs",
                DirectoryPattern = @"%SystemRoot%\SoftwareDistribution\Download",
                FilePattern = "*",
                MinAgeHours = 24,
                RequiresElevation = true,
            },
            new JunkRule
            {
                Id = "crash-dump",
                Category = JunkCategory.CrashDump,
                Description = "Application/system crash dump files",
                DirectoryPattern = @"%LOCALAPPDATA%\CrashDumps",
                FilePattern = "*.dmp",
                MinAgeHours = 0,
            },
        ],
    };
}
