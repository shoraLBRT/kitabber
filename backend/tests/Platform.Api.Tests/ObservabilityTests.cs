using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Platform.Testing.Api;
using Platform.Testing.Observability;

namespace Platform.Api.Tests;

/// <summary>
/// Acceptance checks of E01-09 (#20) that need no Docker: the health probes, metrics in the Prometheus format, the
/// trace id of the web client carried through the API into spans, logs and problem details, structured logs.
/// The database end of the trace is in Platform.Api.IntegrationTests (ObservabilityIntegrationTests).
/// </summary>
public sealed class ObservabilityTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LiveProbeAnswersWithoutRunningAnyCheck()
    {
        await using var api = new PlatformApiFactory().WithModule(services => services.AddHealthChecks()
            .AddCheck("broken", () => HealthCheckResult.Unhealthy(), tags: [ApiObservability.ReadyTag]));
        using var client = api.CreateClient();

        using var response = await client.GetAsync(ApiObservability.LivePath, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task ReadyProbeIsHealthyWhenEveryReadinessCheckPasses()
    {
        await using var api = new PlatformApiFactory().WithModule(services => services.AddHealthChecks()
            .AddCheck("dependency", () => HealthCheckResult.Healthy(), tags: [ApiObservability.ReadyTag])
            .AddCheck("not-for-readiness", () => HealthCheckResult.Unhealthy()));
        using var client = api.CreateClient();

        using var response = await client.GetAsync(ApiObservability.ReadyPath, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadyProbeFailsWhileADependencyIsDown()
    {
        await using var api = new PlatformApiFactory().WithModule(services => services.AddHealthChecks()
            .AddCheck("database", () => HealthCheckResult.Unhealthy("down"), tags: [ApiObservability.ReadyTag]));
        using var client = api.CreateClient();

        using var response = await client.GetAsync(ApiObservability.ReadyPath, Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task MetricsAreServedInThePrometheusFormat()
    {
        await using var api = new PlatformApiFactory().WithModule(TestModule.Register);
        using var client = api.CreateClient();
        (await client.GetAsync("/api/v1/test/items", Ct)).Dispose();

        using var response = await client.GetAsync(ApiObservability.MetricsPath, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("# TYPE http_server_request_duration_seconds histogram", body, StringComparison.Ordinal);
        Assert.Contains("http_route=\"/api/v1/test/items\"", body, StringComparison.Ordinal);
        Assert.Contains("service_name=\"platform-api\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheTraceOfTheWebClientContinuesInTheApi()
    {
        var telemetry = new TestTelemetry();
        await using var api = new PlatformApiFactory().WithModule(TestModule.Register).WithModule(telemetry.Register);
        using var client = api.CreateClient();
        var (traceparent, traceId) = TestTelemetry.NewTraceparent();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test/items");
        request.Headers.Add("traceparent", traceparent);
        (await client.SendAsync(request, Ct)).Dispose();

        var server = Assert.Single(await telemetry.WaitForTraceAsync(traceId, s => s.Kind == ActivityKind.Server, Ct));
        Assert.Equal("GET /api/v1/test/items", server.Name);
        Assert.Equal("/api/v1/test/items", server.Path);
        Assert.Equal(traceparent.Split('-')[2], server.ParentSpanId.ToHexString());
    }

    [Fact]
    public async Task AnErrorIsLoggedAndReportedUnderTheTraceIdOfTheRequest()
    {
        var telemetry = new TestTelemetry();
        await using var api = new PlatformApiFactory().WithModule(TestModule.Register).WithModule(telemetry.Register);
        using var client = api.CreateClient();
        var (traceparent, traceId) = TestTelemetry.NewTraceparent();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test/crash");
        request.Headers.Add("traceparent", traceparent);
        using var response = await client.SendAsync(request, Ct);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(traceId.ToHexString(), problem.RootElement.GetProperty("traceId").GetString(), StringComparison.Ordinal);
        var error = Assert.Single(telemetry.Logs, l => l.Level == LogLevel.Error && l.Exception is InvalidOperationException);
        Assert.Equal(traceId, error.TraceId);
        var server = Assert.Single(await telemetry.WaitForTraceAsync(traceId, s => s.Kind == ActivityKind.Server, Ct));
        Assert.Equal(ActivityStatusCode.Error, server.Status);
    }

    [Fact]
    public async Task ProbesAndScrapesAreNotTraced()
    {
        var telemetry = new TestTelemetry();
        await using var api = new PlatformApiFactory().WithModule(telemetry.Register);
        using var client = api.CreateClient();

        (await client.GetAsync(ApiObservability.LivePath, Ct)).Dispose();
        (await client.GetAsync(ApiObservability.ReadyPath, Ct)).Dispose();
        (await client.GetAsync(ApiObservability.MetricsPath, Ct)).Dispose();

        // The listener is process-wide and sees other tests' hosts too, so the check looks at the paths.
        Assert.DoesNotContain(telemetry.Spans, s => s.Path is not null
            && (s.Path.StartsWith("/health", StringComparison.Ordinal) || s.Path.StartsWith("/metrics", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ConsoleLogsAreStructuredJsonWithTheTraceInTheirScope()
    {
        await using var api = new PlatformApiFactory();
        _ = api.Services;

        var console = api.Services.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>().CurrentValue;
        var json = api.Services.GetRequiredService<IOptionsMonitor<JsonConsoleFormatterOptions>>().CurrentValue;
        var factory = api.Services.GetRequiredService<IOptions<LoggerFactoryOptions>>().Value;

        Assert.Equal(ConsoleFormatterNames.Json, console.FormatterName);
        Assert.True(json.IncludeScopes);
        Assert.True(json.UseUtcTimestamp);
        Assert.True(factory.ActivityTrackingOptions.HasFlag(ActivityTrackingOptions.TraceId));
        Assert.True(factory.ActivityTrackingOptions.HasFlag(ActivityTrackingOptions.SpanId));
    }
}
