using System.Reflection;
using System.Security.Cryptography;
using Platform.Caching;
using Platform.Configuration;
using Platform.Http.RateLimiting;
using Platform.Messaging;
using Platform.Modules.Identity.SignIn;
using Platform.Observability;

namespace Platform.Api;

/// <summary>
/// The settings the API host checks at start (ADR-0007): those of every library the host registers. A module adds
/// its settings here together with its registration; each one is described in docs/CONFIG.md.
/// </summary>
public static class ApiSettings
{
    public static IReadOnlyList<Setting> All { get; } =
    [
        .. ObservabilitySettings.All, .. CacheSettings.All, .. RateLimitingSettings.All, .. IdentitySettings.All,
        .. MessagingSettings.All,
    ];

    /// <summary>
    /// The build writes the OpenAPI document by running the entry point under <c>GetDocument.Insider</c> (ADR-0006),
    /// with no stand settings, no Redis and no broker. There the cache, the rate limit counters and the bus live in
    /// memory; the host never serves a request.
    /// </summary>
    public static WebApplicationBuilder UseInMemoryStoresForOpenApiBuild(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, "GetDocument.Insider", StringComparison.Ordinal))
        {
            builder.Configuration[CacheServiceCollectionExtensions.ProviderKey] = nameof(CacheProvider.InMemory);
            builder.Configuration[MessagingServiceCollectionExtensions.TransportKey] = nameof(MessagingTransport.InMemory);
            // No database is contacted and no token is issued: the host is built to describe its endpoints, never
            // started (yet its hosted services run), so keys of its own are enough.
            builder.Configuration[IdentitySettings.DatabaseKey] ??= "Host=localhost;Database=openapi_build";
            builder.Configuration[IdentitySettings.SyncClientsKey] = "false";
            using var rsa = RSA.Create(2048);
            builder.Configuration[IdentitySettings.SigningKeyKey] = rsa.ExportPkcs8PrivateKeyPem();
            builder.Configuration[IdentitySettings.EncryptionKeyKey] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }

        return builder;
    }
}
