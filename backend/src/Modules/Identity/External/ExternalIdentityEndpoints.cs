using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Modules.Identity.Email;

namespace Platform.Modules.Identity.External;

/// <summary>
/// The external identities of the signed-in account (FR-ID-4, ADR-0098): <c>GET /api/v1/account/identities</c> lists
/// them by provider and time linked (the subject is not shown); <c>DELETE …/{id}</c> unlinks one, unless it is the
/// account's last way to sign in. Linking is the provider's sign-in (#91), not an endpoint of its own.
/// </summary>
public sealed class ExternalIdentityEndpoints : IApiEndpoints
{
    public const string NotFoundCode = "external_identity_not_found";
    public const string LastSignInMethodCode = "last_sign_in_method";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var identities = v1.MapGroup("/account/identities").RequireAuthorization().WithTags("Account");
        identities.MapGet(string.Empty, ListAsync)
            .Produces<ExternalIdentityView[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("ListExternalIdentities")
            .WithSummary("Lists the external sign-in providers linked to the account, in the order they were linked");
        identities.MapDelete("/{identityId:guid}", UnlinkAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("UnlinkExternalIdentity")
            .WithSummary("Unlinks an external sign-in provider from the account, unless it is the last way to sign in");
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal user, ExternalIdentities identities, CancellationToken ct) =>
        AccountClaims.IdOf(user) is { } id
            ? TypedResults.Ok(await identities.ListAsync(id, ct))
            : TypedResults.Unauthorized();

    private static async Task<IResult> UnlinkAsync(
        Guid identityId, ClaimsPrincipal user, ExternalIdentities identities, CancellationToken ct)
    {
        if (AccountClaims.IdOf(user) is not { } id)
        {
            return TypedResults.Unauthorized();
        }

        return await identities.UnlinkAsync(id, identityId, ct) switch
        {
            UnlinkOutcome.Unlinked => TypedResults.NoContent(),
            UnlinkOutcome.LastSignInMethod => ApiProblems.Problem(StatusCodes.Status409Conflict, LastSignInMethodCode,
                "The last way to sign in",
                "Without this provider the account could not sign in: confirm an email or link another provider first."),
            // Another account's identity is answered as a missing one: whose it is stays unknown.
            _ => ApiProblems.Problem(StatusCodes.Status404NotFound, NotFoundCode, "No such external identity",
                "The account has no such provider linked."),
        };
    }
}
