using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Configuration;
using Platform.Modules.Blogs;
using Platform.Modules.Blogs.Contracts;
using Platform.Modules.Comments;
using Platform.Modules.Identity;
using Platform.Modules.Identity.Contracts;
using Platform.Modules.Posts;
using Platform.Modules.Posts.Contracts;
using Platform.Persistence;
using Platform.Seeding;
using Platform.Testing.Containers;
using Platform.Testing.Time;

namespace Platform.Api.IntegrationTests;

/// <summary>
/// The seed command with its real catalog (<c>tools/Seed</c>, ADR-0009) on real PostgreSQL: it fills an empty database
/// with the MVP modules' demo data — accounts, blogs, topics, posts and comments — whose references between modules
/// meet, and a second run adds nothing.
/// </summary>
public sealed class MvpDemoSeedTests(PostgresFixture postgres)
{
    private static readonly (string Schema, int Count)[] _expected =
    [
        (IdentityDbContext.Schema, IdentityDemoSeeder.Count),
        (BlogsDbContext.Schema, IdentityDemo.AccountCount + BlogsDemo.TopicCount),
        (PostsDbContext.Schema, PostsDemoSeeder.Count),
        (CommentsDbContext.Schema, CommentsDemoSeeder.Count),
    ];

    [Fact]
    public async Task SeedFillsEmptyDatabaseWithEveryMvpModule()
    {
        var db = await postgres.CreateDatabaseAsync();

        var (exit, output) = await Seed(db);

        Assert.True(exit == 0, output);
        foreach (var (schema, count) in _expected)
        {
            Assert.Contains($"{schema}: {count} added", output, StringComparison.Ordinal);
        }

        var demo = await Load(db);
        Assert.Equal(IdentityDemo.AccountCount, demo.Accounts.Count);
        Assert.Equal(IdentityDemo.AccountCount, demo.Blogs.Count(b => b.Type == SubsiteType.Personal));
        Assert.Equal(BlogsDemo.TopicCount, TopicFactory.Topics.Count);
        Assert.Equal(BlogsDemo.TopicCount, demo.Blogs.Count(b => b.Type == SubsiteType.Topic));
        Assert.Equal(PostsDemo.Count, demo.Posts.Count);
        Assert.Equal(CommentsDemoSeeder.Count, demo.Comments.Count);
    }

    [Fact]
    public async Task ReferencesBetweenModulesMeetThroughSeedIdentifiers()
    {
        var db = await postgres.CreateDatabaseAsync();
        await Seed(db);
        var demo = await Load(db);
        var data = new SeedData(SeedData.DefaultSeed, TestClock.Create());

        Assert.Equal(Ids(data, IdentityDemo.AccountKind, IdentityDemo.AccountCount), demo.Accounts.Select(a => a.Id).Order());
        Assert.Equal(
            Ids(data, BlogsDemo.BlogKind, IdentityDemo.AccountCount),
            demo.Blogs.Where(b => b.Type == SubsiteType.Personal).Select(b => b.Id).Order());
        Assert.Equal(
            Ids(data, BlogsDemo.TopicKind, BlogsDemo.TopicCount),
            demo.Blogs.Where(b => b.Type == SubsiteType.Topic).Select(b => b.Id).Order());
        Assert.Equal(Ids(data, PostsDemo.PostKind, PostsDemo.Count), demo.Posts.Select(p => p.Id).Order());

        var accounts = demo.Accounts.Select(a => a.Id).ToHashSet();
        var blogs = demo.Blogs.ToDictionary(b => b.Id);
        var posts = demo.Posts.ToDictionary(p => p.Id);
        var comments = demo.Comments.ToDictionary(c => c.Id);

        // Each account owns one personal blog.
        Assert.All(demo.Blogs.Where(b => b.Type == SubsiteType.Personal), b => Assert.Contains(b.OwnerAccountId!.Value, accounts));
        Assert.Equal(accounts.Count, demo.Blogs.Select(b => b.OwnerAccountId).OfType<Guid>().Distinct().Count());

        // Posts are published by existing accounts, in their own blog or in a topic; both kinds are on the stand.
        Assert.All(demo.Posts, p =>
        {
            Assert.Equal(PostStatus.Published, p.Status);
            Assert.Contains(p.AuthorAccountId, accounts);
            var blog = Assert.Contains(p.BlogId, blogs);
            Assert.True(blog.Type == SubsiteType.Topic || blog.OwnerAccountId == p.AuthorAccountId, $"post {p.Id}");
        });
        Assert.Contains(demo.Posts, p => blogs[p.BlogId].Type == SubsiteType.Topic);
        Assert.Contains(demo.Posts, p => blogs[p.BlogId].Type == SubsiteType.Personal);

        // Comments are written by existing accounts under existing posts; a reply is in its parent's post.
        Assert.All(demo.Comments, c =>
        {
            Assert.Contains(c.AuthorAccountId!.Value, accounts);
            Assert.Contains(c.PostId, posts);
            if (c.ParentId is { } parent)
            {
                Assert.Equal(c.PostId, Assert.Contains(parent, comments).PostId);
            }
        });
        Assert.Contains(demo.Comments, c => c.ParentId is not null);
    }

    [Fact]
    public async Task SecondRunAddsNothing()
    {
        var db = await postgres.CreateDatabaseAsync();
        await Seed(db);
        var before = await Load(db);

        var (exit, output) = await Seed(db);

        Assert.True(exit == 0, output);
        foreach (var (schema, _) in _expected)
        {
            Assert.Contains($"{schema}: 0 added", output, StringComparison.Ordinal);
        }

        var after = await Load(db);
        Assert.Equal(before.Accounts.Select(a => a.Id), after.Accounts.Select(a => a.Id));
        Assert.Equal(before.Blogs.Select(b => b.Id), after.Blogs.Select(b => b.Id));
        Assert.Equal(before.Posts.Select(p => p.Id), after.Posts.Select(p => p.Id));
        Assert.Equal(before.Comments.Select(c => c.Id), after.Comments.Select(c => c.Id));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IEnumerable<Guid> Ids(SeedData data, string kind, int count) =>
        Enumerable.Range(0, count).Select(n => data.Id(kind, n)).Order();

    private static async Task<(int Exit, string Output)> Seed(string db)
    {
        using var output = new StringWriter();
        var exit = await SeedApp.RunAsync(
            ["--environment", PlatformEnvironments.Test, $"--ConnectionStrings:Platform={db}"],
            output,
            output,
            (services, connectionString) =>
            {
                Platform.Seed.SeedCatalog.AddModules(services, connectionString);
                services.AddSingleton<TimeProvider>(TestClock.Create());
            });
        return (exit, output.ToString());
    }

    private static async Task<DemoData> Load(string db)
    {
        await using var identity = Context<IdentityDbContext>(db, o => new IdentityDbContext(o));
        await using var blogs = Context<BlogsDbContext>(db, o => new BlogsDbContext(o));
        await using var posts = Context<PostsDbContext>(db, o => new PostsDbContext(o));
        await using var comments = Context<CommentsDbContext>(db, o => new CommentsDbContext(o));
        return new DemoData(
            await identity.Accounts.AsNoTracking().OrderBy(a => a.Id).ToListAsync(Ct),
            await blogs.Subsites.AsNoTracking().OrderBy(b => b.Id).ToListAsync(Ct),
            await posts.Posts.AsNoTracking().OrderBy(p => p.Id).ToListAsync(Ct),
            await comments.Comments.AsNoTracking().OrderBy(c => c.Id).ToListAsync(Ct));
    }

    private static TContext Context<TContext>(string db, Func<DbContextOptions<TContext>, TContext> create)
        where TContext : ModuleDbContext<TContext>, IModuleDbContext
    {
        var options = new DbContextOptionsBuilder<TContext>();
        ModuleDatabaseServiceCollectionExtensions.UseModuleDatabase<TContext>(options, db);
        return create(options.Options);
    }

    private sealed record DemoData(
        List<Account> Accounts, List<Subsite> Blogs, List<Post> Posts, List<Comment> Comments);
}
