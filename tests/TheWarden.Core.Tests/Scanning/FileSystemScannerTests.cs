using TheWarden.Core.Rules;
using TheWarden.Core.Scanning;

namespace TheWarden.Core.Tests.Scanning;

public class FileSystemScannerTests
{
    private static readonly DateTime UtcNow = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FakeClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class IdentityExpander : IEnvironmentExpander
    {
        public string Expand(string pattern) => pattern;
    }

    private sealed class FakeDirectoryReader(Dictionary<string, List<FileEntry>> filesByDirectory) : IDirectoryReader
    {
        public bool DirectoryExists(string path) => filesByDirectory.ContainsKey(path);

        public IEnumerable<FileEntry> EnumerateFiles(string directory) =>
            filesByDirectory.TryGetValue(directory, out var files) ? files : [];

        public IEnumerable<string> EnumerateDirectories(string directory) => [];
    }

    private static RulePack TempRulePack() => new()
    {
        Version = "test",
        Rules =
        [
            new JunkRule
            {
                Id = "temp",
                Category = JunkCategory.UserTemp,
                Description = "temp files",
                DirectoryPattern = @"C:\Temp",
                FilePattern = "*.tmp",
            },
        ],
    };

    [Fact]
    public void Scan_finds_matching_files_in_existing_directories()
    {
        var reader = new FakeDirectoryReader(new Dictionary<string, List<FileEntry>>
        {
            [@"C:\Temp"] =
            [
                new FileEntry(@"C:\Temp\a.tmp", UtcNow.AddDays(-1), 1024),
                new FileEntry(@"C:\Temp\keep.txt", UtcNow.AddDays(-1), 512),
            ],
        });

        var scanner = new FileSystemScanner(TempRulePack(), reader, new IdentityExpander(), new FakeClock(UtcNow));

        var findings = scanner.Scan();

        var finding = Assert.Single(findings);
        Assert.Equal(@"C:\Temp\a.tmp", finding.FullPath);
        Assert.Equal(1024, finding.SizeBytes);
    }

    [Fact]
    public void Scan_skips_directories_that_do_not_exist()
    {
        var reader = new FakeDirectoryReader([]);
        var scanner = new FileSystemScanner(TempRulePack(), reader, new IdentityExpander(), new FakeClock(UtcNow));

        var findings = scanner.Scan();

        Assert.Empty(findings);
    }
}
