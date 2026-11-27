using Platform.Http.Pagination;
using Platform.Modules.Posts.Contracts;
using Platform.Testing.Time;

namespace Platform.Modules.Comments.IntegrationTests;

/// <summary>E09-15: the rules of the tab «Комментарии» of a blog and a topic (ADR-0114). No database.</summary>
public sealed class BlogCommentsTests
{
    private static readonly DateTimeOffset _now = TestClock.Create().GetUtcNow();
    private static readonly Guid _author = Guid.CreateVersion7(_now);
    private static readonly Guid _other = Guid.CreateVersion7(_now.AddSeconds(1));
    private static readonly PostHeadline _post = new(Guid.CreateVersion7(_now.AddSeconds(2)), Guid.CreateVersion7(_now), "Пост", "post");

    [Fact]
    public void ShownKeepsTheOrderAndGivesThePostOfEachComment()
    {
        var first = Native(_author, _post.Id, 0);
        var second = Native(_other, _post.Id, 1);

        var shown = BlogComments.Shown([second, first], Published(_post), new HashSet<Guid>()).ToList();

        Assert.Equal([second.Id, first.Id], shown.Select(c => c.Id));
        Assert.All(shown, c =>
        {
            Assert.Equal(_post.BlogId, c.PostBlogId);
            Assert.Equal("Пост", c.PostTitle);
            Assert.Equal("post", c.PostSlug);
        });
        Assert.Equal("native", shown[0].Kind);
    }

    [Fact]
    public void ShownLeavesOutDeletedCommentsUnpublishedPostsAndBlockedAuthors()
    {
        var kept = Native(_author, _post.Id, 0);
        var deleted = Native(_author, _post.Id, 1);
        deleted.Delete(_now.AddMinutes(1));
        var underDraft = Native(_author, Guid.CreateVersion7(_now.AddSeconds(5)), 2);
        var byBlocked = Native(_other, _post.Id, 3);

        var shown = BlogComments.Shown(
            [kept, deleted, underDraft, byBlocked], Published(_post), new HashSet<Guid> { _other });

        Assert.Equal([kept.Id], shown.Select(c => c.Id));
    }

    [Fact]
    public void ShownGivesTheGuestAuthor()
    {
        var guest = Comment.CreateGuest(
            _post.Id, parent: null, new GuestAuthor("Иван", null), new CommentSource("-100", "7"), "Из канала", _now);

        var shown = Assert.Single(BlogComments.Shown([guest], Published(_post), new HashSet<Guid>()));

        Assert.Equal("guest", shown.Kind);
        Assert.Null(shown.AuthorAccountId);
        Assert.Equal("Иван", shown.Guest?.Name);
    }

    [Fact]
    public void PageWithMoreThanTheLimitEndsWithTheCursorOfItsLastComment()
    {
        var kept = Views(3);

        var page = BlogComments.Page(kept, limit: 2, lastSeen: null, exhausted: false);

        Assert.Equal([kept[0].Id, kept[1].Id], page.Items.Select(c => c.Id));
        Assert.Equal(new BlogCommentCursor(kept[1].CreatedAt, kept[1].Id), Decode(page.NextCursor));
    }

    [Fact]
    public void PageStoppedBeforeTheEndGoesOnAfterTheLastCommentLookedAt()
    {
        var kept = Views(1);
        var lastSeen = new BlogCommentCursor(_now.AddSeconds(-30), Guid.CreateVersion7(_now.AddSeconds(-30)));

        var page = BlogComments.Page(kept, limit: 2, lastSeen, exhausted: false);

        Assert.Single(page.Items);
        Assert.Equal(lastSeen, Decode(page.NextCursor));
    }

    [Fact]
    public void LastPageHasNoCursor()
    {
        var lastSeen = new BlogCommentCursor(_now, Guid.CreateVersion7(_now));

        var page = BlogComments.Page(Views(2), limit: 2, lastSeen, exhausted: true);

        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    private static BlogCommentCursor Decode(string? cursor) =>
        CursorCodec.Decode<BlogCommentCursor>(Assert.IsType<string>(cursor));

    private static Dictionary<Guid, PostHeadline> Published(PostHeadline post) => new() { [post.Id] = post };

    private static Comment Native(Guid author, Guid postId, int second) =>
        Comment.CreateNative(author, postId, parent: null, $"Текст {second}", _now.AddSeconds(second));

    private static List<BlogCommentView> Views(int count) =>
    [
        .. BlogComments.Shown(
            Enumerable.Range(0, count).Select(n => Native(_author, _post.Id, -n)),
            Published(_post),
            new HashSet<Guid>()),
    ];
}
