using System.Diagnostics;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Configuration;
using Platform.Modules.Blogs;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Posts;
using Platform.Modules.Posts.Contracts;
using Platform.Testing.Containers;
using Platform.Workers;

namespace Platform.Modules.Search.IntegrationTests;

/// <summary>
/// E14-01 (FR-SEARCH, ADR-0094): the real workers host (<see cref="WorkerHost.CreateBuilder"/>, in-memory bus, the fake
/// search engine) keeps the indexes in line with the posts and blogs in PostgreSQL. A published post is in the index
/// within a minute of its event, a deleted one and a draft are not; a new topic and a renamed blog follow; the command
/// <c>search-reindex</c> rebuilds both indexes from the database.
/// </summary>
public sealed class SearchIndexingTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly TimeSpan _minute = TimeSpan.FromMinutes(1);
    private static readonly DateTimeOffset _at = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private IHost _host = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private InMemorySearchIndex Index => _host.Services.GetRequiredService<InMemorySearchIndex>();

    public async ValueTask InitializeAsync()
    {
        var database = await postgres.CreateDatabaseAsync();
        _host = WorkerHost.CreateBuilder(
            ["--environment", PlatformEnvironments.Test, "--Messaging:Transport", "InMemory", "--Cache:Provider", "InMemory",
                "--Mail:Provider", "InMemory", "--Search:Provider", "InMemory", $"--ConnectionStrings:Platform={database}"])
            .Build();
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<SearchDbContext>().Database.MigrateAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<PostsDbContext>().Database.MigrateAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<BlogsDbContext>().Database.MigrateAsync(Ct);
        }

        await _host.StartAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync(CancellationToken.None);
        _host.Dispose();
    }

    [Fact]
    public async Task PublishedPostIsFoundWithinAMinuteAndADeletedOneDisappears()
    {
        var post = await SavePostAsync("Как воспитывать котов", "Коты любят <b>спать</b>.", publish: true);
        var draft = await SavePostAsync("Черновик про котов", "Ещё не готов", publish: false);

        var clock = Stopwatch.StartNew();
        await PublishAsync(new PostChanged(post.Id, _at));
        await PublishAsync(new PostChanged(draft.Id, _at));
        Assert.True(clock.Elapsed < _minute, $"indexed in {clock.Elapsed}");

        var indexed = Assert.IsType<IndexedPost>(Assert.Single(Index.Documents(SearchIndexKind.Posts), d => d.Id == post.Id));
        Assert.Equal(("Как воспитывать котов", "Коты любят спать.", post.BlogId, post.AuthorAccountId),
            (indexed.Title, indexed.Text, indexed.BlogId, indexed.AuthorAccountId));
        Assert.Equal([post.Id], await Index.SearchAsync(SearchIndexKind.Posts, "воспитывать", 10, Ct));
        Assert.DoesNotContain(Index.Documents(SearchIndexKind.Posts), d => d.Id == draft.Id);

        await ChangePostAsync(post.Id, p => p.Delete(_at.AddHours(1)));
        await PublishAsync(new PostChanged(post.Id, _at.AddHours(1)));

        Assert.DoesNotContain(Index.Documents(SearchIndexKind.Posts), d => d.Id == post.Id);
        Assert.Empty(await Index.SearchAsync(SearchIndexKind.Posts, "воспитывать", 10, Ct));
    }

    [Fact]
    public async Task EditedPostIsIndexedAsItIsNowWhateverTheOrderOfTheEvents()
    {
        var post = await SavePostAsync("Первый заголовок", "Текст", publish: true);
        var first = new PostChanged(post.Id, _at);
        await ChangePostAsync(post.Id, p => p.Edit(p.BlogId, "Второй заголовок", Body("Новый текст"), _at.AddMinutes(5)));

        await PublishAsync(new PostChanged(post.Id, _at.AddMinutes(5)));
        // A late event of the first version changes nothing: the post is read as it is now.
        await PublishAsync(first);

        var indexed = Assert.IsType<IndexedPost>(Assert.Single(Index.Documents(SearchIndexKind.Posts), d => d.Id == post.Id));
        Assert.Equal(("Второй заголовок", "Новый текст"), (indexed.Title, indexed.Text));
    }

    [Fact]
    public async Task NewTopicAndRenamedBlogFollowAndABlogWithNothingToFindIsLeftOut()
    {
        var topic = Subsite.CreateTopic("Астрономия", $"astro{Guid.NewGuid():N}"[..20], Guid.NewGuid(), _at);
        var blog = Subsite.CreatePersonal(Guid.NewGuid(), _at);
        await SaveAsync(topic, blog);

        await PublishAsync(new BlogChanged(topic.Id, _at));
        await PublishAsync(new BlogChanged(blog.Id, _at));

        var indexed = Assert.IsType<IndexedBlog>(Assert.Single(Index.Documents(SearchIndexKind.Blogs), d => d.Id == topic.Id));
        Assert.Equal((IndexedBlog.TopicKind, "Астрономия"), (indexed.Kind, indexed.Name));
        Assert.DoesNotContain(Index.Documents(SearchIndexKind.Blogs), d => d.Id == blog.Id);

        await ChangeBlogAsync(blog.Id, b =>
        {
            b.Rename("Анна");
            b.Describe("Пишу о кошках");
        });
        await PublishAsync(new BlogChanged(blog.Id, _at.AddMinutes(1)));

        var renamed = Assert.IsType<IndexedBlog>(Assert.Single(Index.Documents(SearchIndexKind.Blogs), d => d.Id == blog.Id));
        Assert.Equal((IndexedBlog.PersonalKind, "Анна", "Пишу о кошках", blog.OwnerAccountId),
            (renamed.Kind, renamed.Name, renamed.Description, renamed.OwnerAccountId));
    }

    [Fact]
    public async Task ReindexCommandRebuildsBothIndexesFromTheDatabase()
    {
        var posts = new List<Post>();
        for (var i = 0; i < SearchReindexer.PageSize + 2; i++)
        {
            posts.Add(await SavePostAsync($"Пост {i}", "Текст", publish: true));
        }

        var draft = await SavePostAsync("Черновик", "Текст", publish: false);
        var deleted = await SavePostAsync("Удалённый", "Текст", publish: true);
        await ChangePostAsync(deleted.Id, p => p.Delete(_at));
        var topic = Subsite.CreateTopic("Химия", $"chem{Guid.NewGuid():N}"[..20], Guid.NewGuid(), _at);
        await SaveAsync(topic, Subsite.CreatePersonal(Guid.NewGuid(), _at));
        var stale = new IndexedPost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Пропал", "Его нет в базе", _at);
        await Index.PutAsync(stale, Ct);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = await SearchReindexCommand.RunAsync(_host.Services, output, error, Ct);

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains($"{posts.Count} posts, 1 blogs and topics", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(posts.Select(p => p.Id).Order(), Index.Documents(SearchIndexKind.Posts).Select(d => d.Id).Order());
        Assert.Equal([topic.Id], Index.Documents(SearchIndexKind.Blogs).Select(d => d.Id));
        Assert.DoesNotContain(Index.Documents(SearchIndexKind.Posts), d => d.Id == draft.Id || d.Id == stale.Id);
    }

    private static string Body(string text) =>
        $$$"""{"version": 1, "blocks": [{"id": "p1", "type": "paragraph", "data": {"text": "{{{text}}}"}}]}""";

    private async Task<Post> SavePostAsync(string title, string text, bool publish)
    {
        var post = Post.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), title, Body(text), _at);
        if (publish)
        {
            post.Publish(_at);
        }

        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        db.Posts.Add(post);
        await db.SaveChangesAsync(Ct);
        return post;
    }

    private async Task ChangePostAsync(Guid id, Action<Post> change)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostsDbContext>();
        change(await db.Posts.SingleAsync(p => p.Id == id, Ct));
        await db.SaveChangesAsync(Ct);
    }

    private async Task SaveAsync(params Subsite[] blogs)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogsDbContext>();
        db.Subsites.AddRange(blogs);
        await db.SaveChangesAsync(Ct);
    }

    private async Task ChangeBlogAsync(Guid id, Action<Subsite> change)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogsDbContext>();
        change(await db.Subsites.SingleAsync(s => s.Id == id, Ct));
        await db.SaveChangesAsync(Ct);
    }

    // Publishes the event and waits until the consumer of the Search schema has handled it: its inbox record is marked
    // consumed after the index was written.
    private async Task PublishAsync<T>(T message)
        where T : class
    {
        var messageId = NewId.NextGuid();
        await _host.Services.GetRequiredService<IBus>().Publish(message, c => c.MessageId = messageId, Ct);
        var deadline = DateTime.UtcNow + _minute;
        while (true)
        {
            await using (var scope = _host.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
                if (await db.Set<InboxState>().AnyAsync(i => i.MessageId == messageId && i.Consumed != null, Ct))
                {
                    return;
                }
            }

            Assert.True(DateTime.UtcNow < deadline, $"{typeof(T).Name} was not handled in a minute");
            await Task.Delay(100, Ct);
        }
    }
}
