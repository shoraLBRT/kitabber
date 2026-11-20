using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Platform.Http;
using Platform.Observability;

namespace Platform.Api;

/// <summary>
/// Observability of the API host (ADR-0015): OpenTelemetry with ASP.NET Core instrumentation, health probes and
/// metrics for Prometheus. The probes and the metrics are outside the versioned API and outside OpenAPI.
/// </summary>
public static class ApiObservability
{
    public const string ServiceName = "platform-api";

    /// <summary>Liveness: the process answers. Checks nothing, so a slow database never restarts the API.</summary>
    public const string LivePath = "/health/live";

    /// <summary>Readiness: every health check tagged <see cref="ReadyTag"/> passes; 503 otherwise.</summary>
    public const string ReadyPath = "/health/ready";

    /// <summary>Metrics in the Prometheus text format. Not routed by the stand's proxy: scraped inside the network.</summary>
    public const string MetricsPath = "/metrics";

    /// <summary>
    /// The tag of a readiness check. Libraries register their checks with it (the database, the cache); MassTransit
    /// tags its bus checks the same way.
    /// </summary>
    public const string ReadyTag = "ready";

    public static WebApplicationBuilder AddApiObservability(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddPlatformObservability(ServiceName)
            .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = context => !IsInfrastructure(context.Request.Path);
            }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddPrometheusExporter());
        builder.Services.AddHealthChecks();
        return builder;
    }

    public static WebApplication MapApiObservability(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = _ => false }).ExcludeFromApi();
        app.MapHealthChecks(ReadyPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) })
            .ExcludeFromApi();
        app.MapPrometheusScrapingEndpoint(MetricsPath).ExcludeFromApi();
        return app;
    }

    // Probes and scrapes run every few seconds; tracing them would bury the requests of real users.
    private static bool IsInfrastructure(PathString path) =>
        path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments(MetricsPath, StringComparison.OrdinalIgnoreCase);
}
