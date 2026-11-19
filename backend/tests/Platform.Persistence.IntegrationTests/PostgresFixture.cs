using System.Text.RegularExpressions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Platform.Persistence.IntegrationTests;

/// <summary>
/// One PostgreSQL container per test run, the same image as compose.yaml; each test gets its own empty database.
/// Without Docker the tests are skipped locally (or with VERIFY_SKIP_DOCKER=1); in CI they fail instead.
/// The general test infrastructure (application factory, Respawn) arrives with E01-15.
/// </summary>
public sealed partial class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _skipReason;

    private static bool InCi => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));

    public async ValueTask InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("VERIFY_SKIP_DOCKER") == "1" && !InCi)
        {
            _skipReason = "VERIFY_SKIP_DOCKER=1";
            return;
        }

        try
        {
            _container = new PostgreSqlBuilder(ComposePostgresImage()).Build();
            await _container.StartAsync();
        }
        catch (Exception e) when (!InCi && e is not ImageNotInComposeException)
        {
            _skipReason = $"Docker is not available: {e.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>Creates a new empty database and returns its connection string.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        Assert.SkipWhen(_skipReason is not null, _skipReason ?? string.Empty);
        var name = $"test_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(_container!.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await create.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name }.ConnectionString;
    }

    private static string ComposePostgresImage()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "compose.yaml")))
        {
            dir = dir.Parent;
        }

        var compose = dir is null ? string.Empty : File.ReadAllText(Path.Combine(dir.FullName, "compose.yaml"));
        var match = PostgresImage().Match(compose);
        return match.Success ? match.Groups[1].Value : throw new ImageNotInComposeException();
    }

    [GeneratedRegex(@"image:\s*(postgres:\S+)")]
    private static partial Regex PostgresImage();

    private sealed class ImageNotInComposeException()
        : InvalidOperationException("compose.yaml with a postgres image was not found above the test binaries");
}

[CollectionDefinition(Name)]
public sealed class PostgresDatabases : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
