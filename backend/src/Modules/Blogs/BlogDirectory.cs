using Microsoft.EntityFrameworkCore;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Blogs.Profiles;
using Platform.Modules.Media.Contracts;

namespace Platform.Modules.Blogs;

/// <summary><see cref="IBlogDirectory"/> over the schema <c>blogs</c> (ADR-0029).</summary>
internal sealed class BlogDirectory(BlogsDbContext db, PersonalBlogs personal, IMediaLibrary media) : IBlogDirectory
{
    public async Task<BlogSummary?> FindAsync(Guid blogId, CancellationToken cancellationToken) =>
        await db.Subsites.AsNoTracking().SingleOrDefaultAsync(s => s.Id == blogId, cancellationToken) is { } blog
            ? Summary(blog)
            : null;

    public async Task<BlogSummary> EnsurePersonalBlogAsync(Guid accountId, CancellationToken cancellationToken) =>
        Summary(await personal.EnsureAsync(accountId, cancellationToken));

    public async Task<IReadOnlyList<BlogCard>> CardsAsync(
        IReadOnlyCollection<Guid> blogIds,
        IReadOnlyCollection<Guid> ownerAccountIds,
        CancellationToken cancellationToken)
    {
        if (blogIds.Count == 0 && ownerAccountIds.Count == 0)
        {
            return [];
        }

        var blogs = await db.Subsites.AsNoTracking()
            .Where(s => blogIds.Contains(s.Id)
                || (s.Type == SubsiteType.Personal && s.OwnerAccountId != null
                    && ownerAccountIds.Contains(s.OwnerAccountId.Value)))
            .ToListAsync(cancellationToken);
        var avatarIds = blogs.Where(b => b.AvatarMediaId is not null).Select(b => b.AvatarMediaId!.Value).Distinct()
            .ToList();
        IReadOnlyDictionary<Guid, IReadOnlyList<ImageSource>> avatars = avatarIds.Count == 0
            ? new Dictionary<Guid, IReadOnlyList<ImageSource>>()
            : await media.ImageSourcesAsync(avatarIds, ProfileEndpoints.AvatarSourceVariants, cancellationToken);
        return
        [
            .. blogs.Select(blog => new BlogCard(
                blog.Id,
                Summary(blog).Kind,
                blog.OwnerAccountId,
                blog.Name,
                blog.Nickname,
                blog.AvatarMediaId is { } id && avatars.TryGetValue(id, out var sources) ? sources : [])),
        ];
    }

    private static BlogSummary Summary(Subsite blog) => new(
        blog.Id,
        blog.Type == SubsiteType.Personal ? BlogKind.Personal : BlogKind.Topic,
        blog.OwnerAccountId);
}
