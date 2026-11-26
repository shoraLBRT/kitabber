using Microsoft.EntityFrameworkCore;
using Platform.Modules.Comments.Contracts;
using Platform.Modules.Posts.Contracts;

namespace Platform.Modules.Comments;

/// <summary><see cref="ICommentDirectory"/> over the schema <c>comments</c> (ADR-0061).</summary>
internal sealed class CommentDirectory(CommentsDbContext db, IPostDirectory posts) : ICommentDirectory
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
}
