using Microsoft.EntityFrameworkCore;
using Platform.Modules.Blogs.Contracts;

namespace Platform.Modules.Blogs;

/// <summary><see cref="IBlogSearchSource"/> over the schema <c>blogs</c> (ADR-0094).</summary>
internal sealed class BlogSearchSource(BlogsDbContext db) : IBlogSearchSource
{
    public async Task<BlogSearchDocument?> FindAsync(Guid blogId, CancellationToken cancellationToken) =>
        (await Findable().Where(s => s.Id == blogId).ToListAsync(cancellationToken)).Select(Document).SingleOrDefault();

    public async Task<IReadOnlyList<BlogSearchDocument>> PageAsync(
        Guid? after, int size, CancellationToken cancellationToken)
    {
        var blogs = Findable();
        if (after is { } last)
        {
            blogs = blogs.Where(s => s.Id.CompareTo(last) > 0);
        }

        return [.. (await blogs.OrderBy(s => s.Id).Take(size).ToListAsync(cancellationToken)).Select(Document)];
    }

    // A blog with neither a name nor a nickname has nothing to be found by.
    private IQueryable<Subsite> Findable() =>
        db.Subsites.AsNoTracking().Where(s => s.Name != null || s.Nickname != null);

    private static BlogSearchDocument Document(Subsite blog) => new(
        blog.Id,
        blog.Type == SubsiteType.Personal ? BlogKind.Personal : BlogKind.Topic,
        blog.OwnerAccountId,
        blog.Name,
        blog.Nickname,
        blog.Description);
}
