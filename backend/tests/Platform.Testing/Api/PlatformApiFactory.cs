using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Platform.Configuration;
using Platform.Testing.Time;

namespace Platform.Testing.Api;

/// <summary>
/// The Api host in process, in the <c>Test</c> environment, with a <see cref="FakeTimeProvider"/> in place of the
/// system clock (ADR-0008). Tests add settings with <see cref="WithSetting"/> and modules or stand-ins with
/// <see cref="WithModule"/> before the first request. Needs no Docker; <see cref="IntegrationApiFactory"/> adds
/// PostgreSQL and Redis.
/// </summary>
public class PlatformApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _settings = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Action<IServiceCollection, IConfiguration>> _modules = [];

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
        });
    }
}
