using System.Diagnostics.CodeAnalysis;

namespace TheWarden.Core.Scanning;

/// <summary>Abstracts real filesystem enumeration so the scanner is unit-testable without touching disk.</summary>
public interface IDirectoryReader
{
    bool DirectoryExists(string path);

    IEnumerable<FileEntry> EnumerateFiles(string directory);
}

/// <summary>Excluded from coverage: thin real-filesystem shim exercised via <see cref="IDirectoryReader"/> fakes in tests.</summary>
[ExcludeFromCodeCoverage]
public sealed class RealDirectoryReader : IDirectoryReader
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<FileEntry> EnumerateFiles(string directory)
    {
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            FileInfo info;
            try
            {
                info = new FileInfo(path);
            }
            catch (IOException)
            {
                // File vanished or is locked between enumeration and stat — skip it rather than fail the whole scan.
                continue;
            }

            yield return new FileEntry(path, info.LastWriteTimeUtc, info.Length);
        }
    }
}
