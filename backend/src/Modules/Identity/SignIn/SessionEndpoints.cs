using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Modules.Identity.Email;

namespace Platform.Modules.Identity.SignIn;

/// <param name="Ended">How many devices were signed out.</param>
public sealed record EndedSessions(int Ended);

/// <summary>
/// The devices of the signed-in account (FR-AUTH, E03-09, ADR-0078): <c>GET /api/v1/account/sessions</c> lists them
/// with the time each was last active and marks the one of the request; <c>DELETE …/{id}</c> signs one out;
/// <c>POST …/sign-out-others</c> signs out every one but the current («Выйти со всех устройств»). A signed-out device
/// loses its refresh tokens and its provider's cookie at once and its access token within a minute.
/// </summary>
public sealed class SessionEndpoints : IApiEndpoints
{
    public const string SessionNotFoundCode = "session_not_found";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var sessions = v1.MapGroup("/account/sessions").RequireAuthorization().WithTags("Account");
        sessions.MapGet(string.Empty, ListAsync)
            .Produces<AccountSessionView[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("ListAccountSessions")
            .WithSummary("Lists the devices signed in to the account, the most recently active first");
        sessions.MapDelete("/{sessionId:guid}", EndAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("EndAccountSession")
            .WithSummary("Signs one device of the account out");
        sessions.MapPost("/sign-out-others", EndOthersAsync)
            .Produces<EndedSessions>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("EndOtherAccountSessions")
            .WithSummary("Signs out every device of the account but the one of the request");
    }

    private static async Task<IResult> ListAsync(ClaimsPrincipal user, AccountSessions sessions, CancellationToken ct) =>
        AccountClaims.IdOf(user) is { } id
            ? TypedResults.Ok(await sessions.ListAsync(id, ProviderSession.SessionOf(user), ct))
            : TypedResults.Unauthorized();

    private static async Task<IResult> EndAsync(
        Guid sessionId, ClaimsPrincipal user, AccountSessions sessions, CancellationToken ct)
    {
        if (AccountClaims.IdOf(user) is not { } id)
        {
            return TypedResults.Unauthorized();
        }

        // Another account's session is answered as a missing one: whose it is stays unknown.
        return await sessions.EndAsync(id, sessionId, ct)
            ? TypedResults.NoContent()
            : ApiProblems.Problem(StatusCodes.Status404NotFound, SessionNotFoundCode, "No such session",
                "The account has no such device signed in.");
    }

    private static async Task<IResult> EndOthersAsync(ClaimsPrincipal user, AccountSessions sessions, CancellationToken ct) =>
        AccountClaims.IdOf(user) is { } id
            ? TypedResults.Ok(new EndedSessions(await sessions.EndOthersAsync(id, ProviderSession.SessionOf(user), ct)))
            : TypedResults.Unauthorized();
}
