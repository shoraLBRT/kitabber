using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Blogs.Topics;
using Platform.Testing.Api;

namespace Platform.Modules.Blogs.IntegrationTests;

/// <summary>
/// E11-12 (FR-TOPIC, ADR-0121): the owner of a topic edits its description, rules and cover, appoints and removes
/// moderators and offers the topic to another person, who becomes its owner only by accepting.
/// </summary>
public sealed class TopicManagementApiTests(ProfilesApi api) : IClassFixture<ProfilesApi>, IAsyncLifetime
{
    private HttpClient _client = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task TheOwnerEditsTheDescriptionAndTheRulesAndEveryoneReadsThem()
    {
        var (topic, owner) = await TopicAsync();

        var saved = await SendAsync(HttpMethod.Put, topic.Id, "/settings", owner, new { description = " Про науку ", rules = "1. Без спама" });

        Assert.Equal(HttpStatusCode.OK, saved.Status);
        Assert.Equal("owner", saved.Body.GetProperty("role").GetString());
        var seen = await SendAsync(HttpMethod.Get, topic.Id, "/management", account: null);
        Assert.Equal(HttpStatusCode.OK, seen.Status);
        Assert.Equal("Про науку", seen.Body.GetProperty("description").GetString());
        Assert.Equal("1. Без спама", seen.Body.GetProperty("rules").GetString());
        Assert.Equal("none", seen.Body.GetProperty("role").GetString());
        Assert.Equal(owner, seen.Body.GetProperty("owner").GetProperty("accountId").GetGuid());
        var profile = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/profiles/{topic.Id}", Ct);
        Assert.Equal("Про науку", profile.GetProperty("description").GetString());
    }

    [Fact]
    public async Task TooLongRulesOrDescriptionAreRefused()
    {
        var (topic, owner) = await TopicAsync();

        var response = await SendAsync(
            HttpMethod.Put, topic.Id, "/settings", owner,
            new { description = new string('а', 161), rules = new string('б', SubsiteRules.RulesMaxLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal(["description", "rules"], ErrorFieldsOf(response.Body));
    }

    [Fact]
    public async Task OnlyTheOwnerManagesTheTopic()
    {
        var (topic, _) = await TopicAsync();
        var (_, stranger) = await PersonAsync("stranger");

        var settings = await SendAsync(HttpMethod.Put, topic.Id, "/settings", stranger, new { rules = "Мои правила" });
        var moderator = await SendAsync(HttpMethod.Post, topic.Id, "/moderators", stranger, new { nickname = "stranger" });
        var transfer = await SendAsync(HttpMethod.Put, topic.Id, "/transfer", stranger, new { nickname = "stranger" });
        var cover = await SendAsync(HttpMethod.Delete, topic.Id, "/cover", stranger);
        var guest = await SendAsync(HttpMethod.Put, topic.Id, "/settings", account: null, new { rules = "Мои правила" });
        var missing = await SendAsync(HttpMethod.Put, Guid.CreateVersion7(), "/settings", stranger, new { rules = "" });

        Assert.All([settings, moderator, transfer, cover], r =>
        {
            Assert.Equal(HttpStatusCode.Forbidden, r.Status);
            Assert.Equal(TopicManagementEndpoints.NotTopicOwnerCode, r.Body.GetProperty("code").GetString());
        });
        Assert.Equal(HttpStatusCode.Unauthorized, guest.Status);
        Assert.Equal(HttpStatusCode.NotFound, missing.Status);
        Assert.Null((await SendAsync(HttpMethod.Get, topic.Id, "/management", null)).Body.GetProperty("rules").GetString());
    }

    [Fact]
    public async Task TheOwnerSetsAndRemovesTheCoverOfTheTopic()
    {
        var (topic, owner) = await TopicAsync();
        var image = await UploadAsync(owner);

        var set = await SendAsync(HttpMethod.Put, topic.Id, "/cover", owner, new { mediaId = image, position = 20 });

        Assert.Equal(HttpStatusCode.OK, set.Status);
        var seen = (await _client.GetFromJsonAsync<JsonElement>($"/api/v1/profiles/{topic.Id}", Ct)).GetProperty("cover");
        Assert.Equal(image, seen.GetProperty("id").GetGuid());
        Assert.Equal(20, seen.GetProperty("position").GetInt32());

        var removed = await SendAsync(HttpMethod.Delete, topic.Id, "/cover", owner);

        Assert.Equal(HttpStatusCode.OK, removed.Status);
        Assert.Equal(JsonValueKind.Null, removed.Body.GetProperty("cover").ValueKind);
    }

    [Fact]
    public async Task ACoverMustBeTheOwnersImage()
    {
        var (topic, owner) = await TopicAsync();
        var (_, other) = await PersonAsync("other_cover");
        var othersImage = await UploadAsync(other);

        var response = await SendAsync(HttpMethod.Put, topic.Id, "/cover", owner, new { mediaId = othersImage, position = 120 });

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal(["mediaId", "position"], ErrorFieldsOf(response.Body));
    }

    [Fact]
    public async Task TheOwnerAppointsAndRemovesModerators()
    {
        var (topic, owner) = await TopicAsync();
        var (blog, anna) = await PersonAsync("anna_mod");
        var (_, boris) = await PersonAsync("boris_mod");

        var first = await SendAsync(HttpMethod.Post, topic.Id, "/moderators", owner, new { nickname = "@Anna_Mod" });
        api.Time.Advance(TimeSpan.FromMinutes(1));
        var second = await SendAsync(HttpMethod.Post, topic.Id, "/moderators", owner, new { nickname = "boris_mod" });

        Assert.Equal(HttpStatusCode.OK, first.Status);
        var moderators = second.Body.GetProperty("moderators").EnumerateArray().ToList();
        Assert.Equal([anna, boris], moderators.Select(m => m.GetProperty("accountId").GetGuid()));
        Assert.Equal(blog.Id, moderators[0].GetProperty("blogId").GetGuid());
        Assert.Equal("anna_mod", moderators[0].GetProperty("nickname").GetString());
        // The role is exposed to the moderator.
        Assert.Equal("moderator", (await SendAsync(HttpMethod.Get, topic.Id, "/management", anna)).Body.GetProperty("role").GetString());

        var removed = await SendAsync(HttpMethod.Delete, topic.Id, $"/moderators/{anna}", owner);

        Assert.Equal(HttpStatusCode.OK, removed.Status);
        Assert.Equal([boris], removed.Body.GetProperty("moderators").EnumerateArray().Select(m => m.GetProperty("accountId").GetGuid()));
        Assert.Equal("none", (await SendAsync(HttpMethod.Get, topic.Id, "/management", anna)).Body.GetProperty("role").GetString());
    }

    [Theory]
    [InlineData("nobody_here")]
    [InlineData("not a nickname")]
    [InlineData("owner_blog")]
    [InlineData("anna_twice")]
    public async Task AWrongModeratorIsRefused(string nickname)
    {
        var (topic, owner) = await TopicAsync("owner_blog");
        await PersonAsync("anna_twice");
        Assert.Equal(
            HttpStatusCode.OK,
            (await SendAsync(HttpMethod.Post, topic.Id, "/moderators", owner, new { nickname = "anna_twice" })).Status);

        var response = await SendAsync(HttpMethod.Post, topic.Id, "/moderators", owner, new { nickname });

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal(["nickname"], ErrorFieldsOf(response.Body));
    }

    [Fact]
    public async Task ATopicChangesHandsOnlyWhenTheRecipientAccepts()
    {
        var (topic, owner) = await TopicAsync();
        var (_, recipient) = await PersonAsync("new_owner");
        var (_, stranger) = await PersonAsync("bystander");
        // The recipient was a moderator: an owner is not a moderator of the own topic.
        await SendAsync(HttpMethod.Post, topic.Id, "/moderators", owner, new { nickname = "new_owner" });

        var offered = await SendAsync(HttpMethod.Put, topic.Id, "/transfer", owner, new { nickname = "new_owner" });

        Assert.Equal(HttpStatusCode.OK, offered.Status);
        Assert.Equal(recipient, offered.Body.GetProperty("transfer").GetProperty("to").GetProperty("accountId").GetGuid());
        Assert.Equal(owner, await OwnerAsync(topic.Id));
        var asRecipient = await SendAsync(HttpMethod.Get, topic.Id, "/management", recipient);
        Assert.True(asRecipient.Body.GetProperty("transferToYou").GetBoolean());
        var asStranger = await SendAsync(HttpMethod.Get, topic.Id, "/management", stranger);
        Assert.Equal(JsonValueKind.Null, asStranger.Body.GetProperty("transfer").ValueKind);
        Assert.False(asStranger.Body.GetProperty("transferToYou").GetBoolean());

        var byStranger = await SendAsync(HttpMethod.Post, topic.Id, "/transfer/accept", stranger);
        Assert.Equal(HttpStatusCode.Forbidden, byStranger.Status);
        Assert.Equal(TopicManagementEndpoints.NoTransferOfferCode, byStranger.Body.GetProperty("code").GetString());

        var accepted = await SendAsync(HttpMethod.Post, topic.Id, "/transfer/accept", recipient);

        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        Assert.Equal("owner", accepted.Body.GetProperty("role").GetString());
        Assert.Empty(accepted.Body.GetProperty("moderators").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, accepted.Body.GetProperty("transfer").ValueKind);
        Assert.Equal(recipient, await OwnerAsync(topic.Id));
        // The former owner runs the topic no more.
        var former = await SendAsync(HttpMethod.Put, topic.Id, "/settings", owner, new { rules = "Мои правила" });
        Assert.Equal(HttpStatusCode.Forbidden, former.Status);
    }

    [Fact]
    public async Task TheRecipientDeclinesAndTheOwnerCancelsAnOffer()
    {
        var (topic, owner) = await TopicAsync();
        var (_, recipient) = await PersonAsync("declines");
        var (_, stranger) = await PersonAsync("not_offered");
        await SendAsync(HttpMethod.Put, topic.Id, "/transfer", owner, new { nickname = "declines" });

        var byStranger = await SendAsync(HttpMethod.Delete, topic.Id, "/transfer", stranger);
        var declined = await SendAsync(HttpMethod.Delete, topic.Id, "/transfer", recipient);
        var acceptAfter = await SendAsync(HttpMethod.Post, topic.Id, "/transfer/accept", recipient);

        Assert.Equal(HttpStatusCode.Forbidden, byStranger.Status);
        Assert.Equal(HttpStatusCode.OK, declined.Status);
        Assert.Equal(HttpStatusCode.Forbidden, acceptAfter.Status);
        Assert.Equal(owner, await OwnerAsync(topic.Id));

        await SendAsync(HttpMethod.Put, topic.Id, "/transfer", owner, new { nickname = "declines" });
        var cancelled = await SendAsync(HttpMethod.Delete, topic.Id, "/transfer", owner);

        Assert.Equal(HttpStatusCode.OK, cancelled.Status);
        Assert.Equal(JsonValueKind.Null, cancelled.Body.GetProperty("transfer").ValueKind);
        Assert.False((await SendAsync(HttpMethod.Get, topic.Id, "/management", recipient)).Body.GetProperty("transferToYou").GetBoolean());
    }

    [Fact]
    public async Task TheTopicIsNotOfferedToItsOwnerOrToNobody()
    {
        var (topic, owner) = await TopicAsync("self_offer");

        var toSelf = await SendAsync(HttpMethod.Put, topic.Id, "/transfer", owner, new { nickname = "self_offer" });
        var toNobody = await SendAsync(HttpMethod.Put, topic.Id, "/transfer", owner, new { nickname = "no_such_blog" });

        Assert.Equal(["nickname"], ErrorFieldsOf(toSelf.Body));
        Assert.Equal(["nickname"], ErrorFieldsOf(toNobody.Body));
    }

    // The same offer again changes nothing: it keeps its time, so the recipient is not notified twice.
    [Fact]
    public async Task TheSameOfferAgainChangesNothing()
    {
        var (topic, owner) = await TopicAsync();
        var (_, recipient) = await PersonAsync("announced");

        await SendAsync(HttpMethod.Put, topic.Id, "/transfer", owner, new { nickname = "announced" });
        var offeredAt = api.Time.GetUtcNow();
        api.Time.Advance(TimeSpan.FromHours(1));
        await SendAsync(HttpMethod.Put, topic.Id, "/transfer", owner, new { nickname = "announced" });

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogsDbContext>();
        var offer = await db.TopicTransferOffers.SingleAsync(o => o.SubsiteId == topic.Id, Ct);
        Assert.Equal((owner, recipient, offeredAt), (offer.FromAccountId, offer.ToAccountId, offer.OfferedAt));
    }

    private async Task<(Subsite Topic, Guid Owner)> TopicAsync(string? ownerNickname = null)
    {
        var (_, owner) = await PersonAsync(ownerNickname ?? $"own{Guid.CreateVersion7().ToString("N")[^12..]}");
        var topic = Subsite.CreateTopic("Наука", $"t{Guid.CreateVersion7().ToString("N")[^12..]}", owner, api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(topic);
        return (topic, owner);
    }

    private async Task<(Subsite Blog, Guid Account)> PersonAsync(string nickname)
    {
        var account = Guid.CreateVersion7();
        var blog = Subsite.CreatePersonal(account, api.Time.GetUtcNow());
        blog.SetNickname(nickname);
        await api.SaveAsync<BlogsDbContext>(blog);
        return (blog, account);
    }

    private async Task<Guid?> OwnerAsync(Guid topicId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<BlogsDbContext>().Subsites
            .Where(s => s.Id == topicId).Select(s => s.OwnerAccountId).SingleAsync(Ct);
    }

    private async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(
        HttpMethod method, Guid topicId, string path, Guid? account, object? body = null)
    {
        using var request = new HttpRequestMessage(method, $"/api/v1/topics/{topicId}{path}");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (account is { } id)
        {
            request.As(id.ToString("D"));
        }

        using var response = await _client.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        return (response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    private async Task<Guid> UploadAsync(Guid account)
    {
        var file = new ByteArrayContent(
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, .. "IHDR"u8, .. new byte[240]]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/media")
        {
            Content = new MultipartFormDataContent { { file, "file", "cover.png" } },
        };
        request.As(account.ToString("D"));
        using var response = await _client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static string[] ErrorFieldsOf(JsonElement body) =>
        [.. body.GetProperty("errors").EnumerateObject().Select(e => e.Name).Order(StringComparer.Ordinal)];
}
