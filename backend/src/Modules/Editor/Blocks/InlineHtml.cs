using AngleSharp.Dom;
using Ganss.Xss;

namespace Platform.Modules.Editor.Blocks;

/// <summary>
/// Cleans the inline HTML of a text field by an allow list (ADR-0023): the formatting the editor offers — bold, italic,
/// marker, link — and line breaks. Any other element is unwrapped and keeps its text; scripts, styles and the like go
/// with their content. Links keep only <c>href</c> with an <c>http</c>, <c>https</c> or <c>mailto</c> address and
/// get <c>rel="nofollow ugc noopener noreferrer"</c>. Safe to share between threads.
/// </summary>
public sealed class InlineHtml
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
}
