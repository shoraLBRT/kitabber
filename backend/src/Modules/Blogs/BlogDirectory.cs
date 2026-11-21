using Microsoft.EntityFrameworkCore;
using Platform.Modules.Blogs.Contracts;

namespace Platform.Modules.Blogs;

/// <summary><see cref="IBlogDirectory"/> over the schema <c>blogs</c> (ADR-0029).</summary>
internal sealed class BlogDirectory(BlogsDbContext db, PersonalBlogs personal) : IBlogDirectory
{
    public async Task<BlogSummary?> FindAsync(Guid blogId, CancellationToken cancellationToken) =>
        await db.Subsites.AsNoTracking().SingleOrDefaultAsync(s => s.Id == blogId, cancellationToken) is { } blog
            ? Summary(blog)
            : null;

    public async Task<BlogSummary> EnsurePersonalBlogAsync(Guid accountId, CancellationToken cancellationToken) =>
        Summary(await personal.EnsureAsync(accountId, cancellationToken));

    private static BlogSummary Summary(Subsite blog) => new(
        blog.Id,
        blog.Type == SubsiteType.Personal ? BlogKind.Personal : BlogKind.Topic,
        blog.OwnerAccountId);
}
