namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>E10-08 (ADR-0117): what a repost is and who may repost what.</summary>
public sealed class RepostRulesTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _author = Guid.CreateVersion7(_now);
    private static readonly Guid _reposter = Guid.CreateVersion7(_now.AddSeconds(1));
    private static readonly Guid _blog = Guid.CreateVersion7(_now.AddSeconds(2));

    [Fact]
    public void RepostIsPublishedAtOnceWithTheTrimmedTextAndNoBlocks()
    {
        var original = Published();

        var repost = Post.CreateRepost(_reposter, _blog, original.Id, "  Почитайте  ", _now);

        Assert.True(repost.IsRepost);
        Assert.Equal(original.Id, repost.RepostOfPostId);
        Assert.Equal("Почитайте", repost.RepostText);
        Assert.Equal(PostStatus.Published, repost.Status);
        Assert.Equal(_now, repost.PublishedAt);
        Assert.Null(repost.Title);
        Assert.Equal(Post.EmptyBody, repost.Body);
        Assert.False(original.IsRepost);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void BlankTextMakesARepostWithoutAText(string? text) =>
        Assert.Null(Post.CreateRepost(_reposter, _blog, Published().Id, text, _now).RepostText);

    [Fact]
    public void RepostIsNotEdited()
    {
        var repost = Post.CreateRepost(_reposter, _blog, Published().Id, null, _now);

        Assert.Throws<InvalidOperationException>(() => repost.Edit(_blog, "Заголовок", PostFactory.Body("Текст."), _now));
    }

    [Fact]
    public void OnlyAPublishedPostOfSomeoneElseIsRepostedWithAShortEnoughText()
    {
        var original = Published();
        var draft = Post.CreateDraft(_author, _blog, "Черновик", PostFactory.Body("Текст."), _now);
        var deleted = Published();
        deleted.Delete(_now);
        var repost = Post.CreateRepost(_reposter, _blog, original.Id, null, _now);

        Assert.Null(PostRules.RepostRefusalOf(original, _reposter, null));
        Assert.Null(PostRules.RepostRefusalOf(original, _reposter, new string('а', PostRules.RepostTextMaxLength)));
        Assert.Equal(RepostRefusal.OwnPost, PostRules.RepostRefusalOf(original, _author, null));
        Assert.Equal(RepostRefusal.NotFound, PostRules.RepostRefusalOf(draft, _reposter, null));
        Assert.Equal(RepostRefusal.NotFound, PostRules.RepostRefusalOf(deleted, _reposter, null));
        Assert.Equal(RepostRefusal.NotFound, PostRules.RepostRefusalOf(repost, _author, null));
        Assert.Equal(
            RepostRefusal.TextTooLong,
            PostRules.RepostRefusalOf(original, _reposter, new string('а', PostRules.RepostTextMaxLength + 1)));
    }

    private static Post Published()
    {
        var post = Post.CreateDraft(_author, _blog, "Пост", PostFactory.Body("Текст."), _now);
        post.Publish(_now);
        return post;
    }
}
