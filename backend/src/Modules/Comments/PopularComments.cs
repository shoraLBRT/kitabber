namespace Platform.Modules.Comments;

/// <summary>A native shown comment of the day that may enter the widget: what the choice needs, without the text.</summary>
/// <param name="Id">The comment's identifier.</param>
/// <param name="PostId">The published post it is under.</param>
/// <param name="AuthorAccountId">Its author.</param>
/// <param name="CreatedAt">When it was written.</param>
public sealed record PopularCandidate(Guid Id, Guid PostId, Guid AuthorAccountId, DateTimeOffset CreatedAt);

/// <summary>One comment of the widget «Популярные комментарии» (E08-12, FR-FEED, ADR-0105).</summary>
/// <param name="Id">The comment's identifier: the link leads to it on its post's page.</param>
/// <param name="PostId">The post it is under.</param>
/// <param name="PostBlogId">The blog of the post: the address by it leads to the canonical one (ADR-0041).</param>
/// <param name="PostTitle">The title of the post; <c>null</c> for a post without one.</param>
/// <param name="PostSlug">The slug of the post's address; <c>null</c> when its title gives none.</param>
/// <param name="AuthorAccountId">The author: always an account, a guest comment never enters the widget.</param>
/// <param name="Text">The beginning of the text, cut as the best comment of a feed card is.</param>
/// <param name="Rating">The sum of its reactions by their weights (ADR-0081); above zero.</param>
/// <param name="CreatedAt">When it was written: within the last day.</param>
public sealed record PopularCommentView(
    Guid Id,
    Guid PostId,
    Guid PostBlogId,
    string? PostTitle,
    string? PostSlug,
    Guid AuthorAccountId,
    string Text,
    int Rating,
    DateTimeOffset CreatedAt);

/// <summary>The widget «Популярные комментарии»: the highest rated first.</summary>
/// <param name="Comments">At most the asked number; empty when no comment of the day has a rating.</param>
public sealed record PopularCommentsView(IReadOnlyList<PopularCommentView> Comments);

/// <summary>
/// The rules of the widget «Популярные комментарии» (E08-12, ADR-0105), with no database: the comments written in the
/// last <see cref="Window"/> with the highest rating, the earlier one on a tie (the smaller id at the same moment, so the
/// order is total). A comment without a rating does not enter it: the widget shows what people liked, not what is new.
/// </summary>
public static class PopularComments
{
    /// <summary>How far back a comment is of the day.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(1);

    /// <summary>How many comments the widget shows when the reader asks for no number.</summary>
    public const int DefaultLimit = 5;

    /// <summary>The most comments one read may ask for.</summary>
    public const int MaxLimit = 10;

    /// <summary>
    /// How many comments are chosen and cached for every reader: more than <see cref="MaxLimit"/>, so a reader who
    /// blocked a few of their authors still gets a full widget.
    /// </summary>
    public const int Stock = 30;

    /// <summary>The <paramref name="count"/> highest rated of <paramref name="candidates"/> that have a rating.</summary>
    public static IReadOnlyList<(PopularCandidate Comment, int Rating)> Pick(
        IEnumerable<PopularCandidate> candidates, IReadOnlyDictionary<Guid, int> ratings, int count)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(ratings);
        return
        [
            .. candidates
                .DistinctBy(c => c.Id)
                .Select(c => (Comment: c, Rating: ratings.GetValueOrDefault(c.Id)))
                .Where(c => c.Rating > 0)
                .OrderByDescending(c => c.Rating)
                .ThenBy(c => c.Comment.CreatedAt)
                .ThenBy(c => c.Comment.Id)
                .Take(count),
        ];
    }

    /// <summary>
    /// What one reader sees of the cached <paramref name="stock"/>: without the comments of the accounts they blocked
    /// (ADR-0091) and of those that left the day since the stock was chosen, at most <paramref name="limit"/>.
    /// </summary>
    public static IReadOnlyList<PopularCommentView> ForReader(
        IReadOnlyList<PopularCommentView> stock, IReadOnlySet<Guid> blocked, DateTimeOffset now, int limit)
    {
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(blocked);
        var since = now - Window;
        return
        [
            .. stock
                .Where(c => c.CreatedAt >= since && !blocked.Contains(c.AuthorAccountId))
                .Take(limit),
        ];
    }
}
