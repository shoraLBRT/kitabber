using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Http.RateLimiting;

namespace Platform.Modules.Identity.Email;

/// <summary>The signed-in account's email.</summary>
/// <param name="Email">The account's email as typed; <c>null</c> when it has none.</param>
/// <param name="ConfirmedAt">When <paramref name="Email"/> was confirmed; <c>null</c> while it is not.</param>
/// <param name="PendingEmail">An address a confirmation code was sent to and not entered yet.</param>
public sealed record AccountEmailState(string? Email, DateTimeOffset? ConfirmedAt, string? PendingEmail);

/// <param name="Email">The new address.</param>
public sealed record ChangeEmailRequest(string? Email);

/// <param name="Code">The code from the letter.</param>
public sealed record ConfirmEmailRequest(string? Code);

/// <summary>
/// <c>/api/v1/account/email</c> (FR-ID, ADR-0020): read the email, set a new one (a code goes to it), confirm it with
/// the code. Sending a code is limited by <see cref="CodePolicy"/>, on top of the platform's limits.
/// </summary>
public sealed class AccountEmailEndpoints : IApiEndpoints
{
    public const string CodePolicy = "identity.email-codes";

    public const string EmailTakenCode = "email_taken";
    public const string ConfirmationInvalidCode = "confirmation_invalid";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var email = v1.MapGroup("/account/email").RequireAuthorization().WithTags("Account");
        email.MapGet(string.Empty, GetAsync)
            .Produces<AccountEmailState>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetAccountEmail");
        email.MapPut(string.Empty, ChangeAsync)
            .WithRateLimit(CodePolicy)
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("ChangeAccountEmail")
            .WithSummary("Sets the account's email and sends it a confirmation code");
        email.MapPost("/confirmation", ConfirmAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("ConfirmAccountEmail")
            .WithSummary("Confirms the email with the code from the letter");
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal user, IdentityDbContext db, CancellationToken ct)
    {
        if (AccountClaims.IdOf(user) is not { } id
            || await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        var pending = await db.EmailConfirmations.AsNoTracking()
            .Where(c => c.AccountId == id && c.NormalizedEmail != account.NormalizedEmail)
            .Select(c => c.Email)
            .SingleOrDefaultAsync(ct);
        return TypedResults.Ok(new AccountEmailState(account.Email, account.EmailConfirmedAt, pending));
    }

    private static async Task<IResult> ChangeAsync(
        ChangeEmailRequest request, ClaimsPrincipal user, AccountEmails emails, CancellationToken ct)
    {
        if (AccountClaims.IdOf(user) is not { } id)
        {
            return TypedResults.Unauthorized();
        }

        return await emails.ChangeAsync(id, request.Email, ct) switch
        {
            EmailChangeResult.CodeSent => TypedResults.Accepted((string?)null),
            EmailChangeResult.AlreadyConfirmed => TypedResults.NoContent(),
            EmailChangeResult.Taken => ApiProblems.Conflict(EmailTakenCode, "Another account has this email."),
            EmailChangeResult.NoAccount => TypedResults.Unauthorized(),
            _ => ApiProblems.Validation(new Dictionary<string, string[]> { ["email"] = ["Not an email address."] }),
        };
    }

    private static async Task<IResult> ConfirmAsync(
        ConfirmEmailRequest request, ClaimsPrincipal user, AccountEmails emails, CancellationToken ct)
    {
        if (AccountClaims.IdOf(user) is not { } id)
        {
            return TypedResults.Unauthorized();
        }

        return await emails.ConfirmAsync(id, request.Code, ct) switch
        {
            EmailConfirmResult.Confirmed => TypedResults.NoContent(),
            EmailConfirmResult.Taken => ApiProblems.Conflict(EmailTakenCode, "Another account has confirmed this email."),
            _ => ApiProblems.Problem(StatusCodes.Status400BadRequest, ConfirmationInvalidCode, "Invalid code",
                "The code is wrong, expired or spent; request a new one."),
        };
    }
}
