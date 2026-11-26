using Microsoft.EntityFrameworkCore;
using Platform.Modules.Reactions.Contracts;

namespace Platform.Modules.Reactions;

/// <summary><see cref="ILikeCounts"/> over the schema <c>reactions</c> (ADR-0062), by the index on <c>post_id</c>.</summary>
internal sealed class LikeCounts(ReactionsDbContext db) : ILikeCounts
{
    public async Task<IReadOnlyDictionary<Guid, int>> CommentLikesAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(postIds);
        if (postIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var ids = postIds.Distinct().ToList();
        return await db.Reactions.AsNoTracking()
            .Where(r => r.TargetKind == ReactionTarget.Comment && r.Kind == ReactionKind.Like && ids.Contains(r.PostId))
            .GroupBy(r => r.TargetId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Id, g => g.Count, cancellationToken);
    }
}
