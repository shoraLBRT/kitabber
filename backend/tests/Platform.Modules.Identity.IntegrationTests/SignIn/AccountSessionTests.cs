using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Modules.Identity.SignIn;
using static Platform.Modules.Identity.IntegrationTests.SignIn.OidcFlow;

namespace Platform.Modules.Identity.IntegrationTests.SignIn;

/// <summary>
/// The devices of an account (E03-09, ADR-0078) on a real database: every sign-in is a device with the time it was last
/// active, the one of the request is marked, and a device signed out from another one — alone or with «Выйти со всех
/// устройств» — loses its access token, its refresh token and its provider's cookie at once in this instance.
/// </summary>
public sealed class AccountSessionTests(IdentityApi api) : IClassFixture<IdentityApi>, IAsyncLifetime
{
    private const string SessionsPath = "/api/v1/account/sessions";
    private const string Chrome = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36";
    private const string Safari = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Mobile/15E148 Safari/604.1";
    private const string Firefox = "Mozilla/5.0 (X11; Linux x86_64; rv:133.0) Gecko/20100101 Firefox/133.0";

    private readonly List<HttpClient> _browsers = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await api.ResetWithClientsAsync();

    public ValueTask DisposeAsync()
    {
        _browsers.ForEach(b => b.Dispose());
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task DevicesAreListedWithTheirLastActivityAndTheCurrentOneIsMarked()
    {
        var account = await api.CreateAccountAsync();
        var laptop = await SignInDeviceAsync(account.Id, Chrome);
        api.Time.Advance(TimeSpan.FromMinutes(5));
        var phone = await SignInDeviceAsync(account.Id, Safari);
        api.Time.Advance(TimeSpan.FromMinutes(5));
        var refreshedAt = api.Time.GetUtcNow();
        var (status, _) = await RefreshAsync(laptop.Browser, laptop.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, status);

        var sessions = await ListAsync(phone.AccessToken);

        Assert.Collection(sessions,
            s =>
            {
                Assert.Equal("Chrome, Windows", s.GetProperty("device").GetString());
                Assert.Equal(refreshedAt, s.GetProperty("lastActiveAt").GetDateTimeOffset());
                Assert.False(s.GetProperty("current").GetBoolean());
            },
            s =>
            {
                Assert.Equal("Safari, iOS", s.GetProperty("device").GetString());
                Assert.True(s.GetProperty("current").GetBoolean());
            });
        Assert.Single(await ListAsync(laptop.AccessToken), s => s.GetProperty("current").GetBoolean()
            && s.GetProperty("device").GetString() == "Chrome, Windows");
    }

    [Fact]
    public async Task SignedOutDeviceStopsWorkingAndTheOthersKeepGoing()
    {
        var account = await api.CreateAccountAsync();
        var laptop = await SignInDeviceAsync(account.Id, Chrome);
        var phone = await SignInDeviceAsync(account.Id, Safari);
        var phoneId = (await ListAsync(phone.AccessToken)).Single(s => s.GetProperty("current").GetBoolean())
            .GetProperty("id").GetGuid();
        using (var check = await WhoAmIAsync(phone.Browser, phone.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        }

        using (var ended = await SendAsync(HttpMethod.Delete, $"{SessionsPath}/{phoneId}", laptop.AccessToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, ended.StatusCode);
        }

        await AssertSignedOutAsync(phone);
        await AssertSignedInAsync(laptop);
        Assert.Single(await ListAsync(laptop.AccessToken));
    }

    [Fact]
    public async Task SignOutOthersEndsEveryDeviceButTheCurrentOne()
    {
        var account = await api.CreateAccountAsync();
        var laptop = await SignInDeviceAsync(account.Id, Chrome);
        var phone = await SignInDeviceAsync(account.Id, Safari);
        var desktop = await SignInDeviceAsync(account.Id, Firefox);
        var stranger = await SignInDeviceAsync((await api.CreateAccountAsync()).Id, Chrome);

        using var response = await SendAsync(HttpMethod.Post, $"{SessionsPath}/sign-out-others", laptop.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("ended").GetInt32());
        await AssertSignedOutAsync(phone);
        await AssertSignedOutAsync(desktop);
        await AssertSignedInAsync(laptop);
        await AssertSignedInAsync(stranger);
        var left = Assert.Single(await ListAsync(laptop.AccessToken));
        Assert.True(left.GetProperty("current").GetBoolean());
    }

    [Fact]
    public async Task OnlyTheOwnerSeesAndEndsTheirDevices()
    {
        var owner = await SignInDeviceAsync((await api.CreateAccountAsync()).Id, Chrome);
        var other = await SignInDeviceAsync((await api.CreateAccountAsync()).Id, Safari);
        var ownerSession = Assert.Single(await ListAsync(owner.AccessToken)).GetProperty("id").GetGuid();

        using (var guest = await SendAsync(HttpMethod.Get, SessionsPath, accessToken: null))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
        }

        using (var guestEnd = await SendAsync(HttpMethod.Post, $"{SessionsPath}/sign-out-others", accessToken: null))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, guestEnd.StatusCode);
        }

        // Another account's device is answered as a missing one, and stays signed in.
        using (var foreign = await SendAsync(HttpMethod.Delete, $"{SessionsPath}/{ownerSession}", other.AccessToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
            Assert.Equal("session_not_found",
                (await foreign.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
        }

        using (var malformed = await SendAsync(HttpMethod.Delete, $"{SessionsPath}/not-a-session", owner.AccessToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
        }

        await AssertSignedInAsync(owner);
        Assert.Single(await ListAsync(other.AccessToken));
    }

    private sealed record Device(HttpClient Browser, string AccessToken, string RefreshToken);

    private async Task<Device> SignInDeviceAsync(Guid accountId, string userAgent)
    {
        var browser = api.CreateBrowser();
        _browsers.Add(browser);
        browser.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        await SignInAtProviderAsync(browser, accountId);
        var pkce = Pkce.Create();
        var code = await CodeAsync(browser, pkce);
        var (status, tokens) = await ExchangeAsync(browser, code, pkce.Verifier);
        Assert.Equal(HttpStatusCode.OK, status);
        return new Device(browser, tokens.GetProperty("access_token").GetString()!, tokens.GetProperty("refresh_token").GetString()!);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? accessToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new("Bearer", accessToken);
        }

        var client = api.CreateClient();
        return await client.SendAsync(request, Ct);
    }

    private async Task<JsonElement[]> ListAsync(string accessToken)
    {
        using var response = await SendAsync(HttpMethod.Get, SessionsPath, accessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).EnumerateArray()];
    }

    private static async Task AssertSignedOutAsync(Device device)
    {
        using (var whoAmI = await WhoAmIAsync(device.Browser, device.AccessToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, whoAmI.StatusCode);
        }

        var (status, body) = await RefreshAsync(device.Browser, device.RefreshToken);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
        // The provider's cookie is no longer a session: a silent sign-in asks who the person is.
        var silent = await AuthorizeAsync(device.Browser, AuthorizeUrl(Pkce.Create(), prompt: "none"));
        Assert.Equal("login_required", silent["error"]);
    }

    private static async Task AssertSignedInAsync(Device device)
    {
        using var whoAmI = await WhoAmIAsync(device.Browser, device.AccessToken);
        Assert.Equal(HttpStatusCode.OK, whoAmI.StatusCode);
        var (status, _) = await RefreshAsync(device.Browser, device.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, status);
    }
}
