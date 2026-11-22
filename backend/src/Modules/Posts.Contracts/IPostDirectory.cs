namespace Platform.Modules.Posts.Contracts;

/// <summary>A published post as other modules see it.</summary>
/// <param name="Id">The post's identifier.</param>
/// <param name="BlogId">The blog (a personal blog or a topic) it is in.</param>
/// <param name="AuthorAccountId">Its author.</param>
public sealed record PostSummary(Guid Id, Guid BlogId, Guid AuthorAccountId);

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
}

/// <summary>What other modules' demo data may rely on (ADR-0009): the demo posts the Posts seeder writes.</summary>
public static class PostsDemo
{
    /// <summary>The kind of the demo posts' identifiers in <c>SeedData.Id</c>; every demo post is published.</summary>
    public const string PostKind = "post";

    /// <summary>How many demo posts there are: <c>SeedData.Id(PostKind, 0 .. Count - 1)</c>, two per demo account.</summary>
    public const int Count = 40;
}
