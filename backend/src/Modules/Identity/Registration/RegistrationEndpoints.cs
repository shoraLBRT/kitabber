using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Http.RateLimiting;
using Platform.Modules.Identity.Email;
using Platform.Modules.Identity.Passwords;
using Platform.Modules.Identity.SignIn;

namespace Platform.Modules.Identity.Registration;

/// <param name="Email">The address to register.</param>
/// <param name="Password">At least 6 characters, at most 128.</param>
public sealed record RegisterRequest(string? Email, string? Password);

/// <param name="RegistrationId">The registration the code belongs to: needed to confirm it and to ask for a new code.</param>
/// <param name="ResendAfterSeconds">How long until a new code may be sent (the resend timer).</param>
public sealed record RegistrationCodeSent(Guid RegistrationId, int ResendAfterSeconds);

/// <param name="Code">The code from the letter.</param>
public sealed record ConfirmRegistrationRequest(string? Code);

/// <summary>
/// <c>/api/v1/registration</c> (FR-AUTH, E03-01): register with an email and a password, ask for a new code once the
/// timer allows, confirm with the code. The confirmation signs the person in at the provider (the
/// <see cref="IdentityModule.SessionScheme"/> cookie), and the site then signs in through the usual authorization code
/// flow (ADR-0018): there is no other way to a token. All three are open to anonymous callers and limited per address
/// by <see cref="Policy"/>, on top of the platform's limits.
/// </summary>
public sealed class RegistrationEndpoints : IApiEndpoints
{
    public const string Policy = "identity.registrations";

    public const string CodeTooSoonCode = "code_too_soon";
    public const string RegistrationNotFoundCode = "registration_not_found";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var registration = v1.MapGroup("/registration").AllowAnonymous().WithTags("Registration");
        registration.MapPost(string.Empty, RegisterAsync)
            .WithRateLimit(Policy)
            .Produces<RegistrationCodeSent>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("Register")
            .WithSummary("Creates an account with an email and a password and sends a confirmation code to the email");
        registration.MapPost("/{registrationId:guid}/code", ResendAsync)
            .WithRateLimit(Policy)
            .Produces<RegistrationCodeSent>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("ResendRegistrationCode")
            .WithSummary("Sends a new confirmation code once the resend timer has run out");
        registration.MapPost("/{registrationId:guid}/confirmation", ConfirmAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("ConfirmRegistration")
            .WithSummary("Confirms the email with the code and signs the person in at the provider");
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request, Registrations registrations, HttpContext context, CancellationToken ct)
    {
        var result = await registrations.RegisterAsync(request.Email, request.Password, ct);
        return result.Status switch
        {
            RegistrationStatus.CodeSent => Sent(result),
            RegistrationStatus.InvalidPassword => ApiProblems.Validation(new Dictionary<string, string[]>
            {
                ["password"] = [$"From {PasswordHasher.MinLength} to {PasswordHasher.MaxLength} characters."],
            }),
            RegistrationStatus.Taken => ApiProblems.Conflict(AccountEmailEndpoints.EmailTakenCode, "Another account has this email."),
            RegistrationStatus.TooSoon => TooSoon(context, result.RetryAfter),
            _ => ApiProblems.Validation(new Dictionary<string, string[]> { ["email"] = ["Not an email address."] }),
        };
    }

    private static async Task<IResult> ResendAsync(
        Guid registrationId, Registrations registrations, HttpContext context, CancellationToken ct)
    {
        var result = await registrations.ResendAsync(registrationId, ct);
        return result.Status switch
        {
            RegistrationStatus.CodeSent => Sent(result),
            RegistrationStatus.TooSoon => TooSoon(context, result.RetryAfter),
            _ => ApiProblems.NotFound(RegistrationNotFoundCode, "No registration waits for confirmation under this id."),
        };
    }

    private static async Task<IResult> ConfirmAsync(
        Guid registrationId, ConfirmRegistrationRequest request, Registrations registrations, HttpContext context,
        CancellationToken ct)
    {
        var (accountId, result) = await registrations.ConfirmAsync(registrationId, request.Code, ct);
        if (accountId is { } id)
        {
            // The provider's session (ADR-0018): /connect/authorize now issues codes to this account.
            await ProviderSession.SignInAsync(context, id);
            return TypedResults.NoContent();
        }

        return result == EmailConfirmResult.Taken
            ? ApiProblems.Conflict(AccountEmailEndpoints.EmailTakenCode, "Another account has confirmed this email.")
            : ApiProblems.Problem(StatusCodes.Status400BadRequest, AccountEmailEndpoints.ConfirmationInvalidCode,
                "Invalid code", "The code is wrong, expired or spent; request a new one.");
    }

    private static Accepted<RegistrationCodeSent> Sent(RegistrationResult result) =>
        TypedResults.Accepted(
            (string?)null,
            new RegistrationCodeSent(result.RegistrationId!.Value, Seconds(result.RetryAfter)));

    private static IResult TooSoon(HttpContext context, TimeSpan retryAfter)
    {
        var seconds = Seconds(retryAfter);
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        return ApiProblems.Problem(StatusCodes.Status429TooManyRequests, CodeTooSoonCode, "Too soon",
            $"A new code can be sent in {seconds} s.");
    }

    private static int Seconds(TimeSpan span) => Math.Max(1, (int)Math.Ceiling(span.TotalSeconds));
}
