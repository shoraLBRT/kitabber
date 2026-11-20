using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Platform.Caching;
using Platform.Realtime;
using Platform.Testing.Api;
using Platform.Testing.Containers;

namespace Platform.Api.IntegrationTests;

/// <summary>
/// Acceptance checks of E01-11 (#25) on real Redis: a signed-in client receives the events of its own topics and not
/// of anyone else's, a client without a live ticket or token is not let in, nobody subscribes to another account,
/// and the Redis backplane carries an event published on one API instance to a client of another (ADR-0019).
/// The client side of reconnecting is checked in frontend/tests/realtime.spec.ts.
/// </summary>
public sealed class RealtimeIntegrationTests(RealtimeRedis redis) : IClassFixture<RealtimeRedis>, IAsyncLifetime
{
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(10);
    private readonly List<IAsyncDisposable> _disposables = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Every test uses accounts and posts of its own, so they share the class's Redis database without a flush.
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var disposable in Enumerable.Reverse(_disposables))
        {
            await disposable.DisposeAsync();
        }
    }

    [Fact]
    public async Task ClientReceivesItsOwnAccountEventsAndNotAnothers()
    {
        var api = Instance();
        Guid alice = Guid.NewGuid(), bob = Guid.NewGuid();
        var aliceClient = await ConnectAsync(api, await TicketAsync(api, alice));
        var bobClient = await ConnectAsync(api, await TicketAsync(api, bob));

        var publisher = api.Services.GetRequiredService<IRealtimePublisher>();
        await publisher.PublishAsync(RealtimeTopic.Account(alice), "notification.created", new { n = 1 }, Ct);
        await publisher.PublishAsync(RealtimeTopic.Account(bob), "notification.created", new { n = 2 }, Ct);

        var aliceEvent = await aliceClient.NextAsync();
        Assert.Equal(RealtimeTopic.Account(alice).Name, aliceEvent.Topic);
        Assert.Equal("notification.created", aliceEvent.Type);
        // Bob's own event comes after Alice's was published: had Alice's reached him, it would be first.
        Assert.Equal(RealtimeTopic.Account(bob).Name, (await bobClient.NextAsync()).Topic);
        Assert.False(aliceClient.HasMore);
        Assert.False(bobClient.HasMore);
    }

    [Fact]
    public async Task OnlySubscribersReceiveThePostTopic()
    {
        var api = Instance();
        Guid alice = Guid.NewGuid(), bob = Guid.NewGuid(), post = Guid.NewGuid();
        var aliceClient = await ConnectAsync(api, await TicketAsync(api, alice));
        // Bob connects with the host's own scheme (a server-side client with a token), not with a ticket.
        var bobClient = await ConnectAsync(api, ticket: null, account: bob);
        await aliceClient.Connection.InvokeAsync("Subscribe", RealtimeTopic.Post(post).Name, Ct);

        var publisher = api.Services.GetRequiredService<IRealtimePublisher>();
        await publisher.PublishAsync(RealtimeTopic.Post(post), "comment.created", new { text = "первый" }, Ct);
        await publisher.PublishAsync(RealtimeTopic.Account(bob), "ping", null, Ct);

        var comment = await aliceClient.NextAsync();
        Assert.Equal(RealtimeTopic.Post(post).Name, comment.Topic);
        Assert.Equal("comment.created", comment.Type);
        Assert.Equal("ping", (await bobClient.NextAsync()).Type);

        await aliceClient.Connection.InvokeAsync("Unsubscribe", RealtimeTopic.Post(post).Name, Ct);
        await publisher.PublishAsync(RealtimeTopic.Post(post), "comment.created", null, Ct);
        await publisher.PublishAsync(RealtimeTopic.Account(alice), "ping", null, Ct);
        Assert.Equal("ping", (await aliceClient.NextAsync()).Type);
    }

    [Theory]
    [InlineData("account:{other}")]
    [InlineData("blog:{other}")]
    [InlineData("post:not-a-uuid")]
    [InlineData("post")]
    [InlineData("")]
    public async Task ClientCannotSubscribeToAnotherAccountOrAnUnknownTopic(string topic)
    {
        var api = Instance();
        Guid alice = Guid.NewGuid(), bob = Guid.NewGuid();
        var aliceClient = await ConnectAsync(api, await TicketAsync(api, alice));
        var bobClient = await ConnectAsync(api, await TicketAsync(api, bob));

        var error = await Assert.ThrowsAsync<HubException>(() =>
            aliceClient.Connection.InvokeAsync("Subscribe", topic.Replace("{other}", bob.ToString("D"), StringComparison.Ordinal), Ct));
        Assert.Contains(RealtimeHub.TopicRefused, error.Message, StringComparison.Ordinal);

        // Still connected, and still not in Bob's group.
        await api.Services.GetRequiredService<IRealtimePublisher>()
            .PublishAsync(RealtimeTopic.Account(bob), "private", null, Ct);
        await api.Services.GetRequiredService<IRealtimePublisher>()
            .PublishAsync(RealtimeTopic.Account(alice), "ping", null, Ct);
        Assert.Equal("ping", (await aliceClient.NextAsync()).Type);
        Assert.Equal("private", (await bobClient.NextAsync()).Type);
    }

    [Fact]
    public async Task TicketInTheAuthorizationHeaderOpensTheHub()
    {
        // Negotiation and long polling carry the ticket as a bearer header, not in the address.
        var api = Instance();
        var alice = Guid.NewGuid();
        var ticket = await TicketAsync(api, alice);
        var client = await ConnectAsync(api, ticket: null, bearer: ticket.Ticket);

        await api.Services.GetRequiredService<IRealtimePublisher>()
            .PublishAsync(RealtimeTopic.Account(alice), "ping", null, Ct);
        Assert.Equal("ping", (await client.NextAsync()).Type);

        var forged = await Assert.ThrowsAsync<HttpRequestException>(() => ConnectAsync(api, ticket: null, bearer: "forged"));
        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
    }

    [Fact]
    public async Task ConnectionWithoutTicketOrTokenIsRejected()
    {
        var api = Instance();

        var anonymous = await Assert.ThrowsAsync<HttpRequestException>(() => ConnectAsync(api, ticket: null));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var forged = await Assert.ThrowsAsync<HttpRequestException>(() => ConnectAsync(api, "forged-ticket"));
        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
    }

    [Fact]
    public async Task ExpiredTicketIsRejected()
    {
        var api = Instance();
        var ticket = await TicketAsync(api, Guid.NewGuid());

        api.Time.Advance(RealtimeTicketStore.Lifetime);

        var expired = await Assert.ThrowsAsync<HttpRequestException>(() => ConnectAsync(api, ticket.Ticket));
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    [Fact]
    public async Task TicketIsIssuedToAnAuthenticatedAccountOnly()
    {
        var api = Instance();
        using var http = api.CreateClient();

        using var anonymous = await http.PostAsync(new Uri("/api/v1/realtime/tickets", UriKind.Relative), null, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        // A ticket opens the hub and nothing else: it does not buy a fresh ticket.
        var ticket = await TicketAsync(api, Guid.NewGuid());
        using var withTicket = await http.PostAsync(
            new Uri($"/api/v1/realtime/tickets?access_token={ticket.Ticket}", UriKind.Relative), null, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, withTicket.StatusCode);

        Assert.True(ticket.ExpiresAt == api.Time.GetUtcNow() + RealtimeTicketStore.Lifetime);
    }

    [Fact]
    public async Task RedisBackplaneDeliversAcrossInstances()
    {
        var first = Instance();
        var second = Instance();
        Guid alice = Guid.NewGuid(), post = Guid.NewGuid();
        // The ticket from one instance opens the hub of another: tickets live in the shared Redis.
        var client = await ConnectAsync(second, await TicketAsync(first, alice));
        await client.Connection.InvokeAsync("Subscribe", RealtimeTopic.Post(post).Name, Ct);

        var publisher = first.Services.GetRequiredService<IRealtimePublisher>();
        await publisher.PublishAsync(RealtimeTopic.Account(alice), "notification.created", null, Ct);
        await publisher.PublishAsync(RealtimeTopic.Post(post), "comment.created", null, Ct);

        Assert.Equal(RealtimeTopic.Account(alice).Name, (await client.NextAsync()).Topic);
        Assert.Equal(RealtimeTopic.Post(post).Name, (await client.NextAsync()).Topic);
    }

    [Fact]
    public async Task NewConnectionOfTheSameAccountRejoinsItsTopic()
    {
        // A reconnect is a new connection: it gets the account topic again without asking.
        var api = Instance();
        var alice = Guid.NewGuid();
        var dropped = await ConnectAsync(api, await TicketAsync(api, alice));
        await dropped.Connection.StopAsync(Ct);
        var again = await ConnectAsync(api, await TicketAsync(api, alice));

        await api.Services.GetRequiredService<IRealtimePublisher>()
            .PublishAsync(RealtimeTopic.Account(alice), "notification.created", null, Ct);

        Assert.Equal("notification.created", (await again.NextAsync()).Type);
    }

    private PlatformApiFactory Instance()
    {
        var api = new PlatformApiFactory()
            .WithSetting(CacheServiceCollectionExtensions.ProviderKey, nameof(CacheProvider.Redis))
            .WithSetting($"ConnectionStrings:{CacheServiceCollectionExtensions.RedisConnectionName}", redis.ConnectionString)
            .WithModule(services => services.AddTestAuthentication());
        _disposables.Add(api);
        return api;
    }

    private static async Task<RealtimeTicket> TicketAsync(PlatformApiFactory api, Guid account)
    {
        using var http = api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/realtime/tickets").As(account.ToString("D"));
        using var response = await http.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RealtimeTicket>(Ct))!;
    }

    private Task<Client> ConnectAsync(PlatformApiFactory api, RealtimeTicket issued) => ConnectAsync(api, issued.Ticket);

    /// <summary>
    /// Opens the hub the way the browser does: the ticket in <c>access_token</c> of the address. Without a ticket,
    /// <paramref name="account"/> signs in through the test scheme, as a server-side client with a token would.
    /// </summary>
    private async Task<Client> ConnectAsync(
        PlatformApiFactory api, string? ticket, Guid? account = null, string? bearer = null)
    {
        var address = new Uri(api.Server.BaseAddress, RealtimeServiceCollectionExtensions.HubPath
            + (ticket is null ? string.Empty : $"?access_token={Uri.EscapeDataString(ticket)}"));
        var connection = new HubConnectionBuilder()
            .WithUrl(address, options =>
            {
                // The in-process server has no sockets: long polling through its handler.
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
                if (bearer is not null)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(bearer);
                }

                if (account is not null)
                {
                    options.Headers[TestAuthentication.AccountHeader] = account.Value.ToString("D");
                }
            })
            .Build();
        var client = new Client(connection);
        try
        {
            await connection.StartAsync(Ct);
            // The server answers the handshake before it puts the connection in its account topic; it reads
            // invocations only after that, so one round trip means the connection is fully joined.
            await connection.InvokeAsync("Unsubscribe", RealtimeTopic.Post(Guid.Empty).Name, Ct);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        _disposables.Add(connection);
        return client;
    }

    private sealed class Client
    {
        private readonly Channel<RealtimeEvent> _events = Channel.CreateUnbounded<RealtimeEvent>();

        public Client(HubConnection connection)
        {
            Connection = connection;
            connection.On<RealtimeEvent>(nameof(IRealtimeClient.Receive), e => _events.Writer.TryWrite(e));
        }

        public HubConnection Connection { get; }

        public bool HasMore => _events.Reader.TryPeek(out _);

        public async Task<RealtimeEvent> NextAsync()
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            timeout.CancelAfter(_wait);
            return await _events.Reader.ReadAsync(timeout.Token);
        }
    }
}

/// <summary>One Redis database for the whole test class: there are only 15 (RedisFixture).</summary>
public sealed class RealtimeRedis(RedisFixture redis) : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        ConnectionString = redis.CreateDatabase();
        await RedisFixture.FlushAsync(ConnectionString);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
