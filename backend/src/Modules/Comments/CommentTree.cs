using Microsoft.EntityFrameworkCore;

namespace Platform.Modules.Comments;

/// <summary>
/// The reads of the comment tree (ADR-0048). Each is one SQL query over <see cref="CommentsDbContext.TreeIndex"/>,
/// ordered by the path: depth first, a parent before its replies, older siblings first.
/// </summary>
public static class CommentTree
{
    /// <summary>Every comment under <paramref name="postId"/>.</summary>
    public static IQueryable<Comment> OfPost(CommentsDbContext db, Guid postId)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.Comments.AsNoTracking()
            .Where(c => c.PostId == postId)
            .OrderBy(c => c.Path);
    }

    /// <summary>
    /// The branch of <paramref name="commentId"/>: that comment first, then every reply under it at any depth. Empty when
    /// there is no such comment. The root's path is found in the same query, by joining the table to itself.
    /// </summary>
    public static IQueryable<Comment> Branch(CommentsDbContext db, Guid commentId)
    {
        ArgumentNullException.ThrowIfNull(db);
        return from root in db.Comments.AsNoTracking()
               where root.Id == commentId
               from comment in db.Comments.AsNoTracking()
               where comment.PostId == root.PostId && comment.Path.StartsWith(root.Path)
               orderby comment.Path
               select comment;
    }
}
