using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using MassTransit;
using Platform.Modules.Identity.Contracts;

namespace Platform.Modules.Identity.IntegrationTests.Contracts;

/// <summary>
/// E18-05 (FR-ID-5, ADR-0087): the account events are a contract for other projects. Their schemas are in
/// docs/contracts; the code must stay compatible with what is published there, and the files must describe the code.
/// After a deliberate, compatible change of an event, run these tests with <c>UPDATE_CONTRACTS=1</c> and commit the
/// schemas. An incompatible change is a new version of the event, never an edit of the old one.
/// </summary>
public sealed class AccountEventContractTests
{
    // What each schema says about its event; the same text is in docs/contracts/README.md.
    private static readonly Dictionary<string, string> _descriptions = new()
    {
        [AccountEventNames.Created] = "Аккаунт создан: у него впервые подтверждён способ входа (ADR-0025).",
        [AccountEventNames.EmailVerified] = "Почта аккаунта подтверждена кодом: первая или новая вместо прежней.",
        [AccountEventNames.Updated] = "Данные аккаунта изменились; changes называет, что именно (сейчас только email).",
        [AccountEventNames.Deleted] = "Аккаунт удалён: модули и проекты стирают данные, которые держат по accountId.",
    };

    // Words a property of an account event never contains (FR-ID-1): credentials, tokens, the address itself.
    private static readonly string[] _forbidden = ["password", "hash", "token", "secret", "code", "stamp", "email"];

    // The schemas are read by people: Cyrillic stays as it is.
    private static readonly JsonSerializerOptions _indented = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly string _directory = Path.Combine(RepositoryRoot(), "docs", "contracts");

    public static TheoryData<string> Events => [.. AccountEventNames.All.Keys];

    [Theory]
    [MemberData(nameof(Events))]
    public async Task SchemaInDocsDescribesTheEvent(string name)
    {
        var generated = Generate(name);
        var path = SchemaPath(name);
        var expected = generated.ToJsonString(_indented) + "\n";
        if (Environment.GetEnvironmentVariable("UPDATE_CONTRACTS") == "1")
        {
            await File.WriteAllTextAsync(path, expected, TestContext.Current.CancellationToken);
        }

        Assert.True(File.Exists(path), $"{path} is missing; run the tests with UPDATE_CONTRACTS=1");
        var published = await ReadAsync(path);
        Assert.True(
            JsonNode.DeepEquals(published, generated),
            $"{path} does not describe {AccountEventNames.All[name].Name}. If the change is compatible, run the tests with "
                + $"UPDATE_CONTRACTS=1; otherwise publish a new version.\nGenerated schema:\n{expected}");
    }

    [Theory]
    [MemberData(nameof(Events))]
    public async Task EventStaysCompatibleWithItsPublishedSchema(string name)
    {
        var published = await ReadAsync(SchemaPath(name));

        Assert.Empty(EventSchemas.Breaks(published, Generate(name)));
    }

    [Fact]
    public void EveryPublishedSchemaHasItsEvent()
    {
        var files = Directory.GetFiles(_directory, "*.schema.json").Select(f => Path.GetFileName(f)).Order().ToList();

        Assert.Equal(AccountEventNames.All.Keys.Select(n => $"{n}.schema.json").Order().ToList(), files);
    }

    [Theory]
    [MemberData(nameof(Events))]
    public void EventTravelsUnderItsVersionedName(string name)
    {
        var type = AccountEventNames.All[name];

        Assert.Matches(@"^account\.[a-z_]+\.v[1-9][0-9]*$", name);
        Assert.Equal($"urn:message:{name}", MessageUrn.ForTypeString(type));
        var entityName = type.GetCustomAttributes(typeof(EntityNameAttribute), inherit: false)
            .Cast<EntityNameAttribute>().Single().EntityName;
        Assert.Equal(name, entityName);
    }

    [Theory]
    [MemberData(nameof(Events))]
    public void EventCarriesNoCredentialsTokensOrEmail(string name)
    {
        var names = EventSchemas.PropertyNames(Generate(name)).ToList();

        Assert.Contains("accountId", names);
        Assert.DoesNotContain(names, n => _forbidden.Any(f => n.Contains(f, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void BusWritesTheEventAsTheSchemaSays()
    {
        var id = Guid.CreateVersion7();
        var at = new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

        var json = EventSchemas.Serialize(new AccountUpdated(id, at, [AccountChanges.Email]));

        Assert.Equal(["accountId", "occurredAt", "changes"], json.Select(p => p.Key));
        Assert.Equal(id, json["accountId"]!.GetValue<Guid>());
        Assert.Equal(at, json["occurredAt"]!.GetValue<DateTimeOffset>());
        Assert.Equal("email", Assert.Single(json["changes"]!.AsArray())!.GetValue<string>());
    }

    [Fact]
    public void AddedPropertyIsCompatible()
    {
        var published = Schema("""{"a": {"type": "string"}}""", "a");
        var current = Schema("""{"a": {"type": "string"}, "b": {"type": "integer"}}""", "a", "b");

        Assert.Empty(EventSchemas.Breaks(published, current));
    }

    [Theory]
    [InlineData("""{}""", "", "was removed")]
    [InlineData("""{"a": {"type": "integer"}}""", "a", "may now be integer")]
    [InlineData("""{"a": {"type": ["string", "null"]}}""", "a", "may now be null")]
    [InlineData("""{"a": {"type": "string", "format": "uuid"}}""", "a", "changed its format")]
    [InlineData("""{"a": {"type": "string"}}""", "", "no longer required")]
    public void BreakingChangeIsReported(string currentProperties, string currentRequired, string problem)
    {
        var published = Schema("""{"a": {"type": "string"}}""", "a");
        var current = Schema(currentProperties, currentRequired.Length == 0 ? [] : [currentRequired]);

        Assert.Contains(EventSchemas.Breaks(published, current), p => p.Contains(problem, StringComparison.Ordinal));
    }

    private static JsonObject Generate(string name) =>
        EventSchemas.Generate(AccountEventNames.All[name], name, _descriptions[name]);

    private static string SchemaPath(string name) => Path.Combine(_directory, $"{name}.schema.json");

    private static async Task<JsonObject> ReadAsync(string path) =>
        JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))!.AsObject();

    private static JsonObject Schema(string properties, params string[] required) => new()
    {
        ["type"] = "object",
        ["properties"] = JsonNode.Parse(properties),
        ["required"] = new JsonArray([.. required.Select(r => (JsonNode)r)]),
    };

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "backend", "Platform.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("repository root (backend/Platform.slnx) not found");
    }
}
