using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace Platform.Testing.Observability;

/// <summary>A finished span as the collector would receive it.</summary>
public sealed record CapturedSpan(string Source, string Name, ActivityKind Kind, ActivityTraceId TraceId, ActivitySpanId SpanId,
    ActivitySpanId ParentSpanId, ActivityStatusCode Status, string? Path);

/// <summary>A log record as the collector would receive it.</summary>
public sealed record CapturedLog(string Category, LogLevel Level, string? Message, ActivityTraceId TraceId, Exception? Exception);

/// <summary>
/// The fake of the OpenTelemetry collector (ADR-0015): spans and log records of the host, kept in memory in place of
/// the OTLP exporter. Register it with <see cref="Register"/> before the host starts.
/// </summary>
public sealed class TestTelemetry
{
    private readonly ConcurrentQueue<CapturedSpan> _spans = new();
    private readonly ConcurrentQueue<CapturedLog> _logs = new();

    public IReadOnlyList<CapturedSpan> Spans => [.. _spans];

    public IReadOnlyList<CapturedLog> Logs => [.. _logs];

    /// <summary>The spans of one trace.</summary>
    public IReadOnlyList<CapturedSpan> Trace(ActivityTraceId traceId) => [.. _spans.Where(s => s.TraceId == traceId)];

    /// <summary>
    /// The spans of one trace that match <paramref name="match"/>, once at least one has ended: a server span ends
    /// after the response reaches the client. Waits up to five seconds.
    /// </summary>
    public async Task<IReadOnlyList<CapturedSpan>> WaitForTraceAsync(ActivityTraceId traceId, Func<CapturedSpan, bool> match,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            var found = Trace(traceId).Where(match).ToList();
            if (found.Count > 0 || DateTime.UtcNow > deadline)
            {
                return found;
            }

            await Task.Delay(20, cancellationToken);
        }
    }

    public void Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddProcessor(new SpanProcessor(_spans)));
        services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddProcessor(new LogProcessor(_logs)));
    }

    /// <summary>A W3C <c>traceparent</c> header of a new sampled trace, as the web client sends it.</summary>
    public static (string Header, ActivityTraceId TraceId) NewTraceparent()
    {
        var traceId = ActivityTraceId.CreateRandom();
        return ($"00-{traceId.ToHexString()}-{ActivitySpanId.CreateRandom().ToHexString()}-01", traceId);
    }

    private sealed class SpanProcessor(ConcurrentQueue<CapturedSpan> spans) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data) => spans.Enqueue(new CapturedSpan(
            data.Source.Name, data.DisplayName, data.Kind, data.TraceId, data.SpanId, data.ParentSpanId, data.Status,
            data.GetTagItem("url.path") as string));
    }

    // Log records are pooled by the SDK, so the processor copies what the tests read.
    private sealed class LogProcessor(ConcurrentQueue<CapturedLog> logs) : BaseProcessor<LogRecord>
    {
        public override void OnEnd(LogRecord data) => logs.Enqueue(new CapturedLog(
            data.CategoryName ?? string.Empty, data.LogLevel, data.FormattedMessage, data.TraceId, data.Exception));
    }
}
