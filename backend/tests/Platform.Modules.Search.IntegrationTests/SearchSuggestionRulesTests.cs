namespace Platform.Modules.Search.IntegrationTests;

/// <summary>
/// E14-02, ADR-0097: the rules of the suggestions — how a query is trimmed and when it is refused, how a post's text
/// becomes an excerpt, and which found blogs a reader with a black list sees.
/// </summary>
public sealed class SearchSuggestionRulesTests
{
    [Theory]
    [InlineData("кот", "кот")]
    [InlineData("  кот   учёный ", "кот учёный")]
    [InlineData("кот\tучёный", "кот учёный")]
    public void QueryIsTrimmedWithItsSpacesCollapsed(string q, string expected)
    {
        var (query, problem) = SearchEndpoints.Normalize(q);

        Assert.Equal(expected, query);
        Assert.Null(problem);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void BlankQueryIsRefused(string? q) => Assert.NotNull(SearchEndpoints.Normalize(q).Problem);

    [Fact]
    public void QueryOfTheLongestLengthIsTakenAndALongerOneRefused()
    {
        Assert.NotNull(SearchEndpoints.Normalize(new string('а', SearchEndpoints.MaxQueryLength)).Query);
        Assert.NotNull(SearchEndpoints.Normalize(new string('а', SearchEndpoints.MaxQueryLength + 1)).Problem);
        // Spaces around do not count.
        Assert.NotNull(SearchEndpoints.Normalize($"  {new string('а', SearchEndpoints.MaxQueryLength)}  ").Query);
    }

    [Fact]
    public void ShortTextIsItsOwnExcerptOnOneLine() =>
        Assert.Equal("Первая строка вторая строка", SearchEndpoints.Excerpt("Первая строка\nвторая   строка"));

    [Fact]
    public void LongTextIsCutAtAWordWithAnEllipsis()
    {
        var text = string.Join(' ', Enumerable.Repeat("слово", 60));

        var excerpt = SearchEndpoints.Excerpt(text);

        Assert.True(excerpt.Length <= SearchEndpoints.ExcerptLength, excerpt);
        Assert.EndsWith("слово…", excerpt, StringComparison.Ordinal);
    }

    [Fact]
    public void LongWordIsCutWhereTheExcerptEnds()
    {
        var excerpt = SearchEndpoints.Excerpt(new string('а', 300));

        Assert.Equal(SearchEndpoints.ExcerptLength, excerpt.Length);
        Assert.EndsWith("…", excerpt, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockedOwnersBlogIsHiddenButATopicNever()
    {
        var owner = Guid.NewGuid();
        HashSet<Guid> blocked = [owner];

        Assert.False(SearchEndpoints.Shows(new IndexedBlog(Guid.NewGuid(), IndexedBlog.PersonalKind, owner, "Блог", null, null), blocked));
        Assert.True(SearchEndpoints.Shows(new IndexedBlog(Guid.NewGuid(), IndexedBlog.TopicKind, owner, "Тема", null, null), blocked));
        Assert.True(SearchEndpoints.Shows(new IndexedBlog(Guid.NewGuid(), IndexedBlog.PersonalKind, Guid.NewGuid(), "Другой", null, null), blocked));
        Assert.True(SearchEndpoints.Shows(new IndexedBlog(Guid.NewGuid(), IndexedBlog.TopicKind, null, "Старая тема", null, null), blocked));
    }
}
