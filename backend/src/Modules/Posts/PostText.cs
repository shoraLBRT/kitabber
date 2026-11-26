using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Platform.Modules.Posts;

/// <summary>
/// The text of a post's blocks without markup (ADR-0094), for the search index: the inline HTML of paragraphs, headers,
/// quotes with their captions, list items and image captions, a block per line. Delimiters and pictures have no text.
/// </summary>
public static partial class PostText
{
    private static readonly string[] _textFields = ["text", "caption"];

    /// <param name="body">The cleaned block document as stored (ADR-0023).</param>
    public static string Of(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (JsonNode.Parse(body)?["blocks"] is not JsonArray blocks)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        foreach (var data in blocks.Select(b => b?["data"]).OfType<JsonObject>())
        {
            foreach (var field in _textFields)
            {
                Append(text, data[field]);
            }

            if (data["items"] is JsonArray items)
            {
                foreach (var item in items)
                {
                    Append(text, item);
                }
            }
        }

        return text.ToString().TrimEnd('\n');
    }

    private static void Append(StringBuilder text, JsonNode? html)
    {
        if (html is JsonValue value && value.TryGetValue<string>(out var inline)
            && Plain(inline) is { Length: > 0 } plain)
        {
            text.Append(plain).Append('\n');
        }
    }

    // The inline HTML is already cleaned by the allow list (ADR-0023): a line break becomes a space, other tags go
    // without one, so a word half in bold stays one word.
    private static string Plain(string html) =>
        Spaces().Replace(WebUtility.HtmlDecode(Tags().Replace(LineBreaks().Replace(html, " "), string.Empty)), " ").Trim();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreaks();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
