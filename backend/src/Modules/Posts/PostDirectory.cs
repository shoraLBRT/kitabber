using Microsoft.EntityFrameworkCore;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Posts;

/// <summary><see cref="IPostDirectory"/> over the schema <c>posts</c> (ADR-0048).</summary>
internal sealed class PostDirectory(PostsDbContext db) : IPostDirectory
{
    public async Task<PostSummary?> FindPublishedAsync(Guid postId, CancellationToken cancellationToken) =>
        await db.Posts.AsNoTracking()
            .Where(p => p.Id == postId && p.Status == PostStatus.Published)
            .Select(p => new PostSummary(p.Id, p.BlogId, p.AuthorAccountId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, PostHeadline>> PublishedHeadlinesAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(postIds);
        var ids = postIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, PostHeadline>();
        }

        var rows = await db.Posts.AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.Status == PostStatus.Published)
            .Select(p => new { p.Id, p.BlogId, p.Title })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(p => p.Id, p => new PostHeadline(p.Id, p.BlogId, p.Title, PostSlug.From(p.Title)));
    }
}
