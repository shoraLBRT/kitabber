using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Comments;
using Platform.Modules.Reactions;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>
/// E08-05: the hotness passes (ADR-0086) — scores from reactions, comments, views and age, written only for posts that
/// changed, computed again when the coefficients change in the running host — and the popular feed
/// <c>/api/v1/feeds/popular</c> ordered by them, on PostgreSQL and Redis in Testcontainers.
/// </summary>
public sealed class HotnessApiTests(PostsApi api) : IClassFixture<PostsApi>, IAsyncLifetime
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
    public async Task PopularFeedOrdersPublishedPostsByHotness()
    {
        var liked = await PublishAsync();
        var discussed = await PublishAsync();
        var quiet = await PublishAsync();
        var draft = Post.CreateDraft(_author, _blog, "Черновик", PostFactory.Body("Текст."), api.Time.GetUtcNow());
        await api.SaveAsync<PostsDbContext>(draft);
        await RateAsync(liked, 100);
        await CommentAsync(discussed, 10);

        var pass = await RunAsync();

        Assert.Equal(new HotnessPass(0, 3, 3, 0), pass);
        var ids = await PopularAsync();
        // 100 points beat 20 points, which beat none, although the quiet post is the youngest.
        Assert.Equal([liked, discussed, quiet], ids);
        Assert.DoesNotContain(draft.Id, ids);
        var stored = await HotnessAsync(liked);
        Assert.Equal(new HotnessInputs(100, 0, 0), stored.Inputs);
        Assert.Equal(HotnessCoefficients.Default.Formula, stored.Formula);
    }

    [Fact]
    public async Task PassWritesOnlyThePostsThatChanged()
    {
        var first = await PublishAsync();
        var second = await PublishAsync();
        var third = await PublishAsync();
        Assert.Equal(3, (await RunAsync()).Written);

        Assert.Equal(new HotnessPass(0, 3, 0, 0), await RunAsync());

        var before = await HotnessAsync(first);
        api.Time.Advance(TimeSpan.FromMinutes(1));
        await RateAsync(second, 3);
        await CommentAsync(third, 1);
        await SetViewsAsync(third, 50);

        Assert.Equal(new HotnessPass(0, 3, 2, 0), await RunAsync());
        Assert.Equal(before.ComputedAt, (await HotnessAsync(first)).ComputedAt);
        Assert.Equal(new HotnessInputs(3, 0, 0), (await HotnessAsync(second)).Inputs);
        Assert.Equal(new HotnessInputs(0, 1, 50), (await HotnessAsync(third)).Inputs);
    }

    [Fact]
    public async Task PostOlderThanTheWindowIsNotReadAgain()
    {
        var old = await PublishAsync();
        await RateAsync(old, 5);
        Assert.Equal(1, (await RunAsync()).Written);
        api.Time.Advance(TimeSpan.FromDays(HotnessCoefficients.Default.WindowDays + 1));
        var fresh = await PublishAsync();
        await RateAsync(old, 500);

        var pass = await RunAsync();

        // Only the new post is read; the old one keeps the score of its last reading.
        Assert.Equal(new HotnessPass(0, 1, 1, 0), pass);
        Assert.Equal(new HotnessInputs(5, 0, 0), (await HotnessAsync(old)).Inputs);
        Assert.NotNull(await HotnessAsync(fresh));
    }

    [Fact]
    public async Task OldPostWithoutAScoreGetsOne()
    {
        var old = await PublishAsync();
        api.Time.Advance(TimeSpan.FromDays(30));

        Assert.Equal(new HotnessPass(0, 1, 1, 0), await RunAsync());
        Assert.Equal([old], await PopularAsync());
    }

    [Fact]
    public async Task CoefficientsChangeInTheRunningHostAndEveryScoreIsComputedAgain()
    {
        var liked = await PublishAsync();
        var discussed = await PublishAsync();
        await RateAsync(liked, 10);
        await CommentAsync(discussed, 2);
        await RunAsync();
        Assert.Equal([liked, discussed], await PopularAsync());

        var configuration = api.Services.GetRequiredService<IConfiguration>();
        configuration[PostsSettings.HotnessCommentWeightKey] = "100";
        try
        {
            // Even a post outside the window is computed again: from its stored inputs.
            api.Time.Advance(TimeSpan.FromDays(HotnessCoefficients.Default.WindowDays + 1));

            Assert.Equal(new HotnessPass(2, 0, 0, 0), await RunAsync());
            Assert.Equal([discussed, liked], await PopularAsync());
            Assert.Equal("r=1;c=100;v=0.02;d=12.5", (await HotnessAsync(liked)).Formula);
            Assert.Equal(new HotnessPass(0, 0, 0, 0), await RunAsync());
        }
        finally
        {
            configuration[PostsSettings.HotnessCommentWeightKey] = null;
        }
    }

    [Fact]
    public async Task InvalidCoefficientsChangeNothing()
    {
        var post = await PublishAsync();
        await RunAsync();
        var before = await HotnessAsync(post);
        await RateAsync(post, 7);
        var configuration = api.Services.GetRequiredService<IConfiguration>();
        configuration[PostsSettings.HotnessDecayHoursKey] = "0";
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(RunAsync);
        }
        finally
        {
            configuration[PostsSettings.HotnessDecayHoursKey] = null;
        }

        Assert.Equal(before.Score, (await HotnessAsync(post)).Score);
    }

    [Fact]
    public async Task PostThatIsNoLongerPublishedLeavesThePopularFeed()
    {
        var kept = await PublishAsync();
        var deleted = await PublishAsync();
        await RunAsync();
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
            var post = await db.Posts.SingleAsync(p => p.Id == deleted, Ct);
            post.Delete(api.Time.GetUtcNow());
            await db.SaveChangesAsync(Ct);
        }

        // Gone from the feed at once, before any pass: the feed reads only published posts.
        Assert.Equal([kept], await PopularAsync());
        Assert.Equal(1, (await RunAsync()).Removed);
    }

    [Fact]
    public async Task PagesFollowEachOtherWithoutRepeats()
    {
        var posts = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var post = await PublishAsync();
            await RateAsync(post, (5 - i) * 100);
            posts.Add(post);
        }

        await RunAsync();
        var first = await _client.GetFromJsonAsync<JsonElement>("/api/v1/feeds/popular?limit=2", Ct);
        var cursor = first.GetProperty("nextCursor").GetString();
        var second = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/feeds/popular?limit=2&cursor={Uri.EscapeDataString(cursor!)}", Ct);
        var third = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/feeds/popular?limit=2&cursor={Uri.EscapeDataString(second.GetProperty("nextCursor").GetString()!)}",
            Ct);

        Assert.Equal(posts, [.. IdsOf(first), .. IdsOf(second), .. IdsOf(third)]);
        Assert.Equal(JsonValueKind.Null, third.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task AnyoneReadsThePopularFeed()
    {
        await PublishAsync();
        await RunAsync();

        using var anonymous = api.CreateClient();
        using var response = await anonymous.GetAsync("/api/v1/feeds/popular", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=101")]
    [InlineData("cursor=not-a-cursor")]
    public async Task InvalidPageIsRefused(string query)
    {
        using var response = await _client.GetAsync($"/api/v1/feeds/popular?{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HotnessPass> RunAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var pass = await scope.ServiceProvider.GetRequiredService<HotnessCalculator>().RunAsync(Ct);
        return pass!;
    }

    private async Task<PostHotness> HotnessAsync(Guid post)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        return await db.Hotness.AsNoTracking().SingleAsync(h => h.PostId == post, Ct);
    }

    private async Task<List<Guid>> PopularAsync()
    {
        // Past the lifetime of a cached page (ADR-0080), so the page shows the scores of the last pass.
        api.Time.Advance(FeedPageCache.Lifetime + TimeSpan.FromSeconds(1));
        return IdsOf(await _client.GetFromJsonAsync<JsonElement>("/api/v1/feeds/popular?limit=100", Ct));
    }

    private static List<Guid> IdsOf(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("post").GetProperty("id").GetGuid())];

    private async Task<Guid> PublishAsync()
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

    private async Task CommentAsync(Guid post, int count)
    {
        var comments = new object[count];
        for (var i = 0; i < count; i++)
        {
            api.Time.Advance(TimeSpan.FromSeconds(1));
            comments[i] = Comment.CreateNative(
                Guid.CreateVersion7(api.Time.GetUtcNow()), post, null, $"Комментарий {i}", api.Time.GetUtcNow());
        }

        await api.SaveAsync<CommentsDbContext>(comments);
    }

    private async Task SetViewsAsync(Guid post, int views)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        await db.Posts.Where(p => p.Id == post).ExecuteUpdateAsync(s => s.SetProperty(p => p.ViewCount, views), Ct);
    }
}
