using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Platform.Modules.Identity.SignIn;

/// <summary>
/// The person's session at the provider (ADR-0018): the <see cref="IdentityModule.SessionScheme"/> cookie that a
/// confirmed registration and every way of signing in set. With it <c>/connect/authorize</c> issues codes to the account;
/// the site gets its tokens only that way. The cookie carries the account's <see cref="Account.SessionStamp"/>, so a
/// password reset ends every session set before it (ADR-0036), and the id of its <see cref="AccountSession"/>, so the
/// device can be ended on its own (ADR-0078).
/// </summary>
internal static class ProviderSession
{
    /// <summary>The claim with the account's <see cref="Account.SessionStamp"/>; absent while the account has none.</summary>
    public const string StampClaim = "identity_stamp";

    /// <summary>
    /// The claim with the <see cref="AccountSession"/> id (OpenID Connect's <c>sid</c>): in the cookie and in every token
    /// issued through it.
    /// </summary>
    public const string SessionClaim = "sid";

    /// <summary>Signs the person in at the provider on a new device session; its id.</summary>
    public static async Task<Guid> SignInAsync(HttpContext context, Guid accountId)
    {
        var services = context.RequestServices;
        var db = services.GetRequiredService<IdentityDbContext>();
        var stamp = await db.Accounts.AsNoTracking().Where(a => a.Id == accountId).Select(a => a.SessionStamp)
            .SingleOrDefaultAsync(context.RequestAborted);
        var sessionId = await services.GetRequiredService<AccountSessions>()
            .StartAsync(accountId, context.Request.Headers[HeaderNames.UserAgent], context.RequestAborted);
        List<Claim> claims = [new("sub", accountId.ToString("D")), new(SessionClaim, sessionId.ToString("D"))];
        if (stamp is not null)
        {
            claims.Add(new Claim(StampClaim, stamp));
        }

        var identity = new ClaimsIdentity(claims, IdentityModule.SessionScheme);
        await context.SignInAsync(IdentityModule.SessionScheme, new ClaimsPrincipal(identity));
        return sessionId;
    }

    /// <summary>
    /// The device session of the cookie <paramref name="principal"/> of <paramref name="accountId"/>, marked active now;
    /// null when the account is gone, its password was reset since, or the session was ended. A cookie set before
    /// sessions had ids gets one now.
    /// </summary>
    public static async Task<Guid?> ResumeAsync(HttpContext context, Guid accountId, ClaimsPrincipal? principal)
    {
        var services = context.RequestServices;
        var db = services.GetRequiredService<IdentityDbContext>();
        var stamp = principal?.FindFirst(StampClaim)?.Value;
        if (!await db.Accounts.AnyAsync(a => a.Id == accountId && a.SessionStamp == stamp, context.RequestAborted))
        {
            return null;
        }

        if (SessionOf(principal) is not { } sessionId)
        {
            return await SignInAsync(context, accountId);
        }

        return await services.GetRequiredService<AccountSessions>().TouchAsync(accountId, sessionId, context.RequestAborted)
            ? sessionId
            : null;
    }

    /// <summary>The device session of a token or a cookie: its <see cref="SessionClaim"/>.</summary>
    public static Guid? SessionOf(ClaimsPrincipal? principal) =>
        Guid.TryParse(principal?.FindFirst(SessionClaim)?.Value, out var id) ? id : null;
}
