using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace Platform.Modules.Posts;

/// <summary>
/// Counts views (E07-06, ADR-0071). This is the seam for the later issues: fast counters in Redis with a flush to the
/// database (E10-09, #75) replace <see cref="DatabaseViewCounter"/>, and the raw events for the analytics database
/// (E16-18, #217) are sent by a decorator around it; the endpoints and the rules stay as they are.
/// </summary>
internal interface IViewCounter
{
    /// <summary>
    /// Counts one opening of a published post by <paramref name="viewer"/> in the period of <paramref name="now"/>;
    /// <c>false</c> when this viewer was already counted in it, or the post is not published or is the viewer's own.
    /// </summary>
    Task<bool> CountOpenAsync(Guid postId, string viewer, Guid? account, DateTimeOffset now, CancellationToken ct);

    /// <summary>Counts the cards of <paramref name="postIds"/> seen by the viewer, each once a period; how many counted.</summary>
    Task<int> CountImpressionsAsync(
        IReadOnlyCollection<Guid> postIds, string viewer, Guid? account, DateTimeOffset now, CancellationToken ct);

    /// <summary>Keeps the longest reading time of the viewer's counted opening in the period; nothing without one.</summary>
    Task RecordReadingAsync(Guid postId, string viewer, int seconds, DateTimeOffset now, CancellationToken ct);

    /// <summary>The secret of the period of <paramref name="now"/> for guest pseudonyms (<see cref="ViewRules.GuestViewer"/>).</summary>
    Task<byte[]> SaltAsync(DateTimeOffset now, CancellationToken ct);
}

/// <summary>
/// The counter in the module's own tables (ADR-0071): a row of <c>posts.post_views</c> per post, kind, viewer and day,
/// written with <c>ON CONFLICT DO NOTHING</c>, and the counter on the post raised in the same statement only when the
/// row is new — so a repeat, even at the same moment from two tabs, never counts twice.
/// </summary>
internal sealed class DatabaseViewCounter(PostsDbContext db, ViewSaltCache salts) : IViewCounter
{
    public async Task<bool> CountOpenAsync(
        Guid postId, string viewer, Guid? account, DateTimeOffset now, CancellationToken ct)
    {
        var period = ViewRules.PeriodOf(now);
        var author = account ?? Guid.Empty;
        var counted = await db.Database.ExecuteSqlAsync(
            $"""
             WITH counted AS (
                 INSERT INTO posts.post_views (post_id, kind, viewer, period_start, created_at)
                 SELECT p.id, 'Open', {viewer}, {period}, {now}
                 FROM posts.posts p
                 WHERE p.id = {postId} AND p.status = 'Published' AND p.deleted_at IS NULL
                   AND p.author_account_id <> {author}
                 ON CONFLICT DO NOTHING
                 RETURNING post_id)
             UPDATE posts.posts SET view_count = view_count + 1
             WHERE id IN (SELECT post_id FROM counted)
             """,
            ct);
        return counted > 0;
    }

    public async Task<int> CountImpressionsAsync(
        IReadOnlyCollection<Guid> postIds, string viewer, Guid? account, DateTimeOffset now, CancellationToken ct)
    {
        var ids = postIds.Distinct().ToArray();
        var period = ViewRules.PeriodOf(now);
        var author = account ?? Guid.Empty;
        return await db.Database.ExecuteSqlAsync(
            $"""
             WITH counted AS (
                 INSERT INTO posts.post_views (post_id, kind, viewer, period_start, created_at)
                 SELECT p.id, 'Impression', {viewer}, {period}, {now}
                 FROM posts.posts p
                 WHERE p.id = ANY({ids}) AND p.status = 'Published' AND p.deleted_at IS NULL
                   AND p.author_account_id <> {author}
                 ON CONFLICT DO NOTHING
                 RETURNING post_id)
             UPDATE posts.posts SET impression_count = impression_count + 1
             WHERE id IN (SELECT post_id FROM counted)
             """,
            ct);
    }

    public async Task RecordReadingAsync(
        Guid postId, string viewer, int seconds, DateTimeOffset now, CancellationToken ct)
    {
        var period = ViewRules.PeriodOf(now);
        await db.Database.ExecuteSqlAsync(
            $"""
             UPDATE posts.post_views SET read_seconds = GREATEST(COALESCE(read_seconds, 0), {seconds})
             WHERE post_id = {postId} AND kind = 'Open' AND viewer = {viewer} AND period_start = {period}
             """,
            ct);
    }

    public async Task<byte[]> SaltAsync(DateTimeOffset now, CancellationToken ct)
    {
        var period = ViewRules.PeriodOf(now);
        if (salts.Get(period) is { } known)
        {
            return known;
        }

        // The first request of the day makes the secret; a request at the same moment on another instance keeps the
        // one already written. The secrets of past days go, so yesterday's pseudonyms cannot be traced back.
        var fresh = RandomNumberGenerator.GetBytes(32);
        await db.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO posts.view_salts (period_start, salt) VALUES ({period}, {fresh}) ON CONFLICT DO NOTHING
             """,
            ct);
        await db.Database.ExecuteSqlAsync($"DELETE FROM posts.view_salts WHERE period_start < {period}", ct);
        var salt = await db.ViewSalts.AsNoTracking()
            .Where(s => s.PeriodStart == period)
            .Select(s => s.Salt)
            .SingleAsync(ct);
        salts.Set(period, salt);
        return salt;
    }
}

/// <summary>The secret of the current period, kept in memory so a view costs no extra read (ADR-0071).</summary>
internal sealed class ViewSaltCache
{
    private readonly Lock _lock = new();
    private DateTimeOffset _period;
    private byte[]? _salt;

    public byte[]? Get(DateTimeOffset period)
    {
        lock (_lock)
        {
            return _salt is not null && _period == period ? _salt : null;
        }
    }

    public void Set(DateTimeOffset period, byte[] salt)
    {
        lock (_lock)
        {
            _period = period;
            _salt = salt;
        }
    }
}
