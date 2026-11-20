namespace Platform.Mail;

/// <summary>Where template files come from: <c>&lt;culture&gt;/&lt;file&gt;</c>, for example <c>ru/login-code.html</c>.</summary>
public interface IMailTemplateSource
{
    /// <summary>The text of the file, or null when the culture has no such file.</summary>
    string? Find(string culture, string file);

    /// <summary>Every template (a <c>.subject.txt</c> file) with its culture.</summary>
    IReadOnlyList<(string Culture, string Template)> List();
}

/// <summary>The templates built into this assembly from <c>backend/src/Mail/Templates</c>.</summary>
public sealed class EmbeddedMailTemplateSource : IMailTemplateSource
{
    private const string Prefix = "Templates/";
    private const string SubjectSuffix = ".subject.txt";

    private readonly Dictionary<string, string> _files;

    private EmbeddedMailTemplateSource()
    {
        var assembly = typeof(EmbeddedMailTemplateSource).Assembly;
        _files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            // MSBuild on Windows writes RecursiveDir with backslashes.
            var name = resource.Replace('\\', '/');
            if (!name.StartsWith(Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            _files[name[Prefix.Length..]] = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        }
    }

    public static EmbeddedMailTemplateSource Instance { get; } = new();

    /// <summary>Every built-in file with its path under <c>Templates/</c>, for checks over all of them.</summary>
    public IReadOnlyDictionary<string, string> Files => _files;

    public string? Find(string culture, string file) => _files.GetValueOrDefault($"{culture}/{file}");

    public IReadOnlyList<(string Culture, string Template)> List() => ListOf(_files.Keys);

    /// <summary>The templates among <paramref name="paths"/> (<c>&lt;culture&gt;/&lt;name&gt;.subject.txt</c>).</summary>
    public static IReadOnlyList<(string Culture, string Template)> ListOf(IEnumerable<string> paths) =>
    [
        .. paths
            .Where(k => k.EndsWith(SubjectSuffix, StringComparison.Ordinal) && k.Contains('/', StringComparison.Ordinal))
            .Select(k =>
            {
                var slash = k.IndexOf('/', StringComparison.Ordinal);
                return (Culture: k[..slash], Template: k[(slash + 1)..^SubjectSuffix.Length]);
            })
            .OrderBy(t => t.Template, StringComparer.Ordinal)
            .ThenBy(t => t.Culture, StringComparer.Ordinal),
    ];
}
