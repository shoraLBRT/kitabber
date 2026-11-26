using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Posts.Contracts;
using Platform.Modules.Preferences.Blocks;
using Platform.Modules.Preferences.Contracts;
using Platform.Testing.Api;

namespace Platform.Modules.Preferences.IntegrationTests;

/// <summary>
/// E11-06 (ADR-0091): the black list of the signed-in account through <c>/api/v1/preferences/me/blocks</c> — block,
/// read the list latest first, unblock, both repeated with the same result; blocking oneself refused; no access for a
/// guest; one account's list is not another's; what other modules and the feeds get from it.
/// </summary>
public sealed class BlockApiTests(PreferencesApi api) : IClassFixture<PreferencesApi>, IAsyncLifetime
{
    private const string _blocks = "/api/v1/preferences/me/blocks";
    private readonly Guid _anna = Guid.CreateVersion7();
    private readonly Guid _boris = Guid.CreateVersion7();
    private readonly Guid _vera = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await api.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task BlockedAccountsAreListedLatestFirstAndUnblockingRemovesOne()
    {
        using var client = api.CreateClient();

        using var first = await SendAsync(client, HttpMethod.Put, $"{_blocks}/{_boris}", _anna);
        api.Time.Advance(TimeSpan.FromMinutes(1));
        using var second = await SendAsync(client, HttpMethod.Put, $"{_blocks}/{_vera}", _anna);
        using var again = await SendAsync(client, HttpMethod.Put, $"{_blocks}/{_boris}", _anna);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        var (accounts, max) = await ListAsync(client, _anna);
        Assert.Equal([_vera, _boris], accounts);
        Assert.Equal(BlockRules.MaxBlocked, max);

        using var removed = await SendAsync(client, HttpMethod.Delete, $"{_blocks}/{_boris}", _anna);
        using var removedAgain = await SendAsync(client, HttpMethod.Delete, $"{_blocks}/{_boris}", _anna);

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removedAgain.StatusCode);
        Assert.Equal([_vera], (await ListAsync(client, _anna)).Accounts);
    }

    [Fact]
    public async Task BlockingOneselfIsAValidationError()
    {
        using var client = api.CreateClient();

        using var response = await SendAsync(client, HttpMethod.Put, $"{_blocks}/{_anna}", _anna);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("accountId", out _));
        Assert.Empty((await ListAsync(client, _anna)).Accounts);
    }

    [Fact]
    public async Task OneAccountsListIsNotAnothers()
    {
        using var client = api.CreateClient();

        using var blocked = await SendAsync(client, HttpMethod.Put, $"{_blocks}/{_vera}", _anna);
        // Boris removing Vera from his own list leaves Anna's as it is.
        using var removed = await SendAsync(client, HttpMethod.Delete, $"{_blocks}/{_vera}", _boris);

        Assert.Empty((await ListAsync(client, _boris)).Accounts);
        Assert.Equal([_vera], (await ListAsync(client, _anna)).Accounts);
    }

    [Fact]
    public async Task GuestHasNoBlackList()
    {
        using var client = api.CreateClient();

        using var read = await SendAsync(client, HttpMethod.Get, _blocks, null);
        using var block = await SendAsync(client, HttpMethod.Put, $"{_blocks}/{_boris}", null);
        using var unblock = await SendAsync(client, HttpMethod.Delete, $"{_blocks}/{_boris}", null);

        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, block.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unblock.StatusCode);
    }

    [Fact]
    public async Task OtherModulesAndTheFeedsSeeWhomTheAccountBlocked()
    {
        using var client = api.CreateClient();
        using var blocked = await SendAsync(client, HttpMethod.Put, $"{_blocks}/{_boris}", _anna);

        await using var scope = api.Services.CreateAsyncScope();
        var list = scope.ServiceProvider.GetRequiredService<IBlockList>();
        var feeds = scope.ServiceProvider.GetServices<IFeedExclusionSource>().OfType<BlockList>().Single();

        Assert.True(await list.IsBlockedAsync(_anna, _boris, Ct));
        Assert.False(await list.IsBlockedAsync(_boris, _anna, Ct));
        Assert.Equal([_boris], await list.BlockedByAsync(_anna, Ct));
        Assert.Empty(await list.BlockedByAsync(_boris, Ct));
        Assert.Equal([_boris], (await feeds.ForReaderAsync(_anna, Ct)).Authors);
        Assert.Same(FeedExclusions.None, await feeds.ForReaderAsync(_vera, Ct));
    }

    private static async Task<(List<Guid> Accounts, int Max)> ListAsync(HttpClient client, Guid account)
    {
        using var response = await SendAsync(client, HttpMethod.Get, _blocks, account);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return ([.. list.GetProperty("accounts").EnumerateArray().Select(a => a.GetProperty("accountId").GetGuid())],
            list.GetProperty("max").GetInt32());
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string path, Guid? account)
    {
        using var request = new HttpRequestMessage(method, path);
        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        return await client.SendAsync(request, Ct);
    }
}
