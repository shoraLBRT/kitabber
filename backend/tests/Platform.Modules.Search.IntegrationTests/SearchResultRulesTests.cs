using System.Text.Json.Nodes;

namespace Platform.Modules.Search.IntegrationTests;

/// <summary>
/// E14-03, ADR-0124: the rules of the results page — which filters are taken, when a period starts, what the fake index
/// finds with the filters, and how the request to OpenSearch carries them.
/// </summary>
public sealed class SearchResultRulesTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _anna = Guid.CreateVersion7();
    private static readonly Guid _boris = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(null, SearchPeriod.All)]
    [InlineData("", SearchPeriod.All)]
    [InlineData("all", SearchPeriod.All)]
    [InlineData("day", SearchPeriod.Day)]
    [InlineData("week", SearchPeriod.Week)]
    [InlineData("month", SearchPeriod.Month)]
    [InlineData("year", SearchPeriod.Year)]
    public void KnownPeriodIsTaken(string? period, SearchPeriod expected) =>
        Assert.Equal(expected, SearchResultEndpoints.ParsePeriod(period));

    [Theory]
    [InlineData("century")]
    [InlineData("Week")]
    public void UnknownPeriodIsRefused(string period) => Assert.Null(SearchResultEndpoints.ParsePeriod(period));

    [Theory]
    [InlineData(null, SearchOrder.Relevance)]
    [InlineData("relevance", SearchOrder.Relevance)]
    [InlineData("date", SearchOrder.Date)]
    public void KnownOrderIsTaken(string? order, SearchOrder expected) =>
        Assert.Equal(expected, SearchResultEndpoints.ParseOrder(order));

    [Fact]
    public void UnknownOrderAndKindAreRefused()
    {
        Assert.Null(SearchResultEndpoints.ParseOrder("rating"));
        Assert.Null(SearchResultEndpoints.ParseKind(null));
        Assert.Null(SearchResultEndpoints.ParseKind("blog"));
        Assert.Equal(IndexedBlog.TopicKind, SearchResultEndpoints.ParseKind("topic"));
        Assert.Equal(IndexedBlog.PersonalKind, SearchResultEndpoints.ParseKind("personal"));
    }

    [Fact]
    public void PeriodStartsThatLongBeforeNow()
    {
        Assert.Null(SearchResultEndpoints.Since(SearchPeriod.All, _now));
        Assert.Equal(_now.AddDays(-1), SearchResultEndpoints.Since(SearchPeriod.Day, _now));
        Assert.Equal(_now.AddDays(-7), SearchResultEndpoints.Since(SearchPeriod.Week, _now));
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero), SearchResultEndpoints.Since(SearchPeriod.Month, _now));
        Assert.Equal(new DateTimeOffset(2025, 11, 28, 12, 0, 0, TimeSpan.Zero), SearchResultEndpoints.Since(SearchPeriod.Year, _now));
    }

    [Fact]
    public async Task FakeIndexFiltersPostsByTitlePeriodAndBlackListAndOrdersThemByDate()
    {
        var index = new InMemorySearchIndex();
        var old = Post(_anna, "Кофе по утрам", "Текст", _now.AddDays(-30));
        var fresh = Post(_anna, "Утро", "Про кофе в тексте", _now.AddHours(-2));
        var blocked = Post(_boris, "Кофе Бориса", "Текст", _now.AddHours(-1));
        foreach (var post in new[] { old, fresh, blocked })
        {
            await index.PutAsync(post, Ct);
        }

        var all = await index.FindAsync(SearchIndexKind.Posts, new SearchQuery("кофе", 0, 10, SearchOrder.Date), Ct);
        var titles = await index.FindAsync(SearchIndexKind.Posts, new SearchQuery("кофе", 0, 10, TitlesOnly: true), Ct);
        var week = await index.FindAsync(
            SearchIndexKind.Posts, new SearchQuery("кофе", 0, 10, PublishedSince: _now.AddDays(-7)), Ct);
        var withoutBoris = await index.FindAsync(
            SearchIndexKind.Posts, new SearchQuery("кофе", 0, 10, ExcludedAccounts: [_boris]), Ct);
        var secondPage = await index.FindAsync(SearchIndexKind.Posts, new SearchQuery("кофе", 1, 1, SearchOrder.Date), Ct);

        Assert.Equal([blocked.Id, fresh.Id, old.Id], all.Select(d => d.Id));
        Assert.Equal(new[] { old.Id, blocked.Id }.Order(), titles.Select(d => d.Id).Order());
        Assert.Equal(new[] { fresh.Id, blocked.Id }.Order(), week.Select(d => d.Id).Order());
        Assert.Equal(new[] { old.Id, fresh.Id }.Order(), withoutBoris.Select(d => d.Id).Order());
        Assert.Equal([fresh.Id], secondPage.Select(d => d.Id));
    }

    [Fact]
    public async Task FakeIndexKeepsBlogsOfTheKindAskedAndTopicsOfABlockedOwner()
    {
        var index = new InMemorySearchIndex();
        var annaBlog = new IndexedBlog(Guid.CreateVersion7(), IndexedBlog.PersonalKind, _anna, "Сад Анны", null, null);
        var borisBlog = new IndexedBlog(Guid.CreateVersion7(), IndexedBlog.PersonalKind, _boris, "Сад Бориса", null, null);
        var borisTopic = new IndexedBlog(Guid.CreateVersion7(), IndexedBlog.TopicKind, _boris, "Сад и огород", null, null);
        foreach (var blog in new[] { annaBlog, borisBlog, borisTopic })
        {
            await index.PutAsync(blog, Ct);
        }

        var personal = await index.FindAsync(
            SearchIndexKind.Blogs,
            new SearchQuery("сад", 0, 10, BlogKind: IndexedBlog.PersonalKind, ExcludedAccounts: [_boris]),
            Ct);
        var topics = await index.FindAsync(
            SearchIndexKind.Blogs,
            new SearchQuery("сад", 0, 10, BlogKind: IndexedBlog.TopicKind, ExcludedAccounts: [_boris]),
            Ct);

        Assert.Equal([annaBlog.Id], personal.Select(d => d.Id));
        Assert.Equal([borisTopic.Id], topics.Select(d => d.Id));
    }

    [Fact]
    public void OpenSearchRequestCarriesTheFiltersOfPosts()
    {
        var since = _now.AddDays(-7);

        var request = OpenSearchIndex.FindRequest(
            SearchIndexKind.Posts,
            new SearchQuery("кофе", 20, 11, SearchOrder.Date, TitlesOnly: true, PublishedSince: since, ExcludedAccounts: [_boris]));

        Assert.Equal(20, request["from"]!.GetValue<int>());
        Assert.Equal(11, request["size"]!.GetValue<int>());
        var query = request["query"]!["bool"]!;
        Assert.Equal(["title"], query["must"]!["multi_match"]!["fields"]!.AsArray().Select(f => f!.GetValue<string>()));
        Assert.Equal(
            since.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            query["filter"]![0]!["range"]!["publishedAt"]!["gte"]!.GetValue<string>());
        Assert.Equal(
            _boris.ToString("D"),
            query["must_not"]![0]!["terms"]!["authorAccountId"]![0]!.GetValue<string>());
        Assert.Equal("desc", request["sort"]![0]!["publishedAt"]!["order"]!.GetValue<string>());
    }

    [Fact]
    public void OpenSearchRequestOfBlogsFiltersTheKindAndLeavesOutOnlyPersonalBlogsOfTheBlocked()
    {
        var request = OpenSearchIndex.FindRequest(
            SearchIndexKind.Blogs,
            new SearchQuery("сад", 0, 6, SearchOrder.Date, TitlesOnly: true, BlogKind: IndexedBlog.TopicKind, ExcludedAccounts: [_boris]));

        var query = request["query"]!["bool"]!;
        // «Только заголовки» and the order by date are for posts: blogs are matched by all their fields, by relevance.
        Assert.Equal(3, query["must"]!["multi_match"]!["fields"]!.AsArray().Count);
        Assert.Null(request["sort"]);
        Assert.Equal(IndexedBlog.TopicKind, query["filter"]![0]!["term"]!["kind"]!.GetValue<string>());
        var blockedPersonal = query["must_not"]![0]!["bool"]!["filter"]!.AsArray();
        Assert.Equal(IndexedBlog.PersonalKind, blockedPersonal[0]!["term"]!["kind"]!.GetValue<string>());
        Assert.Equal(_boris.ToString("D"), blockedPersonal[1]!["terms"]!["ownerAccountId"]![0]!.GetValue<string>());
    }

    [Fact]
    public void OpenSearchRequestWithoutFiltersHasEmptyFilterLists()
    {
        var request = OpenSearchIndex.FindRequest(SearchIndexKind.Posts, new SearchQuery("кофе", 0, 11));

        var query = request["query"]!["bool"]!;
        Assert.Empty(query["filter"]!.AsArray());
        Assert.Empty(query["must_not"]!.AsArray());
        Assert.Equal(2, query["must"]!["multi_match"]!["fields"]!.AsArray().Count);
        Assert.IsType<JsonObject>(request);
    }

    private static IndexedPost Post(Guid author, string title, string text, DateTimeOffset at) =>
        new(Guid.CreateVersion7(), Guid.NewGuid(), author, title, text, at);
}
