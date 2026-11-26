using Microsoft.EntityFrameworkCore;
using Platform.Modules.Reactions.Contracts;

namespace Platform.Modules.Reactions;

/// <summary>
/// Keeps the rating of a target (ADR-0081). Every change of a target's reactions runs in one transaction under a lock
/// on the target, and the rating is counted again from its reactions before the commit: two changes at the same moment
/// wait for each other, so the kept rating always matches the reactions.
/// </summary>
internal static class Ratings
{
    /// <summary>Takes the lock of the target until the end of the current transaction.</summary>
    public static async Task LockAsync(ReactionsDbContext db, ReactionTarget target, Guid targetId, CancellationToken ct)
    {
        var key = $"reactions:{ReactionEvents.NameOf(target)}:{targetId}";
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    }

    /// <summary>Counts the rating of the target again from its reactions and keeps it.</summary>
    public static async Task RecountAsync(
        ReactionsDbContext db, ReactionTarget target, Guid targetId, Guid postId, CancellationToken ct)
    {
        var counts = await db.Reactions.AsNoTracking()
            .Where(r => r.TargetKind == target && r.TargetId == targetId)
            .GroupBy(r => r.Kind)
            .Select(g => new { Kind = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Kind, g => g.Count, ct);
        var value = ReactionWeights.RatingOf(counts);

        var kept = await db.Ratings.SingleOrDefaultAsync(r => r.TargetKind == target && r.TargetId == targetId, ct);
        if (kept is null && value != 0)
        {
            db.Ratings.Add(new Rating { TargetKind = target, TargetId = targetId, PostId = postId, Value = value });
        }
        else if (kept is not null && value == 0)
        {
            db.Ratings.Remove(kept);
        }
        else if (kept is not null)
        {
            kept.Value = value;
        }

        await db.SaveChangesAsync(ct);
    }
}

/// <summary><see cref="IReactionRatings"/> over the kept ratings (ADR-0081), by the index on <c>post_id</c>.</summary>
internal sealed class ReactionRatings(ReactionsDbContext db) : IReactionRatings
{
    public Task<IReadOnlyDictionary<Guid, int>> CommentRatingsAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken) =>
        ReadAsync(ReactionTarget.Comment, postIds, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, int>> PostRatingsAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken) =>
        ReadAsync(ReactionTarget.Post, postIds, cancellationToken);

    private async Task<IReadOnlyDictionary<Guid, int>> ReadAsync(
        ReactionTarget target, IReadOnlyCollection<Guid> postIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(postIds);
        if (postIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var ids = postIds.Distinct().ToList();
        return await db.Ratings.AsNoTracking()
            .Where(r => r.TargetKind == target && ids.Contains(r.PostId))
            .ToDictionaryAsync(r => r.TargetId, r => r.Value, ct);
    }
}
