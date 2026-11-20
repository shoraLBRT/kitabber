using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.Api.IntegrationTests.SampleModule;
using Platform.Testing.Api;
using Platform.Testing.Containers;
using Platform.Testing.Observability;

namespace Platform.Api.IntegrationTests;

/// <summary>The sample module on PostgreSQL and Redis, with the fake collector in place of OTLP.</summary>
public sealed class TracedNotesApi : IntegrationApiFactory
{
    public TracedNotesApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis) => WithModule(NotesModule.Register).WithModule(Telemetry.Register);

    public TestTelemetry Telemetry { get; } = new();
}

/// <summary>
/// Acceptance checks of E01-09 (#20) on real dependencies: a request of the web client is traced down to the database
/// under its trace id, and readiness checks the database and the cache.
/// </summary>
public sealed class ObservabilityIntegrationTests(TracedNotesApi api) : IClassFixture<TracedNotesApi>, IAsyncLifetime
{
    private HttpClient _client = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await api.ResetAsync();
        _client = api.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ARequestOfTheWebClientIsTracedDownToTheDatabase()
    {
        var (traceparent, traceId) = TestTelemetry.NewTraceparent();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sample/notes");
        request.Headers.Add("traceparent", traceparent);
        using var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // The server span ends after the response reaches the client: wait for it, then read the whole trace.
        var server = Assert.Single(await api.Telemetry.WaitForTraceAsync(traceId, s => s.Kind == ActivityKind.Server, Ct));
        var trace = api.Telemetry.Trace(traceId);
        var database = Assert.Single(trace, s => s.Source == "Npgsql");
        Assert.Equal(ActivityKind.Client, database.Kind);
        Assert.Equal(server.SpanId, database.ParentSpanId);
    }

    [Fact]
    public async Task ReadinessChecksTheDatabaseAndTheCache()
    {
        using var response = await _client.GetAsync("/health/ready", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await api.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(c => c.Tags.Contains(ApiObservability.ReadyTag), Ct);
        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Contains(report.Entries.Keys, k => k.StartsWith("database:", StringComparison.Ordinal));
        Assert.Contains("cache", report.Entries.Keys);
    }
}
