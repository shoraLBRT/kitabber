using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Platform.Modules.Identity.External;

/// <summary>
/// A provider of the OAuth 2.0 authorization code flow with PKCE S256 (ADR-0115): the browser goes to
/// <see cref="AuthorizeEndpoint"/>, comes back with a code, the API exchanges it at <see cref="TokenEndpoint"/> with
/// the verifier and reads the person with the access token. Any failure of the provider's servers proves nobody.
/// </summary>
public abstract class OAuthCodeProvider(IHttpClientFactory http, string clientId, string? clientSecret) : IExternalSignInProvider
{
    public abstract string Key { get; }

    public bool ReturnsState => true;

    protected string ClientId { get; } = clientId;

    protected abstract Uri AuthorizeEndpoint { get; }

    protected abstract Uri TokenEndpoint { get; }

    protected abstract string Scope { get; }

    public Uri AuthorizationUri(ExternalChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        return WithQuery(AuthorizeEndpoint, new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = ClientId,
            ["redirect_uri"] = challenge.Callback.AbsoluteUri,
            ["scope"] = Scope,
            ["state"] = challenge.State,
            ["code_challenge"] = CodeChallenge(challenge.CodeVerifier),
            ["code_challenge_method"] = "S256",
        });
    }

    public async Task<ExternalProfile?> CompleteAsync(
        IReadOnlyDictionary<string, string> callback, ExternalChallenge challenge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(challenge);
        if (!callback.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        try
        {
            using var client = http.CreateClient(ExternalSignInSettings.HttpClientName);
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = challenge.Callback.AbsoluteUri,
                ["client_id"] = ClientId,
                ["code_verifier"] = challenge.CodeVerifier,
            };
            if (clientSecret is not null)
            {
                form["client_secret"] = clientSecret;
            }

            AddToTokenForm(form, callback, challenge);
            using var content = new FormUrlEncodedContent(form);
            using var response = await client.PostAsync(TokenEndpoint, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var token = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (Text(token.RootElement, "access_token") is not { } accessToken)
            {
                return null;
            }

            var profile = await ProfileAsync(client, accessToken, cancellationToken);
            return profile is not null && ExternalProviders.IsValidSubject(profile.Subject) ? profile : null;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException
            || (e is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return null;
        }
    }

    /// <summary>Parameters of the code exchange beyond the standard ones.</summary>
    protected virtual void AddToTokenForm(
        Dictionary<string, string> form, IReadOnlyDictionary<string, string> callback, ExternalChallenge challenge)
    {
    }

    /// <summary>The person the access token belongs to; null when the provider does not say.</summary>
    protected abstract Task<ExternalProfile?> ProfileAsync(HttpClient client, string accessToken, CancellationToken cancellationToken);

    /// <summary>A string or a number of <paramref name="element"/> as text; null when it is neither.</summary>
    protected static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() is { Length: > 0 } s ? s : null,
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            }
            : null;

    public static string CodeChallenge(string verifier) =>
        Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    internal static Uri WithQuery(Uri endpoint, IReadOnlyDictionary<string, string> query) =>
        new(endpoint.AbsoluteUri + (endpoint.Query.Length > 0 ? "&" : "?") + string.Join('&', query.Select(p =>
            string.Create(CultureInfo.InvariantCulture, $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"))));
}

/// <summary>
/// Yandex ID (oauth.yandex.ru): the subject is the user's <c>id</c>, the email their <c>default_email</c>, an address
/// of their Yandex account and so the account's own.
/// </summary>
public sealed class YandexProvider(IHttpClientFactory http, string clientId, string clientSecret)
    : OAuthCodeProvider(http, clientId, clientSecret)
{
    public override string Key => ExternalProviders.Yandex;

    protected override Uri AuthorizeEndpoint { get; } = new("https://oauth.yandex.ru/authorize");

    protected override Uri TokenEndpoint { get; } = new("https://oauth.yandex.ru/token");

    protected override string Scope => "login:email";

    protected override async Task<ExternalProfile?> ProfileAsync(
        HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://login.yandex.ru/info?format=json");
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var info = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var email = Text(info.RootElement, "default_email");
        return Text(info.RootElement, "id") is { } id ? new ExternalProfile(id, email, email is not null) : null;
    }
}

/// <summary>
/// VK ID (id.vk.com), a public client with PKCE and no secret: the code exchange also needs the <c>device_id</c> the
/// browser came back with. The subject is the <c>user_id</c>; VK ID gives only an email its owner confirmed.
/// </summary>
public sealed class VkProvider(IHttpClientFactory http, string clientId) : OAuthCodeProvider(http, clientId, null)
{
    public override string Key => ExternalProviders.Vk;

    protected override Uri AuthorizeEndpoint { get; } = new("https://id.vk.com/authorize");

    protected override Uri TokenEndpoint { get; } = new("https://id.vk.com/oauth2/auth");

    protected override string Scope => "email";

    protected override void AddToTokenForm(
        Dictionary<string, string> form, IReadOnlyDictionary<string, string> callback, ExternalChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(challenge);
        form["device_id"] = callback.TryGetValue("device_id", out var device) ? device : string.Empty;
        form["state"] = challenge.State;
    }

    protected override async Task<ExternalProfile?> ProfileAsync(
        HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["access_token"] = accessToken,
        });
        using var response = await client.PostAsync(new Uri("https://id.vk.com/oauth2/user_info"), content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var info = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!info.RootElement.TryGetProperty("user", out var user))
        {
            return null;
        }

        var email = Text(user, "email");
        return Text(user, "user_id") is { } id ? new ExternalProfile(id, email, email is not null) : null;
    }
}

/// <summary>
/// Google (accounts.google.com, OpenID Connect): the subject is <c>sub</c> of the user info, the email counts only
/// with <c>email_verified</c>.
/// </summary>
public sealed class GoogleProvider(IHttpClientFactory http, string clientId, string clientSecret)
    : OAuthCodeProvider(http, clientId, clientSecret)
{
    public override string Key => ExternalProviders.Google;

    protected override Uri AuthorizeEndpoint { get; } = new("https://accounts.google.com/o/oauth2/v2/auth");

    protected override Uri TokenEndpoint { get; } = new("https://oauth2.googleapis.com/token");

    protected override string Scope => "openid email";

    protected override async Task<ExternalProfile?> ProfileAsync(
        HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var info = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var verified = info.RootElement.TryGetProperty("email_verified", out var flag)
            && (flag.ValueKind == JsonValueKind.True
                || (flag.ValueKind == JsonValueKind.String && string.Equals(flag.GetString(), "true", StringComparison.Ordinal)));
        return Text(info.RootElement, "sub") is { } sub
            ? new ExternalProfile(sub, Text(info.RootElement, "email"), verified)
            : null;
    }
}
