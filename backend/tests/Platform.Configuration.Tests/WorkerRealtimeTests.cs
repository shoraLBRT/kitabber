using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Platform.Realtime;
using Platform.Workers;

namespace Platform.Configuration.Tests;

/// <summary>
/// E12-04 (ADR-0123): the workers host sends realtime events — the Notifications module tells the recipients' pages
/// about new notifications — through the Redis backplane of the API's hub, without serving clients itself.
/// </summary>
public sealed class WorkerRealtimeTests
{
    private static string[] Args(string cache) => HostSettings.Workers(
        "--environment", PlatformEnvironments.Production, "--Messaging:Transport", "InMemory", "--Cache:Provider", cache,
        "--Mail:Provider", "InMemory", "--ConnectionStrings:Platform", "Host=localhost;Database=never_contacted",
        "--ConnectionStrings:Redis", "localhost:1,abortConnect=false");

    [Fact]
    public void WorkersPublishThroughTheRedisBackplane()
    {
        using var host = WorkerHost.CreateBuilder(Args("Redis")).Build();

        Assert.NotNull(host.Services.GetRequiredService<IRealtimePublisher>());
        var lifetime = host.Services.GetRequiredService<HubLifetimeManager<RealtimeHub>>();
        Assert.Contains("Redis", lifetime.GetType().Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithTheInMemoryCacheAnEventReachesNoOne()
    {
        using var host = WorkerHost.CreateBuilder(Args("InMemory")).Build();

        await host.Services.GetRequiredService<IRealtimePublisher>().PublishAsync(
            RealtimeTopic.Account(Guid.NewGuid()), "notifications.changed", null, TestContext.Current.CancellationToken);
    }
}
