using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Platform.Modules.Identity.SignIn;

/// <summary>
/// The two endpoints of the authorization code flow that need the platform's own rules. OpenIddict has already checked
/// the protocol before either runs: the client, the exact redirect address, PKCE with S256, the code and its verifier.
/// </summary>
internal static class AuthorizationEndpoint
{
    /// <summary>
    /// Issues a code to the person signed in at the provider (<see cref="IdentityModule.SessionScheme"/>). Without a
    /// session the person goes to the sign-in window and comes back here; with <c>prompt=none</c> the client gets
    /// <c>login_required</c> instead.
    /// </summary>
    public static async Task<IResult> Authorize(HttpContext context, IdentityDbContext db, AccessTokenAudience audience)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request is missing.");
        var session = await context.AuthenticateAsync(IdentityModule.SessionScheme);
        var accountId = session.Succeeded ? AccountOf(session.Principal) : null;
        if (accountId is { } id && !await ProviderSession.IsCurrentAsync(db, id, session.Principal, context.RequestAborted))
        {
            // The account is gone, or its password was reset since (ADR-0036): the session means nothing any more.
            await context.SignOutAsync(IdentityModule.SessionScheme);
            accountId = null;
        }

        if (accountId is null)
        {
            if (request.HasPromptValue(PromptValues.None))
            {
                return Results.Forbid(
                    Error(Errors.LoginRequired, "The user is not signed in."),
                    [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = context.Request.PathBase + context.Request.Path + context.Request.QueryString },
                [IdentityModule.SessionScheme]);
        }

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
        // sub is the account_id (FR-ID-2): the only thing about the person a token says.
        identity.SetClaim(Claims.Subject, accountId.Value.ToString("D"));
        var principal = new ClaimsPrincipal(identity);
        // offline_access asks for a refresh token (ADR-0024); nothing else is granted.
        principal.SetScopes(request.GetScopes().Intersect([Scopes.OpenId, Scopes.OfflineAccess]));
        principal.SetResources(audience.Value);
        principal.SetDestinations(static _ => [Destinations.AccessToken, Destinations.IdentityToken]);
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Exchanges a code or a refresh token for tokens, unless the account was deleted after it was issued. OpenIddict has
    /// already redeemed the refresh token, issued for this client, and, when the token had been redeemed before (a
    /// stolen copy used after the leeway), revoked the whole authorization instead of running this (ADR-0024).
    /// </summary>
    public static async Task<IResult> Exchange(HttpContext context, IdentityDbContext db)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request is missing.");
        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            return Results.Forbid(
                Error(Errors.UnsupportedGrantType, "Only the authorization code flow and refresh tokens are supported."),
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (result.Principal is not { } principal
            || AccountOf(principal) is not { } id
            || !await db.Accounts.AnyAsync(a => a.Id == id, context.RequestAborted))
        {
            return Results.Forbid(
                Error(Errors.InvalidGrant, "The account no longer exists."),
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Ends the person's session at the provider (RP-initiated logout, ADR-0024): the <see cref="IdentityModule.SessionScheme"/>
    /// cookie is removed, so the next sign-in asks who they are again. OpenIddict has already checked that
    /// <c>post_logout_redirect_uri</c> is one the client registered; without it the person lands on the home page.
    /// </summary>
    public static async Task<IResult> EndSession(HttpContext context)
    {
        await context.SignOutAsync(IdentityModule.SessionScheme);
        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static Guid? AccountOf(ClaimsPrincipal? principal) =>
        Guid.TryParse(principal?.FindFirst(Claims.Subject)?.Value ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id
            : null;

    private static AuthenticationProperties Error(string error, string description) => new(new Dictionary<string, string?>
    {
        [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
    });
}
