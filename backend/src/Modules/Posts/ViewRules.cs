using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Platform.Modules.Posts;

/// <summary>What a counted view is (ADR-0071).</summary>
public enum ViewKind
{
    /// <summary>The post's page was opened.</summary>
    Open = 1,

    /// <summary>The post's card was seen in a feed.</summary>
    Impression = 2,
}

/// <summary>
/// One viewer's view of one post in one period (E07-06, ADR-0071). The key — post, kind, viewer, period — is what makes
/// a view count once a day however often the page is opened. <see cref="Viewer"/> is <c>a:{account}</c> for a signed-in
/// account and <c>g:{pseudonym}</c> for a guest; no address and no browser string is stored.
/// </summary>
public sealed class PostViewMark
{
    public Guid PostId { get; init; }

    public ViewKind Kind { get; init; }

    public string Viewer { get; init; } = string.Empty;

    /// <summary>The start of the period (a UTC day) the view is counted in.</summary>
    public DateTimeOffset PeriodStart { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>The longest time the page was read in the period, in seconds; only for an opening, only with consent.</summary>
    public int? ReadSeconds { get; init; }
}

/// <summary>
/// The random secret of one period that turns a guest's network and browser string into a pseudonym (ADR-0071). The
/// secret of a past period is deleted, after which no one can tell whose pseudonym it was.
/// </summary>
public sealed class ViewSalt
{
    public DateTimeOffset PeriodStart { get; init; }

    public byte[] Salt { get; init; } = [];
}

/// <summary>
/// The rules of counting views (E07-06, FR-POST, ADR-0071): the period a viewer is counted once in, who the viewer is,
/// and which requests are bots and are never counted.
/// </summary>
public static partial class ViewRules
{
    /// <summary>The longest viewer key: <c>a:</c> and an account id, or <c>g:</c> and a pseudonym.</summary>
    public const int ViewerMaxLength = 64;

    /// <summary>How many cards one report of impressions names at most.</summary>
    public const int MaxImpressions = 50;

    /// <summary>The longest reading time one report counts, in seconds: two hours.</summary>
    public const int MaxReadSeconds = 7200;

    /// <summary>The start of the period <paramref name="now"/> is in: a viewer counts once per UTC day.</summary>
    public static DateTimeOffset PeriodOf(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);
    }

    /// <summary>The viewer key of a signed-in account.</summary>
    public static string AccountViewer(Guid account) => "a:" + account.ToString("N", CultureInfo.InvariantCulture);

    /// <summary>
    /// The viewer key of a guest: an HMAC of the network (an IPv6 address by its /64) and the browser string under the
    /// period's secret. The same guest gets the same key all day; once the secret is deleted the key leads nowhere.
    /// </summary>
    public static string GuestViewer(byte[] salt, string network, string? userAgent)
    {
        ArgumentNullException.ThrowIfNull(salt);
        var hash = HMACSHA256.HashData(salt, Encoding.UTF8.GetBytes(network + "\n" + (userAgent ?? string.Empty)));
        return "g:" + Convert.ToHexStringLower(hash.AsSpan(0, 16));
    }

    /// <summary>
    /// A request that must not count: no browser string, a known crawler, a preview or a script library, or a
    /// prefetch the person may never look at. The client also reports views from script only after the page is drawn,
    /// so a crawler that runs no script is never counted at all.
    /// </summary>
    public static bool IsBot(string? userAgent, string? purpose)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return true;
        }

        if (purpose is not null && purpose.Contains("prefetch", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return BotAgent().IsMatch(userAgent);
    }

    [GeneratedRegex(
        @"bot\b|bot/|crawl|spider|slurp|scrap|preview|headless|phantomjs|puppeteer|playwright|selenium|lighthouse"
        + @"|facebookexternalhit|embedly|vkshare|mediapartners|feedfetcher"
        + @"|\bcurl/|wget|python-|httpclient|okhttp|go-http-client|java/|axios|node-fetch|libwww|postman|insomnia",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BotAgent();
}
