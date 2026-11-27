using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Tokens;

namespace Platform.Modules.Identity.External;

/// <summary>
/// The fake of every external provider for tests and local runs (ADR-0115, <see cref="ExternalSignInSettings.FakeKey"/>):
/// its sign-in page (<see cref="AuthorizePath"/>, an endpoint of the API itself) asks for the subject and the email and
/// signs in whoever is named; the code it returns is that person, so the exchange needs no server. Refused outside
/// Development and Test.
/// </summary>
public sealed class FakeExternalProvider(string key) : IExternalSignInProvider
{
    /// <summary>The fake's sign-in page; <c>{provider}</c> is the provider's key.</summary>
    public const string AuthorizePath = "/api/v1/sign-in/external/fake/{provider}/authorize";

    public string Key { get; } = key;

    public bool ReturnsState => true;

    public Uri AuthorizationUri(ExternalChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        return OAuthCodeProvider.WithQuery(
            new Uri(challenge.Callback, AuthorizePath.Replace("{provider}", Key, StringComparison.Ordinal)),
            new Dictionary<string, string>
            {
                ["redirect_uri"] = challenge.Callback.AbsoluteUri,
                ["state"] = challenge.State,
            });
    }

    public Task<ExternalProfile?> CompleteAsync(
        IReadOnlyDictionary<string, string> callback, ExternalChallenge challenge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return Task.FromResult(callback.TryGetValue("code", out var code) ? Decode(code) : null);
    }

    /// <summary>The code the fake returns for <paramref name="profile"/>.</summary>
    public static string Encode(ExternalProfile profile) =>
        Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(profile));

    private static ExternalProfile? Decode(string code)
    {
        try
        {
            var profile = JsonSerializer.Deserialize<ExternalProfile>(Base64UrlEncoder.DecodeBytes(code));
            return profile is not null && ExternalProviders.IsValidSubject(profile.Subject) ? profile : null;
        }
        catch (Exception e) when (e is JsonException or FormatException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Maps the fake's sign-in page: without <c>subject</c> a form asks for it; with it the browser goes back to
    /// <c>redirect_uri</c> with the code of that person, or with <c>error=access_denied</c> when <c>deny</c> is set.
    /// Only the callback of this API is a <c>redirect_uri</c> it accepts.
    /// </summary>
    internal static void Map(IEndpointRouteBuilder v1)
    {
        v1.MapGet("/sign-in/external/fake/{provider}/authorize", (string provider, HttpRequest request) =>
        {
            var query = request.Query;
            var redirect = query["redirect_uri"].ToString();
            var state = query["state"].ToString();
            var callback = $"/api/v1/sign-in/external/{provider}/callback";
            if (!Uri.TryCreate(redirect, UriKind.Absolute, out var target)
                || !string.Equals(target.AbsolutePath, callback, StringComparison.Ordinal))
            {
                return Results.BadRequest();
            }

            if (query.ContainsKey("deny"))
            {
                return Results.Redirect(WithQuery(target, ("error", "access_denied"), ("state", state)));
            }

            var subject = query["subject"].ToString();
            if (string.IsNullOrWhiteSpace(subject))
            {
                return Results.Content(Form(provider, redirect, state), "text/html; charset=utf-8");
            }

            var email = query["email"].ToString();
            var profile = new ExternalProfile(
                subject.Trim(), email.Length > 0 ? email : null, query["verified"].ToString() is "true" or "on");
            return Results.Redirect(WithQuery(target, ("code", Encode(profile)), ("state", state)));
        }).AllowAnonymous().ExcludeFromDescription();
    }

    private static string WithQuery(Uri target, params (string Key, string Value)[] query) =>
        target.GetLeftPart(UriPartial.Path) + "?" + string.Join('&',
            query.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value)));

    private static string Form(string provider, string redirect, string state)
    {
        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"ru\"><head><meta charset=\"utf-8\"><title>Фейковый провайдер</title></head><body>");
        html.Append("<h1>Фейковый провайдер «").Append(WebUtility.HtmlEncode(provider)).Append("»</h1>");
        html.Append("<form method=\"get\">");
        html.Append("<input type=\"hidden\" name=\"redirect_uri\" value=\"").Append(WebUtility.HtmlEncode(redirect)).Append("\">");
        html.Append("<input type=\"hidden\" name=\"state\" value=\"").Append(WebUtility.HtmlEncode(state)).Append("\">");
        html.Append("<label>Субъект <input name=\"subject\" required></label>");
        html.Append("<label>Почта <input name=\"email\" type=\"email\"></label>");
        html.Append("<label><input name=\"verified\" type=\"checkbox\" checked> Почта подтверждена</label>");
        html.Append("<button type=\"submit\">Войти</button> <button type=\"submit\" name=\"deny\" value=\"1\" formnovalidate>Отказать</button>");
        html.Append("</form></body></html>");
        return html.ToString();
    }
}
