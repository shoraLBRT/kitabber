using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Platform.Modules.Identity.SignIn;

/// <summary>
/// The person's session at the provider (ADR-0018): the <see cref="IdentityModule.SessionScheme"/> cookie that a
/// confirmed registration and every way of signing in set. With it <c>/connect/authorize</c> issues codes to the account;
/// the site gets its tokens only that way.
/// </summary>
internal static class ProviderSession
{
    public static Task SignInAsync(HttpContext context, Guid accountId)
    {
        var identity = new ClaimsIdentity([new Claim("sub", accountId.ToString("D"))], IdentityModule.SessionScheme);
        return context.SignInAsync(IdentityModule.SessionScheme, new ClaimsPrincipal(identity));
    }
}
