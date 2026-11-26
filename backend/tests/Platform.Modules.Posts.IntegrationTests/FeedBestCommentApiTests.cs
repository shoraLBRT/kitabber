using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Platform.Modules.Blogs;
using Platform.Modules.Comments;
using Platform.Modules.Reactions;
using Platform.Testing.Api;
using Platform.Testing.Containers;

namespace Platform.Modules.Posts.IntegrationTests;

/// <summary>The Api host on PostgreSQL and Redis in Testcontainers, counting the SQL commands of the module contexts.</summary>
public sealed class FeedApi : IntegrationApiFactory
{
    public FeedApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis) => WithModule(services =>
        {
            services.AddTestAuthentication();
            services.ConfigureDbContext<PostsDbContext>(o => o.AddInterceptors(Commands));
            services.ConfigureDbContext<CommentsDbContext>(o => o.AddInterceptors(Commands));
            services.ConfigureDbContext<ReactionsDbContext>(o => o.AddInterceptors(Commands));
            services.ConfigureDbContext<BlogsDbContext>(o => o.AddInterceptors(Commands));
        });

    /// <summary>The SQL commands the host's module contexts sent since the last <see cref="CommandCounter.Reset"/>.</summary>
    public CommandCounter Commands { get; } = new();
}

/// <summary>Counts the SQL commands a context sends.</summary>
public sealed class CommandCounter : DbCommandInterceptor
{
    private int _count;

    public int Count => _count;

    public void Reset() => Interlocked.Exchange(ref _count, 0);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

/// <summary>
/// E08-15: the feed gives each post's card its best comment (ADR-0062) — the most liked shown one, the earlier one on a
/// tie, never a deleted one, none for a post without comments — for a whole page with the same queries.
/// </summary>
public sealed class FeedBestCommentApiTests(FeedApi api) : IClassFixture<FeedApi>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _author;
    private Guid _commenter;
    private Guid _blog;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
        _author = Guid.CreateVersion7(api.Time.GetUtcNow());
        _commenter = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(1));
        _blog = Guid.CreateVersion7(api.Time.GetUtcNow().AddSeconds(2));
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task PostWithCommentsCarriesTheMostLikedOneAndPostWithoutCommentsNone()
    {
        var quiet = await PublishAsync();
        var discussed = await PublishAsync();
        var first = await CommentAsync(discussed, "Первый");
        var liked = await CommentAsync(discussed, "Самый популярный");
        var reply = await CommentAsync(discussed, "Ответ", parent: first);
        await LikeAsync(liked, discussed, 3);
        await LikeAsync(reply, discussed, 2);
        await LikeAsync(first, discussed, 1);

        var items = await FeedAsync();

        var best = BestOf(items, discussed);
        Assert.Equal(liked.Id, best.GetProperty("id").GetGuid());
        Assert.Equal("Самый популярный", best.GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.Null, ItemOf(items, quiet).GetProperty("bestComment").ValueKind);
    }

    [Fact]
    public async Task AmongEquallyLikedCommentsTheEarlierIsTheBest()
    {
        var unliked = await PublishAsync();
        var tied = await PublishAsync();
        var earliest = await CommentAsync(unliked, "Раньше всех");
        await CommentAsync(unliked, "Позже");
        await CommentAsync(tied, "Без лайков, но раньше");
        var earlier = await CommentAsync(tied, "Раньше из равных");
        var later = await CommentAsync(tied, "Позже из равных");
        await LikeAsync(earlier, tied, 2);
        await LikeAsync(later, tied, 2);

        var items = await FeedAsync();

        Assert.Equal(earliest.Id, BestOf(items, unliked).GetProperty("id").GetGuid());
        Assert.Equal(earlier.Id, BestOf(items, tied).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task DeletedCommentIsNeverTheBest()
    {
        var post = await PublishAsync();
        var gone = await CommentAsync(post, "Удалю", delete: true);
        var shown = await CommentAsync(post, "Остался");
        await LikeAsync(gone, post, 5);
        var onlyDeleted = await PublishAsync();
        await CommentAsync(onlyDeleted, "Тоже удалю", delete: true);

        var items = await FeedAsync();

        Assert.Equal(shown.Id, BestOf(items, post).GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, ItemOf(items, onlyDeleted).GetProperty("bestComment").ValueKind);
    }

    [Fact]
    public async Task BestCommentCarriesItsAuthorsBlogOrTheGuestsName()
    {
        var blog = Subsite.CreatePersonal(_commenter, api.Time.GetUtcNow());
        blog.Rename("Борис");
        await api.SaveAsync<BlogsDbContext>(blog);
        var native = await PublishAsync();
        await CommentAsync(native, "Свой");
        var bridged = await PublishAsync();
        api.Time.Advance(TimeSpan.FromSeconds(1));
        var guest = Comment.CreateGuest(
            bridged,
            parent: null,
            new GuestAuthor("Гость из Telegram", null),
            new CommentSource("channel", "1"),
            "Гостевой",
            api.Time.GetUtcNow());
        await api.SaveAsync<CommentsDbContext>(guest);

        var items = await FeedAsync();

        var own = BestOf(items, native);
        Assert.Equal(blog.Id, own.GetProperty("author").GetProperty("id").GetGuid());
        Assert.Equal("Борис", own.GetProperty("author").GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, own.GetProperty("guestName").ValueKind);
        var outside = BestOf(items, bridged);
        Assert.Equal(guest.Id, outside.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, outside.GetProperty("author").ValueKind);
        Assert.Equal("Гость из Telegram", outside.GetProperty("guestName").GetString());
    }

    [Fact]
    public async Task LongTextComesCutWithAnEllipsis()
    {
        var post = await PublishAsync();
        await CommentAsync(post, string.Join(' ', Enumerable.Repeat("слово", 200)));

        var text = BestOf(await FeedAsync(), post).GetProperty("text").GetString()!;

        Assert.EndsWith("…", text, StringComparison.Ordinal);
        Assert.True(text.Length <= 301, $"The excerpt has {text.Length} characters.");
    }

    // No N+1: a page of many posts with comments, likes and comment authors costs the queries of a page of one.
    [Fact]
    public async Task PageOfManyPostsCostsTheQueriesOfAPageOfOne()
    {
        var blog = Subsite.CreatePersonal(_commenter, api.Time.GetUtcNow());
        await api.SaveAsync<BlogsDbContext>(blog);
        var one = await PublishWithLikedCommentAsync();
        api.Commands.Reset();
        var single = await FeedAsync(limit: 1);
        var ofOne = api.Commands.Count;
        for (var i = 0; i < 5; i++)
        {
            await PublishWithLikedCommentAsync();
        }

        api.Commands.Reset();
        var many = await FeedAsync(limit: 6);
        var ofMany = api.Commands.Count;

        Assert.Equal(6, many.GetArrayLength());
        Assert.All(many.EnumerateArray(), item => Assert.NotEqual(JsonValueKind.Null, item.GetProperty("bestComment").ValueKind));
        Assert.NotEqual(JsonValueKind.Null, BestOf(single, one).ValueKind);
        Assert.Equal(ofOne, ofMany);
    }

    private async Task<Guid> PublishWithLikedCommentAsync()
    {
        var post = await PublishAsync();
        await CommentAsync(post, "Без лайков");
        var liked = await CommentAsync(post, "С лайком");
        await LikeAsync(liked, post, 1);
        return post;
    }

    private async Task<Guid> PublishAsync()
    {
        api.Time.Advance(TimeSpan.FromMinutes(1));
        var post = Post.CreateDraft(_author, _blog, "Заголовок", PostFactory.Body("Текст."), api.Time.GetUtcNow());
        post.Publish(api.Time.GetUtcNow());
        await api.SaveAsync<PostsDbContext>(post);
        return post.Id;
    }

    private async Task<Comment> CommentAsync(Guid post, string text, Comment? parent = null, bool delete = false)
    {
        api.Time.Advance(TimeSpan.FromSeconds(1));
        var comment = Comment.CreateNative(_commenter, post, parent, text, api.Time.GetUtcNow());
        if (delete)
        {
            comment.Delete(api.Time.GetUtcNow());
        }

        await api.SaveAsync<CommentsDbContext>(comment);
        return comment;
    }

    private async Task LikeAsync(Comment comment, Guid post, int count)
    {
        var likes = Enumerable.Range(1, count)
            .Select(i => (object)Reaction.Like(
                ReactionTarget.Comment,
                comment.Id,
                post,
                Guid.CreateVersion7(api.Time.GetUtcNow().AddMilliseconds(i)),
                api.Time.GetUtcNow()))
            .Append(new Rating
            {
                TargetKind = ReactionTarget.Comment,
                TargetId = comment.Id,
                PostId = post,
                Value = count * ReactionWeights.Of(ReactionKind.Like),
            })
            .ToArray();
        await api.SaveAsync<ReactionsDbContext>(likes);
    }

    private async Task<JsonElement> FeedAsync(int limit = 20) =>
        (await _client.GetFromJsonAsync<JsonElement>($"/api/v1/feeds/fresh?limit={limit}", Ct)).GetProperty("items");

    private static JsonElement ItemOf(JsonElement items, Guid post) =>
        items.EnumerateArray().Single(i => i.GetProperty("post").GetProperty("id").GetGuid() == post);

    private static JsonElement BestOf(JsonElement items, Guid post) => ItemOf(items, post).GetProperty("bestComment");
}
