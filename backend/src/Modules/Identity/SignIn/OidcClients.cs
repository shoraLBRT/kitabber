using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Platform.Configuration;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Platform.Modules.Identity.SignIn;

/// <summary>
/// The clients of the provider (FR-ID-2): the web client (Nuxt, through its server, the BFF) and the admin panel. Both
/// are public clients of the authorization code flow with PKCE: no client secret to distribute, the code is useless
/// without the verifier, and the redirect address must match one of the configured ones exactly (ADR-0018).
/// </summary>
public static class OidcClients
{
    public const string WebClientId = "web";
    public const string AdminClientId = "admin";

    /// <summary>The clients the configuration defines; a client without redirect addresses is left out.</summary>
    public static IReadOnlyList<OpenIddictApplicationDescriptor> FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        List<OpenIddictApplicationDescriptor> clients = [];
        List<string> problems = [];
        foreach (var (clientId, name, key) in new[]
                 {
                     (WebClientId, "Web", IdentitySettings.WebRedirectUrisKey),
                     (AdminClientId, "Admin", IdentitySettings.AdminRedirectUrisKey),
                 })
        {
            var uris = RedirectUris(configuration[key], key, problems);
            if (uris.Count > 0)
            {
                clients.Add(Describe(clientId, name, uris));
            }
        }

        return problems.Count > 0 ? throw new InvalidConfigurationException(problems) : clients;
    }

    private static OpenIddictApplicationDescriptor Describe(string clientId, string name, IReadOnlyList<Uri> redirectUris)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = ClientTypes.Public,
            ApplicationType = ApplicationTypes.Web,
            // First-party clients: the person signs in to the platform itself, there is nothing to consent to.
            ConsentType = ConsentTypes.Implicit,
            DisplayName = name,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };
        foreach (var uri in redirectUris)
        {
            descriptor.RedirectUris.Add(uri);
            // After signing out the person comes back to the home page of the same site (ADR-0024): the origin of
            // each redirect address, so there is no second list to keep in step.
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri.GetLeftPart(UriPartial.Authority) + "/"));
        }

        return descriptor;
    }

    private static List<Uri> RedirectUris(string? value, string key, List<string> problems)
    {
        List<Uri> uris = [];
        foreach (var part in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Uri.TryCreate(part, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                && string.IsNullOrEmpty(uri.Fragment))
            {
                uris.Add(uri);
            }
            else
            {
                problems.Add($"{key}: '{part}' is not an absolute http(s) address without a fragment");
            }
        }

        return uris;
    }
}

/// <summary>
/// Writes the configured clients into the provider's store when the API starts: the configuration is the source of
/// truth, a client removed from it is removed from the store. Several API instances may start at once, so a client
/// another instance has just created is updated, not created again. Tests that migrate after the host starts call
/// <see cref="SyncAsync"/> themselves.
/// </summary>
public sealed partial class OidcClientSync(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<OidcClientSync> logger)
    : IHostedService
{
    private static readonly string[] _knownClients = [OidcClients.WebClientId, OidcClients.AdminClientId];

    public Task StartAsync(CancellationToken cancellationToken) => SyncAsync(cancellationToken);

    /// <summary>Makes the store hold exactly the configured clients.</summary>
    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        var clients = OidcClients.FromConfiguration(configuration);
        await using var scope = scopes.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        foreach (var client in clients)
        {
            var existing = await manager.FindByClientIdAsync(client.ClientId!, cancellationToken);
            if (existing is null)
            {
                try
                {
                    await manager.CreateAsync(client, cancellationToken);
                    continue;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // Another instance created it a moment ago: update that one.
                    existing = await manager.FindByClientIdAsync(client.ClientId!, cancellationToken);
                    if (existing is null)
                    {
                        throw;
                    }
                }
            }

            await manager.UpdateAsync(existing, client, cancellationToken);
        }

        foreach (var clientId in _knownClients.Except(clients.Select(c => c.ClientId!)))
        {
            if (await manager.FindByClientIdAsync(clientId, cancellationToken) is { } stale)
            {
                await manager.DeleteAsync(stale, cancellationToken);
            }
        }

        LogClients(logger, clients.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "OpenID Connect clients registered: {Count}")]
    private static partial void LogClients(ILogger logger, int count);
}
