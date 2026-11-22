using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Ganss.Xss;

namespace Platform.Modules.Editor.Blocks;

/// <summary>
/// Cleans the inline HTML of a text field by an allow list (ADR-0023): the formatting the editor offers — bold, italic,
/// marker, link — and line breaks. Any other element is unwrapped and keeps its text; scripts, styles and the like go
/// with their content. Links keep only <c>href</c> with an <c>http</c>, <c>https</c> or <c>mailto</c> address and
/// get <c>rel="nofollow ugc noopener noreferrer"</c>. Safe to share between threads.
/// </summary>
public sealed partial class InlineHtml
{
    /// <summary>The <c>rel</c> every link gets: user content is not endorsed and does not reach the opener.</summary>
    public const string LinkRel = "nofollow ugc noopener noreferrer";

    /// <summary>The elements a text field may hold.</summary>
    public static readonly IReadOnlySet<string> AllowedTags =
        new HashSet<string>(["b", "strong", "i", "em", "mark", "a", "br"], StringComparer.OrdinalIgnoreCase);

    /// <summary>The address schemes a link may have.</summary>
    public static readonly IReadOnlySet<string> AllowedSchemes =
        new HashSet<string>(["http", "https", "mailto"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> _droppedWithContent = new(
        ["script", "style", "template", "noscript", "noembed", "noframes", "iframe", "frameset", "object", "embed",
            "svg", "math", "textarea", "select", "option", "title", "head", "xmp", "plaintext"],
        StringComparer.OrdinalIgnoreCase);

    private readonly HtmlSanitizer _sanitizer;

    public InlineHtml()
    {
        _sanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(AllowedTags, StringComparer.OrdinalIgnoreCase),
            AllowedAttributes = new HashSet<string>(["href"], StringComparer.OrdinalIgnoreCase),
            AllowedSchemes = new HashSet<string>(AllowedSchemes, StringComparer.OrdinalIgnoreCase),
            UriAttributes = new HashSet<string>(["href"], StringComparer.OrdinalIgnoreCase),
            // No style attribute or element is allowed, so no CSS property is either.
            AllowedCssProperties = new HashSet<string>(StringComparer.Ordinal),
        })
        {
            KeepChildNodes = true,
        };
        // Unwrapping keeps the text of an unknown element; these go with everything inside them.
        _sanitizer.RemovingTag += (_, e) =>
        {
            if (_droppedWithContent.Contains(e.Tag.LocalName))
            {
                while (e.Tag.FirstChild is { } child)
                {
                    e.Tag.RemoveChild(child);
                }
            }
        };
        _sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is IElement { LocalName: "a" } link)
            {
                link.SetAttribute("rel", LinkRel);
            }
        };
    }

    /// <summary>Returns <paramref name="html"/> with everything outside the allow list removed.</summary>
    public string Clean(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        return _sanitizer.Sanitize(html);
    }

    /// <summary>
    /// Whether cleaned inline HTML shows any text: without the tags and with entities decoded, something other than
    /// white space, no-break and zero-width spaces is left (the "empty post" check of publishing, ADR-0046).
    /// </summary>
    public static bool HasText(string cleanedHtml)
    {
        ArgumentNullException.ThrowIfNull(cleanedHtml);
        var text = WebUtility.HtmlDecode(Tag().Replace(cleanedHtml, " "));
        return text.Any(c => !char.IsWhiteSpace(c) && c != ' ' && c != '​' && c != '﻿');
    }

    /// <summary>
    /// The number of characters a reader sees in cleaned inline HTML: without the tags, with entities decoded and a
    /// line break as one character (the caption limit of an image, ADR-0049).
    /// </summary>
    public static int TextLength(string cleanedHtml)
    {
        ArgumentNullException.ThrowIfNull(cleanedHtml);
        return WebUtility.HtmlDecode(Tag().Replace(Break().Replace(cleanedHtml, "\n"), string.Empty)).Length;
    }

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Break();

    [GeneratedRegex("<[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex Tag();
}
