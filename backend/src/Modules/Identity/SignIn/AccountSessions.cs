using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Platform.Modules.Identity.SignIn;

/// <param name="Id">The session: the <c>sid</c> of its tokens.</param>
/// <param name="Device">The browser and the system, «Chrome, Windows»; null when the browser said nothing known.</param>
/// <param name="CreatedAt">When the device signed in.</param>
/// <param name="LastActiveAt">When it last signed in or refreshed its tokens.</param>
/// <param name="Current">Whether this is the session of the request.</param>
public sealed record AccountSessionView(Guid Id, string? Device, DateTimeOffset CreatedAt, DateTimeOffset LastActiveAt, bool Current);

/// <summary>
/// The devices of an account (E03-09, ADR-0078): started by every sign-in, kept alive by token refreshes, ended one by
/// one or all but the current one. An ended session is deleted; its <c>sid</c> then stops the provider's cookie and
/// the refresh tokens at once, and the access tokens within <see cref="EndedCheckInterval"/>.
/// </summary>
public sealed class AccountSessions(IdentityDbContext db, IMemoryCache cache, TimeProvider time)
{
    /// <summary>How long an API instance trusts its last look at a session: an ended one stops within this.</summary>
    public static readonly TimeSpan EndedCheckInterval = TimeSpan.FromSeconds(30);

    public async Task<Guid> StartAsync(Guid accountId, string? userAgent, CancellationToken cancellationToken)
    {
        var session = AccountSession.Start(accountId, userAgent, time.GetUtcNow());
        db.AccountSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return session.Id;
    }

    /// <summary>Marks the session of <paramref name="accountId"/> active now; false when it was ended or has expired.</summary>
    public async Task<bool> TouchAsync(Guid accountId, Guid sessionId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var since = now - IdentityModule.RefreshTokenLifetime;
        var touched = await db.AccountSessions
            .Where(s => s.Id == sessionId && s.AccountId == accountId && s.LastActiveAt > since)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastActiveAt, now), cancellationToken);
        return touched == 1;
    }

    /// <summary>The live sessions of <paramref name="accountId"/>, the most recently active first.</summary>
    public async Task<IReadOnlyList<AccountSessionView>> ListAsync(Guid accountId, Guid? current, CancellationToken cancellationToken)
    {
        var since = time.GetUtcNow() - IdentityModule.RefreshTokenLifetime;
        var sessions = await db.AccountSessions.AsNoTracking()
            .Where(s => s.AccountId == accountId && s.LastActiveAt > since)
            .OrderByDescending(s => s.LastActiveAt).ThenByDescending(s => s.Id)
            .Select(s => new { s.Id, s.Device, s.CreatedAt, s.LastActiveAt })
            .ToListAsync(cancellationToken);
        return [.. sessions.Select(s => new AccountSessionView(s.Id, s.Device, s.CreatedAt, s.LastActiveAt, s.Id == current))];
    }

    /// <summary>Ends one session of <paramref name="accountId"/>; false when it has none such.</summary>
    public async Task<bool> EndAsync(Guid accountId, Guid sessionId, CancellationToken cancellationToken)
    {
        var ended = await db.AccountSessions.Where(s => s.Id == sessionId && s.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken);
        Forget(sessionId);
        return ended == 1;
    }

    /// <summary>Ends every session of <paramref name="accountId"/> but <paramref name="current"/>; how many were ended.</summary>
    public async Task<int> EndOthersAsync(Guid accountId, Guid? current, CancellationToken cancellationToken)
    {
        var others = await db.AccountSessions.Where(s => s.AccountId == accountId && s.Id != current)
            .Select(s => s.Id).ToListAsync(cancellationToken);
        var ended = await db.AccountSessions.Where(s => s.AccountId == accountId && s.Id != current)
            .ExecuteDeleteAsync(cancellationToken);
        others.ForEach(Forget);
        return ended;
    }

    /// <summary>Ends every session of <paramref name="accountId"/>: its password was reset (ADR-0036).</summary>
    public async Task EndAllAsync(Guid accountId, CancellationToken cancellationToken) =>
        await EndOthersAsync(accountId, current: null, cancellationToken);

    /// <summary>
    /// Whether the session of an access token has been ended: asked on every API request, answered from memory for
    /// <see cref="EndedCheckInterval"/>, so the database is asked at most twice a minute per device and instance.
    /// </summary>
    public async Task<bool> IsEndedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(Key(sessionId), out bool ended))
        {
            return ended;
        }

        ended = !await db.AccountSessions.AsNoTracking().AnyAsync(s => s.Id == sessionId, cancellationToken);
        cache.Set(Key(sessionId), ended, EndedCheckInterval);
        return ended;
    }

    // This instance stops the session at once; the others within EndedCheckInterval.
    private void Forget(Guid sessionId) => cache.Set(Key(sessionId), true, EndedCheckInterval);

    private static string Key(Guid sessionId) => "identity:session-ended:" + sessionId.ToString("N");
}
