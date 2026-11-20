using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Platform.Http.RateLimiting;

namespace Platform.Http;

/// <summary>Wires the REST conventions of ADR-0006 into the request pipeline.</summary>
public static class ApiApplicationExtensions
{
    /// <summary>The path of a version's OpenAPI document, for example <c>/openapi/v1.json</c>.</summary>
    public const string OpenApiRoute = "/openapi/{documentName}.json";

    /// <summary>
    /// The client address from a trusted proxy first; exception and status code handling next, so every error below
    /// becomes problem details; rate limits after authentication (ADR-0016); then every module's endpoints under
    /// <c>/api/v1</c>; then the OpenAPI documents.
    /// </summary>
    public static WebApplication UsePlatformApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // X-Forwarded-For only from Http:TrustedProxies (AddPlatformApi): rate limits count the real client.
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        // Routing and auth explicitly after the error handling: left to WebApplication they run first, and an
        // empty 401 or 403 would bypass the status code pages.
        app.UseRouting();
        if (app.Services.GetService<IAuthenticationSchemeProvider>() is not null)
        {
            app.UseAuthentication();
        }

        if (app.Services.GetService<IAuthorizationHandlerProvider>() is not null)
        {
            app.UseAuthorization();
        }

        if (app.Services.GetService<RateLimitPolicyRegistry>() is not null)
        {
            app.UseMiddleware<RateLimitingMiddleware>();
        }

        var v1 = app.MapGroup(ApiVersions.Prefix(ApiVersions.V1));
        foreach (var endpoints in app.Services.GetServices<IApiEndpoints>())
        {
            endpoints.MapV1(v1);
        }

        app.MapOpenApi(OpenApiRoute);
        return app;
    }

    /// <summary>Marks an infrastructure endpoint (health, metrics) that is not part of the versioned API.</summary>
    public static TBuilder ExcludeFromApi<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.ExcludeFromDescription();
}
