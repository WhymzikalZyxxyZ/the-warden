using Microsoft.Win32;
using TheWarden.Core.Scanning;

namespace TheWarden.Core.Tests.Scanning;

public class RegistryStartupScannerTests
{
    private sealed class FakeRegistryReader(Dictionary<(RegistryHive, string), List<(string, string)>> valuesByKey) : IRegistryReader
    {
        public IEnumerable<(string Name, string Value)> ReadStringValues(RegistryHive hive, string subKeyPath) =>
            valuesByKey.TryGetValue((hive, subKeyPath), out var values) ? values : [];
    }

    [Fact]
    public void Scan_reads_from_the_current_user_run_key()
    {
        var reader = new FakeRegistryReader(new Dictionary<(RegistryHive, string), List<(string, string)>>
        {
            [(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run")] =
                [("Updater", @"C:\Tools\updater.exe")],
        });
        var scanner = new RegistryStartupScanner(reader);

        var entries = scanner.Scan();

        var entry = Assert.Single(entries);
        Assert.Equal("Updater", entry.ValueName);
        Assert.Equal(@"C:\Tools\updater.exe", entry.ResolvedExecutablePath);
    }

    [Fact]
    public void Scan_reads_from_all_three_well_known_run_key_locations()
    {
        var reader = new FakeRegistryReader(new Dictionary<(RegistryHive, string), List<(string, string)>>
        {
            [(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run")] = [("A", @"C:\a.exe")],
            [(RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run")] = [("B", @"C:\b.exe")],
            [(RegistryHive.LocalMachine, @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run")] = [("C", @"C:\c.exe")],
        });
        var scanner = new RegistryStartupScanner(reader);

        var entries = scanner.Scan();

        Assert.Equal(3, entries.Count);
        Assert.Contains(entries, e => e.ValueName == "A");
        Assert.Contains(entries, e => e.ValueName == "B");
        Assert.Contains(entries, e => e.ValueName == "C");
    }

    [Fact]
    public void Scan_returns_empty_when_no_run_keys_exist()
    {
        var scanner = new RegistryStartupScanner(new FakeRegistryReader([]));

        Assert.Empty(scanner.Scan());
    }

    [Theory]
    [InlineData(@"""C:\Program Files\App\app.exe""", @"C:\Program Files\App\app.exe")]
    [InlineData(@"""C:\Program Files\App\app.exe"" --silent", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\Tools\updater.exe", @"C:\Tools\updater.exe")]
    [InlineData(@"C:\Tools\updater.exe --check-now", @"C:\Tools\updater.exe")]
    [InlineData(@"C:\Tools\script.bat -x", @"C:\Tools\script.bat")]
    public void ExtractExecutablePath_resolves_quoted_and_unquoted_commands(string command, string expected)
    {
        Assert.Equal(expected, RegistryStartupScanner.ExtractExecutablePath(command));
    }

    [Fact]
    public void ExtractExecutablePath_returns_null_for_an_empty_command()
    {
        Assert.Null(RegistryStartupScanner.ExtractExecutablePath("   "));
    }

    [Fact]
    public void ExtractExecutablePath_falls_back_to_the_whole_string_when_no_known_extension_is_found()
    {
        Assert.Equal("rundll32 someplugin,Entry", RegistryStartupScanner.ExtractExecutablePath("rundll32 someplugin,Entry"));
    }
}
