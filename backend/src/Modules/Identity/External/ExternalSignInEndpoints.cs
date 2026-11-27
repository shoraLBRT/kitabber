using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Platform.Http;
using Platform.Http.Problems;
using Platform.Http.RateLimiting;
using Platform.Modules.Identity.SignIn;

namespace Platform.Modules.Identity.External;

/// <param name="Key">The provider's key: <c>yandex</c>, <c>vk</c>, <c>google</c>, <c>telegram</c>.</param>
public sealed record ExternalProviderView(string Key);

/// <summary>
/// <c>/api/v1/sign-in/external</c> (FR-AUTH, ADR-0115): signing in, and linking to the signed-in account, with Yandex,
/// VK, Google and Telegram. The list names the providers the settings turn on. The browser goes to
/// <c>…/{provider}</c>, which remembers the attempt in a short encrypted cookie and sends it to the provider; the
/// provider sends it back to <c>…/{provider}/callback</c>, which proves the person, sets the provider's session (or
/// links) and sends the browser on to the page it came from. Every failure goes to the sign-in page (or back to that
/// page, when linking) with <c>externalError</c>; no answer tells whether an account exists.
/// </summary>
public sealed class ExternalSignInEndpoints : IApiEndpoints
{
    /// <summary>Starts and callbacks per network address.</summary>
    public const string Policy = "identity.sign-in.external";

    /// <summary>The cookie of the attempt in progress.</summary>
    public const string CookieName = "identity.external";

    public const string UnknownProviderCode = "external_provider_unknown";
    public const string InvalidReturnUrlCode = "invalid_return_url";

    /// <summary>The values of <c>externalError</c>.</summary>
    public static class Errors
    {
        public const string Expired = "expired";
        public const string Denied = "denied";
        public const string Failed = "failed";
        public const string EmailRequired = "email_required";
        public const string TakenByAnotherAccount = "taken_by_another_account";
        public const string ProviderAlreadyLinked = "provider_already_linked";
        public const string SessionRequired = "session_required";
    }

    /// <summary>How long an attempt may take at the provider.</summary>
    public static readonly TimeSpan AttemptLifetime = TimeSpan.FromMinutes(10);

    private const string CookiePath = "/api/v1/sign-in/external";

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var external = v1.MapGroup("/sign-in/external").AllowAnonymous().WithTags("SignIn");
        external.MapGet(string.Empty, (ExternalSignInProviders providers) =>
                TypedResults.Ok(providers.Keys.Select(k => new ExternalProviderView(k)).ToArray()))
            .Produces<ExternalProviderView[]>()
            .WithName("ListExternalSignInProviders")
            .WithSummary("Lists the external sign-in providers the settings turn on");
        external.MapGet("/{provider}", StartAsync)
            .WithRateLimit(Policy)
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("StartExternalSignIn")
            .WithDescription("A browser navigation, not a call: the answer sends the browser to the provider. returnUrl " +
                "is where it goes once signed in, a path or an address of the site; link=true links the provider to " +
                "the account signed in at the provider instead of signing in.")
            .WithSummary("Sends the browser to an external provider to sign in or to link it");
        external.MapGet("/{provider}/callback", CallbackAsync)
            .WithRateLimit(Policy)
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("CompleteExternalSignIn")
            .WithSummary("Takes the browser back from an external provider and signs the person in or links the provider");
        if (v1.ServiceProvider.GetRequiredService<ExternalSignInProviders>().IsFake)
        {
            FakeExternalProvider.Map(v1);
        }
    }

    private static async Task<IResult> StartAsync(
        string provider, string? returnUrl, bool? link, HttpContext context, ExternalSignInProviders providers,
        IConfiguration configuration, IDataProtectionProvider protection, TimeProvider time)
    {
        if (providers.Find(provider) is not { } signIn)
        {
            return ApiProblems.Problem(StatusCodes.Status404NotFound, UnknownProviderCode, "No such provider",
                "The provider is unknown or not turned on.");
        }

        if (ReturnTarget(returnUrl, context, configuration) is not { } target)
        {
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, InvalidReturnUrlCode, "Invalid return address",
                "returnUrl must be a path or an address of the site.");
        }

        Guid? linkTo = null;
        if (link == true)
        {
            linkTo = await SignedInAccountAsync(context);
            if (linkTo is null)
            {
                return TypedResults.Redirect(WithError(target.Return, Errors.SessionRequired));
            }
        }

        var attempt = new Attempt(
            provider,
            Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(24)),
            Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32)),
            target.Return.AbsoluteUri,
            target.Origin.AbsoluteUri,
            linkTo,
            time.GetUtcNow().Add(AttemptLifetime).ToUnixTimeSeconds());
        context.Response.Cookies.Append(CookieName, Protector(protection).Protect(JsonSerializer.Serialize(attempt)),
            CookieOptions(context, AttemptLifetime));
        return TypedResults.Redirect(signIn.AuthorizationUri(attempt.Challenge(Callback(context, configuration, provider))).AbsoluteUri);
    }

    private static async Task<IResult> CallbackAsync(
        string provider, HttpContext context, ExternalSignInProviders providers, ExternalSignIns signIns,
        IConfiguration configuration, IDataProtectionProvider protection, TimeProvider time)
    {
        var attempt = Read(context, protection);
        context.Response.Cookies.Delete(CookieName, CookieOptions(context, TimeSpan.Zero));
        var signInPage = SignInPath(configuration);
        if (attempt is null || !string.Equals(attempt.Provider, provider, StringComparison.Ordinal)
            || time.GetUtcNow().ToUnixTimeSeconds() > attempt.ExpiresAt
            || providers.Find(provider) is not { } signIn)
        {
            return TypedResults.Redirect(WithError(new Uri(signInPage, UriKind.Relative), Errors.Expired));
        }

        var returnTo = new Uri(attempt.ReturnUrl);
        var failure = attempt.LinkTo is null ? new Uri(new Uri(attempt.Origin), signInPage) : returnTo;
        var query = context.Request.Query.ToDictionary(p => p.Key, p => p.Value.ToString(), StringComparer.Ordinal);
        if (query.ContainsKey("error"))
        {
            return TypedResults.Redirect(WithError(failure, Errors.Denied));
        }

        if (signIn.ReturnsState && !(query.TryGetValue("state", out var state)
            && CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(state), System.Text.Encoding.UTF8.GetBytes(attempt.State))))
        {
            return TypedResults.Redirect(WithError(failure, Errors.Expired));
        }

        var challenge = attempt.Challenge(Callback(context, configuration, provider));
        if (await signIn.CompleteAsync(query, challenge, context.RequestAborted) is not { } profile)
        {
            return TypedResults.Redirect(WithError(failure, Errors.Failed));
        }

        var result = await signIns.ResolveAsync(provider, profile, attempt.LinkTo, context.RequestAborted);
        switch (result.Outcome)
        {
            case ExternalSignInOutcome.SignedIn:
                await ProviderSession.SignInAsync(context, result.AccountId!.Value);
                return TypedResults.Redirect(returnTo.AbsoluteUri);
            case ExternalSignInOutcome.Linked:
                return TypedResults.Redirect(returnTo.AbsoluteUri);
            default:
                return TypedResults.Redirect(WithError(failure, result.Outcome switch
                {
                    ExternalSignInOutcome.EmailRequired => Errors.EmailRequired,
                    ExternalSignInOutcome.TakenByAnotherAccount => Errors.TakenByAnotherAccount,
                    _ => Errors.ProviderAlreadyLinked,
                }));
        }
    }

    // The account signed in at the provider by its session cookie, as /connect/authorize would see it.
    private static async Task<Guid?> SignedInAccountAsync(HttpContext context)
    {
        var session = await context.AuthenticateAsync(IdentityModule.SessionScheme);
        return session.Succeeded && Guid.TryParse(session.Principal.FindFirstValue("sub"), out var accountId)
            && await ProviderSession.ResumeAsync(context, accountId, session.Principal) is not null
            ? accountId
            : null;
    }

    /// <summary>
    /// Where the browser goes once done, and the site's origin: a local path of the request's origin, or an absolute
    /// address on the origin of a redirect address of the site's client (the site and the API may live apart).
    /// </summary>
    internal static (Uri Return, Uri Origin)? ReturnTarget(string? returnUrl, HttpContext context, IConfiguration configuration)
    {
        var request = new Uri(context.Request.Scheme + "://" + context.Request.Host.Value + "/");
        if (string.IsNullOrEmpty(returnUrl))
        {
            return (new Uri(request, "/auth/sign-in?returnTo=/"), request);
        }

        if (returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            && !returnUrl.StartsWith("/\\", StringComparison.Ordinal))
        {
            return (new Uri(request, returnUrl), request);
        }

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var absolute)
            || (absolute.Scheme != Uri.UriSchemeHttps && absolute.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        var origin = new Uri(absolute.GetLeftPart(UriPartial.Authority) + "/");
        var allowed = (configuration[IdentitySettings.WebRedirectUrisKey] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) + "/" : null)
            .Append(request.AbsoluteUri);
        return allowed.Contains(origin.AbsoluteUri, StringComparer.OrdinalIgnoreCase) ? (absolute, origin) : null;
    }

    // The callback address the provider sends the browser back to: on the provider's public address when it is set
    // (Identity:Issuer), as the providers' consoles register it, otherwise on the request's.
    private static Uri Callback(HttpContext context, IConfiguration configuration, string provider)
    {
        var baseUri = configuration[IdentitySettings.IssuerKey] is { Length: > 0 } issuer
            ? new Uri(issuer.TrimEnd('/') + "/")
            : new Uri(context.Request.Scheme + "://" + context.Request.Host.Value + "/");
        return new Uri(baseUri, $"api/v1/sign-in/external/{provider}/callback");
    }

    private static string SignInPath(IConfiguration configuration) =>
        configuration[IdentitySettings.SignInPathKey] is { Length: > 0 } path ? path : IdentitySettings.DefaultSignInPath;

    internal static string WithError(Uri target, string error)
    {
        var text = target.IsAbsoluteUri ? target.AbsoluteUri : target.OriginalString;
        var fragment = text.IndexOf('#', StringComparison.Ordinal) is var hash and >= 0 ? text[hash..] : string.Empty;
        var path = fragment.Length > 0 ? text[..^fragment.Length] : text;
        return path + (path.Contains('?', StringComparison.Ordinal) ? "&" : "?") + "externalError=" + error + fragment;
    }

    private static CookieOptions CookieOptions(HttpContext context, TimeSpan lifetime) => new()
    {
        HttpOnly = true,
        // Lax: the provider sends the browser back by a top-level GET from its own site.
        SameSite = SameSiteMode.Lax,
        Secure = context.Request.IsHttps,
        Path = CookiePath,
        MaxAge = lifetime,
        IsEssential = true,
    };

    private static IDataProtector Protector(IDataProtectionProvider protection) =>
        protection.CreateProtector("Identity.ExternalSignIn.v1");

    private static Attempt? Read(HttpContext context, IDataProtectionProvider protection)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var value) || string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Attempt>(Protector(protection).Unprotect(value));
        }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }

    /// <summary>The attempt in progress, as its cookie keeps it.</summary>
    private sealed record Attempt(
        string Provider, string State, string CodeVerifier, string ReturnUrl, string Origin, Guid? LinkTo, long ExpiresAt)
    {
        public ExternalChallenge Challenge(Uri callback) => new(State, CodeVerifier, callback, new Uri(Origin));
    }
}
