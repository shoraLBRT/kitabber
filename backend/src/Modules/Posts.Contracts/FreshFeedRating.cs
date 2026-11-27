namespace Platform.Modules.Posts.Contracts;

/// <summary>
/// The rating filter of the fresh feed (E08-07, FR-FEED, ADR-0106): every post, or only those whose rating is at least
/// one of the thresholds. The Posts module applies it; the Preferences module remembers the reader's choice.
/// </summary>
public static class FreshFeedRating
{
    /// <summary>The thresholds a reader may choose, lowest first: «от −10», «от +5», «от +10». No threshold is «все».</summary>
    public static IReadOnlyList<int> Thresholds { get; } = [-10, 5, 10];

    /// <summary>Whether <paramref name="value"/> is a threshold of the filter; <c>null</c> («все») always is.</summary>
    public static bool IsAllowed(int? value) => value is not { } threshold || Thresholds.Contains(threshold);

    /// <summary>The message of a refused threshold.</summary>
    public static string Refusal { get; } =
        $"The rating threshold is none (all posts) or one of: {string.Join(", ", Thresholds)}.";
}
