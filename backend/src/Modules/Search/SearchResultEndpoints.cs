using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Http.RateLimiting;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Media.Contracts;
using Platform.Modules.Preferences.Contracts;

namespace Platform.Modules.Search;

/// <summary>A personal blog or a topic in the block «Блоги» or «Темы» of the results (E14-03, ADR-0124).</summary>
/// <param name="Id">The blog.</param>
/// <param name="Type"><c>personal</c> or <c>topic</c>, as in the subscription lists.</param>
/// <param name="Name">The name; <c>null</c> until the owner sets it.</param>
/// <param name="Nickname">The short address without <c>@</c>; <c>null</c> until the owner sets it.</param>
/// <param name="AvatarSources">The avatar for every screen density, narrowest first; empty without an avatar.</param>
/// <param name="Subscribed">The reader is subscribed to it; always <c>false</c> for a guest.</param>
/// <param name="Own">It is the reader's own personal blog: the row has no «Подписаться».</param>
public sealed record FoundBlogView(
    Guid Id,
    string Type,
    string? Name,
    string? Nickname,
    IReadOnlyList<ImageSource> AvatarSources,
    bool Subscribed,
    bool Own);

/// <summary>A post in the block «Посты» of the results (E14-03, ADR-0124).</summary>
/// <param name="Id">The post.</param>
/// <param name="BlogId">Its blog or topic: <c>/u/&lt;blogId&gt;/&lt;id&gt;</c> leads to the post's canonical address.</param>
/// <param name="Title">The title; <c>null</c> for a short post, for which the excerpt stands.</param>
/// <param name="Excerpt">The beginning of its text on one line, at most <see cref="SearchEndpoints.ExcerptLength"/> characters.</param>
/// <param name="PublishedAt">When it was published.</param>
/// <param name="Blog">Its blog or topic as it is now; <c>null</c> when it is gone.</param>
public sealed record FoundPostView(
    Guid Id, Guid BlogId, string? Title, string Excerpt, DateTimeOffset PublishedAt, SuggestedBlogView? Blog);

/// <summary>A page of the block «Блоги» or «Темы».</summary>
/// <param name="Items">The blogs, most relevant first.</param>
/// <param name="HasMore">Whether «Показать ещё» finds more, from the offset after this page.</param>
public sealed record FoundBlogsPage(IReadOnlyList<FoundBlogView> Items, bool HasMore);

/// <summary>A page of the block «Посты».</summary>
/// <param name="Items">The posts in the order asked for.</param>
/// <param name="HasMore">Whether «Показать ещё» finds more, from the offset after this page.</param>
public sealed record FoundPostsPage(IReadOnlyList<FoundPostView> Items, bool HasMore);

/// <summary>The period filter of the block «Посты» (E14-03).</summary>
public enum SearchPeriod
{
    /// <summary>Any time.</summary>
    All,

    /// <summary>The last day.</summary>
    Day,

    /// <summary>The last week.</summary>
    Week,

    /// <summary>The last month.</summary>
    Month,

    /// <summary>The last year.</summary>
    Year,
}

/// <summary>
/// The results page of the search in the API host (E14-03, FR-SEARCH, ADR-0124): the blocks «Блоги», «Темы» and «Посты»
/// for a query, each read page by page by an offset, so «Показать ещё» of a block reads that block alone. The posts take
/// the filters «только заголовки», the period and the order (relevance or date). Open to guests; a signed-in reader
/// gets «Подписаться» marked in the blog rows and does not see the posts and the personal blogs of the accounts they
/// blocked (ADR-0091). Requests count into the rate limit of the suggestions, <see cref="SearchEndpoints.SuggestionsPolicy"/>.
/// </summary>
public sealed partial class SearchResultEndpoints : IApiEndpoints
{
    /// <summary>How many blogs or topics a page of their block holds.</summary>
    public const int BlogsPageSize = 5;

    /// <summary>How many posts a page of their block holds.</summary>
    public const int PostsPageSize = 10;

    /// <summary>The largest offset: the results deeper than that are not read.</summary>
    public const int MaxOffset = 500;

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var search = v1.MapGroup("/search").WithTags("Search");
        search.MapGet("/blogs", GetBlogsAsync)
            .WithRateLimit(SearchEndpoints.SuggestionsPolicy)
            .Produces<FoundBlogsPage>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithName("SearchBlogs")
            .WithSummary("A page of the personal blogs or of the topics a query finds, with the reader's subscriptions");
        search.MapGet("/posts", GetPostsAsync)
            .WithRateLimit(SearchEndpoints.SuggestionsPolicy)
            .Produces<FoundPostsPage>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithName("SearchPosts")
            .WithSummary("A page of the posts a query finds: in the titles only, for a period, by relevance or date");
    }

    /// <summary>The kind of blog of the block, <c>personal</c> or <c>topic</c>; <c>null</c> for anything else.</summary>
    public static string? ParseKind(string? kind) => kind switch
    {
        IndexedBlog.PersonalKind => IndexedBlog.PersonalKind,
        IndexedBlog.TopicKind => IndexedBlog.TopicKind,
        _ => null,
    };

    /// <summary>The period: <c>all</c> or none is any time; <c>null</c> for an unknown one.</summary>
    public static SearchPeriod? ParsePeriod(string? period) => period switch
    {
        null or "" or "all" => SearchPeriod.All,
        "day" => SearchPeriod.Day,
        "week" => SearchPeriod.Week,
        "month" => SearchPeriod.Month,
        "year" => SearchPeriod.Year,
        _ => null,
    };

    /// <summary>The order: <c>relevance</c> or none, or <c>date</c>; <c>null</c> for an unknown one.</summary>
    public static SearchOrder? ParseOrder(string? order) => order switch
    {
        null or "" or "relevance" => SearchOrder.Relevance,
        "date" => SearchOrder.Date,
        _ => null,
    };

    /// <summary>The moment a period starts at <paramref name="now"/>; <c>null</c> for any time.</summary>
    public static DateTimeOffset? Since(SearchPeriod period, DateTimeOffset now) => period switch
    {
        SearchPeriod.Day => now.AddDays(-1),
        SearchPeriod.Week => now.AddDays(-7),
        SearchPeriod.Month => now.AddMonths(-1),
        SearchPeriod.Year => now.AddYears(-1),
        _ => null,
    };

    private static async Task<IResult> GetBlogsAsync(
        string? q,
        string? kind,
        int? offset,
        ClaimsPrincipal user,
        ISearchIndex index,
        IBlogDirectory blogs,
        IBlogSubscriptions subscriptions,
        IBlockList blocks,
        HttpContext context,
        ILogger<SearchResultEndpoints> logger,
        CancellationToken ct)
    {
        var (query, problem) = SearchEndpoints.Normalize(q);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (query is null)
        {
            errors["q"] = [problem!];
        }

        var blogKind = ParseKind(kind);
        if (blogKind is null)
        {
            errors["kind"] = ["The kind is personal or topic."];
        }

        AddOffsetError(errors, offset);
        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        // The answer depends on the reader's black list and subscriptions, so no shared cache keeps it.
        context.Response.Headers.CacheControl = "private, no-store";
        var reader = SearchEndpoints.AccountOf(user);
        var blocked = reader is { } id ? await blocks.BlockedByAsync(id, ct) : new HashSet<Guid>();
        var from = offset ?? 0;
        IReadOnlyList<IndexedDocument> found;
        try
        {
            found = await index.FindAsync(
                SearchIndexKind.Blogs,
                new SearchQuery(query!, from, BlogsPageSize + 1, BlogKind: blogKind, ExcludedAccounts: [.. blocked]),
                ct);
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode is null or >= HttpStatusCode.InternalServerError)
        {
            return Unavailable(logger, exception);
        }

        List<Guid> ids = [.. found.OfType<IndexedBlog>().Take(BlogsPageSize).Select(b => b.Id)];
        // The names and avatars as they are now, not as indexed; a blog gone since is left out.
        var cards = ids.Count == 0
            ? new Dictionary<Guid, BlogCard>()
            : (await blogs.CardsAsync(ids, [], ct)).ToDictionary(c => c.Id);
        var subscribed = reader is { } account && cards.Count > 0
            ? await subscriptions.SubscribedAmongAsync(account, [.. cards.Keys], ct)
            : new HashSet<Guid>();
        List<FoundBlogView> items =
        [
            .. ids.Where(cards.ContainsKey).Select(i => cards[i]).Select(c => new FoundBlogView(
                c.Id,
                c.Kind == BlogKind.Topic ? IndexedBlog.TopicKind : IndexedBlog.PersonalKind,
                c.Name,
                c.Nickname,
                c.AvatarSources,
                subscribed.Contains(c.Id),
                c.Kind == BlogKind.Personal && reader is not null && c.OwnerAccountId == reader)),
        ];
        return TypedResults.Ok(new FoundBlogsPage(items, found.Count > BlogsPageSize));
    }

    private static async Task<IResult> GetPostsAsync(
        string? q,
        bool? titlesOnly,
        string? period,
        string? order,
        int? offset,
        ClaimsPrincipal user,
        ISearchIndex index,
        IBlogDirectory blogs,
        IBlockList blocks,
        TimeProvider time,
        HttpContext context,
        ILogger<SearchResultEndpoints> logger,
        CancellationToken ct)
    {
        var (query, problem) = SearchEndpoints.Normalize(q);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (query is null)
        {
            errors["q"] = [problem!];
        }

        var searchPeriod = ParsePeriod(period);
        if (searchPeriod is null)
        {
            errors["period"] = ["The period is all, day, week, month or year."];
        }

        var searchOrder = ParseOrder(order);
        if (searchOrder is null)
        {
            errors["order"] = ["The order is relevance or date."];
        }

        AddOffsetError(errors, offset);
        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        context.Response.Headers.CacheControl = "private, no-store";
        var blocked = SearchEndpoints.AccountOf(user) is { } reader
            ? await blocks.BlockedByAsync(reader, ct)
            : new HashSet<Guid>();
        IReadOnlyList<IndexedDocument> found;
        try
        {
            found = await index.FindAsync(
                SearchIndexKind.Posts,
                new SearchQuery(
                    query!,
                    offset ?? 0,
                    PostsPageSize + 1,
                    searchOrder!.Value,
                    titlesOnly ?? false,
                    Since(searchPeriod!.Value, time.GetUtcNow()),
                    ExcludedAccounts: [.. blocked]),
                ct);
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode is null or >= HttpStatusCode.InternalServerError)
        {
            return Unavailable(logger, exception);
        }

        List<IndexedPost> posts = [.. found.OfType<IndexedPost>().Take(PostsPageSize)];
        var cards = posts.Count == 0
            ? new Dictionary<Guid, BlogCard>()
            : (await blogs.CardsAsync([.. posts.Select(p => p.BlogId).Distinct()], [], ct)).ToDictionary(c => c.Id);
        List<FoundPostView> items =
        [
            .. posts.Select(p => new FoundPostView(
                p.Id,
                p.BlogId,
                string.IsNullOrWhiteSpace(p.Title) ? null : p.Title,
                SearchEndpoints.Excerpt(p.Text),
                p.PublishedAt,
                cards.TryGetValue(p.BlogId, out var c)
                    ? new SuggestedBlogView(
                        c.Id,
                        c.Kind == BlogKind.Topic ? IndexedBlog.TopicKind : IndexedBlog.PersonalKind,
                        c.Name,
                        c.Nickname,
                        c.AvatarSources)
                    : null)),
        ];
        return TypedResults.Ok(new FoundPostsPage(items, found.Count > PostsPageSize));
    }

    private static void AddOffsetError(Dictionary<string, string[]> errors, int? offset)
    {
        if (offset is < 0 or > MaxOffset)
        {
            errors["offset"] = [$"The offset is from 0 to {MaxOffset}."];
        }
    }

    private static IResult Unavailable(ILogger logger, Exception exception)
    {
        LogUnavailable(logger, exception);
        return ApiProblems.Problem(
            StatusCodes.Status503ServiceUnavailable, SearchEndpoints.UnavailableCode, "Search is unavailable, try again later.");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The search engine did not answer the results")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);
}
