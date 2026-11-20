using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Platform.Realtime;
using static Platform.Modules.Identity.IntegrationTests.SignIn.OidcFlow;

namespace Platform.Modules.Identity.IntegrationTests.SignIn;

/// <summary>
/// The realtime channel (E01-11, ADR-0019) with the provider's real tokens: the site's server trades a live access
/// token for a hub ticket, and an expired token buys none; a server-side client opens the hub with the token itself,
/// and an expired one is not let in.
/// </summary>
public sealed class RealtimeSignInTests(IdentityApi api) : IClassFixture<IdentityApi>, IAsyncLifetime
{
    private const string TicketPath = "/api/v1/realtime/tickets";
    private HttpClient _browser = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetWithClientsAsync();
        _browser = api.CreateBrowser();
    }

    public ValueTask DisposeAsync()
    {
        _browser.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AccessTokenBuysATicketThatOpensTheHub()
    {
        var (_, tokens) = await SignInAsync(api, _browser);

        using var response = await PostTicketAsync(tokens.GetProperty("access_token").GetString());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ticket = (await response.Content.ReadFromJsonAsync<RealtimeTicket>(Ct))!;

        await using var connection = Hub(bearer: ticket.Ticket);
        await connection.StartAsync(Ct);
        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task ExpiredAccessTokenBuysNoTicket()
    {
        var (_, tokens) = await SignInAsync(api, _browser);
        api.Time.Advance(TimeSpan.FromMinutes(16));

        using var response = await PostTicketAsync(tokens.GetProperty("access_token").GetString());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AccessTokenOpensTheHubUntilItExpires()
    {
        var (_, tokens) = await SignInAsync(api, _browser);
        var token = tokens.GetProperty("access_token").GetString()!;

        await using (var live = Hub(bearer: token))
        {
            await live.StartAsync(Ct);
        }

        api.Time.Advance(TimeSpan.FromMinutes(16));
        await using var expired = Hub(bearer: token);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => expired.StartAsync(Ct));
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    private async Task<HttpResponseMessage> PostTicketAsync(string? accessToken)
    {
        using var client = api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, TicketPath);
        request.Headers.Authorization = new("Bearer", accessToken);
        return await client.SendAsync(request, Ct);
    }

    private HubConnection Hub(string bearer) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(api.Server.BaseAddress, RealtimeServiceCollectionExtensions.HubPath), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(bearer);
            })
            .Build();
}
