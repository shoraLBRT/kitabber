using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Http;

namespace Platform.Realtime;

/// <summary>A ticket that opens the realtime connection, and the moment it stops opening one.</summary>
public sealed record RealtimeTicket(string Ticket, DateTimeOffset ExpiresAt);

/// <summary>
/// Hub tickets (ADR-0019). The browser holds no access token (ADR-0018), so the site's server trades the person's
/// token for a ticket that only opens the hub. A ticket is random, bound to the account, lives <see cref="Lifetime"/>
/// and is kept in the platform cache (Redis, shared by every instance) under its SHA-256, so the store never holds a
/// usable ticket. While it lives it may open connections (negotiation, then the transport); a reconnect asks for a
/// new one. An open connection outlives its ticket.
/// </summary>
public sealed class RealtimeTicketStore(IDistributedCache cache, TimeProvider time)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);
    private const string KeyPrefix = "realtime:ticket:";

    public async Task<RealtimeTicket> IssueAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var ticket = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expiresAt = time.GetUtcNow() + Lifetime;
        var entry = JsonSerializer.Serialize(new StoredTicket(accountId, expiresAt));
        await cache.SetStringAsync(
            Key(ticket), entry, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime },
            cancellationToken);
        return new RealtimeTicket(ticket, expiresAt);
    }

    /// <summary>The account of a live ticket, or null for an unknown or expired one.</summary>
    public async Task<Guid?> ValidateAsync(string ticket, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 128)
        {
            return null;
        }

        var entry = await cache.GetStringAsync(Key(ticket), cancellationToken);
        var stored = entry is null ? null : JsonSerializer.Deserialize<StoredTicket>(entry);
        // The cache drops the entry on its own clock; the expiry is checked on the platform clock (TimeProvider).
        return stored is not null && stored.ExpiresAt > time.GetUtcNow() ? stored.AccountId : null;
    }

    private static string Key(string ticket) =>
        KeyPrefix + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ticket)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record StoredTicket(Guid AccountId, DateTimeOffset ExpiresAt);
}

/// <summary>
/// The authentication of the hub: a ticket in <c>access_token</c>, where the SignalR clients put it, since a browser
/// WebSocket carries no headers. Without one, the host's default scheme (the provider's bearer token, or the test
/// scheme), so a server-side client connects with its own token.
/// </summary>
internal sealed class RealtimeTicketHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IAuthenticationSchemeProvider schemes, RealtimeTicketStore tickets)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Realtime.Ticket";
    public const string QueryParameter = "access_token";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var ticket = Request.Query[QueryParameter].ToString();
        if (string.IsNullOrEmpty(ticket))
        {
            var fallback = await schemes.GetDefaultAuthenticateSchemeAsync();
            return fallback is null || fallback.Name == Scheme.Name
                ? AuthenticateResult.NoResult()
                : await Context.AuthenticateAsync(fallback.Name);
        }

        var account = await tickets.ValidateAsync(ticket, Context.RequestAborted);
        if (account is null)
        {
            return AuthenticateResult.Fail("The realtime ticket is unknown or expired.");
        }

        var id = account.Value.ToString("D");
        var identity = new ClaimsIdentity(
            [new Claim(RealtimeAccounts.SubjectClaim, id), new Claim(ClaimTypes.NameIdentifier, id)], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}

/// <summary><c>POST /api/v1/realtime/tickets</c>: a hub ticket for the signed-in account, by bearer token only.</summary>
internal sealed class RealtimeEndpoints : IApiEndpoints
{
    public void MapV1(IEndpointRouteBuilder v1) =>
        v1.MapPost("/realtime/tickets", IssueAsync)
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("CreateRealtimeTicket")
            .WithTags("Realtime");

    private static async Task<Results<Ok<RealtimeTicket>, UnauthorizedHttpResult>> IssueAsync(
        ClaimsPrincipal user, RealtimeTicketStore tickets, CancellationToken ct) =>
        RealtimeAccounts.Of(user) is { } account
            ? TypedResults.Ok(await tickets.IssueAsync(account, ct))
            : TypedResults.Unauthorized();
}
