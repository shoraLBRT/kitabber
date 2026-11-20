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
using Platform.Modules.Identity.Email;
using Platform.Persistence;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Platform.Modules.Identity.SignIn;

/// <summary>
/// The Identity module in the API host (ADR-0002, ADR-0018): the OpenID Connect provider and the validation of the
/// access tokens every API request may carry. The provider answers at the standard addresses
/// (<see cref="DiscoveryPath"/>, <see cref="JwksPath"/>, <see cref="AuthorizationPath"/>, <see cref="TokenPath"/>); the
/// API accepts the tokens of the provider named by <see cref="IdentitySettings.AuthorityKey"/>, this one by default, so
/// switching to a shared account service is a change of settings.
/// </summary>
public static class IdentityModule
{
    public const string DiscoveryPath = "/.well-known/openid-configuration";
    public const string JwksPath = "/.well-known/jwks";
    public const string AuthorizationPath = "/connect/authorize";
    public const string TokenPath = "/connect/token";

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
                    .SetJsonWebKeySetEndpointUris(JwksPath.TrimStart('/'));

                // Only the authorization code flow, only with PKCE, only with S256 (plain is no protection).
                server.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
                server.Configure(options => options.CodeChallengeMethods.Remove(CodeChallengeMethods.Plain));
                server.RegisterScopes(Scopes.OpenId);

                // Access tokens are plain signed JWTs (at+jwt) so any resource server, or a future shared account
                // service, can validate them by the provider's public keys alone.
                server.DisableAccessTokenEncryption();
                server.SetAccessTokenLifetime(TimeSpan.FromMinutes(15))
                    .SetIdentityTokenLifetime(TimeSpan.FromMinutes(15))
                    .SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(2));

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
                    .EnableTokenEndpointPassthrough();
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

    // The account's email and its confirmation (ADR-0020), and the policy other modules require to publish.
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
    }

    /// <summary>Maps the authorization and token endpoints; discovery and keys are answered by the provider itself.</summary>
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapMethods(AuthorizationPath, [HttpMethods.Get, HttpMethods.Post], AuthorizationEndpoint.Authorize)
            .ExcludeFromDescription();
        app.MapPost(TokenPath, AuthorizationEndpoint.Exchange).ExcludeFromDescription();
        return app;
    }

    private static Uri AbsoluteUri(string value, string key) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri
            : throw new Platform.Configuration.InvalidConfigurationException([$"{key}: '{value}' is not an absolute http(s) address"]);
}

/// <summary>The audience every access token of the provider carries (<see cref="IdentitySettings.AudienceKey"/>).</summary>
internal sealed record AccessTokenAudience(string Value);
