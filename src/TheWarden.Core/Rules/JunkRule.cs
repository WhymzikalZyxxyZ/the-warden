namespace TheWarden.Core.Rules;

/// <summary>
/// A single, declarative classification rule. Directory/file patterns use the same
/// simple glob syntax as <see cref="System.IO.Enumeration.FileSystemName.MatchesSimpleExpression"/>
/// (?, *) so rule packs stay human-editable JSON rather than requiring code changes.
/// </summary>
public sealed class JunkRule
{
    public required string Id { get; init; }
    public required JunkCategory Category { get; init; }
    public required string Description { get; init; }

    /// <summary>Environment-variable-expandable directory pattern, e.g. "%TEMP%" or "%LOCALAPPDATA%\Temp".</summary>
    public required string DirectoryPattern { get; init; }

    /// <summary>Simple glob for the file name, e.g. "*.tmp" or "*". Recycle Bin / crash dumps may use "*".</summary>
    public string FilePattern { get; init; } = "*";

    /// <summary>Files newer than this are left alone even if they otherwise match — avoids grabbing an in-progress download.</summary>
    public int MinAgeHours { get; init; } = 0;

    /// <summary>Whether classifying/removing files under this rule requires the process to be elevated.</summary>
    public bool RequiresElevation { get; init; }
}
