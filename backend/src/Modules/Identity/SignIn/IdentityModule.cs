using MassTransit;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Validation.AspNetCore;
using Platform.Http;
using Platform.Http.RateLimiting;
using Platform.Mail;
using Platform.Messaging;
using Platform.Modules.Identity.Captcha;
using Platform.Modules.Identity.Email;
using Platform.Persistence;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Platform.Modules.Identity.SignIn;

/// <summary>
/// The Identity module in the API host (ADR-0002, ADR-0018): the OpenID Connect provider and the validation of the
/// access tokens every API request may carry. The provider answers at the standard addresses
/// (<see cref="DiscoveryPath"/>, <see cref="JwksPath"/>, <see cref="AuthorizationPath"/>, <see cref="TokenPath"/>,
/// <see cref="RevocationPath"/>, <see cref="EndSessionPath"/>); the
/// API accepts the tokens of the provider named by <see cref="IdentitySettings.AuthorityKey"/>, this one by default, so
/// switching to a shared account service is a change of settings.
/// </summary>
public static class IdentityModule
{
    public const string DiscoveryPath = "/.well-known/openid-configuration";
    public const string JwksPath = "/.well-known/jwks";
    public const string AuthorizationPath = "/connect/authorize";
    public const string TokenPath = "/connect/token";
    public const string RevocationPath = "/connect/revoke";
    public const string EndSessionPath = "/connect/endsession";

    /// <summary>
    /// How long a refresh token lives (ADR-0024). Every refresh rotates it and starts the period again, so a person who
    /// visits the site at least once a month stays signed in.
    /// </summary>
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// How long a rotated refresh token still works (ADR-0024): two requests of one browser that refresh at once, on
    /// two instances of the site, both get tokens. Used later, it is a stolen copy: the whole chain is revoked.
    /// </summary>
    public static readonly TimeSpan RefreshTokenReuseLeeway = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The person's session at the provider: a cookie set by the sign-in window (E03) and read by the authorization
    /// endpoint. It is not the session of a client: clients get codes and tokens, never this cookie's contents.
    /// </summary>
    public const string SessionScheme = "Identity.Session";

    public const string SessionCookieName = "identity.session";

    public static WebApplicationBuilder AddIdentityModule(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var configuration = builder.Configuration;
        var environment = builder.Environment.EnvironmentName;
        var services = builder.Services;
        var mayGenerateKeys = ProviderKeys.MayGenerateKeys(environment);
        var signingKey = ProviderKeys.SigningKey(configuration, environment);
        var encryptionKey = ProviderKeys.EncryptionKey(configuration, environment);
        _ = OidcClients.FromConfiguration(configuration);

        services.AddModuleDbContext<IdentityDbContext>(configuration.GetConnectionString(IdentitySettings.ConnectionStringName)!);

        services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
            .AddCookie(SessionScheme, options =>
            {
                options.Cookie.Name = SessionCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = mayGenerateKeys ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.LoginPath = configuration[IdentitySettings.SignInPathKey] is { Length: > 0 } path
                    ? path
                    : IdentitySettings.DefaultSignInPath;
                options.SlidingExpiration = false;
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
            });
        services.AddAuthorization();

        var audience = configuration[IdentitySettings.AudienceKey] is { Length: > 0 } a ? a : IdentitySettings.DefaultAudience;
        var authority = configuration[IdentitySettings.AuthorityKey];
        services.AddOpenIddict()
            .AddCore(core => core.UseEntityFrameworkCore().UseDbContext<IdentityDbContext>())
            .AddServer(server =>
            {
                if (configuration[IdentitySettings.IssuerKey] is { Length: > 0 } issuer)
                {
                    server.SetIssuer(AbsoluteUri(issuer, IdentitySettings.IssuerKey));
                }

                server.SetAuthorizationEndpointUris(AuthorizationPath.TrimStart('/'))
                    .SetTokenEndpointUris(TokenPath.TrimStart('/'))
                    .SetRevocationEndpointUris(RevocationPath.TrimStart('/'))
                    .SetEndSessionEndpointUris(EndSessionPath.TrimStart('/'))
                    .SetJsonWebKeySetEndpointUris(JwksPath.TrimStart('/'));

                // Only the authorization code flow, only with PKCE, only with S256 (plain is no protection).
                server.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
                server.Configure(options => options.CodeChallengeMethods.Remove(CodeChallengeMethods.Plain));
                // Refresh tokens (ADR-0024) for the scope offline_access: rolling (every refresh redeems the old one
                // and issues a new one), and a redeemed one used after the leeway revokes the whole authorization.
                server.AllowRefreshTokenFlow();
                server.RegisterScopes(Scopes.OpenId, Scopes.OfflineAccess);

                // Access tokens are plain signed JWTs (at+jwt) so any resource server, or a future shared account
                // service, can validate them by the provider's public keys alone.
                server.DisableAccessTokenEncryption();
                server.SetAccessTokenLifetime(TimeSpan.FromMinutes(15))
                    .SetIdentityTokenLifetime(TimeSpan.FromMinutes(15))
                    .SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(2))
                    .SetRefreshTokenLifetime(RefreshTokenLifetime)
                    .SetRefreshTokenReuseLeeway(RefreshTokenReuseLeeway);

                if (signingKey is not null)
                {
                    server.AddSigningKey(signingKey);
                }
                else
                {
                    server.AddEphemeralSigningKey();
                }

                if (encryptionKey is not null)
                {
                    server.AddEncryptionKey(encryptionKey);
                }
                else
                {
                    server.AddEphemeralEncryptionKey();
                }

                var aspNetCore = server.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();
                if (mayGenerateKeys)
                {
                    // Development and Test talk plain HTTP; the stands are HTTPS behind the proxy (X-Forwarded-Proto).
                    aspNetCore.DisableTransportSecurityRequirement();
                }
            })
            .AddValidation(validation =>
            {
                if (string.IsNullOrWhiteSpace(authority))
                {
                    validation.UseLocalServer();
                }
                else
                {
                    validation.SetIssuer(AbsoluteUri(authority, IdentitySettings.AuthorityKey));
                    validation.UseSystemNetHttp();
                }

                validation.AddAudiences(audience);
                validation.UseAspNetCore();
            });

        services.AddSingleton<OidcClientSync>();
        if (!string.Equals(configuration[IdentitySettings.SyncClientsKey], "false", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHostedService(provider => provider.GetRequiredService<OidcClientSync>());
        }

        services.AddSingleton(new AccessTokenAudience(audience));
        services.AddCaptcha(configuration, environment, encryptionKey);
        AddEmail(services);
        return builder;
    }

    /// <summary>
    /// Adds the module's outbox to the host's bus (<c>AddPlatformMessaging</c>): the letters the module sends, such as
    /// the email confirmation code, are saved with its changes and delivered from there (ADR-0005, ADR-0017).
    /// </summary>
    public static IBusRegistrationConfigurator AddIdentityOutbox(this IBusRegistrationConfigurator bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        bus.AddModuleOutbox<IdentityDbContext>();
        return bus;
    }

    // The account's email and its confirmation (ADR-0020), the policy other modules require to publish, and the
    // registration by email and password that confirms its address the same way (E03-01).
    private static void AddEmail(IServiceCollection services)
    {
        services.AddPlatformMail();
        services.AddScoped<AccountEmails>();
        services.AddScoped<IAuthorizationHandler, ConfirmedEmailHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ConfirmedEmailResultHandler>();
        services.AddAuthorizationBuilder().AddPolicy(AccountPolicies.ConfirmedEmail, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new ConfirmedEmailRequirement()));
        services.AddRateLimitPolicy(AccountEmailEndpoints.CodePolicy, IdentitySettings.EmailCodePolicy);
        services.AddApiEndpoints<AccountEmailEndpoints>();
        services.AddScoped<Registration.Registrations>();
        services.AddRateLimitPolicy(Registration.RegistrationEndpoints.Policy, IdentitySettings.RegistrationPolicy);
        services.AddApiEndpoints<Registration.RegistrationEndpoints>();
        services.AddScoped<SignIns>();
        services.AddRateLimitPolicy(SignInEndpoints.AttemptPolicy, IdentitySettings.SignInPolicy);
        services.AddRateLimitPolicy(SignInEndpoints.AddressAttemptPolicy, IdentitySettings.SignInAddressPolicy);
        services.AddRateLimitPolicy(SignInEndpoints.FailurePolicy, IdentitySettings.SignInFailurePolicy);
        services.AddRateLimitPolicy(SignInEndpoints.AddressFailurePolicy, IdentitySettings.SignInAddressFailurePolicy);
        services.AddRateLimitPolicy(SignInEndpoints.CodePolicy, IdentitySettings.SignInCodePolicy);
        services.AddRateLimitPolicy(SignInEndpoints.AddressCodePolicy, IdentitySettings.SignInCodeAddressPolicy);
        services.AddApiEndpoints<SignInEndpoints>();
        services.AddScoped<Passwords.AccountPasswords>();
        services.AddApiEndpoints<Passwords.PasswordEndpoints>();
    }

    /// <summary>Maps the authorization and token endpoints; discovery and keys are answered by the provider itself.</summary>
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapMethods(AuthorizationPath, [HttpMethods.Get, HttpMethods.Post], AuthorizationEndpoint.Authorize)
            .ExcludeFromDescription();
        app.MapPost(TokenPath, AuthorizationEndpoint.Exchange).ExcludeFromDescription();
        app.MapMethods(EndSessionPath, [HttpMethods.Get, HttpMethods.Post], (Delegate)AuthorizationEndpoint.EndSession)
            .ExcludeFromDescription();
        return app;
    }

    private static Uri AbsoluteUri(string value, string key) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri
            : throw new Platform.Configuration.InvalidConfigurationException([$"{key}: '{value}' is not an absolute http(s) address"]);
}

/// <summary>The audience every access token of the provider carries (<see cref="IdentitySettings.AudienceKey"/>).</summary>
internal sealed record AccessTokenAudience(string Value);
