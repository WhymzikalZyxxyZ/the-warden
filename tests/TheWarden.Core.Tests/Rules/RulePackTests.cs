using TheWarden.Core.Rules;

namespace TheWarden.Core.Tests.Rules;

public class RulePackTests
{
    [Fact]
    public void Default_rules_all_have_unique_ids()
    {
        var pack = RulePack.Default();

        var distinctIds = pack.Rules.Select(r => r.Id).Distinct().Count();

        Assert.Equal(pack.Rules.Count, distinctIds);
    }

    [Fact]
    public void Default_pack_is_non_empty_and_versioned()
    {
        var pack = RulePack.Default();

        Assert.NotEmpty(pack.Rules);
        Assert.False(string.IsNullOrWhiteSpace(pack.Version));
    }
}
