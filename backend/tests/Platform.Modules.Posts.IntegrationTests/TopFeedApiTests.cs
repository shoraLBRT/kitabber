using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Reactions;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// E08-06: the tops for a day, a week, a month, a year and all time (ADR-0104) — <c>/api/v1/feeds/top/{period}</c>,
/// the posts published within the period by the rating the hotness pass stored, cached per period — on PostgreSQL and
/// Redis in Testcontainers.
/// </summary>
public sealed class TopFeedApiTests(PostsApi api) : IClassFixture<PostsApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _author;
    private Guid _blog;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        _author = Guid.CreateVersion7(api.Time.GetUtcNow());
        _blog = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(1));
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task EachPeriodHoldsThePostsPublishedWithinItByRating()
    {
        // Published about 400 days, 40 days, 5 days and 10 hours before "now"; the older, the more liked.
        var ancient = await PublishAsync(rating: 50);
        api.Time.Advance(TimeSpan.FromDays(360));
        var fortyDaysOld = await PublishAsync(rating: 30);
        api.Time.Advance(TimeSpan.FromDays(35));
        var weekOld = await PublishAsync(rating: 20);
        api.Time.Advance(TimeSpan.FromDays(5) - TimeSpan.FromHours(10));
        var today = await PublishAsync(rating: 1);
        var todayLiked = await PublishAsync(rating: 10);
        var draft = Post.CreateDraft(_author, _blog, "Черновик", PostFactory.Body("Текст."), api.Time.GetUtcNow());
        await api.SaveAsync<PostsDbContext>(draft);
        api.Time.Advance(TimeSpan.FromHours(10));
        await RunPassAsync();

        Assert.Equal([todayLiked, today], await TopAsync("day"));
        Assert.Equal([weekOld, todayLiked, today], await TopAsync("week"));
        Assert.Equal([weekOld, todayLiked, today], await TopAsync("month"));
        Assert.Equal([fortyDaysOld, weekOld, todayLiked, today], await TopAsync("year"));
        Assert.Equal([ancient, fortyDaysOld, weekOld, todayLiked, today], await TopAsync("all"));
        Assert.DoesNotContain(draft.Id, await TopAsync("all"));
    }

    [Fact]
    public async Task EmptyPeriodIsAnEmptyPage()
    {
        await PublishAsync(rating: 5);
        api.Time.Advance(TimeSpan.FromDays(2));
        await RunPassAsync();

        var page = await _client.GetFromJsonAsync<JsonElement>("/api/v1/feeds/top/day", Ct);

        Assert.Empty(IdsOf(page));
        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task PagesAreCachedPerPeriod()
    {
        var first = await PublishAsync(rating: 5);
        var second = await PublishAsync(rating: 0);
        await RunPassAsync();
        Assert.Equal([first, second], await TopAsync("week"));

        // A rating changed by the next pass shows only after the cached page has lived out (ADR-0080); the page of
        // another period is a page of its own.
        await RateAsync(second, 50);
        await RunPassAsync();
        var cached = IdsOf(await _client.GetFromJsonAsync<JsonElement>("/api/v1/feeds/top/week?limit=100", Ct));
        var otherPeriod = IdsOf(await _client.GetFromJsonAsync<JsonElement>("/api/v1/feeds/top/day?limit=100", Ct));

        Assert.Equal([first, second], cached);
        Assert.Equal([second, first], otherPeriod);
        Assert.Equal([second, first], await TopAsync("week"));
    }

    [Fact]
    public async Task PagesFollowEachOtherWithoutRepeats()
    {
        var posts = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            posts.Add(await PublishAsync(rating: (5 - i) * 10));
        }

        await RunPassAsync();
        var first = await _client.GetFromJsonAsync<JsonElement>("/api/v1/feeds/top/all?limit=2", Ct);
        var second = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/feeds/top/all?limit=2&cursor={Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!)}",
            Ct);
        var third = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/feeds/top/all?limit=2&cursor={Uri.EscapeDataString(second.GetProperty("nextCursor").GetString()!)}",
            Ct);

        Assert.Equal(posts, [.. IdsOf(first), .. IdsOf(second), .. IdsOf(third)]);
        Assert.Equal(JsonValueKind.Null, third.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task AnyoneReadsATop()
    {
        await PublishAsync(rating: 1);
        await RunPassAsync();

        using var anonymous = api.CreateClient();
        using var response = await anonymous.GetAsync("/api/v1/feeds/top/day", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("century")]
    [InlineData("Day")]
    [InlineData("1")]
    public async Task UnknownPeriodIsRefused(string period)
    {
        using var response = await _client.GetAsync($"/api/v1/feeds/top/{period}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation_failed", body.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=101")]
    [InlineData("cursor=not-a-cursor")]
    public async Task InvalidPageIsRefused(string query)
    {
        using var response = await _client.GetAsync($"/api/v1/feeds/top/week?{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task RunPassAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HotnessCalculator>().RunAsync(Ct);
    }

    private async Task<List<Guid>> TopAsync(string period)
    {
        // Past the lifetime of a cached page (ADR-0080), so the page shows the ratings of the last pass.
        api.Time.Advance(FeedPageCache.Lifetime + TimeSpan.FromSeconds(1));
        return IdsOf(await _client.GetFromJsonAsync<JsonElement>($"/api/v1/feeds/top/{period}?limit=100", Ct));
    }

    private static List<Guid> IdsOf(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("post").GetProperty("id").GetGuid())];

    private async Task<Guid> PublishAsync(int rating)
    {
        var post = await PublishWithoutPassAsync();
        await RateAsync(post, rating);
        return post;
    }

    private async Task<Guid> PublishWithoutPassAsync()
    {
        api.Time.Advance(TimeSpan.FromMinutes(1));
        var post = Post.CreateDraft(_author, _blog, "Заголовок", PostFactory.Body("Текст."), api.Time.GetUtcNow());
        post.Publish(api.Time.GetUtcNow());
        await api.SaveAsync<PostsDbContext>(post);
        return post.Id;
    }

    // The stored rating as the Reactions module keeps it (ADR-0081), set to the value.
    private async Task RateAsync(Guid post, int value)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReactionsDbContext>();
        var rating = await db.Ratings.SingleOrDefaultAsync(
            r => r.TargetKind == ReactionTarget.Post && r.TargetId == post, Ct);
        if (rating is null)
        {
            db.Ratings.Add(new Rating { TargetKind = ReactionTarget.Post, TargetId = post, PostId = post, Value = value });
        }
        else
        {
            rating.Value = value;
        }

        await db.SaveChangesAsync(Ct);
    }
}
