namespace TheWarden.Core.Scanning;

public readonly record struct FileEntry(string FullPath, DateTime LastWriteTimeUtc, long SizeBytes);
