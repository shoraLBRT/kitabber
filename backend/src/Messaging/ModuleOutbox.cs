using MassTransit;
using MassTransit.DependencyInjection;
using MassTransit.EntityFrameworkCoreIntegration;
using MassTransit.Middleware.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Platform.Messaging;

/// <summary>
/// The outbox of one module (ADR-0005): what is published or sent through it is stored in the outbox tables of
/// <typeparamref name="TContext"/> and leaves for the broker only after that context's <c>SaveChanges</c>, in the same
/// transaction as the module's changes. Several modules in one host each publish through their own
/// <see cref="IModuleOutbox{TContext}"/>; inside a consumer the message goes out with the consumer's own outbox.
/// Registered by <see cref="OutboxRegistrationExtensions.AddModuleOutbox{TContext}"/>; scoped, like the context.
/// </summary>
public interface IModuleOutbox<TContext> : IPublishEndpoint, ISendEndpointProvider
    where TContext : DbContext;

internal sealed class ModuleOutbox<TContext> : IModuleOutbox<TContext>, IDisposable
    where TContext : DbContext
{
    // MassTransit's bus outbox of the context: a plain scope writes to the context's outbox, a consumer's scope to
    // the consume context's outbox in the same context. Created per scope, so it never leaks into another module.
    private readonly EntityFrameworkScopedBusContextProvider<IBus, TContext> _provider;

    public ModuleOutbox(IServiceProvider services, ModuleOutboxNotification<TContext> notification) =>
        _provider = ActivatorUtilities.CreateInstance<EntityFrameworkScopedBusContextProvider<IBus, TContext>>(
            services,
            notification.Notification);

    internal ScopedBusContext Context => _provider.Context;

    private IPublishEndpoint Publisher => Context.PublishEndpoint;

    public void Dispose() => _provider.Dispose();

    public Task Publish<T>(T message, CancellationToken cancellationToken = default)
        where T : class => Publisher.Publish(message, cancellationToken);

    public Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default)
        where T : class => Publisher.Publish(message, publishPipe, cancellationToken);

    public Task Publish<T>(T message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default)
        where T : class => Publisher.Publish(message, publishPipe, cancellationToken);

    public Task Publish(object message, CancellationToken cancellationToken = default) =>
        Publisher.Publish(message, cancellationToken);

    public Task Publish(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) =>
        Publisher.Publish(message, publishPipe, cancellationToken);

    public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default) =>
        Publisher.Publish(message, messageType, cancellationToken);

    public Task Publish(
        object message,
        Type messageType,
        IPipe<PublishContext> publishPipe,
        CancellationToken cancellationToken = default) =>
        Publisher.Publish(message, messageType, publishPipe, cancellationToken);

    public Task Publish<T>(object values, CancellationToken cancellationToken = default)
        where T : class => Publisher.Publish<T>(values, cancellationToken);

    public Task Publish<T>(object values, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default)
        where T : class => Publisher.Publish(values, publishPipe, cancellationToken);

    public Task Publish<T>(object values, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default)
        where T : class => Publisher.Publish<T>(values, publishPipe, cancellationToken);

    public ConnectHandle ConnectPublishObserver(IPublishObserver observer) => Publisher.ConnectPublishObserver(observer);

    public Task<ISendEndpoint> GetSendEndpoint(Uri address) => Context.SendEndpointProvider.GetSendEndpoint(address);

    public ConnectHandle ConnectSendObserver(ISendObserver observer) => Context.SendEndpointProvider.ConnectSendObserver(observer);
}

/// <summary>
/// The scope's <see cref="IPublishEndpoint"/> outside a consumer: the outbox of the host's first module
/// (<see cref="OutboxRegistrationExtensions.AddModuleOutbox{TContext}"/>). It serves shared services that know no
/// module, such as the mailer; a module's own code publishes through its <see cref="IModuleOutbox{TContext}"/>.
/// </summary>
internal sealed class HostBusOutboxProvider<TContext>(IServiceProvider services) : IScopedBusContextProvider<IBus>
    where TContext : DbContext
{
    public ScopedBusContext Context =>
        ((ModuleOutbox<TContext>)services.GetRequiredService<IModuleOutbox<TContext>>()).Context;
}

/// <summary>
/// Wakes the delivery service of one module's outbox after that module saves: a publisher and a delivery service per
/// context, never shared, since MassTransit's notification holds a single waiter.
/// </summary>
internal sealed class ModuleOutboxNotification<TContext>(IOptions<OutboxDeliveryServiceOptions> options)
    where TContext : DbContext
{
    public IBusOutboxNotification Notification { get; } = new BusOutboxNotification(options);
}

/// <summary>Marks that the host's bus outbox is taken (by the first <c>AddModuleOutbox</c>).</summary>
internal sealed record HostBusOutbox(Type Context);
