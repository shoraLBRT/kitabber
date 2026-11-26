using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Platform.Caching;

namespace Platform.Modules.Posts;

/// <summary>
/// Counts views (E07-06, ADR-0071). Fast counters in Redis with a batched flush to the database (E10-09, ADR-0083)
/// are <see cref="BufferedViewCounter"/>; the raw events for the analytics database (E16-18, #217) are sent by a
/// decorator around it; the endpoints and the rules stay as they are.
/// </summary>
internal interface IViewCounter
{
    /// <summary>
    /// Counts one opening of a post by <paramref name="viewer"/> in the period of <paramref name="now"/>; <c>false</c>
    /// when this viewer was already counted in it. A post that is not published or is the viewer's own is left out
    /// when the count reaches the database.
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
/// The counter in Redis (ADR-0083). A view costs one atomic step in <see cref="IWriteBuffer"/>: the mark "this viewer
/// is counted for this post, kind and day" is set and, only when it is new, the view is appended to the buffer.
/// <see cref="ViewFlusher"/> writes the buffer to <c>posts.post_views</c> and the counters of the post in batches.
/// </summary>
internal sealed class BufferedViewCounter(IWriteBuffer buffer, PostsDbContext db, ViewSaltCache salts) : IViewCounter
{
    // The mark outlives its day, so a view at 23:59 and its repeat at 00:01 of the same period never meet a gap.
    private static readonly TimeSpan _markLifetime = TimeSpan.FromDays(2);

    public Task<bool> CountOpenAsync(
        Guid postId, string viewer, Guid? account, DateTimeOffset now, CancellationToken ct) =>
        CountAsync(ViewEntry.View(ViewKind.Open, postId, viewer, account, now), ct);

    public async Task<int> CountImpressionsAsync(
        IReadOnlyCollection<Guid> postIds, string viewer, Guid? account, DateTimeOffset now, CancellationToken ct)
    {
        var counted = await Task.WhenAll(postIds.Distinct().Select(id =>
            CountAsync(ViewEntry.View(ViewKind.Impression, id, viewer, account, now), ct)));
        return counted.Count(c => c);
    }

    public Task RecordReadingAsync(Guid postId, string viewer, int seconds, DateTimeOffset now, CancellationToken ct) =>
        buffer.AppendAsync(ViewFlusher.Buffer, ViewEntry.Reading(postId, viewer, seconds, now).Format(), ct);

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

    private Task<bool> CountAsync(ViewEntry entry, CancellationToken ct) =>
        buffer.AppendOnceAsync(ViewFlusher.Buffer, entry.MarkKey(), _markLifetime, entry.Format(), ct);
}

/// <summary>
/// One entry of the view buffer: a counted view (<see cref="ViewKind"/>) or a reading time. Kept as a short line,
/// <c>kind|post|account or seconds|period|time|viewer</c>, with the viewer last because only it is free text.
/// </summary>
internal sealed record ViewEntry(
    char Kind, Guid PostId, Guid Account, int Seconds, DateTimeOffset Period, DateTimeOffset At, string Viewer)
{
    public const char Open = 'O';
    public const char Impression = 'I';
    public const char Read = 'R';

    public static ViewEntry View(ViewKind kind, Guid postId, string viewer, Guid? account, DateTimeOffset now) =>
        new(kind == ViewKind.Open ? Open : Impression, postId, account ?? Guid.Empty, 0, ViewRules.PeriodOf(now),
            now, viewer);

    public static ViewEntry Reading(Guid postId, string viewer, int seconds, DateTimeOffset now) =>
        new(Read, postId, Guid.Empty, seconds, ViewRules.PeriodOf(now), now, viewer);

    /// <summary>The mark that keeps the viewer from being counted twice for this post, kind and period.</summary>
    public string MarkKey() =>
        string.Create(CultureInfo.InvariantCulture, $"{Kind}:{Period.ToUnixTimeMilliseconds()}:{PostId:N}:{Viewer}");

    public string Format()
    {
        var middle = Kind == Read ? Seconds.ToString(CultureInfo.InvariantCulture) : Account.ToString("N");
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Kind}|{PostId:N}|{middle}|{Period.ToUnixTimeMilliseconds()}|{At.ToUnixTimeMilliseconds()}|{Viewer}");
    }

    /// <summary>The entry of <paramref name="line"/>, or <c>null</c> for a line this version cannot read.</summary>
    public static ViewEntry? Parse(string line)
    {
        var parts = line.Split('|', 6);
        if (parts is not [{ Length: 1 } kind, var post, var middle, var period, var at, { Length: > 0 } viewer]
            || kind[0] is not (Open or Impression or Read)
            || !Guid.TryParseExact(post, "N", out var postId)
            || !long.TryParse(period, NumberStyles.None, CultureInfo.InvariantCulture, out var periodMs)
            || !long.TryParse(at, NumberStyles.None, CultureInfo.InvariantCulture, out var atMs))
        {
            return null;
        }

        var account = Guid.Empty;
        var seconds = 0;
        if (kind[0] == Read
                ? !int.TryParse(middle, NumberStyles.None, CultureInfo.InvariantCulture, out seconds)
                : !Guid.TryParseExact(middle, "N", out account))
        {
            return null;
        }

        return new ViewEntry(kind[0], postId, account, seconds, DateTimeOffset.FromUnixTimeMilliseconds(periodMs),
            DateTimeOffset.FromUnixTimeMilliseconds(atMs), viewer);
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
