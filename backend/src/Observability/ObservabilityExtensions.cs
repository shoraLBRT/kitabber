using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Platform.Observability;

/// <summary>OpenTelemetry for a platform host (ADR-0015).</summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Sources and meters the platform listens to: its own code (<c>Platform.*</c>), PostgreSQL (Npgsql) and the bus
    /// (MassTransit). A module names its <see cref="ActivitySource"/> and meter <c>Platform.&lt;Module&gt;</c>.
    /// </summary>
    public static IReadOnlyList<string> Sources { get; } = ["Platform.*", "Npgsql", "MassTransit"];

    /// <summary>
    /// Traces, metrics and logs of the host under <paramref name="serviceName"/>: the platform's sources, outgoing
    /// HTTP, the .NET runtime, and the trace and span id in every log scope. Everything goes over OTLP to the collector
    /// in <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>; without it nothing leaves the process. The host adds its own
    /// instrumentation and exporters to the returned builder.
    /// </summary>
    public static OpenTelemetryBuilder AddPlatformObservability(this IHostApplicationBuilder builder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        // Structured console logs carry the W3C trace and span id of the current activity in their scope.
        builder.Logging.Configure(options => options.ActivityTrackingOptions =
            ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId | ActivityTrackingOptions.ParentId);
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
        });

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString())
                .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]))
            .WithTracing(tracing => tracing
                .AddSource([.. Sources])
                .AddHttpClientInstrumentation(options => options.RecordException = true))
            .WithMetrics(metrics => metrics
                .AddMeter([.. Sources])
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration[ObservabilitySettings.OtlpEndpointKey]))
        {
            // Reads the endpoint, headers and protocol from the standard OTEL_EXPORTER_OTLP_* settings.
            telemetry.UseOtlpExporter();
        }

        return telemetry;
    }
}
