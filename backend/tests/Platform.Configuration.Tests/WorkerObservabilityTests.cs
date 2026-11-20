using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Platform.Workers;

namespace Platform.Configuration.Tests;

/// <summary>E01-09 (#20) for the workers host: OpenTelemetry listens to the platform, the bus and the database.</summary>
public sealed class WorkerObservabilityTests
{
    private static readonly string[] _args =
        ["--environment", PlatformEnvironments.Production, "--Messaging:Transport", "InMemory", "--Cache:Provider", "InMemory"];

    [Theory]
    [InlineData("Platform.Workers.Sample")]
    [InlineData("Npgsql")]
    [InlineData("MassTransit")]
    public void WorkersTraceThePlatformTheDatabaseAndTheBus(string source)
    {
        using var host = WorkerHost.CreateBuilder(_args).Build();
        Assert.NotNull(host.Services.GetService<TracerProvider>());
        Assert.NotNull(host.Services.GetService<MeterProvider>());

        using var activitySource = new ActivitySource(source);
        using var activity = activitySource.StartActivity("work");

        Assert.NotNull(activity);
        Assert.True(activity.Recorded);
    }

    [Fact]
    public void WorkersWriteStructuredJsonLogsOnStands()
    {
        using var host = WorkerHost.CreateBuilder(_args).Build();

        var console = host.Services.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>().CurrentValue;
        Assert.Equal(ConsoleFormatterNames.Json, console.FormatterName);
    }

    [Fact]
    public void ACollectorAddressIsAccepted()
    {
        using var host = WorkerHost.CreateBuilder([.. _args, "--OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:4317"]).Build();

        Assert.NotNull(host.Services.GetService<TracerProvider>());
    }
}
