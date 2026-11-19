using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Api.IntegrationTests.SampleModule;
using Platform.Configuration;
using Platform.Persistence;
using Platform.Seeding;
using Platform.Testing.Containers;
using Platform.Testing.Time;

namespace Platform.Api.IntegrationTests;

/// <summary>
/// The seed command (<c>tools/Seed</c>) on real PostgreSQL with the sample module: it fills an empty database,
/// gives the same data for the same seed, and adds nothing on a second run (ADR-0009).
/// </summary>
public sealed class DemoSeedTests(PostgresFixture postgres)
{
    [Fact]
    public async Task SeedFillsEmptyDatabase()
    {
        var db = await postgres.CreateDatabaseAsync();

        var (exit, output) = await Seed(db);

        Assert.True(exit == 0, output);
        Assert.Contains($"{NotesDbContext.Schema}: {NotesDemoSeeder.Count} added", output, StringComparison.Ordinal);
        Assert.Equal(NotesDemoSeeder.Count, (await Notes(db)).Count);
    }

    [Fact]
    public async Task SameSeedGivesSameDataAndOtherSeedOtherData()
    {
        var first = await postgres.CreateDatabaseAsync();
        var second = await postgres.CreateDatabaseAsync();
        var other = await postgres.CreateDatabaseAsync();

        await Seed(first);
        await Seed(second);
        await Seed(other, "--Seed:RandomSeed=42");

        var expected = await Notes(first);
        Assert.Equal(expected, await Notes(second));
        Assert.Empty((await Notes(other)).Select(n => n.Id).Intersect(expected.Select(n => n.Id)));
        Assert.Equal(TestClock.Start - TimeSpan.FromMinutes(30), expected.Max(n => n.CreatedAt), TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task SecondRunAddsNothingAndRestoresWhatIsMissing()
    {
        var db = await postgres.CreateDatabaseAsync();
        await Seed(db);
        var before = await Notes(db);

        var (exit, output) = await Seed(db);
        Assert.Equal(0, exit);
        Assert.Contains($"{NotesDbContext.Schema}: 0 added", output, StringComparison.Ordinal);
        Assert.Equal(before, await Notes(db));

        await using (var context = Context(db))
        {
            await context.Notes.Where(n => n.Id == before[0].Id).ExecuteDeleteAsync(Ct);
        }

        (_, output) = await Seed(db);
        Assert.Contains($"{NotesDbContext.Schema}: 1 added", output, StringComparison.Ordinal);
        Assert.Equal(before, await Notes(db));
    }

    [Fact]
    public async Task StagingIsSeededOnlyWhenExplicitlyAllowed()
    {
        var db = await postgres.CreateDatabaseAsync();

        var (refused, _) = await Seed(db, "--environment", PlatformEnvironments.Staging);
        var (allowed, output) = await Seed(
            db, "--environment", PlatformEnvironments.Staging, $"--{SeedApp.AllowProtectedEnvironmentKey}=true");

        Assert.Equal(SeedApp.RefusedExitCode, refused);
        Assert.True(allowed == 0, output);
        Assert.Equal(NotesDemoSeeder.Count, (await Notes(db)).Count);
    }

    [Fact]
    public async Task SeedToolWithItsRealCatalogRunsOnCleanDatabase()
    {
        var db = await postgres.CreateDatabaseAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await SeedApp.RunAsync(
            ["--environment", PlatformEnvironments.Test, $"--ConnectionStrings:Platform={db}"],
            output, error, Platform.Seed.SeedCatalog.AddModules);

        Assert.True(exit == 0, error.ToString());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(int Exit, string Output)> Seed(string db, params string[] options)
    {
        using var output = new StringWriter();
        string[] args = options.Contains("--environment")
            ? [.. options, $"--ConnectionStrings:Platform={db}"]
            : ["--environment", PlatformEnvironments.Test, .. options, $"--ConnectionStrings:Platform={db}"];
        var exit = await SeedApp.RunAsync(args, output, output, (services, connectionString) =>
        {
            services.AddModuleDbContext<NotesDbContext>(connectionString);
            services.AddDemoSeeder<NotesDemoSeeder>();
            services.AddSingleton<TimeProvider>(TestClock.Create());
        });
        return (exit, output.ToString());
    }

    private static async Task<List<NoteView>> Notes(string db)
    {
        await using var context = Context(db);
        var rows = await context.Notes.OrderBy(n => n.Id).ToListAsync(Ct);
        return [.. rows.Select(NoteView.From)];
    }

    private static NotesDbContext Context(string db)
    {
        var options = new DbContextOptionsBuilder<NotesDbContext>();
        ModuleDatabaseServiceCollectionExtensions.UseModuleDatabase<NotesDbContext>(options, db);
        return new NotesDbContext(options.Options);
    }
}
