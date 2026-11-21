using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Messaging;
using Platform.Modules.Identity.Contracts;

namespace Platform.Modules.Blogs;

/// <summary>The personal blog every account has (FR-PROFILE, ADR-0025).</summary>
public sealed class PersonalBlogs(BlogsDbContext db, TimeProvider time)
{
    /// <summary>The personal blog of <paramref name="accountId"/>, or <c>null</c> when it has none yet.</summary>
    public Task<Subsite?> FindAsync(Guid accountId, CancellationToken cancellationToken) =>
        db.Subsites.SingleOrDefaultAsync(
            s => s.OwnerAccountId == accountId && s.Type == SubsiteType.Personal, cancellationToken);

    /// <summary>
    /// Adds the personal blog of <paramref name="accountId"/> to the context when it has none; nothing is saved. The
    /// blog that exists or the one added.
    /// </summary>
    public async Task<Subsite> AddIfMissingAsync(Guid accountId, DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        if (await FindAsync(accountId, cancellationToken) is { } blog)
        {
            return blog;
        }

        blog = Subsite.CreatePersonal(accountId, createdAt);
        db.Subsites.Add(blog);
        return blog;
    }

    /// <summary>
    /// The personal blog of <paramref name="accountId"/>, created now and saved if it is missing: for an account that
    /// signs in before the workers have handled its <see cref="AccountActivated"/>, or that was activated before blogs
    /// existed.
    /// </summary>
    public async Task<Subsite> EnsureAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var blog = await AddIfMissingAsync(accountId, time.GetUtcNow(), cancellationToken);
        if (db.Entry(blog).State != EntityState.Added)
        {
            return blog;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return blog;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: BlogsDbContext.PersonalBlogIndex,
        })
        {
            // The workers (or a parallel request) created it first.
            db.Entry(blog).State = EntityState.Detached;
            return await FindAsync(accountId, cancellationToken)
                ?? throw new InvalidOperationException("The personal blog vanished right after it was created.");
        }
    }
}

/// <summary>
/// Creates the personal blog of an account that has just been activated. Runs in the workers; the blog and the inbox
/// record are saved together after it returns (<see cref="AccountActivatedConsumerDefinition"/>), so a repeat of the
/// message changes nothing.
/// </summary>
public sealed class AccountActivatedConsumer(PersonalBlogs blogs) : IConsumer<AccountActivated>
{
    public async Task Consume(ConsumeContext<AccountActivated> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await blogs.AddIfMissingAsync(context.Message.AccountId, context.Message.ActivatedAt, context.CancellationToken);
    }
}

/// <summary>The inbox of the Blogs schema for <see cref="AccountActivatedConsumer"/> (ADR-0005).</summary>
public sealed class AccountActivatedConsumerDefinition : ModuleConsumerDefinition<AccountActivatedConsumer, BlogsDbContext>;
