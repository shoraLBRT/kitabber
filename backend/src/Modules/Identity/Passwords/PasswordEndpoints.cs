using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Http.RateLimiting;
using Platform.Modules.Identity.Email;
using Platform.Modules.Identity.SignIn;

namespace Platform.Modules.Identity.Passwords;

/// <param name="Email">The account's email.</param>
public sealed record PasswordResetRequest(string? Email);

/// <param name="ResetId">The request the code belongs to: needed to enter it.</param>
/// <param name="ResendAfterSeconds">How long until a new code may be requested (the resend timer).</param>
public sealed record PasswordResetRequested(Guid ResetId, int ResendAfterSeconds);

/// <param name="Code">The code from the letter.</param>
/// <param name="Password">The new password.</param>
public sealed record PasswordResetConfirmation(string? Code, string? Password);

/// <param name="CurrentPassword">The account's password now.</param>
/// <param name="NewPassword">The password it gets.</param>
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

/// <summary>
/// The password after registration (FR-AUTH, E03-05, ADR-0036). Anonymous <c>/api/v1/password-reset</c>: a code to the
/// account's email, then the code and the new password, which end every earlier session and sign the person in at the
/// provider. <c>PUT /api/v1/account/password</c>: the signed-in owner changes it with the current one. Both send a
/// letter about the change. Requests share the limits of signing in (<see cref="SignInEndpoints"/>).
/// </summary>
public sealed class PasswordEndpoints : IApiEndpoints
{
    public const string CurrentPasswordInvalidCode = "current_password_invalid";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var reset = v1.MapGroup("/password-reset").AllowAnonymous().WithTags("SignIn");
        reset.MapPost(string.Empty, RequestResetAsync)
            .WithRateLimit(SignInEndpoints.CodePolicy)
            .Produces<PasswordResetRequested>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("RequestPasswordReset")
            .WithSummary("Sends a password reset code to the email when an account has it; answers alike either way");
        reset.MapPost("/{resetId:guid}/confirmation", ConfirmResetAsync)
            .WithRateLimit(SignInEndpoints.AttemptPolicy)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("ResetPassword")
            .WithSummary("Sets the new password with the code from the letter, ends every earlier session and signs the person in");

        v1.MapPut("/account/password", ChangeAsync)
            .RequireAuthorization()
            .WithTags("Account")
            .WithRateLimit(SignInEndpoints.AttemptPolicy)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("ChangeAccountPassword")
            .WithSummary("Changes the signed-in account's password, given the current one");
    }

    private static async Task<IResult> RequestResetAsync(
        PasswordResetRequest request, AccountPasswords passwords, RateLimitCounter limits, HttpContext context, CancellationToken ct)
    {
        if (EmailAddresses.Normalize(request.Email) is null)
        {
            return InvalidEmail();
        }

        if (await limits.CountAsync(SignInEndpoints.AddressCodePolicy, SignInEndpoints.AddressPartition(request.Email), ct) is { } wait)
        {
            return SignInEndpoints.TooManyRequests(context, wait);
        }

        var result = await passwords.RequestResetAsync(request.Email, ct);
        return result.Status switch
        {
            PasswordResetRequestStatus.Requested => TypedResults.Accepted(
                (string?)null, new PasswordResetRequested(result.ResetId!.Value, SignInEndpoints.Seconds(result.RetryAfter))),
            PasswordResetRequestStatus.TooSoon => SignInEndpoints.CodeTooSoon(context, result.RetryAfter),
            _ => InvalidEmail(),
        };
    }

    private static async Task<IResult> ConfirmResetAsync(
        Guid resetId, PasswordResetConfirmation request, AccountPasswords passwords, HttpContext context, CancellationToken ct)
    {
        var result = await passwords.ResetAsync(resetId, request.Code, request.Password, ct);
        switch (result.Status)
        {
            case PasswordChangeStatus.Changed:
                await ProviderSession.SignInAsync(context, result.AccountId!.Value);
                return TypedResults.NoContent();
            case PasswordChangeStatus.InvalidPassword:
                return InvalidPassword("password");
            default:
                return ApiProblems.Problem(StatusCodes.Status400BadRequest, AccountEmailEndpoints.ConfirmationInvalidCode,
                    "Invalid code", "The code is wrong, expired or spent; request a new one.");
        }
    }

    private static async Task<IResult> ChangeAsync(
        ChangePasswordRequest request, ClaimsPrincipal user, AccountPasswords passwords, CancellationToken ct)
    {
        if (AccountClaims.IdOf(user) is not { } id)
        {
            return TypedResults.Unauthorized();
        }

        var result = await passwords.ChangeAsync(id, request.CurrentPassword, request.NewPassword, ct);
        return result.Status switch
        {
            PasswordChangeStatus.Changed => TypedResults.NoContent(),
            PasswordChangeStatus.InvalidPassword => InvalidPassword("newPassword"),
            _ => ApiProblems.Problem(StatusCodes.Status400BadRequest, CurrentPasswordInvalidCode, "Wrong password",
                "The current password is wrong."),
        };
    }

    private static IResult InvalidEmail() =>
        ApiProblems.Validation(new Dictionary<string, string[]> { ["email"] = ["Not an email address."] });

    private static IResult InvalidPassword(string field) => ApiProblems.Validation(new Dictionary<string, string[]>
    {
        [field] = [$"From {PasswordHasher.MinLength} to {PasswordHasher.MaxLength} characters, not only spaces."],
    });
}
