using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Platform.Api.IntegrationTests.SampleModule;
using Platform.Testing.Api;
using Platform.Testing.Containers;
using Platform.Testing.Time;

[assembly: AssemblyFixture(typeof(PostgresFixture))]
[assembly: AssemblyFixture(typeof(RedisFixture))]

namespace Platform.Api.IntegrationTests;

/// <summary>The Api host with the sample module, on PostgreSQL and Redis in Testcontainers.</summary>
public sealed class NotesApi : IntegrationApiFactory
{
    public NotesApi(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis) => WithModule(NotesModule.Register);
}

/// <summary>
/// The example of an API integration test (ADR-0008): the happy path, access rights and a validation error, with
/// the clock and a clean database and cache for every test.
/// </summary>
public sealed class NotesApiTests(NotesApi api) : IClassFixture<NotesApi>, IAsyncLifetime
{
    private const string _path = "/api/v1/sample/notes";
    private HttpClient _client = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        // The first reset starts the host; a client created before it would start it without the containers.
        await api.ResetAsync();
        _client = api.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AuthorCreatesNotesStampedByTheTestClock()
    {
        var first = await CreateAsync("account-1", "Первая");
        api.Time.Advance(TimeSpan.FromMinutes(5));
        var second = await CreateAsync("account-1", "Вторая");

        Assert.Equal(api.Time.GetUtcNow() - TimeSpan.FromMinutes(5), first.CreatedAt);
        Assert.Equal(TimeSpan.FromMinutes(5), second.CreatedAt - first.CreatedAt);
        Assert.Equal("account-1", first.AuthorId);
        var list = await _client.GetFromJsonAsync<NoteView[]>(_path, Ct);
        Assert.Equal(["Вторая", "Первая"], list!.Select(n => n.Title));
    }

    [Fact]
    public async Task ModeratorDeletesANoteArrangedByTheFactory()
    {
        var notes = new NoteFactory(api.Data);
        var kept = notes.Build();
        var doomed = notes.Build(n => n.Title = "Удалить");
        await api.SaveAsync<NotesDbContext>(kept, doomed);

        using var response = await SendAsync(HttpMethod.Delete, $"{_path}/{doomed.Id}", "account-2", "moderator");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var list = await _client.GetFromJsonAsync<NoteView[]>(_path, Ct);
        Assert.Equal([NoteView.From(kept)], list!);
    }

    [Fact]
    public async Task DemoDataIsServedByTheApi()
    {
        var added = await api.SeedDemoAsync();

        var list = await _client.GetFromJsonAsync<NoteView[]>(_path, Ct);

        Assert.Equal(NotesDemoSeeder.Count, added);
        Assert.Equal(NotesDemoSeeder.Count, list!.Length);
        Assert.Equal(list.OrderByDescending(n => n.CreatedAt).Select(n => n.Id), list.Select(n => n.Id));
        Assert.Equal(0, await api.SeedDemoAsync());
    }

    [Fact]
    public async Task ClockStartsAtTheFixedInstant()
    {
        Assert.True(api.Time.GetUtcNow() >= TestClock.Start);
        await using var scope = api.Services.CreateAsyncScope();
        Assert.Same(api.Time, scope.ServiceProvider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public async Task AnonymousCannotCreate()
    {
        using var response = await _client.PostAsJsonAsync(_path, new { title = "без входа" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await CountNotesAsync());
    }

    [Fact]
    public async Task OnlyModeratorDeletes()
    {
        var note = await CreateAsync("account-1", "Удалить");

        using var asAuthor = await SendAsync(HttpMethod.Delete, $"{_path}/{note.Id}", "account-1");
        using var asModerator = await SendAsync(HttpMethod.Delete, $"{_path}/{note.Id}", "account-2", "moderator");

        Assert.Equal(HttpStatusCode.Forbidden, asAuthor.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, asModerator.StatusCode);
        Assert.Equal(0, await CountNotesAsync());
    }

    [Fact]
    public async Task TooLongTitleIsAValidationProblem()
    {
        using var response = await SendAsync(HttpMethod.Post, _path, "account-1", body: new { title = new string('x', 101) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("title", out _), problem.ToString());
        Assert.Equal(0, await CountNotesAsync());
    }

    [Fact]
    public async Task ListIsCachedInRedis()
    {
        await CreateAsync("account-1", "В кэше");

        await _client.GetFromJsonAsync<NoteView[]>(_path, Ct);

        var cache = api.Services.GetRequiredService<IDistributedCache>();
        var cached = await cache.GetStringAsync(NotesModule.ListCacheKey, Ct);
        Assert.Contains("В кэше", JsonSerializer.Deserialize<NoteView[]>(cached!)!.Select(n => n.Title));
    }

    /// <summary>Runs twice: each run leaves a note and a cache entry, and each starts without them.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task EveryTestStartsWithAnEmptyDatabaseAndCache(int run)
    {
        var cache = api.Services.GetRequiredService<IDistributedCache>();
        Assert.Equal(0, await CountNotesAsync());
        Assert.Null(await cache.GetStringAsync(NotesModule.ListCacheKey, Ct));

        await CreateAsync("account-1", $"Прогон {run}");
        await _client.GetFromJsonAsync<NoteView[]>(_path, Ct);

        Assert.Equal(1, await CountNotesAsync());
        Assert.NotNull(await cache.GetStringAsync(NotesModule.ListCacheKey, Ct));
    }

    private async Task<NoteView> CreateAsync(string account, string title)
    {
        using var response = await SendAsync(HttpMethod.Post, _path, account, body: new { title });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<NoteView>(Ct))!;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string account, string? role = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.As(account, role is null ? [] : [role]);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task<int> CountNotesAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NotesDbContext>().Notes.CountAsync(Ct);
    }
}
