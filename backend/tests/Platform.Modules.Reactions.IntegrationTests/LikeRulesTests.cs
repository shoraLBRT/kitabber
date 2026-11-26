using Platform.Testing.Time;

namespace Platform.Modules.Reactions.IntegrationTests;

/// <summary>E10-01: the rules of a like and its model. No database.</summary>
public sealed class LikeRulesTests
{
    private static readonly DateTimeOffset _now = TestClock.Create().GetUtcNow();
    private static readonly Guid _reader = Guid.CreateVersion7(_now);
    private static readonly Guid _author = Guid.CreateVersion7(_now.AddSeconds(1));

    [Fact]
    public void AnotherAuthorsContentIsLiked() => Assert.Null(LikeRules.Refusal(_reader, _author));

    [Fact]
    public void OwnContentIsNotLiked() => Assert.Equal(LikeRefusal.OwnContent, LikeRules.Refusal(_author, _author));

    [Fact]
    public void GuestCommentIsNotLiked() => Assert.Equal(LikeRefusal.GuestComment, LikeRules.Refusal(_reader, null));

    [Fact]
    public void LikeOfAPostIsUnderThatPost()
    {
        var post = Guid.CreateVersion7(_now.AddSeconds(2));

        var like = Reaction.Like(ReactionTarget.Post, post, post, _reader, _now);

        Assert.Equal(ReactionKind.Like, like.Kind);
        Assert.Equal(post, like.PostId);
        Assert.Throws<ArgumentException>(
            () => Reaction.Like(ReactionTarget.Post, post, Guid.CreateVersion7(_now.AddSeconds(3)), _reader, _now));
        Assert.Throws<ArgumentException>(() => Reaction.Like(ReactionTarget.Comment, post, post, Guid.Empty, _now));
    }
}
