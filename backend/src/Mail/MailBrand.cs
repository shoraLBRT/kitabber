using System.Text.Json;

namespace Platform.Mail;

/// <summary>
/// What a letter takes from the brand: the product name and the light palette. The name is temporary, so it comes
/// only from <c>brand/brand.json</c> (ADR-0011), built into this assembly; templates write <c>{{product}}</c>.
/// </summary>
public sealed record MailBrand(string ProductName, IReadOnlyDictionary<string, string> Colors)
{
    /// <summary>The brand of <c>brand/brand.json</c>.</summary>
    public static MailBrand Default { get; } = Load();

    private static MailBrand Load()
    {
        using var stream = typeof(MailBrand).Assembly.GetManifestResourceStream("brand.json")
            ?? throw new InvalidOperationException("brand.json is not built into Platform.Mail");
        using var json = JsonDocument.Parse(stream);
        var root = json.RootElement;
        var colors = root.GetProperty("palette").GetProperty("light").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
        return new MailBrand(root.GetProperty("name").GetString()!, colors);
    }
}
