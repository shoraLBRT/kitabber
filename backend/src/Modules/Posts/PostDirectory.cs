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
}
