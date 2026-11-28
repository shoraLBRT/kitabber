using MassTransit;
using Microsoft.EntityFrameworkCore;
using Platform.Messaging;
using Platform.Modules.Identity.Contracts;

namespace Platform.Modules.Blogs.Topics;

/// <summary>
/// Erases the topic roles of a deleted account (FR-ID-5, ADR-0087, ADR-0121): its appointments as a moderator and the
/// transfer offers it made or received. The removal and the inbox record are saved together after it returns
/// (<see cref="AccountDeletedBlogsConsumerDefinition"/>); a repeat, or an account without such rows, finds nothing.
/// </summary>
public sealed class AccountDeletedBlogsConsumer(BlogsDbContext db) : IConsumer<AccountDeleted>
{
    public async Task Consume(ConsumeContext<AccountDeleted> context)
    {
        var account = context.Message.AccountId;
        db.TopicModerators.RemoveRange(await db.TopicModerators
            .Where(m => m.AccountId == account)
            .ToListAsync(context.CancellationToken));
        db.TopicTransferOffers.RemoveRange(await db.TopicTransferOffers
            .Where(o => o.FromAccountId == account || o.ToAccountId == account)
            .ToListAsync(context.CancellationToken));
    }
}

/// <summary>The inbox of the Blogs schema for <see cref="AccountDeletedBlogsConsumer"/> (ADR-0005).</summary>
public sealed class AccountDeletedBlogsConsumerDefinition
    : ModuleConsumerDefinition<AccountDeletedBlogsConsumer, BlogsDbContext>;
