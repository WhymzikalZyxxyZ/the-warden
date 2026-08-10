using System.Diagnostics.CodeAnalysis;
using System.IO.Enumeration;

namespace TheWarden.Core.Rules;

/// <summary>
/// Matches candidate files against a <see cref="RulePack"/>. Pure, side-effect-free,
/// and takes "now" as a parameter so age-based rules are deterministically testable.
/// </summary>
public sealed class JunkRuleEngine(RulePack rulePack, IEnvironmentExpander environmentExpander)
{
    private readonly RulePack _rulePack = rulePack;
    private readonly IEnvironmentExpander _environmentExpander = environmentExpander;

    public JunkRuleEngine(RulePack rulePack) : this(rulePack, new SystemEnvironmentExpander())
    {
    }

    /// <summary>
    /// Returns the first rule that classifies <paramref name="filePath"/> as junk, or
    /// null if no rule matches. Age comparisons use <paramref name="utcNow"/> so tests
    /// don't depend on wall-clock time.
    /// </summary>
    public JunkRule? Match(string filePath, DateTime lastWriteTimeUtc, DateTime utcNow)
    {
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var fileName = Path.GetFileName(filePath);

        foreach (var rule in _rulePack.Rules)
        {
            var expandedDirectory = _environmentExpander.Expand(rule.DirectoryPattern)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!string.Equals(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    expandedDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!FileSystemName.MatchesSimpleExpression(rule.FilePattern, fileName))
            {
                continue;
            }

            var age = utcNow - lastWriteTimeUtc;
            if (age.TotalHours < rule.MinAgeHours)
            {
                continue;
            }

            return rule;
        }

        return null;
    }
}

/// <summary>Abstracts environment-variable expansion so rule matching is testable without touching the real environment.</summary>
public interface IEnvironmentExpander
{
    string Expand(string pattern);
}

[ExcludeFromCodeCoverage]
public sealed class SystemEnvironmentExpander : IEnvironmentExpander
{
    public string Expand(string pattern) => Environment.ExpandEnvironmentVariables(pattern);
}
