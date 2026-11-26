using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Caching;
using Platform.Testing.Api;
using Platform.Testing.Data;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// E10-09: view counters in Redis with a batched flush to the database (ADR-0083) — a count waits in the buffer, not
/// in the process, so it survives the restart of the instance that took it, and applying a batch again changes
/// nothing.
/// </summary>
public sealed class ViewFlushTests(PostsApi api) : IClassFixture<PostsApi>, IAsyncLifetime
{
    private const string _chrome =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36";

    private Guid _author;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _author = Guid.CreateVersion7(api.Time.GetUtcNow());
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CountsWaitInTheBufferUntilTheFlush()
    {
        var post = await SavePostAsync();

        await OpenAsync(api, post.Id, Guid.CreateVersion7());
        await OpenAsync(api, post.Id, Guid.CreateVersion7());

        Assert.Equal(0, await ViewCountAsync(post.Id));
        Assert.Equal(2, await FlushAsync());
        Assert.Equal(2, await ViewCountAsync(post.Id));
    }

    [Fact]
    public async Task CountsSurviveARestartOfTheInstanceThatTookThem()
    {
        var post = await SavePostAsync();

        // An instance that never flushes takes the views and goes away, as a process that is restarted.
        await using (var stopped = api.WithWebHostBuilder(b => b.UseSetting(ViewFlusher.FlushSecondsKey, "0")))
        {
            using var client = stopped.CreateClient();
            for (var i = 0; i < 3; i++)
            {
                await OpenAsync(client, post.Id, Guid.CreateVersion7());
            }
        }

        Assert.Equal(0, await ViewCountAsync(post.Id));
        Assert.Equal(3, await FlushAsync());
        Assert.Equal(3, await ViewCountAsync(post.Id));
    }

    [Fact]
    public async Task ABatchAppliedTwiceCountsOnce()
    {
        var first = await SavePostAsync();
        var second = await SavePostAsync();
        var reader = Guid.CreateVersion7();
        await OpenAsync(api, first.Id, reader);
        await OpenAsync(api, second.Id, reader);
        await OpenAsync(api, second.Id, Guid.CreateVersion7());

        // An instance claims the batch and stops before completing it; after the lease another one applies it.
        var buffer = api.Services.GetRequiredService<IWriteBuffer>();
        var abandoned = await buffer.ClaimAsync(ViewFlusher.Buffer, ViewFlusher.Lease, Ct);
        Assert.Equal(3, abandoned!.Entries.Count);
        Assert.Equal(0, await FlushAsync());
        api.Time.Advance(ViewFlusher.Lease + TimeSpan.FromSeconds(1));
        await FlushAsync();
        Assert.Equal((1, 2), (await ViewCountAsync(first.Id), await ViewCountAsync(second.Id)));

        // The same entries applied once more — the first claimer did write them before it stopped — change nothing.
        foreach (var entry in abandoned.Entries)
        {
            await buffer.AppendAsync(ViewFlusher.Buffer, entry, Ct);
        }

        Assert.Equal(3, await FlushAsync());
        Assert.Equal((1, 2), (await ViewCountAsync(first.Id), await ViewCountAsync(second.Id)));
        await using var scope = api.Services.CreateAsyncScope();
        Assert.Equal(3, await scope.ServiceProvider.GetRequiredService<PostsDbContext>().ViewMarks.CountAsync(Ct));
    }

    private async Task<Post> SavePostAsync()
    {
        var post = new PostFactory(api.Data).Build();
        var mine = Post.CreateDraft(_author, post.BlogId, post.Title, post.Body, api.Time.GetUtcNow());
        mine.Publish(api.Time.GetUtcNow());
        await api.SaveAsync<PostsDbContext>(mine);
        return mine;
    }

    private static async Task OpenAsync(PostsApi host, Guid postId, Guid reader)
    {
        using var client = host.CreateClient();
        await OpenAsync(client, postId, reader);
    }

    private static async Task OpenAsync(HttpClient client, Guid postId, Guid reader)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/posts/{postId}/open");
        request.Headers.TryAddWithoutValidation("User-Agent", _chrome);
        request.As(reader.ToString("D"));
        using var response = await client.SendAsync(request, Ct);
        response.EnsureSuccessStatusCode();
    }

    private Task<int> FlushAsync() => api.Services.GetRequiredService<ViewFlusher>().FlushAsync(Ct);

    private async Task<int> ViewCountAsync(Guid postId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PostsDbContext>().Posts
            .Where(p => p.Id == postId).Select(p => p.ViewCount).SingleAsync(Ct);
    }
}
