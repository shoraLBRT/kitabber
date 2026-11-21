using System.Globalization;
using System.Text.Json;

namespace Platform.Mail.Tests;

/// <summary>Templates of E01-12: every one has a snapshot per culture, is localizable and takes the product name from the brand.</summary>
public sealed class TemplateTests
{
    /// <summary>A fixed brand: snapshots do not change with the temporary product name.</summary>
    public static readonly MailBrand TestBrand = new("Платформа", new Dictionary<string, string>
    {
        ["background"] = "#F7F6F2",
        ["surface"] = "#FFFFFF",
        ["text"] = "#1C1E21",
        ["muted"] = "#5A6069",
        ["accent"] = "#0B6E5B",
        ["onAccent"] = "#FFFFFF",
    });

    /// <summary>The values a snapshot renders each template with. A new template adds its line here.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Samples =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["login-code"] = new Dictionary<string, string> { ["code"] = "482913", ["lifetime"] = "10 минут" },
            ["email-confirmation"] = new Dictionary<string, string> { ["code"] = "305718", ["lifetime"] = "30 минут" },
            ["password-reset"] = new Dictionary<string, string> { ["code"] = "620194", ["lifetime"] = "15 минут" },
            ["password-changed"] = new Dictionary<string, string> { ["when"] = "21 ноября 2026 в 14:05 (UTC)" },
        };

    private static readonly MailTemplateRenderer _renderer = new(EmbeddedMailTemplateSource.Instance, TestBrand);

    public static TheoryData<string, string> Templates()
    {
        var data = new TheoryData<string, string>();
        foreach (var (culture, template) in EmbeddedMailTemplateSource.Instance.List())
        {
            data.Add(template, culture);
        }

        return data;
    }

    [Fact]
    public void LoginCodeTemplateIsBuiltIn() =>
        Assert.Contains((MailTemplateRenderer.DefaultCulture, "login-code"), EmbeddedMailTemplateSource.Instance.List());

    [Theory]
    [MemberData(nameof(Templates))]
    public void TemplateMatchesItsSnapshot(string template, string culture)
    {
        Assert.True(Samples.TryGetValue(template, out var values), $"add sample values of '{template}' to {nameof(Samples)}");

        var email = _renderer.Render(template, CultureInfo.GetCultureInfo(culture), values);

        Snapshot.Match($"{template}.{culture}",
            $"Subject: {email.Subject}\n\n----- text -----\n{email.Text}\n----- html -----\n{email.Html}");
    }

    [Fact]
    public void EveryTemplateExistsInTheDefaultCultureWithAllItsParts()
    {
        var source = EmbeddedMailTemplateSource.Instance;
        foreach (var (culture, template) in source.List())
        {
            Assert.NotNull(source.Find(MailTemplateRenderer.DefaultCulture, $"{template}.subject.txt"));
            foreach (var part in new[] { $"{template}.txt", $"{template}.html", "_layout.txt", "_layout.html" })
            {
                Assert.True(source.Find(culture, part) is not null, $"{culture}/{part} is missing");
            }
        }
    }

    [Fact]
    public void ValuesAreEscapedInHtmlAndKeptInText()
    {
        var email = _renderer.Render("login-code", CultureInfo.GetCultureInfo("ru"),
            new Dictionary<string, string> { ["code"] = "<b>1&2</b>", ["lifetime"] = "10 минут" });

        Assert.Contains("&lt;b&gt;1&amp;2&lt;/b&gt;", email.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>1&2</b>", email.Html, StringComparison.Ordinal);
        Assert.Contains("<b>1&2</b>", email.Text, StringComparison.Ordinal);
        Assert.Equal("Код для входа: <b>1&2</b>", email.Subject);
    }

    [Fact]
    public void MissingValueIsAnErrorThatNamesIt()
    {
        var e = Assert.Throws<ArgumentException>(() => _renderer.Render("login-code", CultureInfo.GetCultureInfo("ru"),
            new Dictionary<string, string> { ["code"] = "1" }));

        Assert.Contains("{{lifetime}}", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownTemplateIsAnError() =>
        Assert.Throws<ArgumentException>(() => _renderer.Render("no-such-template", CultureInfo.InvariantCulture,
            new Dictionary<string, string>()));

    [Fact]
    public void ValuesMayNotOverrideTheProductName() =>
        Assert.Throws<ArgumentException>(() => _renderer.Render("login-code", CultureInfo.InvariantCulture,
            new Dictionary<string, string> { ["code"] = "1", ["lifetime"] = "1", ["product"] = "Другое" }));

    [Theory]
    [InlineData("ru-RU", "ru")]
    [InlineData("en", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("de", "ru")]
    [InlineData("", "ru")]
    public void CultureFallsBackToItsParentThenToRussian(string requested, string used)
    {
        var source = new DictionarySource(new Dictionary<string, string>
        {
            ["ru/hello.subject.txt"] = "Привет, {{name}}",
            ["ru/hello.txt"] = "Привет, {{name}}",
            ["ru/hello.html"] = "<p>Привет, {{name}}</p>",
            ["ru/_layout.txt"] = "{{content}}",
            ["ru/_layout.html"] = "{{content}}",
            ["en/hello.subject.txt"] = "Hello, {{name}}",
            ["en/hello.txt"] = "Hello, {{name}}",
            ["en/hello.html"] = "<p>Hello, {{name}}</p>",
            ["en/_layout.txt"] = "{{content}}\n-- {{product}}",
            ["en/_layout.html"] = "{{content}}",
        });
        var renderer = new MailTemplateRenderer(source, TestBrand);

        var email = renderer.Render("hello", CultureInfo.GetCultureInfo(requested), new Dictionary<string, string> { ["name"] = "Аня" });

        Assert.Equal(used, renderer.ResolveCulture("hello", CultureInfo.GetCultureInfo(requested)));
        Assert.Equal(used == "en" ? "Hello, Аня" : "Привет, Аня", email.Subject);
        Assert.Equal(used == "en" ? "Hello, Аня\n-- Платформа\n" : "Привет, Аня\n", email.Text);
    }

    [Fact]
    public void ProductNameComesFromBrandJson()
    {
        var brand = JsonDocument.Parse(File.ReadAllText(RepositoryFile("brand/brand.json"))).RootElement;
        var name = brand.GetProperty("name").GetString()!;

        Assert.Equal(name, MailBrand.Default.ProductName);
        Assert.Equal(brand.GetProperty("palette").GetProperty("light").GetProperty("accent").GetString(),
            MailBrand.Default.Colors["accent"]);

        var email = new MailTemplateRenderer(EmbeddedMailTemplateSource.Instance, MailBrand.Default)
            .Render("login-code", CultureInfo.GetCultureInfo("ru"), Samples["login-code"]);
        Assert.Contains(name, email.Text, StringComparison.Ordinal);
        Assert.Contains(name, email.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplatesDoNotWriteTheProductName()
    {
        var name = MailBrand.Default.ProductName;
        var offending = EmbeddedMailTemplateSource.Instance.Files
            .Where(f => f.Value.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Key)
            .ToList();

        Assert.True(offending.Count == 0, $"use {{{{product}}}} instead of the name in: {string.Join(", ", offending)}");
    }

    internal static string RepositoryFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, relative)))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new InvalidOperationException($"{relative} was not found above the test binaries")
            : Path.Combine(dir.FullName, relative);
    }

    private sealed class DictionarySource(Dictionary<string, string> files) : IMailTemplateSource
    {
        public string? Find(string culture, string file) => files.GetValueOrDefault($"{culture}/{file}");

        public IReadOnlyList<(string Culture, string Template)> List() => EmbeddedMailTemplateSource.ListOf(files.Keys);
    }
}
