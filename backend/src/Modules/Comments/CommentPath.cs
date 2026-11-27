using System.Globalization;

namespace Platform.Modules.Comments;

/// <summary>
/// The materialized path of a comment (ADR-0048): the identifiers of its ancestors and its own, root first, each as 32
/// hex digits, joined by <see cref="Separator"/>. The identifiers are UUID v7, whose hex digits sort by creation time,
/// and every segment has the same width, so ordering by the path (in the byte collation <c>"C"</c>) walks the tree
/// depth first, older siblings first. A branch is every path that starts with its root's path.
/// </summary>
public static class CommentPath
{
    /// <summary>Between the segments; it sorts before every hex digit, so a parent comes before its replies.</summary>
    public const char Separator = '.';

    /// <summary>The width of one segment: a UUID in hex without dashes.</summary>
    public const int SegmentLength = 32;

    /// <summary>The path of <paramref name="id"/> under the comment whose path is <paramref name="parentPath"/>.</summary>
    public static string Of(string? parentPath, Guid id)
    {
        var segment = id.ToString("N", CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(parentPath) ? segment : $"{parentPath}{Separator}{segment}";
    }

    /// <summary>The identifiers of the comments above the one whose path is <paramref name="path"/>, root first.</summary>
    public static IReadOnlyList<Guid> Ancestors(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Split(Separator);
        return [.. segments.Take(segments.Length - 1).Select(s => Guid.ParseExact(s, "N"))];
    }
}
