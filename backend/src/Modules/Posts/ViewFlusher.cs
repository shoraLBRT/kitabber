using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Caching;

namespace Platform.Modules.Posts;

/// <summary>
/// Writes the view buffer to the database in batches (ADR-0083). Every statement is idempotent: a view row is
/// inserted with <c>ON CONFLICT DO NOTHING</c> and the post's counter grows only by the rows really inserted, and a
/// reading time is kept with <c>GREATEST</c>. So a batch applied twice — its instance stopped before completing it and
/// another claimed it again — changes nothing the second time, and the database stays the one that dedupes.
/// </summary>
public sealed partial class ViewFlusher(
    IWriteBuffer buffer, IServiceScopeFactory scopes, ILogger<ViewFlusher> log) : IDisposable
{
    /// <summary>The name of the view buffer in <see cref="IWriteBuffer"/>.</summary>
    public const string Buffer = "posts-views";

    /// <summary>
    /// Seconds between flushes on an instance (5 by default); 0 turns the timer and the flush on stop off, so the
    /// instance only appends to the buffer and the others flush it.
    /// </summary>
    public const string FlushSecondsKey = PostsSettings.ViewFlushSecondsKey;

    /// <summary>How long a claimed batch stays with its instance before another may take it.</summary>
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(1);

    // A flush stops after this many batches so a busy buffer never keeps it going for ever; the next tick goes on.
    private const int _maxBatches = 20;

    // Rows per statement, so a large batch does not become one huge parameter array.
    private const int _chunk = 2000;

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Applies what is in the buffer now; the number of entries applied.</summary>
    public async Task<int> FlushAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var applied = 0;
            for (var i = 0; i < _maxBatches && await buffer.ClaimAsync(Buffer, Lease, ct) is { } batch; i++)
            {
                await ApplyAsync(batch.Entries, ct);
                await buffer.CompleteAsync(batch, ct);
                applied += batch.Entries.Count;
            }

            return applied;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task ApplyAsync(IReadOnlyList<string> lines, CancellationToken ct)
    {
        var entries = new List<ViewEntry>(lines.Count);
        foreach (var line in lines)
        {
            if (ViewEntry.Parse(line) is { } entry)
            {
                entries.Add(entry);
            }
            else
            {
                LogUnreadable(log, line);
            }
        }

        if (entries.Count == 0)
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostsDbContext>();

        // Views first, so the reading times of the same batch find the opening they belong to.
        foreach (var chunk in entries.Where(e => e.Kind != ViewEntry.Read).Chunk(_chunk))
        {
            await ApplyViewsAsync(db, chunk, ct);
        }

        foreach (var chunk in entries.Where(e => e.Kind == ViewEntry.Read).Chunk(_chunk))
        {
            await ApplyReadingAsync(db, chunk, ct);
        }
    }

    private static Task<int> ApplyViewsAsync(PostsDbContext db, ViewEntry[] views, CancellationToken ct)
    {
        var kinds = views.Select(v => v.Kind == ViewEntry.Open ? nameof(ViewKind.Open) : nameof(ViewKind.Impression))
            .ToArray();
        var posts = views.Select(v => v.PostId).ToArray();
        var accounts = views.Select(v => v.Account).ToArray();
        var viewers = views.Select(v => v.Viewer).ToArray();
        var periods = views.Select(v => v.Period).ToArray();
        var times = views.Select(v => v.At).ToArray();

        // The rules of ADR-0071 hold here: only a published post, never the author's own.
        return db.Database.ExecuteSqlAsync(
            $"""
             WITH input AS (
                 SELECT * FROM unnest({kinds}::text[], {posts}::uuid[], {accounts}::uuid[], {viewers}::text[],
                                      {periods}::timestamptz[], {times}::timestamptz[])
                     AS t(kind, post_id, account, viewer, period_start, created_at)),
             counted AS (
                 INSERT INTO posts.post_views (post_id, kind, viewer, period_start, created_at)
                 SELECT DISTINCT ON (i.post_id, i.kind, i.viewer, i.period_start)
                        i.post_id, i.kind, i.viewer, i.period_start, i.created_at
                 FROM input i
                 JOIN posts.posts p ON p.id = i.post_id
                 WHERE p.status = 'Published' AND p.deleted_at IS NULL AND p.author_account_id <> i.account
                 ORDER BY i.post_id, i.kind, i.viewer, i.period_start, i.created_at
                 ON CONFLICT DO NOTHING
                 RETURNING post_id, kind),
             totals AS (
                 SELECT post_id,
                        count(*) FILTER (WHERE kind = 'Open') AS opens,
                        count(*) FILTER (WHERE kind = 'Impression') AS impressions
                 FROM counted GROUP BY post_id)
             UPDATE posts.posts p
             SET view_count = p.view_count + t.opens, impression_count = p.impression_count + t.impressions
             FROM totals t
             WHERE p.id = t.post_id
             """,
            ct);
    }

    private static Task<int> ApplyReadingAsync(PostsDbContext db, ViewEntry[] readings, CancellationToken ct)
    {
        var posts = readings.Select(r => r.PostId).ToArray();
        var viewers = readings.Select(r => r.Viewer).ToArray();
        var periods = readings.Select(r => r.Period).ToArray();
        var seconds = readings.Select(r => r.Seconds).ToArray();
        return db.Database.ExecuteSqlAsync(
            $"""
             UPDATE posts.post_views v
             SET read_seconds = GREATEST(COALESCE(v.read_seconds, 0), r.seconds)
             FROM (SELECT post_id, viewer, period_start, max(seconds) AS seconds
                   FROM unnest({posts}::uuid[], {viewers}::text[], {periods}::timestamptz[], {seconds}::int[])
                       AS t(post_id, viewer, period_start, seconds)
                   GROUP BY post_id, viewer, period_start) r
             WHERE v.post_id = r.post_id AND v.kind = 'Open' AND v.viewer = r.viewer
               AND v.period_start = r.period_start
             """,
            ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped an unreadable entry of the view buffer: {Line}")]
    private static partial void LogUnreadable(ILogger logger, string line);
}

/// <summary>
/// Flushes the view buffer every <c>Posts:Views:FlushSeconds</c> (5 by default; 0 turns the flush off on this
/// instance) and once more when the host stops (ADR-0083). What a stopped instance did not flush stays in Redis for
/// the next flush of any instance.
/// </summary>
internal sealed partial class ViewFlushService(
    ViewFlusher flusher, TimeProvider time, IConfiguration configuration, ILogger<ViewFlushService> log)
    : BackgroundService
{
    private readonly TimeSpan _interval = PostsSettings.ReadViewFlushInterval(configuration);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_interval <= TimeSpan.Zero)
        {
            return;
        }

        using var timer = new PeriodicTimer(_interval, time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await FlushAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping: one more flush below, while the host's services are still there.
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await FlushAsync(timeout.Token);
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        try
        {
            await flusher.FlushAsync(ct);
        }
        catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // The batch stays in the buffer; its lease runs out and the next flush takes it again.
            LogFlushFailed(log, error);
        }
        catch (OperationCanceledException)
        {
            // Stopping before the flush ended: the claimed batch is taken again after its lease.
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Flushing the view buffer failed; it will be retried")]
    private static partial void LogFlushFailed(ILogger logger, Exception error);
}
