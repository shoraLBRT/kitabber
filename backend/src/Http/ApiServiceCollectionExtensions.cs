using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Http.Problems;
using Platform.Http.RateLimiting;

namespace Platform.Http;

/// <summary>Registers the REST conventions of ADR-0006 in the Api host.</summary>
public static class ApiServiceCollectionExtensions
{
    /// <summary>The title of the OpenAPI document. The product name is temporary, so it is not used here.</summary>
    public const string DocumentTitle = "Platform API";

    /// <summary>
    /// Problem details with a code for every error, request validation from data annotations, one OpenAPI
    /// document per version, and the system <see cref="TimeProvider"/>.
    /// </summary>
    public static IServiceCollection AddPlatformApi(this IServiceCollection services)
    {
        // Code takes the time from TimeProvider only, so tests can replace the clock (ADR-0008).
        services.TryAddSingleton(TimeProvider.System);
        services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsConventions.Apply);
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddValidation();

        // The client address behind the stand's proxy (ADR-0016): X-Forwarded-For is believed only from loopback
        // and the networks of Http:TrustedProxies; anyone else could forge it and dodge the rate limits.
        services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            var networks = configuration[RateLimitingSettings.TrustedProxiesKey];
            foreach (var network in (networks ?? string.Empty).Split(
                         ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.TryParse(network, out var parsed)
                    ? parsed
                    : throw new InvalidOperationException(
                        $"{RateLimitingSettings.TrustedProxiesKey}: '{network}' is not a network in CIDR form (10.0.0.0/8)."));
            }
        });

        // Numbers are numbers on the wire: no quoted numbers, so the OpenAPI schema says "integer", not
        // "integer or string", and the generated client gets plain number types.
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);

        services.AddOpenApi(ApiVersions.V1, options =>
        {
            var prefix = ApiVersions.Prefix(ApiVersions.V1).TrimStart('/') + "/";
            options.ShouldInclude = description =>
                description.RelativePath?.StartsWith(prefix, StringComparison.Ordinal) == true;
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = DocumentTitle;
                document.Info.Version = ApiVersions.V1;
                // The servers list comes from the request host; it would make the document differ per machine.
                document.Servers?.Clear();
                return Task.CompletedTask;
            });
        });

        return services;
    }

    /// <summary>Adds a module's endpoints; the host maps them under the version prefix.</summary>
    public static IServiceCollection AddApiEndpoints<T>(this IServiceCollection services)
        where T : class, IApiEndpoints
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IApiEndpoints, T>());
        return services;
    }
}
