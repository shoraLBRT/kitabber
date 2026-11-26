using MassTransit;
using Platform.Messaging;

namespace Platform.Modules.Blogs;

/// <summary>
/// The former Identity event <c>AccountActivated</c> as it lies on the bus: the message type and the exchange of the
/// removed <c>Platform.Modules.Identity.Contracts.AccountActivated</c>. Identity no longer publishes it; only
/// <see cref="AccountActivatedConsumer"/> reads it, to drain what was queued before the switch to
/// <c>account.created.v1</c> (ADR-0087, item 9). Not a contract: removed together with that consumer (#424).
/// </summary>
/// <param name="AccountId">The account_id (UUID v7).</param>
/// <param name="ActivatedAt">When the account was activated.</param>
[MessageUrn(Name)]
[EntityName(Name)]
public sealed record LegacyAccountActivated(Guid AccountId, DateTimeOffset ActivatedAt)
{
    /// <summary>The message type (after <c>urn:message:</c>) and the exchange the old event had.</summary>
    public const string Name = "Platform.Modules.Identity.Contracts:AccountActivated";
}

/// <summary>
/// Drains the queue <c>account-activated</c> of the former event (<see cref="LegacyAccountActivated"/>) for one release
/// after the switch to <c>account.created.v1</c>: a message queued during that deploy still creates the blog, and the
/// repeat by <see cref="AccountCreatedBlogsConsumer"/> changes nothing. The class name keeps the old queue name.
/// </summary>
public sealed class AccountActivatedConsumer(PersonalBlogs blogs) : IConsumer<LegacyAccountActivated>
{
    public async Task Consume(ConsumeContext<LegacyAccountActivated> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await blogs.AddIfMissingAsync(context.Message.AccountId, context.Message.ActivatedAt, context.CancellationToken);
    }
}

/// <summary>The inbox of the Blogs schema for <see cref="AccountActivatedConsumer"/> (ADR-0005).</summary>
public sealed class AccountActivatedConsumerDefinition : ModuleConsumerDefinition<AccountActivatedConsumer, BlogsDbContext>;
