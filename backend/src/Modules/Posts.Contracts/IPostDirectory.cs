namespace Platform.Modules.Posts.Contracts;

/// <summary>A published post as other modules see it.</summary>
/// <param name="Id">The post's identifier.</param>
/// <param name="BlogId">The blog (a personal blog or a topic) it is in.</param>
/// <param name="AuthorAccountId">Its author.</param>
public sealed record PostSummary(Guid Id, Guid BlogId, Guid AuthorAccountId);

/// <summary>What a link to a published post needs (E08-12): its address and the words it is shown by.</summary>
/// <param name="Id">The post's identifier.</param>
/// <param name="BlogId">The blog (a personal blog or a topic) it is in; the address by it leads to the canonical one.</param>
/// <param name="Title">Its title; <c>null</c> for a post without one, such as a quick post.</param>
/// <param name="Slug">The slug of its address; <c>null</c> when the title gives none.</param>
public sealed record PostHeadline(Guid Id, Guid BlogId, string? Title, string? Slug);

/// <summary>
/// The posts other modules may ask about (ADR-0048): the Posts module implements it over its own tables, so the caller
/// never reads the schema <c>posts</c>.
/// </summary>
public interface IPostDirectory
{
    /// <summary>
    /// The published post <paramref name="postId"/>, or <c>null</c> when there is none: unknown, a draft or deleted.
    /// A post no one but its author may see is not there for other modules.
    /// </summary>
    Task<PostSummary?> FindPublishedAsync(Guid postId, CancellationToken cancellationToken);

    /// <summary>
    /// The published posts among <paramref name="postIds"/>, by id, read with one query (E08-12); a post that is not
    /// published (unknown, a draft or deleted) is missing in the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, PostHeadline>> PublishedHeadlinesAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken);
}

/// <summary>What other modules' demo data may rely on (ADR-0009): the demo posts the Posts seeder writes.</summary>
public static class PostsDemo
{
    /// <summary>The kind of the demo posts' identifiers in <c>SeedData.Id</c>; every demo post is published.</summary>
    public const string PostKind = "post";

    /// <summary>How many demo posts there are: <c>SeedData.Id(PostKind, 0 .. Count - 1)</c>, two per demo account,
    /// the even one in the author's personal blog and the odd one in a demo topic.</summary>
    public const int Count = 40;
}
