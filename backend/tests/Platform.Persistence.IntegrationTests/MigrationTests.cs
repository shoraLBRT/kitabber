using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Migrator;
using Platform.Persistence.IntegrationTests.SampleModules;
using Platform.Testing.Containers;

namespace Platform.Persistence.IntegrationTests;

/// <summary>The migration command on a real PostgreSQL: apply on a clean database, roll back the last migration.</summary>
public sealed class MigrationTests(PostgresFixture postgres)
{
    private static void AddSampleModules(IServiceCollection services, string connectionString) => services
        .AddModuleDbContext<AlphaDbContext>(connectionString)
        .AddModuleDbContext<BetaDbContext>(connectionString);

    [Fact]
    public async Task MigrateAppliesEveryModuleIntoItsOwnSchemaOnCleanDatabase()
    {
        var db = await postgres.CreateDatabaseAsync();

        var (exit, output) = await Run(db, "migrate");

        Assert.Equal(0, exit);
        Assert.Contains("alpha: applied 2 migration(s)", output, StringComparison.Ordinal);
        Assert.Contains("beta: applied 1 migration(s)", output, StringComparison.Ordinal);
        Assert.Equal(["__ef_migrations_history", "categories", "notes"], await Tables(db, "alpha"));
        Assert.Equal(["__ef_migrations_history", "notes"], await Tables(db, "beta"));
        Assert.Empty(await Tables(db, "public"));
        Assert.Equal(["20261119000001_CreateNotes", "20261119000002_AddCategories"], await History(db, "alpha"));
        Assert.Equal(["20261119000001_CreateNotes"], await History(db, "beta"));
        Assert.Equal(["games", "news"], await Strings(db, "SELECT name FROM alpha.categories ORDER BY name"));
    }

    [Fact]
    public async Task MigrateTwiceChangesNothing()
    {
        var db = await postgres.CreateDatabaseAsync();
        await Run(db, "migrate");

        var (exit, output) = await Run(db, "migrate");

        Assert.Equal(0, exit);
        Assert.Contains("alpha: applied 0 migration(s)", output, StringComparison.Ordinal);
        Assert.Equal(2, (await History(db, "alpha")).Count);
    }

    [Fact]
    public async Task RollbackRevertsOnlyTheLastMigrationOfThatModule()
    {
        var db = await postgres.CreateDatabaseAsync();
        await Run(db, "migrate");
        await Execute(db, "INSERT INTO alpha.notes (id, title) VALUES (gen_random_uuid(), 'kept')");

        var (exit, output) = await Run(db, "rollback", "alpha");

        Assert.Equal(0, exit);
        Assert.Contains("alpha: rolled back 20261119000002_AddCategories", output, StringComparison.Ordinal);
        Assert.Equal(["__ef_migrations_history", "notes"], await Tables(db, "alpha"));
        Assert.Equal(["20261119000001_CreateNotes"], await History(db, "alpha"));
        Assert.Equal(["kept"], await Strings(db, "SELECT title FROM alpha.notes"));
        Assert.Equal(["20261119000001_CreateNotes"], await History(db, "beta"));

        var (status, statusOutput) = await Run(db, "status");
        Assert.Equal(0, status);
        Assert.Contains("alpha: 1 applied, 1 pending", statusOutput, StringComparison.Ordinal);

        // The rolled-back migration applies again, reference data included.
        await Run(db, "migrate");
        Assert.Equal(2, (await History(db, "alpha")).Count);
        Assert.Equal(["games", "news"], await Strings(db, "SELECT name FROM alpha.categories ORDER BY name"));
    }

    [Fact]
    public async Task RollbackOfUnknownModuleFails()
    {
        var db = await postgres.CreateDatabaseAsync();

        var (exit, output) = await Run(db, "rollback", "nope");

        Assert.Equal(1, exit);
        Assert.Contains("Unknown module schema 'nope'", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigratorWithRealModuleCatalogRunsOnCleanDatabase()
    {
        var db = await postgres.CreateDatabaseAsync();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await MigratorApp.RunAsync(["migrate", $"--ConnectionStrings:Platform={db}"], output, error);

        Assert.True(exit == 0, error.ToString());
    }

    private static async Task<(int Exit, string Output)> Run(string connectionString, params string[] words)
    {
        using var output = new StringWriter();
        var exit = await MigratorApp.RunAsync(
            [.. words, $"--ConnectionStrings:Platform={connectionString}"], output, output, AddSampleModules);
        return (exit, output.ToString());
    }

    private static Task<List<string>> Tables(string db, string schema) => Strings(
        db, $"SELECT table_name FROM information_schema.tables WHERE table_schema = '{schema}' ORDER BY table_name");

    private static Task<List<string>> History(string db, string schema) => Strings(
        db, $"SELECT migration_id FROM {schema}.{ModuleDatabase.HistoryTable} ORDER BY migration_id");

    private static async Task<List<string>> Strings(string db, string sql)
    {
        await using var connection = new NpgsqlConnection(db);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<string>();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    private static async Task Execute(string db, string sql)
    {
        await using var connection = new NpgsqlConnection(db);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
