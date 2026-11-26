using MassTransit;
using MassTransit.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    /// Registers the outbox of <typeparamref name="TContext"/> on PostgreSQL: the module publishes and sends through
    /// <see cref="IModuleOutbox{TContext}"/>, which writes to this context, and the delivery service of this host sends
    /// the stored messages. Each module of a host calls it for its own context, so every module writes to the outbox
    /// of its own schema. The first module registered also backs the scope's plain <see cref="IPublishEndpoint"/>
    /// and <see cref="ISendEndpointProvider"/>, which shared services such as the mailer use; later calls leave it.
    /// </summary>
    public static IBusRegistrationConfigurator AddModuleOutbox<TContext>(
        this IBusRegistrationConfigurator bus,
        Action<IEntityFrameworkOutboxConfigurator>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(bus);
        if (bus.Any(d => d.ServiceType == typeof(IModuleOutbox<TContext>)))
        {
            throw new InvalidOperationException($"The outbox of {typeof(TContext).Name} is already registered.");
        }

        // UseBusOutbox adds the delivery service of this context and points the scope's IPublishEndpoint at it; the
        // pointer is set back below, so the host's first module keeps it whatever is registered after it.
        bus.AddEntityFrameworkOutbox<TContext>(outbox =>
        {
            outbox.UsePostgres();
            outbox.UseBusOutbox();
            outbox.QueryDelay = TimeSpan.FromSeconds(1);
            outbox.DuplicateDetectionWindow = TimeSpan.FromDays(1);
            configure?.Invoke(outbox);
        });
        bus.AddScoped<IModuleOutbox<TContext>, ModuleOutbox<TContext>>();

        var host = bus.FirstOrDefault(d => d.ServiceType == typeof(HostBusOutbox))?.ImplementationInstance as HostBusOutbox;
        if (host is null)
        {
            host = new HostBusOutbox(typeof(TContext));
            bus.AddSingleton(host);
        }

        bus.Replace(ServiceDescriptor.Scoped(
            typeof(IScopedBusContextProvider<IBus>),
            typeof(HostBusOutboxProvider<>).MakeGenericType(host.Context)));
        return bus;
    }
}
