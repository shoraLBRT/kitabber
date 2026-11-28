using Platform.Modules.Preferences.Hidden;

namespace Platform.Modules.Preferences.IntegrationTests;

/// <summary>E11-07 (ADR-0119): the rules of hiding posts and topics from the feeds. No database.</summary>
public sealed class HiddenRulesTests
{
    private static readonly Guid _id = Guid.CreateVersion7();

    [Fact]
    public void APostAndATopicMayBeHidden()
    {
        Assert.Null(HiddenRules.HidePostProblem(_id, 0));
        Assert.Null(HiddenRules.HideTopicProblem(_id, 0));
    }

    [Fact]
    public void NothingIsHiddenWithoutAnIdentifier()
    {
        Assert.NotNull(HiddenRules.HidePostProblem(Guid.Empty, 0));
        Assert.NotNull(HiddenRules.HideTopicProblem(Guid.Empty, 0));
    }

    [Fact]
    public void FullListTakesNoMore()
    {
        Assert.Null(HiddenRules.HidePostProblem(_id, HiddenRules.MaxHidden - 1));
        Assert.NotNull(HiddenRules.HidePostProblem(_id, HiddenRules.MaxHidden));
        Assert.Null(HiddenRules.HideTopicProblem(_id, HiddenRules.MaxHidden - 1));
        Assert.NotNull(HiddenRules.HideTopicProblem(_id, HiddenRules.MaxHidden));
    }
}
