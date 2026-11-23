using Platform.Modules.Media.Contracts;

namespace Platform.Modules.Blogs.Contracts;

/// <summary>What a blog is, as other modules see it (ADR-0025).</summary>
public enum BlogKind
{
    /// <summary>The personal blog of an account: only its owner posts in it.</summary>
    Personal = 1,

    /// <summary>A topic: many authors post in it.</summary>
    Topic = 2,
}

/// <summary>A blog as other modules see it.</summary>
/// <param name="Id">The blog's identifier.</param>
/// <param name="Kind">A personal blog or a topic.</param>
/// <param name="OwnerAccountId">The owner of a personal blog; <c>null</c> for a topic.</param>
public sealed record BlogSummary(Guid Id, BlogKind Kind, Guid? OwnerAccountId);

/// <summary>A blog as a card shows it, such as the author or the topic of a post in a feed (ADR-0052).</summary>
/// <param name="Id">The blog's identifier.</param>
/// <param name="Kind">A personal blog or a topic.</param>
/// <param name="OwnerAccountId">The owner of a personal blog; <c>null</c> for a topic.</param>
/// <param name="Name">The blog's name; <c>null</c> until the owner sets it.</param>
/// <param name="Nickname">The short address without <c>@</c>; <c>null</c> until the owner sets it.</param>
/// <param name="AvatarSources">The avatar for every screen density, narrowest first; empty without an avatar.</param>
public sealed record BlogCard(
    Guid Id,
    BlogKind Kind,
    Guid? OwnerAccountId,
    string? Name,
    string? Nickname,
    IReadOnlyList<ImageSource> AvatarSources);

/// <summary>
/// The blogs other modules may ask about (ADR-0029): the Blogs module implements it over its own tables, so the
/// caller never reads the schema <c>blogs</c>.
/// </summary>
public interface IBlogDirectory
{
    /// <summary>The blog <paramref name="blogId"/>, or <c>null</c> when there is none.</summary>
    Task<BlogSummary?> FindAsync(Guid blogId, CancellationToken cancellationToken);

    /// <summary>The personal blog of <paramref name="accountId"/>, created and saved now when it is still missing.</summary>
    Task<BlogSummary> EnsurePersonalBlogAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>
    /// The cards of the blogs <paramref name="blogIds"/> and of the personal blogs of <paramref name="ownerAccountIds"/>,
    /// read at once for a whole page of a feed (ADR-0052). A blog that does not exist is left out; a missing personal
    /// blog is not created.
    /// </summary>
    Task<IReadOnlyList<BlogCard>> CardsAsync(
        IReadOnlyCollection<Guid> blogIds,
        IReadOnlyCollection<Guid> ownerAccountIds,
        CancellationToken cancellationToken);
}

/// <summary>What other modules' demo data may rely on (ADR-0009): the demo blogs the Blogs seeder writes.</summary>
public static class BlogsDemo
{
    /// <summary>
    /// The kind of the demo blogs' identifiers in <c>SeedData.Id</c>: the <c>n</c>-th demo blog is the personal blog
    /// of the <c>n</c>-th demo account.
    /// </summary>
    public const string BlogKind = "blog";

    /// <summary>The kind of the demo topics' identifiers in <c>SeedData.Id</c> (ADR-0046).</summary>
    public const string TopicKind = "topic";

    /// <summary>How many demo topics there are: <c>SeedData.Id(TopicKind, 0 .. TopicCount - 1)</c>.</summary>
    public const int TopicCount = 6;
}
