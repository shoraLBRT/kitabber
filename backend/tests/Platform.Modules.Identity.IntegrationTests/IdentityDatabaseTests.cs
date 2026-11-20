using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Persistence;
using Platform.Testing.Containers;
using Platform.Testing.Data;
using Platform.Testing.Time;

namespace Platform.Modules.Identity.IntegrationTests;

/// <summary>The Identity module on a real PostgreSQL: its migration, its schema and its demo accounts.</summary>
public sealed class IdentityDatabaseTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MigrationCreatesAccountsInTheIdentitySchemaOnly()
    {
        var db = await postgres.CreateDatabaseAsync();

        await using (var provider = Provider(db))
        {
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync(Ct);
        }

        Assert.Equal(
            ["identity.__ef_migrations_history", "identity.accounts"],
            await Strings(db, """
                SELECT table_schema || '.' || table_name FROM information_schema.tables
                WHERE table_schema NOT IN ('pg_catalog', 'information_schema') ORDER BY 1
                """));
        Assert.Equal(
            ["created_at timestamp with time zone NO", "id uuid NO"],
            await Strings(db, """
                SELECT column_name || ' ' || data_type || ' ' || is_nullable FROM information_schema.columns
                WHERE table_schema = 'identity' AND table_name = 'accounts' ORDER BY column_name
                """));
    }

    [Fact]
    public async Task CreatedAccountIsStoredWithItsUuidV7()
    {
        var db = await postgres.CreateDatabaseAsync();
        await using var provider = Provider(db);
        var account = Account.Create(TestClock.Create());
        await using (var scope = provider.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await identity.Database.MigrateAsync(Ct);
            identity.Accounts.Add(account);
            await identity.SaveChangesAsync(Ct);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
                .Accounts.SingleAsync(a => a.Id == account.Id, Ct);
            Assert.True(AccountIds.IsVersion7(stored.Id));
            Assert.Equal(TestClock.Start, stored.CreatedAt);
        }

        // The database keeps the identifier the application gave, never one of its own.
        Assert.Equal([account.Id.ToString()], await Strings(db, "SELECT id::text FROM identity.accounts"));
    }

    [Fact]
    public async Task DemoSeederAddsAccountsOnceWithTheIdentifiersOtherModulesUse()
    {
        var db = await postgres.CreateDatabaseAsync();
        await using var provider = Provider(db);
        await using var scope = provider.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await identity.Database.MigrateAsync(Ct);
        var data = TestData.Create(TestClock.Create());
        var seeder = new IdentityDemoSeeder(identity);

        var first = await seeder.SeedAsync(data, Ct);
        var second = await seeder.SeedAsync(data, Ct);

        Assert.Equal(IdentityDemoSeeder.Count, first);
        Assert.Equal(0, second);
        // Another module's factory takes the author Id("account", 3) without reading this schema.
        var author = data.Id("account", 3);
        Assert.True(await identity.Accounts.AnyAsync(a => a.Id == author, Ct));
        Assert.All(await identity.Accounts.ToListAsync(Ct), a => Assert.True(a.CreatedAt < data.Now));
    }

    private static ServiceProvider Provider(string connectionString) =>
        new ServiceCollection().AddModuleDbContext<IdentityDbContext>(connectionString).BuildServiceProvider();

    private static async Task<List<string>> Strings(string db, string sql)
    {
        await using var connection = new NpgsqlConnection(db);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var result = new List<string>();
        while (await reader.ReadAsync(Ct))
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }
}
