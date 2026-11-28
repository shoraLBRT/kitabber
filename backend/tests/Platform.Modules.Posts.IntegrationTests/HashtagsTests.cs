using Microsoft.EntityFrameworkCore;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// E11-08, ADR-0120: the hashtags of a post — what is a tag, its stored lower-case form, and that a post takes its tags
/// when it is published and again when a published post is edited.
/// </summary>
public sealed class HashtagsTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TagsAreTheWordsAfterAHashInLowerCaseEachOnceInTheOrderTheyCome()
    {
        Assert.Equal(
            ["кино", "film_2026", "ваш_выбор"],
            Hashtags.Of("Про #Кино и #FILM_2026.\n#кино снова, (#ваш_выбор)"));
    }

    [Theory]
    [InlineData("страница site.ru/page#part")]
    [InlineData("слово#внутри")]
    [InlineData("только цифры #2026")]
    [InlineData("решётка # одна")]
    [InlineData("двойная ##тег")]
    public void WordGluedToTextOrWithoutALetterIsNotATag(string text) => Assert.Empty(Hashtags.Of(text));

    [Fact]
    public void TooLongWordIsNotATagAndAPostKeepsAtMostTheFirstTwenty()
    {
        Assert.Empty(Hashtags.Of("#" + new string('а', Hashtags.MaxLength + 1)));
        Assert.Equal([new string('а', Hashtags.MaxLength)], Hashtags.Of("#" + new string('а', Hashtags.MaxLength)));

        var many = string.Join(' ', Enumerable.Range(0, 30).Select(i => $"#тег{i}"));
        Assert.Equal([.. Enumerable.Range(0, Hashtags.MaxPerPost).Select(i => $"тег{i}")], Hashtags.Of(many));
    }

    [Fact]
    public void TagsAreReadFromTheTextOfEveryBlockWithoutMarkup()
    {
        var body = PostFactory.Body("Про <b>#Котов</b> и&nbsp;#собак", "Кавычки &#39;не тег&#39;");

        Assert.Equal(["котов", "собак"], Hashtags.OfBody(body));
    }

    [Theory]
    [InlineData("Кино", "кино")]
    [InlineData("#КИНО", "кино")]
    [InlineData(" film_2026 ", "film_2026")]
    [InlineData("2026", null)]
    [InlineData("два слова", null)]
    [InlineData("", null)]
    [InlineData("#", null)]
    public void AskedTagIsReadInAnyCaseWithOrWithoutItsHash(string asked, string? stored) =>
        Assert.Equal(stored, Hashtags.Parse(asked));

    [Fact]
    public void PostTakesItsTagsWhenPublishedAndAgainWhenAPublishedPostIsEdited()
    {
        var author = Guid.CreateVersion7(_now);
        var blog = Guid.CreateVersion7(_now);
        var post = Post.CreateDraft(author, blog, "Заголовок", PostFactory.Body("Черновик про #Кино"), _now);
        Assert.Empty(post.Tags);

        post.Publish(_now);
        Assert.Equal(["кино"], post.Tags);

        post.Edit(blog, "Заголовок", PostFactory.Body("Теперь про #Театр"), _now.AddMinutes(1));
        Assert.Equal(["театр"], post.Tags);
    }

    [Fact]
    public void DraftEditedKeepsNoTags()
    {
        var blog = Guid.CreateVersion7(_now);
        var post = Post.CreateDraft(Guid.CreateVersion7(_now), blog, null, PostFactory.Body("Текст"), _now);

        post.Edit(blog, null, PostFactory.Body("Про #кино"), _now);

        Assert.Empty(post.Tags);
    }

    // The feed of a tag asks with the array overlap operator, which the GIN index of the tags serves.
    [Fact]
    public void FeedOfATagAsksTheIndexOfTheTags()
    {
        using var db = new PostsDesignTimeFactory().CreateDbContext([]);
        string[] wanted = ["кино"];

        var sql = db.Posts.Where(p => p.Tags.Any(t => wanted.Contains(t))).ToQueryString();

        Assert.Contains("&&", sql, StringComparison.Ordinal);
    }
}
