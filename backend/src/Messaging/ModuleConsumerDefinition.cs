using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Platform.Messaging;

/// <summary>
/// Endpoint settings for a consumer of a module that keeps an outbox: the message id goes to the module's
/// <c>inbox_state</c>, so a redelivered message is handled once; the consumer's changes in
/// <typeparamref name="TContext"/> and the messages it publishes are saved in one transaction after it returns
/// (the consumer does not call <c>SaveChanges</c>).
/// </summary>
public abstract class ModuleConsumerDefinition<TConsumer, TContext> : ConsumerDefinition<TConsumer>
    where TConsumer : class, IConsumer
    where TContext : DbContext
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<TConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(endpointConfigurator);
        endpointConfigurator.UseEntityFrameworkOutbox<TContext>(context);
    }
}
