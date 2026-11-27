using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Platform.Modules.Identity.External;

/// <summary>
/// Telegram Login (oauth.telegram.org, ADR-0115): the browser goes to Telegram with the bot's id and comes back to the
/// site's page <see cref="ReturnPath"/>, which hands the signed data to the API's callback. The data is proved by its
/// <c>hash</c>, HMAC-SHA-256 of the fields with the SHA-256 of the bot's token, and is fresh for
/// <see cref="MaxAge"/>. Nothing here calls Telegram: the bridge's gateway (ADR-0003) stays the only code that does.
/// Telegram gives no email, so its sign-in finds only an account it is already linked to.
/// </summary>
public sealed class TelegramLoginProvider(string botToken, TimeProvider time) : IExternalSignInProvider
{
    /// <summary>The page of the site Telegram returns to (frontend/pages/auth/telegram.vue).</summary>
    public const string ReturnPath = "/auth/telegram";

    /// <summary>How old the signed data may be.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

    public string Key => ExternalProviders.Telegram;

    public bool ReturnsState => false;

    public Uri AuthorizationUri(ExternalChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        var origin = challenge.SiteOrigin.GetLeftPart(UriPartial.Authority);
        return OAuthCodeProvider.WithQuery(new Uri("https://oauth.telegram.org/auth"), new Dictionary<string, string>
        {
            ["bot_id"] = botToken[..botToken.IndexOf(':', StringComparison.Ordinal)],
            ["origin"] = origin,
            ["request_access"] = "write",
            ["return_to"] = origin + ReturnPath,
        });
    }

    public Task<ExternalProfile?> CompleteAsync(
        IReadOnlyDictionary<string, string> callback, ExternalChallenge challenge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return Task.FromResult(Verify(callback, botToken, time.GetUtcNow()) && callback.TryGetValue("id", out var id)
            && ExternalProviders.IsValidSubject(id)
            ? new ExternalProfile(id, null, false)
            : null);
    }

    /// <summary>Whether <paramref name="data"/> is signed for the bot and fresh at <paramref name="now"/>.</summary>
    public static bool Verify(IReadOnlyDictionary<string, string> data, string botToken, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrEmpty(botToken);
        if (!data.TryGetValue("hash", out var hash) || hash.Length != 64
            || !data.TryGetValue("auth_date", out var date)
            || !long.TryParse(date, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return false;
        }

        var signedAt = DateTimeOffset.FromUnixTimeSeconds(Math.Min(seconds, 253402300799));
        if (signedAt > now + TimeSpan.FromMinutes(1) || now - signedAt > MaxAge)
        {
            return false;
        }

        byte[] given;
        try
        {
            given = Convert.FromHexString(hash);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Sign(data, botToken), given);
    }

    /// <summary>The <c>hash</c> Telegram gives <paramref name="data"/> (its other fields) for the bot.</summary>
    public static byte[] Sign(IReadOnlyDictionary<string, string> data, string botToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        var check = string.Join('\n', data
            .Where(p => !string.Equals(p.Key, "hash", StringComparison.Ordinal))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => p.Key + "=" + p.Value));
        return HMACSHA256.HashData(SHA256.HashData(Encoding.UTF8.GetBytes(botToken)), Encoding.UTF8.GetBytes(check));
    }
}
