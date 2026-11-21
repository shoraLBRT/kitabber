namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>E07-02: the slug of a post's address, transliterated from its title (ADR-0041).</summary>
public sealed class PostSlugTests
{
    [Theory]
    [InlineData("Привет, мир!", "privet-mir")]
    [InlineData("Щука и ёжик: съешь же ещё этих мягких булок", "shchuka-i-ezhik-sesh-zhe-eshche-etih-myagkih-bulok")]
    [InlineData("Чайник, Юла и Яйцо", "chaynik-yula-i-yayco")]
    [InlineData("  Rust 1.80 — что нового?  ", "rust-1-80-chto-novogo")]
    [InlineData("Café déjà vu", "cafe-deja-vu")]
    [InlineData("Їжак і ґанок", "yizhak-i-ganok")]
    [InlineData("C#, F# и .NET", "c-f-i-net")]
    [InlineData("100%", "100")]
    [InlineData("Котики🙂собачки", "kotiki-sobachki")]
    public void TitleBecomesLatinWordsJoinedByHyphens(string title, string slug) =>
        Assert.Equal(slug, PostSlug.From(title));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!! ??? 🙂")]
    public void PostWithoutWordsInTheTitleHasNoSlug(string? title) => Assert.Null(PostSlug.From(title));

    [Fact]
    public void LongSlugIsCutAtTheLastWordThatFits()
    {
        var slug = PostSlug.From(string.Join(' ', Enumerable.Repeat("слово", 20)));

        Assert.NotNull(slug);
        Assert.True(slug.Length <= PostSlug.MaxLength, slug);
        Assert.False(slug.EndsWith('-'));
        Assert.All(slug.Split('-'), word => Assert.Equal("slovo", word));
    }

    [Fact]
    public void OneLongWordIsCutAtTheLimit()
    {
        var slug = PostSlug.From(new string('я', 40));

        Assert.Equal(PostSlug.MaxLength, slug!.Length);
    }

    [Fact]
    public void SlugFollowsTheTitle()
    {
        var now = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var post = Post.CreateDraft(Guid.CreateVersion7(now), Guid.CreateVersion7(now), "Старый", PostFactory.Body("a"), now);

        post.Edit(post.BlogId, "Новый заголовок", PostFactory.Body("a"), now);

        Assert.Equal("novyy-zagolovok", PostSlug.From(post.Title));
    }
}
