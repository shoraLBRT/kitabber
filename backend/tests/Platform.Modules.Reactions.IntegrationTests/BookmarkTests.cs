namespace Platform.Modules.Reactions.IntegrationTests;

/// <summary>E10-07: the rules of a bookmark itself (ADR-0113).</summary>
public sealed class BookmarkTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ABookmarkOnAPostIsUnderThatPostAndKeepsItsAuthor()
    {
        var post = Guid.NewGuid();
        var author = Guid.NewGuid();
        var account = Guid.NewGuid();

        var bookmark = Bookmark.Put(ReactionTarget.Post, post, post, account, author, _now);

        Assert.Equal((post, post, account, author, _now), (bookmark.TargetId, bookmark.PostId, bookmark.AccountId,
            bookmark.TargetAuthorId, bookmark.CreatedAt));
        Assert.Throws<ArgumentException>(
            () => Bookmark.Put(ReactionTarget.Post, post, Guid.NewGuid(), account, author, _now));
        Assert.Throws<ArgumentException>(() => Bookmark.Put(ReactionTarget.Post, post, post, account, null, _now));
    }

    [Fact]
    public void AGuestCommentHasNoAuthorAccount()
    {
        var bookmark = Bookmark.Put(ReactionTarget.Comment, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, _now);

        Assert.Null(bookmark.TargetAuthorId);
    }

    [Fact]
    public void ABookmarkNeedsItsTargetPostAndAccount()
    {
        var id = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => Bookmark.Put(ReactionTarget.Comment, Guid.Empty, id, id, null, _now));
        Assert.Throws<ArgumentException>(() => Bookmark.Put(ReactionTarget.Comment, id, Guid.Empty, id, null, _now));
        Assert.Throws<ArgumentException>(() => Bookmark.Put(ReactionTarget.Comment, id, id, Guid.Empty, null, _now));
    }
}
