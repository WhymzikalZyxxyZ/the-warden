using System.Diagnostics.CodeAnalysis;
using Microsoft.Win32;

namespace TheWarden.Core.Scanning;

/// <summary>Abstracts registry reads so RegistryStartupScanner is unit-testable without touching the real registry.</summary>
public interface IRegistryReader
{
    IEnumerable<(string Name, string Value)> ReadStringValues(RegistryHive hive, string subKeyPath);
}

/// <summary>Excluded from coverage: thin real-registry shim exercised via <see cref="IRegistryReader"/> fakes in tests.</summary>
[ExcludeFromCodeCoverage]
public sealed class RealRegistryReader : IRegistryReader
{
    public IEnumerable<(string Name, string Value)> ReadStringValues(RegistryHive hive, string subKeyPath)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(subKeyPath);
        if (key is null)
        {
            yield break;
        }

        foreach (var name in key.GetValueNames())
        {
            if (key.GetValue(name) is string value && value.Length > 0)
            {
                yield return (name, value);
            }
        }
    }
}
