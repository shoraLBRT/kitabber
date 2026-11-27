using System.Security.Claims;
using Platform.Http.Pagination;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Posts;

/// <summary>
/// What a reader keeps out of the feeds (ADR-0089, ADR-0091): every registered <see cref="IFeedExclusionSource"/>
/// together. The shared feeds (fresh, popular, a blog's) are cached for everyone (ADR-0080), so the exclusions of one
/// reader are applied to a page after it is read from the cache and never cached themselves.
/// </summary>
public static class ReaderExclusions
{
    /// <summary>What the signed-in <paramref name="user"/> keeps out; nothing for a guest.</summary>
    public static async Task<FeedExclusions> ForAsync(
        ClaimsPrincipal user, IEnumerable<IFeedExclusionSource> sources, CancellationToken ct) =>
        PostEndpoints.AccountOf(user) is { } reader ? await ForAsync(reader, sources, ct) : FeedExclusions.None;

    /// <summary>What <paramref name="reader"/> keeps out, by every source.</summary>
    public static async Task<FeedExclusions> ForAsync(
        Guid reader, IEnumerable<IFeedExclusionSource> sources, CancellationToken ct)
    {
        var excluded = FeedExclusions.None;
        foreach (var source in sources)
        {
            excluded = excluded.Union(await source.ForReaderAsync(reader, ct));
        }

        return excluded;
    }

    /// <summary>
    /// <paramref name="page"/> without what <paramref name="excluded"/> names: the posts it hides, the posts of the blogs
    /// it hides and of the authors it blocked, and the best comment of a card when its author is blocked. The cursor
    /// stays the one of the whole page, so the next page follows the cached one and the feed neither repeats nor skips
    /// a post; a page may therefore come shorter than asked, even empty with a next cursor.
    /// </summary>
    public static CursorPage<FeedPostView> Apply(CursorPage<FeedPostView> page, FeedExclusions excluded)
    {
        if (excluded.Posts.Count == 0 && excluded.Blogs.Count == 0 && excluded.Authors.Count == 0)
        {
            return page;
        }

        var posts = excluded.Posts.ToHashSet();
        var blogs = excluded.Blogs.ToHashSet();
        var authors = excluded.Authors.ToHashSet();
        return new CursorPage<FeedPostView>(
            [
                .. page.Items
                    .Where(i => !posts.Contains(i.Post.Id)
                        && !blogs.Contains(i.Post.BlogId)
                        && !authors.Contains(i.Post.AuthorAccountId)
                        && (i.Original is not { } original
                            || (!posts.Contains(original.Post.Id)
                                && !blogs.Contains(original.Post.BlogId)
                                && !authors.Contains(original.Post.AuthorAccountId))))
                    .Select(i => i.BestComment?.AuthorAccountId is { } commenter && authors.Contains(commenter)
                        ? i with { BestComment = null }
                        : i),
            ],
            page.NextCursor);
    }
}
