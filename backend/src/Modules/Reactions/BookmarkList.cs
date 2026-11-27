using Microsoft.EntityFrameworkCore;
using Platform.Modules.Reactions.Contracts;

namespace Platform.Modules.Reactions;

/// <summary>
/// <see cref="IBookmarkList"/> over the schema <c>reactions</c> (ADR-0116), by the index on
/// <c>(account_id, created_at)</c>: newest first, the target's id ordering bookmarks put at the same moment.
/// </summary>
internal sealed class BookmarkList(ReactionsDbContext db) : IBookmarkList
{
    public async Task<IReadOnlyList<BookmarkEntry>> PageAsync(
        Guid accountId, BookmarkKind kind, BookmarkPosition? after, int count, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var target = kind == BookmarkKind.Post ? ReactionTarget.Post : ReactionTarget.Comment;
        var rows = db.Bookmarks.AsNoTracking().Where(b => b.AccountId == accountId && b.TargetKind == target);
        if (after is not null)
        {
            rows = rows.Where(b => EF.Functions.LessThan(
                ValueTuple.Create(b.CreatedAt, b.TargetId), ValueTuple.Create(after.BookmarkedAt, after.TargetId)));
        }

        return await rows
            .OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.TargetId)
            .Take(count)
            .Select(b => new BookmarkEntry(b.TargetId, b.PostId, b.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
