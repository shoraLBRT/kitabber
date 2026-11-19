using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Persistence.IntegrationTests.SampleModules;
using Platform.Testing.Containers;
using Platform.Testing.Database;

namespace Platform.Persistence.IntegrationTests;

/// <summary>Respawn between tests: the module rows go, the schemas and the migration history stay (ADR-0008).</summary>
public sealed class DatabaseResetTests(PostgresFixture postgres)
{
    [Fact]
    public async Task ResetEmptiesModuleTablesAndKeepsTheMigrationHistory()
    {
        var (db, schemas) = await MigratedDatabaseAsync();
        await Execute(db, "INSERT INTO alpha.notes (id, title) VALUES (gen_random_uuid(), 'a')");
        await Execute(db, "INSERT INTO beta.notes (id, title) VALUES (gen_random_uuid(), 'b')");
        var reset = await DatabaseReset.CreateAsync(db, schemas);

        await reset.ResetAsync();

        Assert.Equal(0, await Count(db, "alpha.notes"));
        Assert.Equal(0, await Count(db, "beta.notes"));
        Assert.Equal(0, await Count(db, "alpha.categories"));
        Assert.Equal(2, await Count(db, "alpha.__ef_migrations_history"));
        Assert.Equal(1, await Count(db, "beta.__ef_migrations_history"));
    }

    [Fact]
    public async Task KeptTablesSurviveTheReset()
    {
        var (db, schemas) = await MigratedDatabaseAsync();
        await Execute(db, "INSERT INTO alpha.notes (id, title) VALUES (gen_random_uuid(), 'a')");
        var reset = await DatabaseReset.CreateAsync(db, schemas, keepTables: ["alpha.categories"]);

        await reset.ResetAsync();
        await reset.ResetAsync();

        Assert.Equal(0, await Count(db, "alpha.notes"));
        Assert.Equal(2, await Count(db, "alpha.categories"));
    }

    [Fact]
    public async Task KeptTableNeedsItsSchema()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => DatabaseReset.CreateAsync("Host=localhost", ["alpha"], keepTables: ["categories"]));
        Assert.Contains("schema.table", error.Message, StringComparison.Ordinal);
    }

    private async Task<(string Db, string[] Schemas)> MigratedDatabaseAsync()
    {
        var db = await postgres.CreateDatabaseAsync();
        await using var services = new ServiceCollection()
            .AddModuleDbContext<AlphaDbContext>(db)
            .AddModuleDbContext<BetaDbContext>(db)
            .BuildServiceProvider();
        var migrator = ActivatorUtilities.CreateInstance<ModuleMigrator>(services);
        await migrator.MigrateAsync(TextWriter.Null, TestContext.Current.CancellationToken);
        return (db, [.. migrator.Modules.Select(m => m.Schema)]);
    }

    private static async Task Execute(string db, string sql)
    {
        await using var connection = new NpgsqlConnection(db);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> Count(string db, string table)
    {
        await using var connection = new NpgsqlConnection(db);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table}", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
