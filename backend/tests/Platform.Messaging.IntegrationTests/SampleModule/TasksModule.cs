using MassTransit;
using Microsoft.EntityFrameworkCore;
using Platform.Persistence;

namespace Platform.Messaging.IntegrationTests.SampleModule;

/// <summary>
/// A second sample module with its own schema and outbox, for a host where two modules publish (#281, ADR-0005).
/// </summary>
public sealed class TasksDbContext(DbContextOptions<TasksDbContext> options)
    : ModuleDbContext<TasksDbContext>(options), IModuleDbContext
{
    public static string Schema => "tasks_sample";

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<TaskItem>(task => task.Property(t => t.Title).HasMaxLength(200));
        modelBuilder.AddOutboxTables();
    }
}

public sealed class TaskItem
{
    public Guid Id { get; set; }

    public required string Title { get; set; }
}

/// <summary>The module's event contract.</summary>
public sealed record TaskCreated(Guid TaskId);

public sealed class TaskCreatedConsumer(ReceivedLog log) : IConsumer<TaskCreated>
{
    public Task Consume(ConsumeContext<TaskCreated> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        log.Add(context.Message.TaskId);
        return Task.CompletedTask;
    }
}

public sealed class TaskCreatedConsumerDefinition : ModuleConsumerDefinition<TaskCreatedConsumer, TasksDbContext>;
