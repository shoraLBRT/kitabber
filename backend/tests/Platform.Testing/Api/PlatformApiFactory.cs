using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Platform.Caching;
using Platform.Configuration;
using Platform.Messaging;
using Platform.Modules.Identity;
using Platform.Modules.Identity.SignIn;
using Platform.Modules.Media;
using Platform.Testing.Time;

namespace Platform.Testing.Api;

/// <summary>
/// The Api host in process, in the <c>Test</c> environment, with a <see cref="FakeTimeProvider"/> in place of the
/// system clock (ADR-0008). Tests add settings with <see cref="WithSetting"/> and modules or stand-ins with
/// <see cref="WithModule"/> before the first request. The cache and the rate limit counters are in memory
/// (<c>Cache:Provider=InMemory</c>) unless a test sets otherwise. Needs no Docker; <see cref="IntegrationApiFactory"/> adds
/// PostgreSQL and Redis.
/// </summary>
public class PlatformApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The address of imgproxy in test hosts: nothing answers there, the links are only checked.</summary>
    public const string ImgproxyBaseUrl = "http://img.test/";

    /// <summary>The imgproxy signing key of test hosts, in hex (ADR-0037).</summary>
    public const string ImgproxyKey = "6b6579";

    /// <summary>The imgproxy signing salt of test hosts, in hex (ADR-0037).</summary>
    public const string ImgproxySalt = "73616c74";

    private readonly Dictionary<string, string?> _settings = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Action<IServiceCollection, IConfiguration>> _modules = [];

    /// <summary>Whether the host has a real database; without one the module databases' readiness checks are left out.</summary>
    protected virtual bool HasDatabase => false;

    /// <summary>The clock of the host: every <see cref="TimeProvider"/> in it is this one.</summary>
    public FakeTimeProvider Time { get; } = TestClock.Create();

    /// <summary>Sets a configuration value of the host, as an environment variable would.</summary>
    public PlatformApiFactory WithSetting(string key, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _settings[key] = value;
        return this;
    }

    /// <summary>
    /// Registers services the way a module does in the host, with the host configuration. Test data (factories and
    /// the seeder, E01-17) will hook in here too.
    /// </summary>
    public PlatformApiFactory WithModule(Action<IServiceCollection, IConfiguration> register)
    {
        ArgumentNullException.ThrowIfNull(register);
        _modules.Add(register);
        return this;
    }

    /// <inheritdoc cref="WithModule(Action{IServiceCollection, IConfiguration})"/>
    public PlatformApiFactory WithModule(Action<IServiceCollection> register)
    {
        ArgumentNullException.ThrowIfNull(register);
        return WithModule((services, _) => register(services));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment(PlatformEnvironments.Test);
        builder.UseSetting(CacheServiceCollectionExtensions.ProviderKey, nameof(CacheProvider.InMemory));
        // The Identity module needs the platform database; in process there is none (IntegrationApiFactory gives
        // one), and nothing reaches it: the clients are not written at start and its readiness check is left out.
        builder.UseSetting(IdentitySettings.DatabaseKey, "Host=localhost;Port=1;Database=no_database");
        builder.UseSetting(IdentitySettings.SyncClientsKey, "false");
        // The bus is in memory; a test that needs a broker sets Messaging:Transport back to RabbitMq.
        builder.UseSetting(MessagingServiceCollectionExtensions.TransportKey, nameof(MessagingTransport.InMemory));
        // The object storage is the in-memory fake; a test of the S3 storage starts MinIO itself (MinioFixture).
        builder.UseSetting(MediaSettings.ProviderKey, nameof(StorageProvider.InMemory));
        // Images are delivered by imgproxy (ADR-0037); in process there is none, and the links are only built.
        builder.UseSetting(MediaSettings.ImagesBaseUrlKey, ImgproxyBaseUrl);
        builder.UseSetting(MediaSettings.ImagesKeyKey, ImgproxyKey);
        builder.UseSetting(MediaSettings.ImagesSaltKey, ImgproxySalt);
        // Hotness passes (ADR-0086) would run whenever a test moves the clock; a test of hotness runs them itself.
        builder.UseSetting(Platform.Modules.Posts.PostsSettings.HotnessIntervalSecondsKey, "0");
        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices((context, services) =>
        {
            foreach (var register in _modules)
            {
                register(services, context.Configuration);
            }
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            if (!HasDatabase)
            {
                // Without a database the Identity outbox has nothing to deliver and would only fail to connect.
                foreach (var outbox in services.Where(IsIdentityOutboxService).ToList())
                {
                    services.Remove(outbox);
                }

                services.Configure<HealthCheckServiceOptions>(options =>
                {
                    foreach (var check in options.Registrations.Where(r => r.Name.StartsWith("database:", StringComparison.Ordinal)).ToList())
                    {
                        options.Registrations.Remove(check);
                    }
                });
            }
        });
    }

    private static bool IsIdentityOutboxService(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(IHostedService)
        && descriptor.ImplementationType is { IsGenericType: true } type
        && type.GetGenericArguments().Contains(typeof(IdentityDbContext));
}
