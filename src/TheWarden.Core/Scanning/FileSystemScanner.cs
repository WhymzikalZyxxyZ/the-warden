using TheWarden.Core.Rules;

namespace TheWarden.Core.Scanning;

/// <summary>
/// Walks the directories referenced by a <see cref="RulePack"/> and yields every file
/// the <see cref="JunkRuleEngine"/> classifies as junk. Read-only — scanning never
/// deletes or moves anything; that's <see cref="Quarantine.QuarantineManager"/>'s job.
/// </summary>
public sealed class FileSystemScanner(RulePack rulePack, IDirectoryReader directoryReader, IEnvironmentExpander environmentExpander, IClock clock)
{
    private readonly RulePack _rulePack = rulePack;
    private readonly IDirectoryReader _directoryReader = directoryReader;
    private readonly IEnvironmentExpander _environmentExpander = environmentExpander;
    private readonly IClock _clock = clock;
    private readonly JunkRuleEngine _ruleEngine = new(rulePack, environmentExpander);

    public FileSystemScanner(RulePack rulePack)
        : this(rulePack, new RealDirectoryReader(), new SystemEnvironmentExpander(), new SystemClock())
    {
    }

    public IReadOnlyList<FileFinding> Scan()
    {
        var findings = new List<FileFinding>();
        var utcNow = _clock.UtcNow;

        var expandedDirectories = _rulePack.Rules
            .Select(rule => _environmentExpander.Expand(rule.DirectoryPattern))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in expandedDirectories)
        {
            if (!_directoryReader.DirectoryExists(directory))
            {
                continue;
            }

            foreach (var entry in _directoryReader.EnumerateFiles(directory))
            {
                var rule = _ruleEngine.Match(entry.FullPath, entry.LastWriteTimeUtc, utcNow);
                if (rule is null)
                {
                    continue;
                }

                findings.Add(new FileFinding(
                    entry.FullPath,
                    rule.Category,
                    rule.Id,
                    rule.Description,
                    entry.SizeBytes,
                    entry.LastWriteTimeUtc,
                    rule.RequiresElevation));
            }
        }

        return findings;
    }
}
