using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Platform.Mail;

/// <summary>A letter ready to send.</summary>
public sealed record RenderedEmail(string Subject, string Text, string Html);

/// <summary>
/// Renders a template (ADR-0017). A template is three files in <c>Templates/&lt;culture&gt;/</c>:
/// <c>&lt;name&gt;.subject.txt</c>, <c>&lt;name&gt;.txt</c> and <c>&lt;name&gt;.html</c>; the text and the HTML are put
/// into that culture's <c>_layout.txt</c> and <c>_layout.html</c> at <c>{{content}}</c>. Placeholders are
/// <c>{{name}}</c>; values are escaped in HTML. Built in: <c>{{product}}</c>, <c>{{subject}}</c> and
/// <c>{{color.&lt;role&gt;}}</c> (the light palette of the brand). A placeholder without a value is an error, so a
/// letter never goes out with a hole in it.
/// </summary>
public sealed partial class MailTemplateRenderer(IMailTemplateSource source, MailBrand brand)
{
    /// <summary>The language of the product: every template exists in it, other cultures fall back to it.</summary>
    public const string DefaultCulture = "ru";

    private const string Content = "content";
    private const string Subject = "subject";
    private const string Product = "product";
    private const string ColorPrefix = "color.";

    public RenderedEmail Render(string template, CultureInfo culture, IReadOnlyDictionary<string, string> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(values);
        var reserved = values.Keys.Where(IsBuiltIn).ToList();
        if (reserved.Count > 0)
        {
            throw new ArgumentException(
                $"values may not set the built-in placeholders {string.Join(", ", reserved)}", nameof(values));
        }

        var language = ResolveCulture(template, culture);
        string File(string name) => source.Find(language, name)
            ?? throw new InvalidOperationException($"mail template {language}/{name} is missing");

        string? Value(string key) => key == Product ? brand.ProductName
            : key.StartsWith(ColorPrefix, StringComparison.Ordinal)
                ? brand.Colors.GetValueOrDefault(key[ColorPrefix.Length..])
                : values.GetValueOrDefault(key);

        var subjectFile = $"{template}.subject.txt";
        var subject = Fill(subjectFile, File(subjectFile), Value, html: false).Trim();
        string? WithSubject(string key) => key == Subject ? subject : Value(key);

        var text = Fill($"{template}.txt", File($"{template}.txt"), WithSubject, html: false).Trim();
        var html = Fill($"{template}.html", File($"{template}.html"), WithSubject, html: true).Trim();
        var textLayout = Fill("_layout.txt", File("_layout.txt"), k => k == Content ? text : WithSubject(k), html: false);
        var htmlLayout = Fill("_layout.html", File("_layout.html"), k => k == Content ? html : WithSubject(k), html: true);
        return new RenderedEmail(subject, textLayout.Trim() + "\n", htmlLayout.Trim() + "\n");
    }

    /// <summary>The culture whose template is used: <paramref name="culture"/>, its parents, then <see cref="DefaultCulture"/>.</summary>
    public string ResolveCulture(string template, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        for (var c = culture; !string.IsNullOrEmpty(c.Name); c = c.Parent)
        {
            if (source.Find(c.Name, $"{template}.subject.txt") is not null)
            {
                return c.Name;
            }
        }

        return source.Find(DefaultCulture, $"{template}.subject.txt") is not null
            ? DefaultCulture
            : throw new ArgumentException($"there is no mail template '{template}'", nameof(template));
    }

    private static bool IsBuiltIn(string key) =>
        key is Content or Subject or Product || key.StartsWith(ColorPrefix, StringComparison.Ordinal);

    // {{content}} is the already rendered body: it is inserted as is, every other value is escaped in HTML.
    private static string Fill(string file, string text, Func<string, string?> value, bool html)
    {
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        var result = Placeholder().Replace(text, m =>
        {
            var key = m.Groups[1].Value;
            var v = value(key);
            if (v is null)
            {
                missing.Add(key);
                return m.Value;
            }

            return html && key != Content ? WebUtility.HtmlEncode(v) : v;
        });
        return missing.Count == 0
            ? result
            : throw new ArgumentException(
                $"mail template {file} has no value for {string.Join(", ", missing.Select(k => "{{" + k + "}}"))}");
    }

    [GeneratedRegex(@"\{\{\s*([a-z][a-z0-9_.-]*)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}
