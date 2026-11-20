using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Caching;
using Platform.Http;
using Platform.Http.RateLimiting;
using Platform.Testing.Api;

namespace Platform.Api.Tests;

/// <summary>
/// Acceptance checks of E01-10 (#21) in process, with the in-memory counters on the fake clock: over a limit the API
/// answers 429 with Retry-After and problem details, policies come from settings, counters are per account and per
/// client IP, and only a trusted proxy may name the client. Shared counters of several instances on Redis are checked
/// in Platform.Api.IntegrationTests (RateLimitingIntegrationTests).
/// </summary>
public sealed class RateLimitingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OverTheLimitIsTooManyRequestsWithRetryAfter()
    {
        using var factory = Api().WithSetting("RateLimiting:Policies:Default:PermitLimit", "3");
        AtSecond(factory, 15);
        using var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/rate/ping", Ct)).StatusCode);
        }

        using var rejected = await client.GetAsync("/api/v1/rate/ping", Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(45), rejected.Headers.RetryAfter?.Delta);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync(Ct)).RootElement;
        Assert.Equal(429, problem.GetProperty("status").GetInt32());
        Assert.Equal("too_many_requests", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task TheNextWindowAllowsRequestsAgain()
    {
        using var factory = Api().WithSetting("RateLimiting:Policies:Default:PermitLimit", "1");
        AtSecond(factory, 50);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/rate/ping", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/v1/rate/ping", Ct)).StatusCode);

        factory.Time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/rate/ping", Ct)).StatusCode);
    }

    [Fact]
    public async Task WindowAndLimitComeFromSettings()
    {
        using var factory = Api()
            .WithSetting("RateLimiting:Policies:Default:PermitLimit", "2")
            .WithSetting("RateLimiting:Policies:Default:WindowSeconds", "10");
        AtSecond(factory, 3);
        using var client = factory.CreateClient();
        await client.GetAsync("/api/v1/rate/ping", Ct);
        await client.GetAsync("/api/v1/rate/ping", Ct);

        using var rejected = await client.GetAsync("/api/v1/rate/ping", Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(7), rejected.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task WritesHaveTheirOwnStricterLimit()
    {
        using var factory = Api().WithSetting("RateLimiting:Policies:Writes:PermitLimit", "1");
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/rate/ping", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync("/api/v1/rate/ping", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/rate/ping", Ct)).StatusCode);
    }

    [Fact]
    public async Task EachAccountHasItsOwnCounter()
    {
        using var factory = Api().WithSetting("RateLimiting:Policies:Default:PermitLimit", "1");
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await Send(client, account: "alice"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Send(client, account: "alice"));
        Assert.Equal(HttpStatusCode.OK, await Send(client, account: "bob"));
        // An anonymous request counts by IP, not into an account.
        Assert.Equal(HttpStatusCode.OK, await Send(client));
    }

    [Fact]
    public async Task AnonymousRequestsCountByClientIpBehindATrustedProxy()
    {
        using var factory = Api()
            .WithSetting("RateLimiting:Policies:Default:PermitLimit", "1")
            .WithSetting("Http:TrustedProxies", "10.0.0.0/8");
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await Send(client, remote: "10.0.0.2", forwardedFor: "203.0.113.7"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Send(client, remote: "10.0.0.3", forwardedFor: "203.0.113.7"));
        Assert.Equal(HttpStatusCode.OK, await Send(client, remote: "10.0.0.2", forwardedFor: "203.0.113.8"));
    }

    [Fact]
    public async Task AnUntrustedClientCannotDodgeTheLimitWithXForwardedFor()
    {
        using var factory = Api()
            .WithSetting("RateLimiting:Policies:Default:PermitLimit", "1")
            .WithSetting("Http:TrustedProxies", "10.0.0.0/8");
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await Send(client, remote: "198.51.100.1", forwardedFor: "203.0.113.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Send(client, remote: "198.51.100.1", forwardedFor: "203.0.113.2"));
    }

    [Fact]
    public async Task AnIpPolicyCountsSignedInRequestsByAddressToo()
    {
        using var factory = Api()
            .WithSetting("RateLimiting:Policies:Default:PermitLimit", "1")
            .WithSetting("RateLimiting:Policies:Default:Partition", "Ip");
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await Send(client, account: "alice", remote: "198.51.100.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Send(client, account: "bob", remote: "198.51.100.1"));
    }

    [Fact]
    public async Task AnEndpointPolicyAddsToTheBuiltInOnes()
    {
        using var factory = Api();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/rate/strict", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/rate/strict", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/v1/rate/strict", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/rate/ping", Ct)).StatusCode);
    }

    [Fact]
    public async Task AModulePolicyIsConfigurableToo()
    {
        using var factory = Api().WithSetting($"RateLimiting:Policies:{RateModule.StrictPolicy}:PermitLimit", "5");
        using var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/rate/strict", Ct)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/v1/rate/strict", Ct)).StatusCode);
    }

    [Fact]
    public async Task ExemptEndpointsHealthProbesAndDisabledLimitsAreNotCounted()
    {
        using var factory = Api().WithSetting("RateLimiting:Policies:Default:PermitLimit", "1");
        using var client = factory.CreateClient();
        using var off = Api()
            .WithSetting("RateLimiting:Policies:Default:PermitLimit", "1")
            .WithSetting("RateLimiting:Enabled", "false");
        using var offClient = off.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/rate/free", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await offClient.GetAsync("/api/v1/rate/ping", Ct)).StatusCode);
        }
    }

    [Fact]
    public async Task UnknownPathsUnderApiAreCountedSoScannersHitTheLimit()
    {
        using var factory = Api().WithSetting("RateLimiting:Policies:Default:PermitLimit", "1");
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/no-such-path", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/v1/other-path", Ct)).StatusCode);
    }

    [Fact]
    public async Task ABrokenCounterStoreLetsRequestsThrough()
    {
        using var factory = Api()
            .WithSetting("RateLimiting:Policies:Default:PermitLimit", "1")
            .WithModule(services =>
            {
                services.RemoveAll<IRateCounter>();
                services.AddSingleton<IRateCounter, BrokenCounter>();
            });
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/rate/ping", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/rate/ping", Ct)).StatusCode);
    }

    [Theory]
    [InlineData("RateLimiting:Policies:Default:PermitLimit", "0")]
    [InlineData("RateLimiting:Policies:Default:WindowSeconds", "a minute")]
    [InlineData("RateLimiting:Policies:Default:Partition", "Cookie")]
    [InlineData("RateLimiting:Policies:Defualt:PermitLimit", "10")]
    [InlineData("RateLimiting:Enabled", "yes")]
    public void AWrongSettingStopsTheHost(string key, string value)
    {
        using var factory = Api().WithSetting(key, value);

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(key[..key.LastIndexOf(':')], error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:2::/64")]
    public void AnIpv6ClientIsCountedByItsSlash64(string address, string counted) =>
        Assert.Equal(counted, ClientAddress.Network(IPAddress.Parse(address)));

    private static PlatformApiFactory Api() =>
        new PlatformApiFactory().WithModule(TestModule.Register).WithModule(RateModule.Register);

    // Moves the fake clock to the given second of the next minute, so Retry-After is known exactly.
    private static void AtSecond(PlatformApiFactory factory, int second)
    {
        var now = factory.Time.GetUtcNow();
        var next = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero).AddMinutes(1);
        factory.Time.SetUtcNow(next.AddSeconds(second));
    }

    private static async Task<HttpStatusCode> Send(
        HttpClient client, string? account = null, string? remote = null, string? forwardedFor = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/rate/ping");
        if (account is not null)
        {
            request.As(account);
        }

        if (remote is not null)
        {
            request.Headers.Add(RateModule.RemoteHeader, remote);
        }

        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        using var response = await client.SendAsync(request, Ct);
        return response.StatusCode;
    }

    private sealed class BrokenCounter : IRateCounter
    {
        public Task<long> IncrementAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken = default) =>
            throw new TimeoutException("Redis does not answer");
    }
}

/// <summary>Endpoints and a module policy for the rate limit tests, and a way to set the connection's address.</summary>
internal sealed class RateModule : IApiEndpoints
{
    public const string StrictPolicy = "test.strict";

    /// <summary>The test server has no client address; this header sets it, as a real connection would.</summary>
    public const string RemoteHeader = "X-Test-Remote-Address";

    public static void Register(IServiceCollection services)
    {
        services.AddApiEndpoints<RateModule>();
        services.AddRateLimitPolicy(StrictPolicy, new RateLimitPolicy(2, TimeSpan.FromMinutes(1)));
        services.AddSingleton<IStartupFilter, RemoteAddressFilter>();
    }

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var rate = v1.MapGroup("/rate");
        rate.MapGet("/ping", () => TypedResults.Ok());
        rate.MapPost("/ping", () => TypedResults.Ok());
        rate.MapGet("/strict", () => TypedResults.Ok()).WithRateLimit(StrictPolicy);

        // Exempt for the test only: a real exemption needs a reason.
        rate.MapGet("/free", () => TypedResults.Ok()).WithoutRateLimit();
    }

    private sealed class RemoteAddressFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(RemoteHeader, out var remote))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(remote.ToString());
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
