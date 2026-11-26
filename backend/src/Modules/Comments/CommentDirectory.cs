using Microsoft.EntityFrameworkCore;
using Platform.Modules.Comments.Contracts;
using Platform.Modules.Posts.Contracts;
using Platform.Modules.Reactions.Contracts;

namespace Platform.Modules.Comments;

/// <summary><see cref="ICommentDirectory"/> over the schema <c>comments</c> (ADR-0061, ADR-0062).</summary>
internal sealed class CommentDirectory(CommentsDbContext db, IPostDirectory posts, ILikeCounts likes) : ICommentDirectory
{
    public async Task<CommentSummary?> FindShownAsync(Guid commentId, CancellationToken cancellationToken)
    {
        var comment = await db.Comments.AsNoTracking()
            .Where(c => c.Id == commentId && c.DeletedAt == null)
            .Select(c => new CommentSummary(c.Id, c.PostId, c.Kind == CommentKind.Guest ? null : c.AuthorAccountId))
            .SingleOrDefaultAsync(cancellationToken);
        return comment is not null && await posts.FindPublishedAsync(comment.PostId, cancellationToken) is not null
            ? comment
            : null;
    }

    // Four queries for any page (ADR-0062): the likes of the posts' comments, the earliest shown comment of each post,
    // the liked ones that are still shown, and the text of the chosen ones the second query did not bring.
    public async Task<IReadOnlyDictionary<Guid, BestComment>> BestShownAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(postIds);
        var ids = postIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, BestComment>();
        }

        var counts = await likes.CommentLikesAsync(ids, cancellationToken);
        var shown = db.Comments.AsNoTracking().Where(c => ids.Contains(c.PostId) && c.DeletedAt == null);
        var earliest = await shown
            .GroupBy(c => c.PostId)
            .Select(g => g.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).First())
            .ToListAsync(cancellationToken);
        var likedIds = counts.Keys.ToList();
        var liked = likedIds.Count == 0
            ? []
            : await shown
                .Where(c => likedIds.Contains(c.Id))
                .Select(c => new BestCandidate(c.Id, c.PostId, c.CreatedAt))
                .ToListAsync(cancellationToken);

        var chosen = BestComments.Pick(
            [.. earliest.Select(c => new BestCandidate(c.Id, c.PostId, c.CreatedAt)), .. liked], counts);
        var loaded = earliest.ToDictionary(c => c.Id);
        var missing = chosen.Values.Select(c => c.Id).Where(id => !loaded.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            foreach (var comment in await shown.Where(c => missing.Contains(c.Id)).ToListAsync(cancellationToken))
            {
                loaded[comment.Id] = comment;
            }
        }

        // A chosen comment deleted between the queries is left out: the card shows no block rather than a stub.
        return chosen.Values
            .Where(c => loaded.ContainsKey(c.Id))
            .Select(c => loaded[c.Id])
            .ToDictionary(c => c.PostId, c => new BestComment(
                c.Id,
                c.PostId,
                c.Kind == CommentKind.Guest ? null : c.AuthorAccountId,
                c.Kind == CommentKind.Guest ? c.GuestName : null,
                BestComments.Excerpt(c.Text),
                counts.GetValueOrDefault(c.Id),
                c.CreatedAt));
    }
}
