using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Posts.Contracts;
using Platform.Modules.Reactions;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// E08-07: the rating filter of the fresh feed (ADR-0106) — <c>/api/v1/feeds/fresh?minRating=…</c> keeps the posts
/// rated at least the threshold by the rating the hotness pass stored, newest first, each threshold cached as a feed of
/// its own — on PostgreSQL and Redis in Testcontainers.
/// </summary>
public sealed class FreshFeedRatingApiTests(PostsApi api) : IClassFixture<PostsApi>, IAsyncLifetime
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
    public async Task EachThresholdKeepsThePostsRatedAtLeastThatMuchNewestFirst()
    {
        var disliked = await PublishAsync(rating: -20);
        var slightlyDisliked = await PublishAsync(rating: -10);
        var plain = await PublishAsync(rating: 0);
        var five = await PublishAsync(rating: 5);
        var nine = await PublishAsync(rating: 9);
        var liked = await PublishAsync(rating: 30);
        await RunPassAsync();

        Assert.Equal([liked, nine, five, plain, slightlyDisliked, disliked], await FreshAsync(null));
        Assert.Equal([liked, nine, five, plain, slightlyDisliked], await FreshAsync(-10));
        Assert.Equal([liked, nine, five], await FreshAsync(5));
        Assert.Equal([liked], await FreshAsync(10));
    }

    [Fact]
    public async Task PostThePassHasNotReadYetCountsAsUnrated()
    {
        var fresh = await PublishAsync(rating: 50);

        Assert.Equal([fresh], await FreshAsync(-10));
        Assert.Empty(await FreshAsync(5));

        await RunPassAsync();
        Assert.Equal([fresh], await FreshAsync(5));
    }

    [Fact]
    public async Task DraftIsNotInAFilteredFeed()
    {
        var published = await PublishAsync(rating: 10);
        var draft = Post.CreateDraft(_author, _blog, "Черновик", PostFactory.Body("Текст."), api.Time.GetUtcNow());
        await api.SaveAsync<PostsDbContext>(draft);
        await RateAsync(draft.Id, 10);
        await RunPassAsync();

        Assert.Equal([published], await FreshAsync(10));
    }

    [Fact]
    public async Task PagesOfAThresholdFollowEachOtherWithoutRepeats()
    {
        var kept = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            var post = await PublishAsync(rating: i % 2 == 0 ? 10 : 0);
            if (i % 2 == 0)
            {
                kept.Insert(0, post);
            }
        }

        await RunPassAsync();
        api.Time.Advance(FeedPageCache.Lifetime + TimeSpan.FromSeconds(1));
        var first = await _client.GetFromJsonAsync<JsonElement>("/api/v1/feeds/fresh?minRating=5&limit=2", Ct);
        var cursor = Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!);
        var second = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/feeds/fresh?minRating=5&limit=2&cursor={cursor}", Ct);

        Assert.Equal(kept, [.. IdsOf(first), .. IdsOf(second)]);
        Assert.Equal(JsonValueKind.Null, second.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task EachThresholdIsCachedAsAFeedOfItsOwn()
    {
        var post = await PublishAsync(rating: 0);
        await RunPassAsync();
        Assert.Equal([post], await FreshAsync(null));
        Assert.Empty(await FreshAsync(5));

        // A rating changed by the next pass shows in a threshold's page only after that page has lived out (ADR-0080);
        // another threshold read for the first time sees it at once.
        await RateAsync(post, 20);
        await RunPassAsync();
        var cached = IdsOf(await _client.GetFromJsonAsync<JsonElement>("/api/v1/feeds/fresh?minRating=5&limit=100", Ct));
        var other = IdsOf(await _client.GetFromJsonAsync<JsonElement>("/api/v1/feeds/fresh?minRating=10&limit=100", Ct));

        Assert.Empty(cached);
        Assert.Equal([post], other);
        Assert.Equal([post], await FreshAsync(5));
    }

    [Fact]
    public async Task AnyoneReadsAFilteredFeed()
    {
        await PublishAsync(rating: 10);
        await RunPassAsync();

        using var anonymous = api.CreateClient();
        using var response = await anonymous.GetAsync("/api/v1/feeds/fresh?minRating=10", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("7")]
    [InlineData("-5")]
    public async Task UnknownThresholdIsRefused(string threshold)
    {
        using var response = await _client.GetAsync($"/api/v1/feeds/fresh?minRating={threshold}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation_failed", body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty("minRating", out _));
    }

    [Fact]
    public void ThresholdsAreTheOnesOfTheSpec()
    {
        Assert.Equal([-10, 5, 10], FreshFeedRating.Thresholds);
        Assert.True(FreshFeedRating.IsAllowed(null));
        Assert.All(FreshFeedRating.Thresholds, t => Assert.True(FreshFeedRating.IsAllowed(t)));
        Assert.False(FreshFeedRating.IsAllowed(0));
        Assert.Equal("fresh", FreshFeedEndpoints.CacheName(null));
        Assert.Equal("fresh:-10", FreshFeedEndpoints.CacheName(-10));
    }

    private async Task RunPassAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HotnessCalculator>().RunAsync(Ct);
    }

    private async Task<List<Guid>> FreshAsync(int? minRating)
    {
        // Past the lifetime of a cached page (ADR-0080), so the page shows the ratings of the last pass.
        api.Time.Advance(FeedPageCache.Lifetime + TimeSpan.FromSeconds(1));
        var query = minRating is { } threshold ? $"minRating={threshold}&" : string.Empty;
        return IdsOf(await _client.GetFromJsonAsync<JsonElement>($"/api/v1/feeds/fresh?{query}limit=100", Ct));
    }

    private static List<Guid> IdsOf(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("post").GetProperty("id").GetGuid())];

    private async Task<Guid> PublishAsync(int rating)
    {
        api.Time.Advance(TimeSpan.FromMinutes(1));
        var post = Post.CreateDraft(_author, _blog, "Заголовок", PostFactory.Body("Текст."), api.Time.GetUtcNow());
        post.Publish(api.Time.GetUtcNow());
        await api.SaveAsync<PostsDbContext>(post);
        await RateAsync(post.Id, rating);
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
