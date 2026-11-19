using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Platform.Testing.Api;

/// <summary>
/// A stand-in for the real sign-in (OpenID Connect, ADR-0002) in API tests: a request with <see cref="AccountHeader"/>
/// is authenticated as that account, with the roles of <see cref="RoleHeader"/> (comma-separated). A request without
/// the account header is anonymous.
/// </summary>
public static class TestAuthentication
{
    public const string SchemeName = "Test";
    public const string AccountHeader = "X-Test-Account";
    public const string RoleHeader = "X-Test-Role";

    /// <summary>Makes the test scheme the default one.</summary>
    public static AuthenticationBuilder AddTestAuthentication(this IServiceCollection services) =>
        services.AddAuthentication(SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(SchemeName, null);

    /// <summary>Signs the request in as <paramref name="accountId"/> with <paramref name="roles"/>.</summary>
    public static HttpRequestMessage As(this HttpRequestMessage request, string accountId, params string[] roles)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.Add(AccountHeader, accountId);
        if (roles.Length > 0)
        {
            request.Headers.Add(RoleHeader, string.Join(',', roles));
        }

        return request;
    }
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(TestAuthentication.AccountHeader, out var account))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        List<Claim> claims = [new(ClaimTypes.NameIdentifier, account.ToString()), new(ClaimTypes.Name, account.ToString())];
        if (Request.Headers.TryGetValue(TestAuthentication.RoleHeader, out var roles))
        {
            claims.AddRange(roles.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(role => new Claim(ClaimTypes.Role, role)));
        }

        var identity = new ClaimsIdentity(claims, TestAuthentication.SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), TestAuthentication.SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
