using System.Text.RegularExpressions;

namespace Platform.Modules.Posts;

/// <summary>
/// The hashtags of a post (E11-08, FR-TOPIC, ADR-0120): a <c>#</c> and a word of letters, digits and <c>_</c> with at
/// least one letter, not glued to a word or an entity before it (<c>page#part</c>, <c>&amp;#39;</c>). The case is not
/// told apart: a tag is kept lower-case. The reader's renderer (<c>frontend/utils/hashtags.ts</c>) links the same words.
/// </summary>
public static partial class Hashtags
{
    /// <summary>The longest tag; a longer word after <c>#</c> is not a tag at all.</summary>
    public const int MaxLength = 64;

    /// <summary>At most this many tags of a post are kept, the first ones of the text.</summary>
    public const int MaxPerPost = 20;

    /// <summary>The distinct tags of a post's text (<see cref="PostText"/>), lower-case, in the order they first come.</summary>
    public static IReadOnlyList<string> Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. Tag().Matches(text)
            .Select(m => m.Groups["tag"].Value)
            .Where(HasLetter)
            .Select(Normalize)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxPerPost)];
    }

    /// <summary>The tags of a stored block document (ADR-0023).</summary>
    public static IReadOnlyList<string> OfBody(string body) => Of(PostText.Of(body));

    /// <summary>
    /// The stored form of a tag asked for by its address — with or without the leading <c>#</c>, in any case — or
    /// <c>null</c> when it is not a tag.
    /// </summary>
    public static string? Parse(string? tag)
    {
        var word = tag?.Trim();
        if (word is { Length: > 0 } && word[0] == '#')
        {
            word = word[1..];
        }

        return word is { Length: > 0 and <= MaxLength } && Word().IsMatch(word) && HasLetter(word) ? Normalize(word) : null;
    }

    private static string Normalize(string tag) => tag.ToLowerInvariant();

    private static bool HasLetter(string word) => word.Any(char.IsLetter);

    [GeneratedRegex(@"(?<![\p{L}\p{M}\p{N}_&#])#(?<tag>[\p{L}\p{M}\p{N}_]{1,64})(?![\p{L}\p{M}\p{N}_])")]
    private static partial Regex Tag();

    [GeneratedRegex(@"^[\p{L}\p{M}\p{N}_]+$")]
    private static partial Regex Word();
}
