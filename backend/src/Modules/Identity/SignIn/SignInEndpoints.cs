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
using Platform.Modules.Identity.Captcha;
using Platform.Modules.Identity.Email;
using Platform.Modules.Identity.Registration;

namespace Platform.Modules.Identity.SignIn;

/// <param name="Email">The account's email.</param>
/// <param name="Password">The account's password.</param>
/// <param name="Captcha">The solution of a captcha challenge (<c>GET /api/v1/sign-in/captcha</c>); needed only after the
/// answer <c>captcha_required</c>.</param>
public sealed record PasswordSignInRequest(string? Email, string? Password, string? Captcha = null);

/// <param name="Email">The address to send the code to.</param>
public sealed record SignInCodeRequest(string? Email);

/// <param name="ChallengeId">The request the code belongs to: needed to enter it.</param>
/// <param name="ResendAfterSeconds">How long until a new code may be sent (the resend timer).</param>
public sealed record SignInCodeSent(Guid ChallengeId, int ResendAfterSeconds);

/// <param name="Code">The code from the letter.</param>
/// <param name="Captcha">The solution of a captcha challenge; needed only after the answer <c>captcha_required</c>.</param>
public sealed record SignInCodeConfirmation(string? Code, string? Captcha = null);

/// <summary>
/// <c>/api/v1/sign-in</c> (FR-AUTH, E03-02, ADR-0022): sign in with the email and the password, or with a one-time code
/// sent to the email. Success sets the provider's session (<see cref="IdentityModule.SessionScheme"/>) and the site then
/// signs in through the usual authorization code flow (ADR-0018). No answer tells whether an address is registered.
/// Open to anonymous callers and limited per network address and per email address, on top of the platform's limits.
/// After a series of failures from one network address, or to one email address, each further attempt needs a solved
/// captcha (E03-12, ADR-0038): the answer is 400 <c>captcha_required</c> until it comes, alike for any address.
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

    /// <summary>Failed password sign-ins and code entries per network address before a captcha is required.</summary>
    public const string FailurePolicy = "identity.sign-in.failures";

    /// <summary>Failed password sign-ins per email address before a captcha is required.</summary>
    public const string AddressFailurePolicy = "identity.sign-in.failures.address";

    public const string InvalidCredentialsCode = "invalid_credentials";

    public const string CaptchaRequiredCode = "captcha_required";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var signIn = v1.MapGroup("/sign-in").AllowAnonymous().WithTags("SignIn");
        signIn.MapPost("/password", PasswordAsync)
            .WithRateLimit(AttemptPolicy)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("SignInWithPassword")
            .WithDescription("After a series of failed attempts the answer is 400 captcha_required until the request " +
                "carries a solved captcha from GET /api/v1/sign-in/captcha.")
            .WithSummary("Signs the person in at the provider with the email and the password");
        signIn.MapPost("/code", RequestCodeAsync)
            .WithRateLimit(CodePolicy)
            .Produces<SignInCodeSent>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("RequestSignInCode")
            .WithSummary("Sends a one-time sign-in code to the email, registered or not");
        signIn.MapGet("/captcha", (ICaptcha captcha) => TypedResults.Ok(captcha.CreateChallenge()))
            .Produces<CaptchaChallenge>()
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("GetSignInCaptcha")
            .WithSummary("Issues a captcha challenge for a sign-in that answered captcha_required");
        signIn.MapPost("/code/{challengeId:guid}/confirmation", ConfirmCodeAsync)
            .WithRateLimit(AttemptPolicy)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("SignInWithCode")
            .WithDescription("After a series of failed attempts from the network address the answer is 400 " +
                "captcha_required until the request carries a solved captcha.")
            .WithSummary("Signs the person in at the provider with the code from the letter, creating the account if need be");
    }

    private static async Task<IResult> PasswordAsync(
        PasswordSignInRequest request, SignIns signIns, RateLimitCounter limits, ICaptcha captcha, HttpContext context,
        CancellationToken ct)
    {
        var address = AddressPartition(request.Email);
        if (await limits.CountAsync(AddressAttemptPolicy, address, ct) is { } wait)
        {
            return TooManyRequests(context, wait);
        }

        var network = NetworkPartition(context);
        if ((await limits.IsSpentAsync(FailurePolicy, network, ct) || await limits.IsSpentAsync(AddressFailurePolicy, address, ct))
            && !await captcha.VerifyAsync(request.Captcha, ct))
        {
            return CaptchaRequired();
        }

        if (await signIns.SignInWithPasswordAsync(request.Email, request.Password, ct) is not { } accountId)
        {
            await limits.CountAsync(FailurePolicy, network, ct);
            await limits.CountAsync(AddressFailurePolicy, address, ct);
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
        Guid challengeId, SignInCodeConfirmation request, SignIns signIns, RateLimitCounter limits, ICaptcha captcha,
        HttpContext context, CancellationToken ct)
    {
        var network = NetworkPartition(context);
        if (await limits.IsSpentAsync(FailurePolicy, network, ct) && !await captcha.VerifyAsync(request.Captcha, ct))
        {
            return CaptchaRequired();
        }

        if (await signIns.SignInWithCodeAsync(challengeId, request.Code, ct) is not { } accountId)
        {
            await limits.CountAsync(FailurePolicy, network, ct);
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, AccountEmailEndpoints.ConfirmationInvalidCode,
                "Invalid code", "The code is wrong, expired or spent; request a new one.");
        }

        await ProviderSession.SignInAsync(context, accountId);
        return TypedResults.NoContent();
    }

    // The email address as a counter partition: hashed, so the counters do not keep addresses, and normalised when it
    // is one, so case and Gmail dots do not give more tries.
    internal static string AddressPartition(string? email)
    {
        var key = EmailAddresses.Normalize(email) ?? email?.Trim().ToUpperInvariant() ?? string.Empty;
        return "e:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32];
    }

    // The network address as the platform's limits count it (ADR-0016): IPv4 as is, IPv6 by its /64.
    private static string NetworkPartition(HttpContext context) =>
        "ip:" + ClientAddress.Network(context.Connection.RemoteIpAddress);

    private static IResult CaptchaRequired() =>
        ApiProblems.Problem(StatusCodes.Status400BadRequest, CaptchaRequiredCode, "Captcha required",
            "Too many failed attempts: solve the captcha from GET /api/v1/sign-in/captcha and send its solution.");

    internal static IResult TooManyRequests(HttpContext context, TimeSpan retryAfter)
    {
        var seconds = Seconds(retryAfter);
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        return ApiProblems.Problem(StatusCodes.Status429TooManyRequests, ApiErrorCodes.TooManyRequests,
            "Too many requests", $"Rate limit exceeded; retry in {seconds} s.");
    }

    internal static IResult CodeTooSoon(HttpContext context, TimeSpan retryAfter)
    {
        var seconds = Seconds(retryAfter);
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        return ApiProblems.Problem(StatusCodes.Status429TooManyRequests, RegistrationEndpoints.CodeTooSoonCode,
            "Too soon", $"A new code can be sent in {seconds} s.");
    }

    internal static int Seconds(TimeSpan span) => Math.Max(1, (int)Math.Ceiling(span.TotalSeconds));
}
