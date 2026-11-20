using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Http.RateLimiting;
using Platform.Modules.Identity.Email;
using Platform.Modules.Identity.Registration;

namespace Platform.Modules.Identity.SignIn;

/// <param name="Email">The account's email.</param>
/// <param name="Password">The account's password.</param>
public sealed record PasswordSignInRequest(string? Email, string? Password);

/// <param name="Email">The address to send the code to.</param>
public sealed record SignInCodeRequest(string? Email);

/// <param name="ChallengeId">The request the code belongs to: needed to enter it.</param>
/// <param name="ResendAfterSeconds">How long until a new code may be sent (the resend timer).</param>
public sealed record SignInCodeSent(Guid ChallengeId, int ResendAfterSeconds);

/// <param name="Code">The code from the letter.</param>
public sealed record SignInCodeConfirmation(string? Code);

/// <summary>
/// <c>/api/v1/sign-in</c> (FR-AUTH, E03-02, ADR-0022): sign in with the email and the password, or with a one-time code
/// sent to the email. Success sets the provider's session (<see cref="IdentityModule.SessionScheme"/>) and the site then
/// signs in through the usual authorization code flow (ADR-0018). No answer tells whether an address is registered.
/// Open to anonymous callers and limited per network address and per email address, on top of the platform's limits.
/// </summary>
public sealed class SignInEndpoints : IApiEndpoints
{
    /// <summary>Password attempts and code entries per network address.</summary>
    public const string AttemptPolicy = "identity.sign-in";

    /// <summary>Password attempts per email address, counted by the endpoint itself.</summary>
    public const string AddressAttemptPolicy = "identity.sign-in.address";

    /// <summary>Code requests per network address.</summary>
    public const string CodePolicy = "identity.sign-in-codes";

    /// <summary>Code requests (letters) per email address, counted by the endpoint itself.</summary>
    public const string AddressCodePolicy = "identity.sign-in-codes.address";

    public const string InvalidCredentialsCode = "invalid_credentials";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var signIn = v1.MapGroup("/sign-in").AllowAnonymous().WithTags("SignIn");
        signIn.MapPost("/password", PasswordAsync)
            .WithRateLimit(AttemptPolicy)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("SignInWithPassword")
            .WithSummary("Signs the person in at the provider with the email and the password");
        signIn.MapPost("/code", RequestCodeAsync)
            .WithRateLimit(CodePolicy)
            .Produces<SignInCodeSent>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("RequestSignInCode")
            .WithSummary("Sends a one-time sign-in code to the email, registered or not");
        signIn.MapPost("/code/{challengeId:guid}/confirmation", ConfirmCodeAsync)
            .WithRateLimit(AttemptPolicy)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("SignInWithCode")
            .WithSummary("Signs the person in at the provider with the code from the letter, creating the account if need be");
    }

    private static async Task<IResult> PasswordAsync(
        PasswordSignInRequest request, SignIns signIns, RateLimitCounter limits, HttpContext context, CancellationToken ct)
    {
        if (await limits.CountAsync(AddressAttemptPolicy, AddressPartition(request.Email), ct) is { } wait)
        {
            return TooManyRequests(context, wait);
        }

        if (await signIns.SignInWithPasswordAsync(request.Email, request.Password, ct) is not { } accountId)
        {
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, InvalidCredentialsCode, "Invalid credentials",
                "The email or the password is wrong.");
        }

        await ProviderSession.SignInAsync(context, accountId);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> RequestCodeAsync(
        SignInCodeRequest request, SignIns signIns, RateLimitCounter limits, HttpContext context, CancellationToken ct)
    {
        if (EmailAddresses.Normalize(request.Email) is null)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]> { ["email"] = ["Not an email address."] });
        }

        if (await limits.CountAsync(AddressCodePolicy, AddressPartition(request.Email), ct) is { } wait)
        {
            return TooManyRequests(context, wait);
        }

        var result = await signIns.RequestCodeAsync(request.Email, ct);
        return result.Status switch
        {
            SignInCodeStatus.CodeSent => TypedResults.Accepted(
                (string?)null, new SignInCodeSent(result.ChallengeId!.Value, Seconds(result.RetryAfter))),
            SignInCodeStatus.TooSoon => CodeTooSoon(context, result.RetryAfter),
            _ => ApiProblems.Validation(new Dictionary<string, string[]> { ["email"] = ["Not an email address."] }),
        };
    }

    private static async Task<IResult> ConfirmCodeAsync(
        Guid challengeId, SignInCodeConfirmation request, SignIns signIns, HttpContext context, CancellationToken ct)
    {
        if (await signIns.SignInWithCodeAsync(challengeId, request.Code, ct) is not { } accountId)
        {
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, AccountEmailEndpoints.ConfirmationInvalidCode,
                "Invalid code", "The code is wrong, expired or spent; request a new one.");
        }

        await ProviderSession.SignInAsync(context, accountId);
        return TypedResults.NoContent();
    }

    // The email address as a counter partition: hashed, so the counters do not keep addresses, and normalised when it
    // is one, so case and Gmail dots do not give more tries.
    private static string AddressPartition(string? email)
    {
        var key = EmailAddresses.Normalize(email) ?? email?.Trim().ToUpperInvariant() ?? string.Empty;
        return "e:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32];
    }

    private static IResult TooManyRequests(HttpContext context, TimeSpan retryAfter)
    {
        var seconds = Seconds(retryAfter);
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        return ApiProblems.Problem(StatusCodes.Status429TooManyRequests, ApiErrorCodes.TooManyRequests,
            "Too many requests", $"Rate limit exceeded; retry in {seconds} s.");
    }

    private static IResult CodeTooSoon(HttpContext context, TimeSpan retryAfter)
    {
        var seconds = Seconds(retryAfter);
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        return ApiProblems.Problem(StatusCodes.Status429TooManyRequests, RegistrationEndpoints.CodeTooSoonCode,
            "Too soon", $"A new code can be sent in {seconds} s.");
    }

    private static int Seconds(TimeSpan span) => Math.Max(1, (int)Math.Ceiling(span.TotalSeconds));
}
