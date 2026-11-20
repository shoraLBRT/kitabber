using System.Net;
using Platform.Testing.Containers;
using static Platform.Modules.Identity.IntegrationTests.SignIn.OidcFlow;

namespace Platform.Modules.Identity.IntegrationTests.SignIn;

/// <summary>The Identity host with a Default rate limit of two requests a minute.</summary>
public sealed class LimitedIdentityApi : IdentityApi
{
    public LimitedIdentityApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis) => WithSetting("RateLimiting:Policies:Default:PermitLimit", "2");
}

/// <summary>
/// The signed-in identity feeds the rate limits (ADR-0016): requests with an access token count against its account,
/// not against the address they come from.
/// </summary>
public sealed class OidcRateLimitTests(LimitedIdentityApi api) : IClassFixture<LimitedIdentityApi>, IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await api.ResetWithClientsAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task RequestsCountAgainstTheAccountOfTheToken()
    {
        using var firstBrowser = api.CreateBrowser();
        using var secondBrowser = api.CreateBrowser();
        var first = (await SignInAsync(api, firstBrowser)).Tokens.GetProperty("access_token").GetString();
        var second = (await SignInAsync(api, secondBrowser)).Tokens.GetProperty("access_token").GetString();
        using var client = api.CreateClient();

        // All four requests come from the same address; the third is the first account's third in the window.
        List<HttpStatusCode> statuses = [];
        foreach (var token in new[] { first, first, first, second })
        {
            using var response = await WhoAmIAsync(client, token);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests, HttpStatusCode.OK], statuses);
    }
}
