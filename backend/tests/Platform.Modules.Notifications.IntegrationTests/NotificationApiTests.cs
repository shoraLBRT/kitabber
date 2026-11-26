using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Blogs.Contracts;
using Platform.Testing.Api;
using Platform.Testing.Containers;

namespace Platform.Modules.Notifications.IntegrationTests;

/// <summary>The Api host on PostgreSQL and Redis in Testcontainers, signed in by the test scheme.</summary>
public sealed class NotificationsApi : IntegrationApiFactory
{
    public NotificationsApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis) => WithModule(services => services.AddTestAuthentication());
}

/// <summary>
/// E12-02 (FR-NOTIFY, ADR-0093): the notifications of the signed-in account through <c>/api/v1/notifications/me</c> —
/// the feed newest first, page by page with a cursor, with the person who acted; the counter of the unread ones;
/// marking the shown ones read, which lowers the counter and changes nothing for another's notification; refused
/// requests; no access for a guest.
/// </summary>
public sealed class NotificationApiTests(NotificationsApi api) : IClassFixture<NotificationsApi>, IAsyncLifetime
{
    private const string _feed = "/api/v1/notifications/me";
    private const string _unread = "/api/v1/notifications/me/unread";
    private const string _read = "/api/v1/notifications/me/read";
    private readonly Guid _anna = Guid.CreateVersion7();
    private readonly Guid _boris = Guid.CreateVersion7();
    private readonly Guid _post = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await api.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task FeedIsNewestFirstPageByPageWithThePersonWhoActed()
    {
        await NameBlogAsync(_boris, "Борис");
        var saved = await SaveAsync(_anna, 3);
        using var client = api.CreateClient();

        using var first = await SendAsync(client, HttpMethod.Get, $"{_feed}?limit=2", _anna);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains("no-store", first.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
        var page = await first.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([saved[2].Id, saved[1].Id], items.Select(i => i.GetProperty("id").GetGuid()));
        var row = items[0];
        Assert.Equal("comment_reply", row.GetProperty("type").GetString());
        Assert.Equal(_post, row.GetProperty("postId").GetGuid());
        Assert.False(row.GetProperty("read").GetBoolean());
        Assert.Equal(_boris, row.GetProperty("actor").GetProperty("accountId").GetGuid());
        Assert.Equal("Борис", row.GetProperty("actor").GetProperty("name").GetString());
        var cursor = page.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrEmpty(cursor));

        using var second = await SendAsync(
            client, HttpMethod.Get, $"{_feed}?limit=2&cursor={Uri.EscapeDataString(cursor!)}", _anna);
        var rest = await second.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(
            [saved[0].Id],
            rest.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
        Assert.Equal(JsonValueKind.Null, rest.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task MarkingTheShownOnesReadLowersTheCounter()
    {
        var saved = await SaveAsync(_anna, 3);
        var others = await SaveAsync(_boris, 1);
        using var client = api.CreateClient();
        Assert.Equal(3, await UnreadAsync(client, _anna));

        // Anna names one of Boris's notifications too: it stays unread.
        using var marked = await SendAsync(
            client, HttpMethod.Post, _read, _anna, new { ids = new[] { saved[2].Id, saved[1].Id, others[0].Id } });
        using var again = await SendAsync(client, HttpMethod.Post, _read, _anna, new { ids = new[] { saved[2].Id } });

        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        Assert.Equal(1, (await marked.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("unread").GetInt32());
        Assert.Equal(1, (await again.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("unread").GetInt32());
        Assert.Equal(1, await UnreadAsync(client, _anna));
        Assert.Equal(1, await UnreadAsync(client, _boris));
        using var feed = await SendAsync(client, HttpMethod.Get, _feed, _anna);
        var read = (await feed.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("read").GetBoolean());
        Assert.Equal([true, true, false], read);
    }

    [Fact]
    public async Task OneAccountsFeedIsNotAnothers()
    {
        await SaveAsync(_anna, 2);
        using var client = api.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Get, _feed, _boris);

        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Equal(0, await UnreadAsync(client, _boris));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(NotificationEndpoints.MaxMarked + 1)]
    public async Task MarkingNoneOrTooManyIsAValidationError(int count)
    {
        using var client = api.CreateClient();

        using var response = await SendAsync(
            client, HttpMethod.Post, _read, _anna, new { ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("ids", out _));
    }

    [Fact]
    public async Task LimitOutOfRangeIsAValidationError()
    {
        using var client = api.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Get, $"{_feed}?limit=0", _anna);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GuestHasNoNotifications()
    {
        using var client = api.CreateClient();

        using var feed = await SendAsync(client, HttpMethod.Get, _feed, null);
        using var unread = await SendAsync(client, HttpMethod.Get, _unread, null);
        using var read = await SendAsync(client, HttpMethod.Post, _read, null, new { ids = new[] { Guid.NewGuid() } });

        Assert.Equal(HttpStatusCode.Unauthorized, feed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unread.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
    }

    // Notifications of a recipient from Boris, a minute apart, the oldest first.
    private async Task<List<Notification>> SaveAsync(Guid recipient, int count)
    {
        var start = api.Time.GetUtcNow();
        List<Notification> saved =
        [
            .. Enumerable.Range(0, count).Select(i => new Notification
            {
                Id = Guid.CreateVersion7(),
                RecipientAccountId = recipient,
                Type = NotificationType.CommentReply,
                ActorAccountId = _boris,
                PostId = _post,
                CommentId = Guid.CreateVersion7(),
                SourceKey = $"comment:{Guid.NewGuid():N}",
                CreatedAt = start.AddMinutes(i),
            }),
        ];
        await api.SaveAsync<NotificationsDbContext>([.. saved]);
        return saved;
    }

    // The personal blog of an account, with a name: the feed shows the person who acted by it.
    private async Task NameBlogAsync(Guid account, string name)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var blog = await scope.ServiceProvider.GetRequiredService<IBlogDirectory>().EnsurePersonalBlogAsync(account, Ct);
        var db = scope.ServiceProvider.GetRequiredService<Blogs.BlogsDbContext>();
        var subsite = await db.Subsites.SingleAsync(s => s.Id == blog.Id, Ct);
        subsite.Rename(name);
        await db.SaveChangesAsync(Ct);
    }

    private static async Task<int> UnreadAsync(HttpClient client, Guid account)
    {
        using var response = await SendAsync(client, HttpMethod.Get, _unread, account);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("unread").GetInt32();
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string path, Guid? account, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request, Ct);
    }
}
