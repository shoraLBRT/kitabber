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

/// <summary>A personal blog or a topic among the suggestions (E14-02, ADR-0097).</summary>
/// <param name="Id">The blog.</param>
/// <param name="Kind">
/// <c>personal</c> or <c>topic</c>: a topic's address is <c>/&lt;nickname&gt;</c>, a personal blog's <c>/u/&lt;nickname&gt;</c>.
/// </param>
/// <param name="Name">The name; <c>null</c> until the owner sets it.</param>
/// <param name="Nickname">The short address without <c>@</c>; <c>null</c> until the owner sets it.</param>
/// <param name="AvatarSources">The avatar for every screen density, narrowest first; empty without an avatar.</param>
public sealed record SuggestedBlogView(
    Guid Id, string Kind, string? Name, string? Nickname, IReadOnlyList<ImageSource> AvatarSources);

/// <summary>A post among the suggestions (E14-02, ADR-0097).</summary>
/// <param name="Id">The post.</param>
/// <param name="BlogId">Its blog or topic: <c>/u/&lt;blogId&gt;/&lt;id&gt;</c> leads to the post's canonical address.</param>
/// <param name="Title">The title; <c>null</c> for a short post, for which the excerpt stands.</param>
/// <param name="Excerpt">The beginning of its text on one line, at most <see cref="SearchEndpoints.ExcerptLength"/> characters.</param>
/// <param name="PublishedAt">When it was published.</param>
public sealed record SuggestedPostView(
    Guid Id, Guid BlogId, string? Title, string Excerpt, DateTimeOffset PublishedAt);

/// <summary>What a query being typed in the header's search finds (E14-02, ADR-0097).</summary>
/// <param name="Query">The query as it was searched: trimmed, its spaces collapsed.</param>
/// <param name="Blogs">Personal blogs and topics, most relevant first.</param>
/// <param name="Posts">Published posts, most relevant first.</param>
public sealed record SearchSuggestionsView(
    string Query, IReadOnlyList<SuggestedBlogView> Blogs, IReadOnlyList<SuggestedPostView> Posts);

/// <summary>
/// The suggestions of the header's search in the API host (E14-02, FR-SEARCH, ADR-0097): blogs, topics and posts for a
/// query still being typed, its last word as the start of a word. Open to guests; a signed-in reader does not see the
/// posts and personal blogs of the accounts they blocked (ADR-0091). The query history is not here: it stays in the
/// browser (ADR-0097). Requests count into a rate limit of their own, <see cref="SuggestionsPolicy"/>, as well as the
/// built-in ones.
/// </summary>
public sealed partial class SearchEndpoints : IApiEndpoints
{
    /// <summary>The rate limit of the suggestions: a request per pause in typing.</summary>
    public const string SuggestionsPolicy = "search.suggestions";

    /// <summary>The longest query, in characters after trimming.</summary>
    public const int MaxQueryLength = 100;

    /// <summary>How many blogs and topics come, together.</summary>
    public const int MaxBlogs = 4;

    /// <summary>How many posts come.</summary>
    public const int MaxPosts = 5;

    /// <summary>The longest excerpt of a post's text.</summary>
    public const int ExcerptLength = 140;

    /// <summary>The problem code of a search engine that does not answer.</summary>
    public const string UnavailableCode = "search_unavailable";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        v1.MapGroup("/search").WithTags("Search")
            .MapGet("/suggestions", GetSuggestionsAsync)
            .WithRateLimit(SuggestionsPolicy)
            .Produces<SearchSuggestionsView>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithName("GetSearchSuggestions")
            .WithSummary("Blogs, topics and posts for a query being typed, its last word as the start of a word");
    }

    /// <summary>
    /// <paramref name="q"/> trimmed with its spaces collapsed, or why it cannot be searched: blank, or longer than
    /// <see cref="MaxQueryLength"/>.
    /// </summary>
    public static (string? Query, string? Problem) Normalize(string? q)
    {
        var query = string.Join(' ', (q ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return query.Length switch
        {
            0 => (null, "Type something to search for."),
            > MaxQueryLength => (null, $"A query is at most {MaxQueryLength} characters."),
            _ => (query, null),
        };
    }

    /// <summary>The beginning of <paramref name="text"/> on one line, cut at a word with an ellipsis when longer.</summary>
    public static string Excerpt(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var line = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (line.Length <= ExcerptLength)
        {
            return line;
        }

        var cut = line.LastIndexOf(' ', ExcerptLength - 1);
        return string.Concat(line.AsSpan(0, cut > 0 ? cut : ExcerptLength - 1), "…");
    }

    /// <summary>Whether a found blog is shown to a reader who blocked <paramref name="blocked"/>: a topic always is.</summary>
    public static bool Shows(IndexedBlog blog, IReadOnlySet<Guid> blocked)
    {
        ArgumentNullException.ThrowIfNull(blog);
        ArgumentNullException.ThrowIfNull(blocked);
        return blog.Kind == IndexedBlog.TopicKind || blog.OwnerAccountId is not { } owner || !blocked.Contains(owner);
    }

    private static async Task<IResult> GetSuggestionsAsync(
        string? q,
        ClaimsPrincipal user,
        ISearchIndex index,
        IBlogDirectory blogs,
        IBlockList blocks,
        HttpContext context,
        ILogger<SearchEndpoints> logger,
        CancellationToken ct)
    {
        var (query, problem) = Normalize(q);
        if (query is null)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["q"] = [problem!] });
        }

        // The answer depends on the reader's black list, so no shared cache keeps it.
        context.Response.Headers.CacheControl = "private, no-store";
        IReadOnlySet<Guid> blocked = AccountOf(user) is { } reader
            ? await blocks.BlockedByAsync(reader, ct)
            : new HashSet<Guid>();
        IReadOnlyList<IndexedDocument> foundBlogs;
        IReadOnlyList<IndexedDocument> foundPosts;
        try
        {
            // More than shown by the blocked ones, so leaving them out rarely makes the list short.
            foundBlogs = await index.SuggestAsync(SearchIndexKind.Blogs, query, MaxBlogs + blocked.Count, ct);
            foundPosts = await index.SuggestAsync(SearchIndexKind.Posts, query, MaxPosts + blocked.Count, ct);
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode is null or >= HttpStatusCode.InternalServerError)
        {
            LogUnavailable(logger, exception);
            return ApiProblems.Problem(
                StatusCodes.Status503ServiceUnavailable, UnavailableCode, "Search is unavailable, try again later.");
        }

        List<Guid> blogIds =
            [.. foundBlogs.OfType<IndexedBlog>().Where(b => Shows(b, blocked)).Select(b => b.Id).Take(MaxBlogs)];
        // The names and avatars as they are now, not as indexed; a blog gone since is left out.
        var cards = blogIds.Count == 0
            ? new Dictionary<Guid, BlogCard>()
            : (await blogs.CardsAsync(blogIds, [], ct)).ToDictionary(c => c.Id);
        List<SuggestedBlogView> blogViews =
        [
            .. blogIds.Where(cards.ContainsKey).Select(id => cards[id]).Select(c => new SuggestedBlogView(
                c.Id,
                c.Kind == BlogKind.Topic ? IndexedBlog.TopicKind : IndexedBlog.PersonalKind,
                c.Name,
                c.Nickname,
                c.AvatarSources)),
        ];
        List<SuggestedPostView> postViews =
        [
            .. foundPosts.OfType<IndexedPost>()
                .Where(p => !blocked.Contains(p.AuthorAccountId))
                .Take(MaxPosts)
                .Select(p => new SuggestedPostView(
                    p.Id, p.BlogId, string.IsNullOrWhiteSpace(p.Title) ? null : p.Title, Excerpt(p.Text), p.PublishedAt)),
        ];
        return TypedResults.Ok(new SearchSuggestionsView(query, blogViews, postViews));
    }

    // The account of a signed-in request: the sub claim of the access token (FR-ID-2).
    internal static Guid? AccountOf(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;

    [LoggerMessage(Level = LogLevel.Warning, Message = "The search engine did not answer the suggestions")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);
}
