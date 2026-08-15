using System.Diagnostics.CodeAnalysis;

namespace TheWarden.Core.Scanning;

/// <summary>Abstracts real filesystem enumeration so the scanner is unit-testable without touching disk.</summary>
public interface IDirectoryReader
{
    bool DirectoryExists(string path);

    IEnumerable<FileEntry> EnumerateFiles(string directory);

    /// <summary>Immediate subdirectories only — callers that want to recurse do so themselves, one level at a time, so depth stays a caller-owned decision rather than baked into this abstraction.</summary>
    IEnumerable<string> EnumerateDirectories(string directory);
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

    public IEnumerable<string> EnumerateDirectories(string directory)
    {
        // Directory.GetDirectories (eager, not EnumerateDirectories) so every I/O
        // error surfaces right here inside the try — EnumerateDirectories defers
        // the actual work to iteration time, which would be outside this catch.
        string[] subdirectories;
        try
        {
            subdirectories = Directory.GetDirectories(directory);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // A locked-down or vanished subfolder shouldn't fail the whole walk.
            yield break;
        }

        foreach (var path in subdirectories)
        {
            // Reparse points (junctions/symlinks) are skipped rather than followed — a
            // circular junction would otherwise turn an unbounded FullDrive walk into an
            // infinite loop. CommonLocations mode has a depth cap as a second backstop,
            // but FullDrive mode has none, so this is the one that actually matters there.
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(path);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            yield return path;
        }
    }
}
