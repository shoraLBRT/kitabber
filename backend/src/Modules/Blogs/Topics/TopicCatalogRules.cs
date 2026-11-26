namespace Platform.Modules.Blogs.Topics;

/// <summary>The rules of the topic catalogue and the menu's topic list (E11-02, FR-TOPIC, ADR-0088), apart from the database.</summary>
public static class TopicCatalogRules
{
    /// <summary>The menu shows at most this many topics; «Показать все» leads to the catalogue.</summary>
    public const int MenuSize = 10;

    /// <summary>A search is at most this many characters.</summary>
    public const int QueryMaxLength = 50;

    /// <summary>The search text as it is matched: trimmed, inner spaces collapsed; <c>null</c> for no search.</summary>
    public static string? NormalizeQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        return string.Join(' ', query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Whether a normalized search is short enough.</summary>
    public static bool IsValidQuery(string? normalized) => normalized is null || normalized.Length <= QueryMaxLength;

    /// <summary>
    /// <paramref name="text"/> for a <c>LIKE</c> pattern with <c>\</c> as the escape: its <c>\</c>, <c>%</c> and
    /// <c>_</c> match themselves.
    /// </summary>
    public static string EscapeLike(string text) =>
        text.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

    /// <summary>The <c>ILIKE</c> pattern of a search: the text anywhere in the name or the address.</summary>
    public static string ContainsPattern(string normalized) => $"%{EscapeLike(normalized)}%";
}
