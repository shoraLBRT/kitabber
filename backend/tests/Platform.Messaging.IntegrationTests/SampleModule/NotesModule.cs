using System.Collections.Concurrent;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Platform.Persistence;

namespace Platform.Messaging.IntegrationTests.SampleModule;

/// <summary>
/// A sample module with an outbox: it stores notes and publishes <see cref="NoteCreated"/>; its consumer records
/// every handled event in <c>processed_events</c>. Real modules follow the same shape (ADR-0005).
/// </summary>
public sealed class NotesDbContext(DbContextOptions<NotesDbContext> options)
    : ModuleDbContext<NotesDbContext>(options), IModuleDbContext
{
    public static string Schema => "notes_sample";

    public DbSet<Note> Notes => Set<Note>();

    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Note>(note => note.Property(n => n.Title).HasMaxLength(200));
        modelBuilder.AddOutboxTables();
    }
}

public sealed class Note
{
    public Guid Id { get; set; }

    public required string Title { get; set; }
}

public sealed class ProcessedEvent
{
    public Guid Id { get; set; }

    public Guid NoteId { get; set; }
}

/// <summary>The module's event contract.</summary>
public sealed record NoteCreated(Guid NoteId);

/// <summary>What consumers saw, shared by the test and every host it starts.</summary>
public sealed class ReceivedLog
{
    private readonly ConcurrentQueue<Guid> _ids = new();

    public void Add(Guid id) => _ids.Enqueue(id);

    public int Count(Guid id) => _ids.Count(x => x == id);

    public int Total => _ids.Count;
}

public sealed class NoteCreatedConsumer(NotesDbContext db, ReceivedLog log) : IConsumer<NoteCreated>
{
    public Task Consume(ConsumeContext<NoteCreated> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        db.ProcessedEvents.Add(new ProcessedEvent { Id = Guid.NewGuid(), NoteId = context.Message.NoteId });
        log.Add(context.Message.NoteId);
        return Task.CompletedTask;
    }
}

public sealed class NoteCreatedConsumerDefinition : ModuleConsumerDefinition<NoteCreatedConsumer, NotesDbContext>;
