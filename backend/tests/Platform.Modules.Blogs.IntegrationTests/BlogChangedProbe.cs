using System.Collections.Concurrent;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Blogs.Contracts;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>
/// Listens to <see cref="BlogChanged"/> on the bus of an API host (ADR-0094) on an endpoint of its own, so tests that
/// share the host do not see each other's events.
/// </summary>
public sealed class BlogChangedProbe : IAsyncDisposable
{
    private readonly ConcurrentQueue<BlogChanged> _events = new();
    private readonly HostReceiveEndpointHandle _endpoint;

    private BlogChangedProbe(IServiceProvider services) =>
        _endpoint = services.GetRequiredService<IReceiveEndpointConnector>().ConnectReceiveEndpoint(
            $"blogs-tests-blog-changed-{Guid.NewGuid():N}",
            (_, e) => e.Handler<BlogChanged>(c =>
            {
                _events.Enqueue(c.Message);
                return Task.CompletedTask;
            }));

    public static async Task<BlogChangedProbe> StartAsync(IServiceProvider services)
    {
        var probe = new BlogChangedProbe(services);
        await probe._endpoint.Ready;
        return probe;
    }

    /// <summary>Waits until <paramref name="count"/> events about <paramref name="blogId"/> came, at most 30 s.</summary>
    public async Task WaitForAsync(Guid blogId, int count = 1)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (_events.Count(e => e.BlogId == blogId) < count)
        {
            Assert.True(DateTime.UtcNow < deadline, $"BlogChanged #{count} of {blogId} was not published in 30s");
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    public async ValueTask DisposeAsync() => await _endpoint.StopAsync(CancellationToken.None);
}
