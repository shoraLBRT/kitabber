using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Platform.Messaging;

/// <summary>
/// The transactional outbox of a module (ADR-0005). The module's context keeps the outbox tables in its own schema
/// (<see cref="AddOutboxTables"/>), events published in a request are stored with the module's changes in the same
/// <c>SaveChanges</c> and sent to the broker afterwards; consumers dedupe redelivered messages through the inbox
/// (<see cref="ModuleConsumerDefinition{TConsumer, TContext}"/>).
/// </summary>
public static class OutboxRegistrationExtensions
{
    /// <summary>
    /// Adds <c>inbox_state</c>, <c>outbox_message</c> and <c>outbox_state</c> to the module's model; call it from
    /// <c>OnModelCreating</c>. The tables go into the module schema and into its migrations.
    /// </summary>
    public static ModelBuilder AddOutboxTables(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.AddTransactionalOutboxEntities();
        return modelBuilder;
    }

    /// <summary>
    /// Registers the outbox of <typeparamref name="TContext"/> on PostgreSQL and makes it the bus outbox: within a
    /// scope, <see cref="IPublishEndpoint"/> and <see cref="ISendEndpointProvider"/> write to this context, and the
    /// delivery service of this host sends the stored messages.
    /// </summary>
    public static IBusRegistrationConfigurator AddModuleOutbox<TContext>(
        this IBusRegistrationConfigurator bus,
        Action<IEntityFrameworkOutboxConfigurator>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(bus);
        bus.AddEntityFrameworkOutbox<TContext>(outbox =>
        {
            outbox.UsePostgres();
            outbox.UseBusOutbox();
            outbox.QueryDelay = TimeSpan.FromSeconds(1);
            outbox.DuplicateDetectionWindow = TimeSpan.FromDays(1);
            configure?.Invoke(outbox);
        });
        return bus;
    }
}
