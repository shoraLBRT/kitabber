using System.Text.RegularExpressions;
using Platform.Modules.Editor.Blocks;

namespace Platform.Modules.Editor.Tests;

/// <summary>The allow list of inline HTML (ADR-0023) against XSS vectors and the formatting the editor offers.</summary>
public sealed partial class InlineHtmlTests
{
    private readonly InlineHtml _html = new();

    /// <summary>
    /// XSS vectors after the OWASP filter evasion cheat sheet: scripts, event handlers, dangerous schemes (also
    /// encoded and split), embedded documents, SVG and MathML, styles, forms, broken markup.
    /// </summary>
    public static TheoryData<string> Vectors() =>
    [
        "<script>alert(1)</script>",
        "<SCRIPT SRC=https://evil.test/x.js></SCRIPT>",
        "<scr<script>ipt>alert(1)</script>",
        "<img src=x onerror=alert(1)>",
        "<IMG SRC=\"javascript:alert('XSS');\">",
        "<svg onload=alert(1)>",
        "<svg><script>alert(1)</script></svg>",
        "<math><mtext><table><mglyph><style><img src=x onerror=alert(1)>",
        "<body onload=alert(1)>",
        "<iframe src=\"javascript:alert(1)\"></iframe>",
        "<object data=\"data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==\"></object>",
        "<embed src=\"https://evil.test/x.swf\">",
        "<a href=\"javascript:alert(1)\">x</a>",
        "<a href=\"JaVaScRiPt:alert(1)\">x</a>",
        "<a href=\"jav&#x09;ascript:alert(1)\">x</a>",
        "<a href=\"&#106;&#97;&#118;&#97;&#115;&#99;&#114;&#105;&#112;&#116;&#58;alert(1)\">x</a>",
        "<a href=\"data:text/html,<script>alert(1)</script>\">x</a>",
        "<a href=\"vbscript:msgbox(1)\">x</a>",
        "<a href=\"https://ok.test/\" onclick=\"alert(1)\" onmouseover=\"alert(1)\">x</a>",
        "<a href=\"https://ok.test/\" style=\"position:fixed;top:0;left:0;width:100%;height:100%\">x</a>",
        "<b onmouseover=alert(1)>x</b>",
        "<mark style=\"background:url(javascript:alert(1))\">x</mark>",
        "<div style=\"width: expression(alert(1))\">x</div>",
        "<style>@import 'https://evil.test/x.css';</style>",
        "<link rel=stylesheet href=https://evil.test/x.css>",
        "<meta http-equiv=\"refresh\" content=\"0;url=javascript:alert(1)\">",
        "<base href=\"javascript:alert(1)//\">",
        "<form action=\"javascript:alert(1)\"><input type=submit></form>",
        "<details open ontoggle=alert(1)>",
        "<video><source onerror=alert(1)></video>",
        "<<script>alert(1)//<</script>",
        "<a href=\"https://ok.test/\"><img src=x onerror=alert(1)></a>",
        "<noscript><p title=\"</noscript><img src=x onerror=alert(1)>\"></noscript>",
        "<template><script>alert(1)</script></template>",
        "\"><script>alert(1)</script>",
    ];

    [Theory]
    [MemberData(nameof(Vectors))]
    public void NeutralisesXssVectors(string vector)
    {
        var clean = _html.Clean(vector);

        // Only allowed elements remain as markup; a link has only href (http, https, mailto) and rel. Anything else
        // is left at most as encoded text, which a browser shows and does not run.
        foreach (Match tag in Tag().Matches(clean))
        {
            Assert.Contains(tag.Groups["name"].Value.ToLowerInvariant(), InlineHtml.AllowedTags);
            foreach (Match attribute in Attribute().Matches(tag.Groups["attributes"].Value))
            {
                var name = attribute.Groups["name"].Value.ToLowerInvariant();
                Assert.Contains(name, (string[])["href", "rel"]);
                if (name == "href")
                {
                    var scheme = new Uri(attribute.Groups["value"].Value).Scheme;
                    Assert.Contains(scheme, InlineHtml.AllowedSchemes);
                }
            }
        }

        Assert.DoesNotContain("<script", clean, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void KeepsTheEditorFormatting()
    {
        var clean = _html.Clean("<b>bold</b> <i>italic</i> <mark class=\"cdx-marker\">marked</mark><br>"
            + "<strong>s</strong> <em>e</em> <a href=\"https://example.test/page?q=1\" target=\"_blank\">link</a>");

        Assert.Equal("<b>bold</b> <i>italic</i> <mark>marked</mark><br><strong>s</strong> <em>e</em> "
            + "<a href=\"https://example.test/page?q=1\" rel=\"nofollow ugc noopener noreferrer\">link</a>", clean);
    }

    [Fact]
    public void UnwrapsOtherElementsAndKeepsTheirText()
    {
        Assert.Equal("Title and text", _html.Clean("<h1>Title</h1> <div><span>and</span> text</div>"));
    }

    [Fact]
    public void EncodesPlainText()
    {
        Assert.Equal("a &lt; b &amp; c", _html.Clean("a < b & c"));
    }

    [Fact]
    public void KeepsMailLinks()
    {
        Assert.Equal("<a href=\"mailto:someone@example.test\" rel=\"nofollow ugc noopener noreferrer\">mail</a>",
            _html.Clean("<a href=\"mailto:someone@example.test\">mail</a>"));
    }

    [GeneratedRegex(@"<\s*/?\s*(?<name>[a-zA-Z][a-zA-Z0-9]*)(?<attributes>[^>]*)>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"(?<name>[^\s=/""']+)(\s*=\s*""(?<value>[^""]*)"")?")]
    private static partial Regex Attribute();
}
