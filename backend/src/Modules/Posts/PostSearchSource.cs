using Microsoft.EntityFrameworkCore;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Posts;

/// <summary><see cref="IPostSearchSource"/> over the schema <c>posts</c> (ADR-0094): published posts that are not deleted.</summary>
internal sealed class PostSearchSource(PostsDbContext db) : IPostSearchSource
{
    public async Task<PostSearchDocument?> FindAsync(Guid postId, CancellationToken cancellationToken) =>
        (await Visible().Where(p => p.Id == postId).ToListAsync(cancellationToken)).Select(Document).SingleOrDefault();

    public async Task<IReadOnlyList<PostSearchDocument>> PageAsync(
        Guid? after, int size, CancellationToken cancellationToken)
    {
        var posts = Visible();
        if (after is { } last)
        {
            posts = posts.Where(p => p.Id.CompareTo(last) > 0);
        }

        return [.. (await posts.OrderBy(p => p.Id).Take(size).ToListAsync(cancellationToken)).Select(Document)];
    }

    private IQueryable<Post> Visible() =>
        // A repost is not searched: it has no text of its own to find (ADR-0117).
        db.Posts.AsNoTracking()
            .Where(p => p.Status == PostStatus.Published && p.DeletedAt == null && p.RepostOfPostId == null);

    private static PostSearchDocument Document(Post post) => new(
        post.Id, post.BlogId, post.AuthorAccountId, post.Title, PostText.Of(post.Body), post.PublishedAt!.Value);
}
