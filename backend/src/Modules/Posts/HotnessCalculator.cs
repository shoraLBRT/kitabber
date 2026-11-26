using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Modules.Comments.Contracts;
using Platform.Modules.Reactions.Contracts;

namespace Platform.Modules.Posts;

/// <summary>What one hotness pass did (ADR-0086).</summary>
/// <param name="Rescored">Scores computed again from their stored inputs because the coefficients changed.</param>
/// <param name="Read">Posts whose reactions, comments and views were read.</param>
/// <param name="Written">Of those, the posts whose score was written: new posts and posts whose inputs changed.</param>
/// <param name="Removed">Rows of posts that are no longer published, removed.</param>
public sealed record HotnessPass(int Rescored, int Read, int Written, int Removed);

/// <summary>
/// One pass of the hotness computation (E08-05, ADR-0086), incremental: it reads the inputs only of posts published
/// within the window and of posts that have no score yet, and writes only the scores whose inputs changed. When the
/// coefficients changed, the scores computed with the old ones are first computed again from their stored inputs,
/// without asking other modules. One instance at a time: a pass holds a transaction-level advisory lock, and a pass that
/// does not get it does nothing.
/// </summary>
public sealed class HotnessCalculator(
    PostsDbContext db,
    IReactionRatings ratings,
    ICommentDirectory comments,
    IConfiguration configuration,
    TimeProvider time)
{
    /// <summary>The key of the advisory lock of the passes.</summary>
    public const long LockKey = 0x686f746e657373; // "hotness"

    private const int _chunk = 500;

    /// <summary>
    /// Runs a pass with the coefficients the configuration has now; <c>null</c> when another instance is running one.
    /// Invalid coefficients throw <see cref="InvalidOperationException"/> and change nothing.
    /// </summary>
    public async Task<HotnessPass?> RunAsync(CancellationToken ct)
    {
        var coefficients = PostsSettings.ReadHotness(configuration);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var locked = await db.Database
            .SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock({LockKey}) AS \"Value\"")
            .SingleAsync(ct);
        if (!locked)
        {
            return null;
        }

        var now = time.GetUtcNow();
        var rescored = await RescoreAsync(coefficients, now, ct);
        var (read, written) = await RefreshAsync(coefficients, now, ct);
        var removed = await db.Hotness
            .Where(h => !db.Posts.Any(p => p.Id == h.PostId && p.Status == PostStatus.Published))
            .ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return new HotnessPass(rescored, read, written, removed);
    }

    // A score written with other coefficients is computed again from its own row: the formula needs nothing else. The
    // state row says whether any can be: an unchanged formula costs one read.
    private async Task<int> RescoreAsync(HotnessCoefficients coefficients, DateTimeOffset now, CancellationToken ct)
    {
        var formula = coefficients.Formula;
        var state = await db.HotnessFormula.SingleOrDefaultAsync(s => s.Id == HotnessFormulaState.Current, ct);
        if (state?.Formula == formula)
        {
            return 0;
        }

        if (state is null)
        {
            db.HotnessFormula.Add(new HotnessFormulaState { Formula = formula });
        }
        else
        {
            state.Formula = formula;
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        var rescored = 0;
        while (true)
        {
            var rows = await db.Hotness.Where(h => h.Formula != formula).OrderBy(h => h.PostId).Take(_chunk)
                .ToListAsync(ct);
            foreach (var row in rows)
            {
                row.Update(row.PublishedAt, row.Inputs, coefficients, now);
            }

            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            rescored += rows.Count;
            if (rows.Count < _chunk)
            {
                return rescored;
            }
        }
    }

    private async Task<(int Read, int Written)> RefreshAsync(
        HotnessCoefficients coefficients, DateTimeOffset now, CancellationToken ct)
    {
        var since = now - TimeSpan.FromDays(coefficients.WindowDays);
        var read = 0;
        var written = 0;
        (DateTimeOffset At, Guid Id)? after = null;
        while (true)
        {
            var candidates = db.Posts.AsNoTracking().Where(p =>
                p.Status == PostStatus.Published && p.PublishedAt != null
                && (p.PublishedAt >= since || !db.Hotness.Any(h => h.PostId == p.Id)));
            if (after is { } last)
            {
                candidates = candidates.Where(p => EF.Functions.GreaterThan(
                    ValueTuple.Create(p.PublishedAt!.Value, p.Id), ValueTuple.Create(last.At, last.Id)));
            }

            var posts = await candidates
                .OrderBy(p => p.PublishedAt).ThenBy(p => p.Id)
                .Select(p => new { p.Id, PublishedAt = p.PublishedAt!.Value, p.ViewCount })
                .Take(_chunk)
                .ToListAsync(ct);
            if (posts.Count == 0)
            {
                return (read, written);
            }

            var ids = posts.Select(p => p.Id).ToList();
            var rated = await ratings.PostRatingsAsync(ids, ct);
            var counted = await comments.ShownCountsAsync(ids, ct);
            var stored = await db.Hotness.Where(h => ids.Contains(h.PostId)).ToDictionaryAsync(h => h.PostId, ct);
            foreach (var post in posts)
            {
                var inputs = new HotnessInputs(
                    rated.GetValueOrDefault(post.Id), counted.GetValueOrDefault(post.Id), post.ViewCount);
                if (!stored.TryGetValue(post.Id, out var row))
                {
                    db.Hotness.Add(PostHotness.Compute(post.Id, post.PublishedAt, inputs, coefficients, now));
                    written++;
                }
                else if (!row.IsCurrent(post.PublishedAt, inputs, coefficients))
                {
                    row.Update(post.PublishedAt, inputs, coefficients, now);
                    written++;
                }
            }

            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            read += posts.Count;
            after = (posts[^1].PublishedAt, posts[^1].Id);
        }
    }
}

/// <summary>
/// Runs a hotness pass every <c>Posts:Hotness:IntervalSeconds</c> (60 by default; 0 turns the passes off on this
/// instance), ADR-0086. Invalid settings stop the host at the start; settings made invalid later skip the passes, with an
/// error in the log, and the scores stay as they were.
/// </summary>
internal sealed partial class HotnessService(
    IServiceScopeFactory scopes, TimeProvider time, IConfiguration configuration, ILogger<HotnessService> log)
    : BackgroundService
{
    private readonly TimeSpan _interval = Validate(configuration);

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
                await RunAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping; the next instance goes on from the stored scores.
        }
    }

    private static TimeSpan Validate(IConfiguration configuration)
    {
        PostsSettings.ReadHotness(configuration);
        return PostsSettings.ReadHotnessInterval(configuration);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<HotnessCalculator>().RunAsync(ct);
        }
        catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            LogPassFailed(log, error);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The hotness pass failed; the scores stay as they were")]
    private static partial void LogPassFailed(ILogger logger, Exception error);
}
