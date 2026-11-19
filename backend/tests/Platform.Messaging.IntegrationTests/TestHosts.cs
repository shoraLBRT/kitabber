using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Workers;

namespace Platform.Messaging.IntegrationTests;

internal static class TestHosts
{
    /// <summary>Builds the workers host (<see cref="WorkerHost"/>) with test configuration and test consumers.</summary>
    public static IHost Worker(
        IDictionary<string, string?> settings,
        Action<IServiceCollection>? services = null,
        Action<IBusRegistrationConfigurator>? bus = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            EnvironmentName = "Test",
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(settings)
        {
            ["Cache:Provider"] = "InMemory",
        });
        WorkerHost.Configure(builder.Services, builder.Configuration, bus);
        services?.Invoke(builder.Services);
        return builder.Build();
    }

    public static Dictionary<string, string?> RabbitMq(string connectionString) => new()
    {
        ["Messaging:Transport"] = "RabbitMq",
        ["ConnectionStrings:RabbitMq"] = connectionString,
    };

    /// <summary>Waits until <paramref name="condition"/> holds; fails after <paramref name="timeout"/>.</summary>
    public static async Task Eventually(Func<Task<bool>> condition, TimeSpan timeout, string because)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!await condition())
        {
            if (cts.IsCancellationRequested)
            {
                Assert.Fail($"timed out after {timeout.TotalSeconds:0}s waiting until {because}");
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    public static Task Eventually(Func<bool> condition, TimeSpan timeout, string because) =>
        Eventually(() => Task.FromResult(condition()), timeout, because);
}
