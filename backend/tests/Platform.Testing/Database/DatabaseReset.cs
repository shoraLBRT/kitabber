using Npgsql;
using Platform.Persistence;
using Respawn;
using Respawn.Graph;

namespace Platform.Testing.Database;

/// <summary>
/// Empties the module tables of a test database between tests with Respawn (ADR-0008). The schemas and the migration
/// history stay, so the migrations run once per test class, not once per test. Reference data seeded by a migration
/// is deleted too, unless its table is listed in <c>keepTables</c>.
/// </summary>
public sealed class DatabaseReset
{
    private readonly string _connectionString;
    private readonly Respawner? _respawner;

    private DatabaseReset(string connectionString, Respawner? respawner)
    {
        _connectionString = connectionString;
        _respawner = respawner;
    }

    /// <summary>
    /// Reads the tables of <paramref name="schemas"/> once; call it after the migrations.
    /// </summary>
    /// <param name="connectionString">The test database.</param>
    /// <param name="schemas">Module schemas to empty (<see cref="ModuleDatabase.Schema"/>).</param>
    /// <param name="keepTables">Tables to keep as they are, as <c>schema.table</c>.</param>
    public static async Task<DatabaseReset> CreateAsync(
        string connectionString, IEnumerable<string> schemas, IEnumerable<string>? keepTables = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(schemas);
        var include = schemas.Distinct(StringComparer.Ordinal).ToArray();
        if (include.Length == 0)
        {
            return new DatabaseReset(connectionString, null);
        }

        Table[] ignore =
        [
            new(ModuleDatabase.HistoryTable),
            .. (keepTables ?? []).Select(Parse),
        ];
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = include,
            TablesToIgnore = ignore,
        });
        return new DatabaseReset(connectionString, respawner);
    }

    /// <summary>Deletes every row of the module tables.</summary>
    public async Task ResetAsync()
    {
        if (_respawner is null)
        {
            return;
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    private static Table Parse(string table)
    {
        var dot = table.IndexOf('.', StringComparison.Ordinal);
        return dot > 0
            ? new Table(table[..dot], table[(dot + 1)..])
            : throw new ArgumentException($"'{table}': give the table as schema.table.", nameof(table));
    }
}
