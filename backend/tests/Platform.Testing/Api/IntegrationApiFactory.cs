using Microsoft.Extensions.DependencyInjection;
using Platform.Caching;
using Platform.Persistence;
using Platform.Testing.Containers;
using Platform.Testing.Database;

namespace Platform.Testing.Api;

/// <summary>
/// The Api host on real PostgreSQL and Redis from Testcontainers (ADR-0008). Use it as a class fixture: the test
/// class gets its own database (migrated once, with every registered module) and its own Redis database. Call
/// <see cref="ResetAsync"/> from the test class's <c>InitializeAsync</c>: the first call starts the host, every call
/// empties the module tables (Respawn) and Redis, so each test starts from a clean state. Without Docker the test is
/// skipped locally and fails in CI, as with the container fixtures.
/// </summary>
public class IntegrationApiFactory(PostgresFixture postgres, RedisFixture redis) : PlatformApiFactory
{
    /// <summary>The connection string name of the platform database, as in the migrator (ADR-0004).</summary>
    public const string DatabaseConnectionName = "Platform";

    private readonly SemaphoreSlim _start = new(1, 1);
    private DatabaseReset? _reset;

    /// <summary>The test database of this factory; set by the first <see cref="ResetAsync"/>.</summary>
    public string DatabaseConnectionString { get; private set; } = string.Empty;

    /// <summary>The Redis database of this factory; set by the first <see cref="ResetAsync"/>.</summary>
    public string RedisConnectionString { get; private set; } = string.Empty;

    /// <summary>Tables, as <c>schema.table</c>, whose rows survive a reset: reference data seeded by migrations.</summary>
    protected virtual IEnumerable<string> KeepTables => [];

    /// <summary>Starts the host on the first call, then brings the database and Redis back to their clean state.</summary>
    public async Task ResetAsync()
    {
        await StartAsync();
        await _reset!.ResetAsync();
        await RedisFixture.FlushAsync(RedisConnectionString);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _start.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task StartAsync()
    {
        await _start.WaitAsync();
        try
        {
            if (_reset is not null)
            {
                return;
            }

            DatabaseConnectionString = await postgres.CreateDatabaseAsync();
            RedisConnectionString = redis.CreateDatabase();
            WithSetting($"ConnectionStrings:{DatabaseConnectionName}", DatabaseConnectionString);
            WithSetting($"ConnectionStrings:{CacheServiceCollectionExtensions.RedisConnectionName}", RedisConnectionString);
            WithSetting(CacheServiceCollectionExtensions.ProviderKey, nameof(CacheProvider.Redis));

            await using var scope = Services.CreateAsyncScope();
            var migrator = ActivatorUtilities.CreateInstance<ModuleMigrator>(scope.ServiceProvider);
            await migrator.MigrateAsync(TextWriter.Null);
            _reset = await DatabaseReset.CreateAsync(
                DatabaseConnectionString, migrator.Modules.Select(m => m.Schema), KeepTables);
        }
        finally
        {
            _start.Release();
        }
    }
}
