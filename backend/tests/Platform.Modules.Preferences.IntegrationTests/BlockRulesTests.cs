using Platform.Modules.Preferences.Blocks;

namespace Platform.Modules.Preferences.IntegrationTests;

/// <summary>E11-06 (ADR-0091): the rules of the black list. No database.</summary>
public sealed class BlockRulesTests
{
    private static readonly Guid _owner = Guid.CreateVersion7();
    private static readonly Guid _other = Guid.CreateVersion7();

    [Fact]
    public void AnotherAccountMayBeBlocked() => Assert.Null(BlockRules.BlockProblem(_owner, _other, 0));

    [Fact]
    public void OneselfMayNotBeBlocked() => Assert.NotNull(BlockRules.BlockProblem(_owner, _owner, 0));

    [Fact]
    public void EmptyAccountMayNotBeBlocked() => Assert.NotNull(BlockRules.BlockProblem(_owner, Guid.Empty, 0));

    [Fact]
    public void FullListTakesNoMore()
    {
        Assert.Null(BlockRules.BlockProblem(_owner, _other, BlockRules.MaxBlocked - 1));
        Assert.NotNull(BlockRules.BlockProblem(_owner, _other, BlockRules.MaxBlocked));
    }
}
