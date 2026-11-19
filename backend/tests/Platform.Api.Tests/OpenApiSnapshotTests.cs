using System.Text.Json;
using Platform.Testing.Api;

namespace Platform.Api.Tests;

/// <summary>
/// The OpenAPI document is written at build time and must equal the committed snapshot
/// <c>backend/src/Api/openapi/v1.json</c>, the file the frontend client is generated from (ADR-0006).
/// After a deliberate API change, run the tests with <c>UPDATE_OPENAPI_SNAPSHOT=1</c> and commit the snapshot.
/// </summary>
public sealed class OpenApiSnapshotTests
{
    private static readonly string _apiProject = Path.Combine(FindBackend(), "src", "Api");
    private static readonly string _snapshot = Path.Combine(_apiProject, "openapi", "v1.json");

    [Fact]
    public async Task BuildTimeDocumentMatchesTheSnapshot()
    {
        var generated = Path.Combine(_apiProject, "obj", "openapi", "Platform.Api.json");
        Assert.True(File.Exists(generated), $"{generated} was not written by the build");

        var document = await File.ReadAllTextAsync(generated, TestContext.Current.CancellationToken);
        await AssertMatchesSnapshotAsync(document);
    }

    [Fact]
    public async Task ServedDocumentMatchesTheSnapshot()
    {
        using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var document = await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        await AssertMatchesSnapshotAsync(document);
    }

    [Fact]
    public async Task DocumentCoversModuleEndpointsUnderTheVersionOnly()
    {
        using var factory = new PlatformApiFactory().WithModule(TestModule.Register);
        using var client = factory.CreateClient();

        var document = await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var paths = JsonDocument.Parse(document).RootElement.GetProperty("paths")
            .EnumerateObject().Select(p => p.Name).ToList();

        Assert.Contains("/api/v1/test/items", paths);
        Assert.All(paths, path => Assert.StartsWith("/api/v1/", path, StringComparison.Ordinal));
        Assert.True(document.Contains("\"name\": \"cursor\"", StringComparison.Ordinal), document);
        Assert.True(document.Contains("\"name\": \"limit\"", StringComparison.Ordinal), document);
    }

    private static async Task AssertMatchesSnapshotAsync(string document)
    {
        var actual = Normalize(document);
        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI_SNAPSHOT") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_snapshot)!);
            await File.WriteAllTextAsync(_snapshot, actual, TestContext.Current.CancellationToken);
            return;
        }

        Assert.True(File.Exists(_snapshot), $"{_snapshot} is missing; run the tests with UPDATE_OPENAPI_SNAPSHOT=1");
        var expected = Normalize(await File.ReadAllTextAsync(_snapshot, TestContext.Current.CancellationToken));
        Assert.True(
            expected == actual,
            $"The OpenAPI document differs from {_snapshot}. If the API change is deliberate, run the tests with "
            + $"UPDATE_OPENAPI_SNAPSHOT=1 and commit the snapshot.\nActual document:\n{actual}");
    }

    private static string Normalize(string json) => json.ReplaceLineEndings("\n").TrimEnd() + "\n";

    private static string FindBackend()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Platform.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("backend/Platform.slnx not found above " + AppContext.BaseDirectory);
    }
}
