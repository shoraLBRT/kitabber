using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Caching;
using Platform.Http;
using Platform.Persistence;
using Platform.Seeding;
using Platform.Testing.Api;

namespace Platform.Api.IntegrationTests.SampleModule;

/// <summary>
/// A sample module the way a real one plugs into the Api host: its own schema and migrations, endpoints under
/// <c>/api/v1</c>, the clock from <see cref="TimeProvider"/>, the list cached in Redis. It exists to show and check
/// the test framework (ADR-0008).
/// </summary>
public sealed class NotesModule : IApiEndpoints
{
    public const string ModeratorPolicy = "moderator";
    public const string ListCacheKey = "sample-notes:list";

    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<NotesDbContext>(
            configuration.GetConnectionString(IntegrationApiFactory.DatabaseConnectionName)!);
        services.AddPlatformCache(configuration);
        services.AddDemoSeeder<NotesDemoSeeder>();
        services.AddValidation();
        services.AddApiEndpoints<NotesModule>();
        services.AddTestAuthentication();
        services.AddAuthorizationBuilder().AddPolicy(ModeratorPolicy, policy => policy.RequireRole("moderator"));
    }

    public void MapV1(IEndpointRouteBuilder v1)
    {
        var notes = v1.MapGroup("/sample/notes");
        notes.MapGet("/", List);
        notes.MapPost("/", Create).RequireAuthorization();
        notes.MapDelete("/{id:guid}", Delete).RequireAuthorization(ModeratorPolicy);
    }

    private static async Task<Ok<NoteView[]>> List(NotesDbContext db, IDistributedCache cache, CancellationToken ct)
    {
        var cached = await cache.GetStringAsync(ListCacheKey, ct);
        if (cached is not null)
        {
            return TypedResults.Ok(JsonSerializer.Deserialize<NoteView[]>(cached)!);
        }

        var rows = await db.Notes.OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id).ToListAsync(ct);
        var views = rows.Select(NoteView.From).ToArray();
        await cache.SetStringAsync(ListCacheKey, JsonSerializer.Serialize(views), ct);
        return TypedResults.Ok(views);
    }

    private static async Task<Created<NoteView>> Create(
        CreateNote request, ClaimsPrincipal user, NotesDbContext db, IDistributedCache cache, TimeProvider time,
        CancellationToken ct)
    {
        var note = new Note
        {
            Id = Guid.NewGuid(),
            AuthorId = user.FindFirstValue(ClaimTypes.NameIdentifier)!,
            Title = request.Title,
            CreatedAt = time.GetUtcNow(),
        };
        db.Notes.Add(note);
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(ListCacheKey, ct);
        return TypedResults.Created($"/api/v1/sample/notes/{note.Id}", NoteView.From(note));
    }

    private static async Task<Results<NoContent, NotFound>> Delete(
        Guid id, NotesDbContext db, IDistributedCache cache, CancellationToken ct)
    {
        var deleted = await db.Notes.Where(n => n.Id == id).ExecuteDeleteAsync(ct);
        await cache.RemoveAsync(ListCacheKey, ct);
        return deleted == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }
}

public sealed record CreateNote([Required, StringLength(100)] string Title);

public sealed record NoteView(Guid Id, string AuthorId, string Title, DateTimeOffset CreatedAt)
{
    public static NoteView From(Note note) => new(note.Id, note.AuthorId, note.Title, note.CreatedAt);
}

public sealed class NotesDbContext(DbContextOptions<NotesDbContext> options)
    : ModuleDbContext<NotesDbContext>(options), IModuleDbContext
{
    public static string Schema => "notes_api_sample";

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Note>(note =>
        {
            note.Property(n => n.Title).HasMaxLength(100);
            note.Property(n => n.AuthorId).HasMaxLength(100);
        });
    }
}

public sealed class Note
{
    public Guid Id { get; set; }

    public required string AuthorId { get; set; }

    public required string Title { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>For <c>dotnet ef migrations add</c>; no database is contacted.</summary>
public sealed class NotesDesignTimeFactory : IDesignTimeDbContextFactory<NotesDbContext>
{
    public NotesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotesDbContext>();
        ModuleDatabaseServiceCollectionExtensions.UseModuleDatabase<NotesDbContext>(
            options, "Host=localhost;Database=design_time");
        return new NotesDbContext(options.Options);
    }
}
