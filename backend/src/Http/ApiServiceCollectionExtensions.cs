using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Http.Problems;

namespace Platform.Http;

/// <summary>Registers the REST conventions of ADR-0006 in the Api host.</summary>
public static class ApiServiceCollectionExtensions
{
    /// <summary>The title of the OpenAPI document. The product name is temporary, so it is not used here.</summary>
    public const string DocumentTitle = "Platform API";

    /// <summary>
    /// Problem details with a code for every error, request validation from data annotations, and one OpenAPI
    /// document per version.
    /// </summary>
    public static IServiceCollection AddPlatformApi(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsConventions.Apply);
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddValidation();

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
