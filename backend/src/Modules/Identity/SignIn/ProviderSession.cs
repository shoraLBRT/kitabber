using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Modules.Identity.SignIn;

/// <summary>
/// The person's session at the provider (ADR-0018): the <see cref="IdentityModule.SessionScheme"/> cookie that a
/// confirmed registration and every way of signing in set. With it <c>/connect/authorize</c> issues codes to the account;
/// the site gets its tokens only that way. The cookie carries the account's <see cref="Account.SessionStamp"/>, so a
/// password reset ends every session set before it (ADR-0036).
/// </summary>
internal static class ProviderSession
{
    /// <summary>The claim with the account's <see cref="Account.SessionStamp"/>; absent while the account has none.</summary>
    public const string StampClaim = "identity_stamp";

    public static async Task SignInAsync(HttpContext context, Guid accountId)
    {
        var db = context.RequestServices.GetRequiredService<IdentityDbContext>();
        var stamp = await db.Accounts.AsNoTracking().Where(a => a.Id == accountId).Select(a => a.SessionStamp)
            .SingleOrDefaultAsync(context.RequestAborted);
        List<Claim> claims = [new("sub", accountId.ToString("D"))];
        if (stamp is not null)
        {
            claims.Add(new Claim(StampClaim, stamp));
        }

        var identity = new ClaimsIdentity(claims, IdentityModule.SessionScheme);
        await context.SignInAsync(IdentityModule.SessionScheme, new ClaimsPrincipal(identity));
    }

    /// <summary>Whether the session <paramref name="principal"/> of <paramref name="accountId"/> is still current.</summary>
    public static Task<bool> IsCurrentAsync(IdentityDbContext db, Guid accountId, ClaimsPrincipal? principal, CancellationToken ct)
    {
        var stamp = principal?.FindFirst(StampClaim)?.Value;
        return db.Accounts.AnyAsync(a => a.Id == accountId && a.SessionStamp == stamp, ct);
    }
}
