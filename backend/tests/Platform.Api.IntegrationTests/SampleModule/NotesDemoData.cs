using Platform.Seeding;

namespace Platform.Api.IntegrationTests.SampleModule;

/// <summary>
/// The factory of the sample module (ADR-0009): a valid note with a stable identifier, an author taken by the
/// identifier of an account (never by reading another module's tables), a title and a time in the past.
/// </summary>
public sealed class NoteFactory(SeedData data) : ObjectFactory<Note>(data)
{
    public const int Authors = 5;

    private static readonly string[] _words =
        ["Заметка", "о", "чае", "дождь", "книга", "утро", "дорога", "письмо", "сад", "город", "песня", "вечер"];

    public override string Kind => "sample-note";

    protected override Note Create(int number) => new()
    {
        Id = IdOf(number),
        AuthorId = Data.Id("account", number % Authors).ToString(),
        Title = $"{string.Join(' ', Enumerable.Range(0, 3).Select(_ => _words[Random.Next(_words.Length)]))} №{number + 1}",
        CreatedAt = Data.Now - TimeSpan.FromMinutes(30 * (number + 1)) - TimeSpan.FromSeconds(Random.Next(600)),
    };
}

/// <summary>The demo data of the sample module: <see cref="Count"/> notes, added only where they are missing.</summary>
public sealed class NotesDemoSeeder(NotesDbContext db) : IDemoSeeder
{
    public const int Count = 20;

    public string Name => NotesDbContext.Schema;

    public Task<int> SeedAsync(SeedData data, CancellationToken cancellationToken) =>
        db.AddMissingAsync(new NoteFactory(data).BuildMany(Count).ToList(), n => n.Id, cancellationToken);
}
