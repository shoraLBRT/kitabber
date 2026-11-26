namespace Platform.Modules.Reactions.IntegrationTests;

/// <summary>E10-05: the reference of reaction weights and the rating it gives (ADR-0081). No database.</summary>
public sealed class ReactionWeightsTests
{
    [Fact]
    public void EveryReactionKindHasAWeightThatNeverLowersARating() =>
        Assert.All(Enum.GetValues<ReactionKind>(), kind => Assert.True(ReactionWeights.Of(kind) >= 0));

    [Fact]
    public void LikeWeighsOne() => Assert.Equal(1, ReactionWeights.Of(ReactionKind.Like));

    [Fact]
    public void RatingIsTheSumOfTheReactionsByTheirWeights()
    {
        Assert.Equal(0, ReactionWeights.RatingOf([]));
        Assert.Equal(
            3 * ReactionWeights.Of(ReactionKind.Like),
            ReactionWeights.RatingOf([new KeyValuePair<ReactionKind, int>(ReactionKind.Like, 3)]));
    }

    [Fact]
    public void UnknownKindOrNegativeCountIsAnError()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReactionWeights.Of((ReactionKind)999));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ReactionWeights.RatingOf([new KeyValuePair<ReactionKind, int>(ReactionKind.Like, -1)]));
    }
}
