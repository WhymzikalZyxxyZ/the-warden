using TheWarden.Core.Rules;

namespace TheWarden.Core.Scanning;

/// <summary>A file the scanner classified as junk, plus enough evidence for the user to make their own call.</summary>
public sealed record FileFinding(
    string FullPath,
    JunkCategory Category,
    string RuleId,
    string RuleDescription,
    long SizeBytes,
    DateTime LastWriteTimeUtc,
    bool RequiresElevation);
