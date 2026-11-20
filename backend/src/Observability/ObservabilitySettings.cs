using Platform.Configuration;

namespace Platform.Observability;

/// <summary>The settings <see cref="ObservabilityExtensions.AddPlatformObservability"/> reads (docs/CONFIG.md).</summary>
public static class ObservabilitySettings
{
    /// <summary>The standard OpenTelemetry variable: the collector that receives traces, metrics and logs.</summary>
    public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>The standard OpenTelemetry variable: headers sent to the collector, such as an API key.</summary>
    public const string OtlpHeadersKey = "OTEL_EXPORTER_OTLP_HEADERS";

    /// <summary>The console log format of Microsoft.Extensions.Logging.</summary>
    public const string LogFormatKey = "Logging:Console:FormatterName";

    public static IReadOnlyList<Setting> All { get; } =
    [
        new(OtlpEndpointKey,
            "OTLP collector (gRPC) for traces, metrics and logs, for example http://otel-collector:4317; without it nothing is sent"),
        new(OtlpHeadersKey, "headers for the collector as key=value,key2=value2, for example the key of a hosted backend")
        {
            Secret = true,
        },
        new(LogFormatKey, "console log format: json (structured, one object per line) or simple (for a developer)")
        {
            Default = "json",
            AllowedValues = ["json", "simple", "systemd"],
        },
    ];
}
