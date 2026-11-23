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

    /// <summary>
    /// What a read shows of <paramref name="ordered"/>, comments in the order of the tree (ADR-0054): those at most
    /// <paramref name="levels"/> levels deeper than <paramref name="start"/> — 0, the post, for a tree; the first
    /// comment's depth for a branch — each with the number of replies under it at any depth, the hidden ones too.
    /// <c>null</c> levels show everything. One pass counts the replies: they are the comments after it that are deeper.
    /// </summary>
    public static IReadOnlyList<ShownComment> Shown(IReadOnlyList<Comment> ordered, int start, int? levels)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        var replies = new int[ordered.Count];
        var open = new Stack<int>();
        for (var i = 0; i < ordered.Count; i++)
        {
            while (open.Count > 0 && ordered[open.Peek()].Depth >= ordered[i].Depth)
            {
                open.Pop();
            }

            foreach (var ancestor in open)
            {
                replies[ancestor]++;
            }

            open.Push(i);
        }

        var deepest = levels is { } most ? start + most : int.MaxValue;
        return [.. ordered.Select((c, i) => new ShownComment(c, replies[i])).Where(s => s.Comment.Depth <= deepest)];
    }
}

/// <summary>A comment a read shows, with the number of replies under it at any depth.</summary>
/// <param name="Comment">The comment.</param>
/// <param name="Replies">Every reply under it, at any depth, shown or not.</param>
public sealed record ShownComment(Comment Comment, int Replies);
