using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Platform.Configuration;

namespace Platform.Modules.Blogs.Topics;

/// <summary>The limits of creating topics (E11-11, ADR-0084), read once at start from <see cref="BlogsSettings"/>.</summary>
/// <param name="MaxPerAccount">The most topics one account may own.</param>
/// <param name="StopWords">
/// The stop list of names and addresses, in the form <see cref="TopicCreationRules.Normalize"/> gives: a word of a topic's
/// name or address that starts with one of them is refused.
/// </param>
public sealed record TopicLimits(int MaxPerAccount, IReadOnlyList<string> StopWords)
{
    /// <summary>The default number of topics per account (ADR-0084: a proposal awaiting the owner's confirmation).</summary>
    public const int DefaultMaxPerAccount = 5;

    /// <summary>
    /// The default stop list (ADR-0084): words that would make a topic look like the platform's own or its administration's.
    /// The real list is the content policy's (docs/content-policy.md) and is set with <see cref="BlogsSettings.StopWordsKey"/>.
    /// </summary>
    public const string DefaultStopWords =
        "admin,moderator,official,support,администр,модератор,официальн,поддержк";

    public static TopicLimits Default { get; } = new(DefaultMaxPerAccount, TopicCreationRules.ParseStopWords(DefaultStopWords));
}

/// <summary>The settings of the Blogs module in the API (ADR-0007, ADR-0084); each is described in docs/CONFIG.md.</summary>
public static class BlogsSettings
{
    public const string MaxTopicsPerAccountKey = "Blogs:Topics:MaxPerAccount";

    public const string StopWordsKey = "Blogs:Topics:StopWords";

    /// <summary>The most topics per account the setting may ask for.</summary>
    public const int MaxTopicsPerAccountLimit = 1000;

    public static IReadOnlyList<Setting> All { get; } =
    [
        new(MaxTopicsPerAccountKey, $"the most topics one account may create and own, 1 to {MaxTopicsPerAccountLimit}")
        {
            Default = TopicLimits.DefaultMaxPerAccount.ToString(CultureInfo.InvariantCulture),
        },
        new(StopWordsKey, "comma-separated stop list of topic names and addresses: a word that starts with an entry is refused")
        {
            Default = TopicLimits.DefaultStopWords,
        },
    ];

    /// <summary>
    /// The limits; a number of topics that is not a whole number from 1 to <see cref="MaxTopicsPerAccountLimit"/> stops the
    /// host. An empty stop list is allowed: then only the reserved addresses are refused.
    /// </summary>
    public static TopicLimits ReadLimits(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var max = configuration[MaxTopicsPerAccountKey];
        var maxPerAccount = TopicLimits.DefaultMaxPerAccount;
        if (!string.IsNullOrWhiteSpace(max))
        {
            maxPerAccount = int.TryParse(max, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                && number >= 1 && number <= MaxTopicsPerAccountLimit
                    ? number
                    : throw new InvalidOperationException(
                        $"{MaxTopicsPerAccountKey}: '{max}' is not a whole number from 1 to {MaxTopicsPerAccountLimit}.");
        }

        var stopWords = configuration[StopWordsKey] is { } list
            ? TopicCreationRules.ParseStopWords(list)
            : TopicLimits.Default.StopWords;
        return new TopicLimits(maxPerAccount, stopWords);
    }
}

/// <summary>
/// What a new topic's name and address must be (E11-11, ADR-0084), on top of the profile rules of <see cref="SubsiteRules"/>.
/// </summary>
public static class TopicCreationRules
{
    /// <summary>A topic's name: at least this many characters (it has to say what the topic is about).</summary>
    public const int NameMinLength = 2;

    /// <summary>
    /// Addresses a topic may never take (ADR-0084): a topic lives at <c>/&lt;address&gt;</c>, so a page of the site there
    /// would hide it, and the service prefixes are not pages at all. <c>ReservedAddressesTests</c> checks that every
    /// top-level page of <c>frontend/pages</c> is here; shorter names than <see cref="SubsiteRules.NicknameMinLength"/>
    /// (<c>/u</c>, <c>/ui</c>) are never addresses anyway.
    /// </summary>
    public static IReadOnlySet<string> ReservedAddresses { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        // Pages of the site (frontend/pages).
        "branch", "brand", "index", "login", "menu", "new", "onboarding", "post", "profile", "reactions", "register",
        "settings", "tokens", "topics", "write",
        // Service prefixes of the site and the API.
        "about", "account", "admin", "api", "assets", "auth", "bff", "connect", "favicon", "health", "img", "media",
        "robots", "sitemap", "static",
        // Sections the spec names and that are to come as pages.
        "bookmarks", "drafts", "feed", "help", "logout", "messages", "notifications", "popular", "privacy", "rating",
        "rules", "search", "subscriptions", "support", "tags", "terms", "top",
    };

    /// <summary>Whether <paramref name="nickname"/>, in the stored form, is an address no topic may take.</summary>
    public static bool IsReservedAddress(string nickname) => ReservedAddresses.Contains(nickname);

    /// <summary>Whether a name is long enough: <see cref="NameMinLength"/> characters after trimming.</summary>
    public static bool IsLongEnoughName(string? name) => SubsiteRules.LengthOf(SubsiteRules.Clean(name)) >= NameMinLength;

    /// <summary>
    /// The stop-list entry of <paramref name="text"/> that one of its words starts with, or <c>null</c>. Words are runs of
    /// letters and digits in <see cref="Normalize"/>'s form, so <c>_</c>, spaces and punctuation separate them:
    /// «Официальные новости» and <c>admin_news</c> are caught, «Бадминтон» and <c>badminton</c> are not.
    /// </summary>
    public static string? StopWordIn(string? text, IReadOnlyList<string> stopWords)
    {
        ArgumentNullException.ThrowIfNull(stopWords);
        if (string.IsNullOrWhiteSpace(text) || stopWords.Count == 0)
        {
            return null;
        }

        foreach (var word in Words(Normalize(text)))
        {
            foreach (var stop in stopWords)
            {
                if (word.StartsWith(stop, StringComparison.Ordinal))
                {
                    return stop;
                }
            }
        }

        return null;
    }

    /// <summary>The stop list from its setting: comma-separated entries, each in <see cref="Normalize"/>'s form, without empty ones.</summary>
    public static IReadOnlyList<string> ParseStopWords(string list)
    {
        ArgumentNullException.ThrowIfNull(list);
        return [.. list.Split(',').Select(Normalize).Where(entry => entry.Length > 0).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The form names are compared in: lower case, <c>ё</c> as <c>е</c>, every character that is not a letter or a digit
    /// as a space, trimmed.
    /// </summary>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant())
        {
            result.Append(c switch
            {
                'ё' => 'е',
                _ when char.IsLetterOrDigit(c) => c,
                _ => ' ',
            });
        }

        return result.ToString().Trim();
    }

    private static string[] Words(string normalized) =>
        normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
