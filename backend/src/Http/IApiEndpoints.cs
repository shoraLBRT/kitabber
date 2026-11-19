using Microsoft.AspNetCore.Routing;

namespace Platform.Http;

/// <summary>
/// The endpoints of one module. A module registers its implementation with
/// <see cref="ApiServiceCollectionExtensions.AddApiEndpoints{T}"/>; the host maps it under the version prefix,
/// so a module never writes <c>/api/v1</c> itself.
/// </summary>
public interface IApiEndpoints
{
    /// <summary>Maps the module's version 1 endpoints. <paramref name="v1"/> is already prefixed with <c>/api/v1</c>.</summary>
    void MapV1(IEndpointRouteBuilder v1);
}
