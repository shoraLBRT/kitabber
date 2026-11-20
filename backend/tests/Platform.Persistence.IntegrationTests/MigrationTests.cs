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
        Assert.Contains("identity: applied", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// FR-ID-1 on the database the real modules migrate, so a migration with hand-written SQL is checked too: no
    /// column for mail, a password or an external identifier outside the schema <c>identity</c>, and no foreign key
    /// from one schema to another. The same rule on the EF Core models is in Platform.Architecture.Tests.
    /// </summary>
    [Fact]
    public async Task RealModulesKeepCredentialsInIdentityAndNoForeignKeysBetweenSchemas()
    {
        var db = await postgres.CreateDatabaseAsync();
        using var output = new StringWriter();
        Assert.Equal(0, await MigratorApp.RunAsync(["migrate", $"--ConnectionStrings:Platform={db}"], output, output));

        Assert.Contains("identity.accounts", await Strings(db, _allTables));
        Assert.Empty(await Strings(db, _credentialColumnsOutsideIdentity));
        Assert.Empty(await Strings(db, _foreignKeysBetweenSchemas));
    }

    /// <summary>The queries above really find violations: a table that breaks both rules is reported.</summary>
    [Fact]
    public async Task CredentialColumnsAndForeignKeysBetweenSchemasAreFound()
    {
        var db = await postgres.CreateDatabaseAsync();
        using var output = new StringWriter();
        Assert.Equal(0, await MigratorApp.RunAsync(["migrate", $"--ConnectionStrings:Platform={db}"], output, output));

        await Execute(db, """
            CREATE SCHEMA posts;
            CREATE TABLE posts.posts (
                id uuid PRIMARY KEY,
                author_id uuid NOT NULL CONSTRAINT fk_posts_accounts REFERENCES identity.accounts (id),
                author_email text,
                yandex_external_id text);
            """);

        Assert.Equal(
            ["posts.posts.author_email", "posts.posts.yandex_external_id"],
            await Strings(db, _credentialColumnsOutsideIdentity));
        Assert.Equal(["posts.posts -> identity.accounts"], await Strings(db, _foreignKeysBetweenSchemas));
    }

    private const string _allTables = "SELECT table_schema || '.' || table_name FROM information_schema.tables";

    private const string _credentialColumnsOutsideIdentity = """
        SELECT table_schema || '.' || table_name || '.' || column_name FROM information_schema.columns
        WHERE table_schema NOT IN ('identity', 'pg_catalog', 'information_schema')
          AND (column_name ~ '(email|e_mail|password|passwd)'
               OR column_name ~ '(^|_)(external_id|provider_key|provider_subject|login_provider|oidc_subject)$')
        ORDER BY 1
        """;

    private const string _foreignKeysBetweenSchemas = """
        SELECT sn.nspname || '.' || s.relname || ' -> ' || tn.nspname || '.' || t.relname
        FROM pg_constraint c
        JOIN pg_class s ON s.oid = c.conrelid JOIN pg_namespace sn ON sn.oid = s.relnamespace
        JOIN pg_class t ON t.oid = c.confrelid JOIN pg_namespace tn ON tn.oid = t.relnamespace
        WHERE c.contype = 'f' AND s.relnamespace <> t.relnamespace
        ORDER BY 1
        """;

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
