namespace Platform.Modules.Posts.Contracts;

/// <summary>
/// What one reader keeps out of their feeds (ADR-0089): posts they hid, blogs and topics they hid, and authors they
/// blocked. An empty set is an empty list.
/// </summary>
/// <param name="Posts">The posts the reader hid from feeds (E11-07).</param>
/// <param name="Blogs">The blogs and topics the reader hid from feeds (E11-07).</param>
/// <param name="Authors">The accounts the reader blocked (E11-06).</param>
public sealed record FeedExclusions(
    IReadOnlyCollection<Guid> Posts,
    IReadOnlyCollection<Guid> Blogs,
    IReadOnlyCollection<Guid> Authors)
{
    /// <summary>Nothing is kept out.</summary>
    public static FeedExclusions None { get; } = new([], [], []);

    /// <summary>Everything either set keeps out.</summary>
    public FeedExclusions Union(FeedExclusions other) =>
        new(
            [.. Posts.Union(other.Posts)],
            [.. Blogs.Union(other.Blogs)],
            [.. Authors.Union(other.Authors)]);
}

/// <summary>
/// A source of what a reader keeps out of their feeds (ADR-0089), such as the black list (E11-06) or the hidden posts
/// and topics (E11-07). The module that owns the list implements it over its own tables and registers it; the feeds of
/// the Posts module ask every registered source and leave out what any of them names. With no source, nothing is left
/// out.
/// </summary>
public interface IFeedExclusionSource
{
    /// <summary>What <paramref name="accountId"/> keeps out of their feeds.</summary>
    Task<FeedExclusions> ForReaderAsync(Guid accountId, CancellationToken cancellationToken);
}
