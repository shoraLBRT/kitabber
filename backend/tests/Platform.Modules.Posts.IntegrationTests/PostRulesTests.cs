using Platform.Modules.Blogs.Contracts;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>E07-01: the rules of a post — its origin, the soft deletion and who may do what.</summary>
public sealed class PostRulesTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _author = Guid.CreateVersion7(_now);
    private static readonly Guid _stranger = Guid.CreateVersion7(_now.AddSeconds(1));
    private static readonly Guid _blog = Guid.CreateVersion7(_now.AddSeconds(2));
    private static readonly string _body = PostFactory.Body("Текст.");

    [Fact]
    public void NewPostIsANativeDraftWithoutASource()
    {
        var post = Post.CreateDraft(_author, _blog, "  Заголовок  ", _body, _now);

        Assert.Equal(PostStatus.Draft, post.Status);
        Assert.Equal(PostOrigin.Native, post.Origin);
        Assert.Null(post.Source);
        Assert.Equal("Заголовок", post.Title);
        Assert.Equal(7, post.Id.Version);
        Assert.Equal(_now, post.CreatedAt);
        Assert.Equal(_now, post.UpdatedAt);
        Assert.Null(post.PublishedAt);
        Assert.False(post.IsDeleted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankTitleMakesAShortPost(string? title) =>
        Assert.Null(Post.CreateDraft(_author, _blog, title, _body, _now).Title);

    [Fact]
    public void CarriedOverPostKeepsItsTelegramSource()
    {
        var source = new PostSource("-1001234567890", "42", new Uri("https://t.me/channel/42"));

        var post = Post.CreateImported(_author, _blog, null, _body, source, _now);

        Assert.Equal(PostOrigin.Telegram, post.Origin);
        Assert.Equal(source, post.Source);
    }

    [Fact]
    public void CarriedOverPostMayLackTheLinkButNotTheMessage()
    {
        var post = Post.CreateImported(_author, _blog, null, _body, new PostSource("channel", "7", Url: null), _now);

        Assert.Null(post.Source!.Url);
        Assert.Throws<ArgumentException>(() =>
            Post.CreateImported(_author, _blog, null, _body, new PostSource("channel", " ", null), _now));
    }

    [Fact]
    public void PostNeedsAnAuthorABlogAndABody()
    {
        Assert.Throws<ArgumentException>(() => Post.CreateDraft(Guid.Empty, _blog, null, _body, _now));
        Assert.Throws<ArgumentException>(() => Post.CreateDraft(_author, Guid.Empty, null, _body, _now));
        Assert.Throws<ArgumentException>(() => Post.CreateDraft(_author, _blog, null, " ", _now));
    }

    [Fact]
    public void PublishingSetsTheTimeOnce()
    {
        var post = Post.CreateDraft(_author, _blog, null, _body, _now);

        post.Publish(_now.AddMinutes(1));
        post.Publish(_now.AddMinutes(2));

        Assert.Equal(PostStatus.Published, post.Status);
        Assert.Equal(_now.AddMinutes(1), post.PublishedAt);
    }

    [Fact]
    public void DeletionIsSoftAndADeletedPostNoLongerChanges()
    {
        var post = Post.CreateDraft(_author, _blog, null, _body, _now);

        post.Delete(_now.AddMinutes(1));
        post.Delete(_now.AddMinutes(2));

        Assert.True(post.IsDeleted);
        Assert.Equal(_now.AddMinutes(1), post.DeletedAt);
        Assert.Throws<InvalidOperationException>(() => post.Edit(_blog, "Новый", _body, _now.AddMinutes(3)));
        Assert.Throws<InvalidOperationException>(() => post.Publish(_now.AddMinutes(3)));
        Assert.False(PostRules.MaySee(post, _author));
        Assert.False(PostRules.MayChange(post, _author));
    }

    [Fact]
    public void DraftIsSeenByItsAuthorOnlyAndPublishedPostByEveryone()
    {
        var post = Post.CreateDraft(_author, _blog, null, _body, _now);

        Assert.True(PostRules.MaySee(post, _author));
        Assert.False(PostRules.MaySee(post, _stranger));
        Assert.False(PostRules.MaySee(post, null));

        post.Publish(_now);

        Assert.True(PostRules.MaySee(post, _stranger));
        Assert.True(PostRules.MaySee(post, null));
    }

    [Fact]
    public void OnlyTheAuthorChangesAPost()
    {
        var post = Post.CreateDraft(_author, _blog, null, _body, _now);
        post.Publish(_now);

        Assert.True(PostRules.MayChange(post, _author));
        Assert.False(PostRules.MayChange(post, _stranger));
    }

    [Fact]
    public void PersonalBlogTakesPostsOfItsOwnerAndATopicOfAnyone()
    {
        var personal = new BlogSummary(_blog, BlogKind.Personal, _author);
        var topic = new BlogSummary(_blog, BlogKind.Topic, OwnerAccountId: null);

        Assert.True(PostRules.MayPostIn(personal, _author));
        Assert.False(PostRules.MayPostIn(personal, _stranger));
        Assert.True(PostRules.MayPostIn(topic, _stranger));
    }
}
