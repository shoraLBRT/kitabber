using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Platform.Http;
using Platform.Http.Problems;

namespace Platform.Modules.Identity.Email;

/// <summary>The requirement of <see cref="AccountPolicies.ConfirmedEmail"/>: the signed-in account's email is confirmed.</summary>
public sealed class ConfirmedEmailRequirement : IAuthorizationRequirement;

/// <summary>
/// Reads the confirmation from the database on every request, not from the access token: a token issued before the
/// confirmation would otherwise keep the account out for its whole lifetime.
/// </summary>
internal sealed class ConfirmedEmailHandler(AccountEmails emails) : AuthorizationHandler<ConfirmedEmailRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ConfirmedEmailRequirement requirement)
    {
        if (AccountClaims.IdOf(context.User) is not { } account)
        {
            return;
        }

        var cancellationToken = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        if (await emails.IsConfirmedAsync(account, cancellationToken))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this, "The account's email is not confirmed."));
        }
    }
}

/// <summary>
/// Answers a request refused by <see cref="ConfirmedEmailHandler"/> with 403 and the code
/// <see cref="AccountPolicies.EmailNotConfirmedCode"/>, so a client can offer to confirm the email; every other result
/// goes to the default handler.
/// </summary>
internal sealed class ConfirmedEmailResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure?.FailureReasons.Any(r => r.Handler is ConfirmedEmailHandler) == true)
        {
            return ApiProblems.Forbidden(AccountPolicies.EmailNotConfirmedCode, "Confirm the account's email first.")
                .ExecuteAsync(context);
        }

        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}

/// <summary>The account of a signed-in request: the <c>sub</c> claim of the access token.</summary>
public static class AccountClaims
{
    public static Guid? IdOf(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true
        && Guid.TryParse((user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
            ? id
            : null;
}
