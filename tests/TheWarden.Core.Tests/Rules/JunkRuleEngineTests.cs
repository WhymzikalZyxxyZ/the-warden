using TheWarden.Core.Rules;

namespace TheWarden.Core.Tests.Rules;

public class JunkRuleEngineTests
{
    private static readonly DateTime UtcNow = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FakeExpander : IEnvironmentExpander
    {
        public string Expand(string pattern) => pattern switch
        {
            "%TEMP%" => @"C:\Users\test\AppData\Local\Temp",
            "%SystemRoot%\\Temp" => @"C:\Windows\Temp",
            _ => pattern,
        };
    }

    private static RulePack SinglePack(int minAgeHours = 0, string filePattern = "*.tmp") => new()
    {
        Version = "test",
        Rules =
        [
            new JunkRule
            {
                Id = "user-temp",
                Category = JunkCategory.UserTemp,
                Description = "test rule",
                DirectoryPattern = "%TEMP%",
                FilePattern = filePattern,
                MinAgeHours = minAgeHours,
            },
        ],
    };

    [Fact]
    public void Match_returns_rule_when_directory_and_pattern_match()
    {
        var engine = new JunkRuleEngine(SinglePack(), new FakeExpander());

        var rule = engine.Match(@"C:\Users\test\AppData\Local\Temp\scratch.tmp", UtcNow, UtcNow);

        Assert.NotNull(rule);
        Assert.Equal("user-temp", rule!.Id);
    }

    [Fact]
    public void Match_returns_null_when_directory_does_not_match()
    {
        var engine = new JunkRuleEngine(SinglePack(), new FakeExpander());

        var rule = engine.Match(@"C:\Users\test\Documents\scratch.tmp", UtcNow, UtcNow);

        Assert.Null(rule);
    }

    [Fact]
    public void Match_returns_null_when_file_pattern_does_not_match()
    {
        var engine = new JunkRuleEngine(SinglePack(filePattern: "*.tmp"), new FakeExpander());

        var rule = engine.Match(@"C:\Users\test\AppData\Local\Temp\keepme.docx", UtcNow, UtcNow);

        Assert.Null(rule);
    }

    [Fact]
    public void Match_returns_null_when_file_is_younger_than_min_age()
    {
        var engine = new JunkRuleEngine(SinglePack(minAgeHours: 24), new FakeExpander());
        var lastWrite = UtcNow.AddHours(-1);

        var rule = engine.Match(@"C:\Users\test\AppData\Local\Temp\scratch.tmp", lastWrite, UtcNow);

        Assert.Null(rule);
    }

    [Fact]
    public void Match_returns_rule_when_file_is_older_than_min_age()
    {
        var engine = new JunkRuleEngine(SinglePack(minAgeHours: 24), new FakeExpander());
        var lastWrite = UtcNow.AddHours(-25);

        var rule = engine.Match(@"C:\Users\test\AppData\Local\Temp\scratch.tmp", lastWrite, UtcNow);

        Assert.NotNull(rule);
    }
}
