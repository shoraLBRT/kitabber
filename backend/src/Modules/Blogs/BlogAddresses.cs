using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Modules.Blogs.Topics;

namespace Platform.Modules.Blogs;

/// <summary>
/// The one address space of personal blogs and topics (ADR-0084, ADR-0090): a topic lives at <c>/&lt;address&gt;</c> and a
/// personal blog at <c>/u/&lt;nickname&gt;</c>, but <c>/&lt;nickname&gt;</c> leads to the blog too, so a nickname and a
/// topic's address are checked by the same rules: the reserved addresses and the stop list, the nicknames of other blogs
/// and topics, and the nicknames other blogs gave up less than <see cref="SubsiteRules.FormerNicknameHold"/> ago.
/// </summary>
public static class BlogAddresses
{
    /// <summary>
    /// Whether <paramref name="nickname"/>, in the stored form, may never be taken: a reserved address of the site
    /// (<see cref="TopicCreationRules.ReservedAddresses"/>) or a word of the stop list.
    /// </summary>
    public static bool IsRefused(string nickname, TopicLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        return TopicCreationRules.IsReservedAddress(nickname)
            || TopicCreationRules.StopWordIn(nickname, limits.StopWords) is not null;
    }

    /// <summary>
    /// Whether <paramref name="nickname"/>, in the stored form, belongs at <paramref name="now"/> to a blog or topic other
    /// than <paramref name="subsiteId"/> (<c>null</c> for a topic not made yet): it is its nickname, or a nickname it gave
    /// up within <see cref="SubsiteRules.FormerNicknameHold"/>.
    /// </summary>
    public static async Task<bool> IsTakenAsync(
        BlogsDbContext db, string nickname, Guid? subsiteId, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var others = db.Subsites.Where(s => s.Nickname == nickname);
        var formers = db.FormerNicknames.Where(f => f.Nickname == nickname);
        if (subsiteId is { } id)
        {
            others = others.Where(s => s.Id != id);
            formers = formers.Where(f => f.SubsiteId != id);
        }

        if (await others.AnyAsync(ct))
        {
            return true;
        }

        var heldSince = now - SubsiteRules.FormerNicknameHold;
        return await formers.AnyAsync(f => f.ReleasedAt > heldSince, ct);
    }

    /// <summary>
    /// Marks the record of <paramref name="nickname"/> as given up by a blog for removal, so its address stops leading
    /// there once a blog or topic takes it; nothing is saved. Call it after <see cref="IsTakenAsync"/> said it is free.
    /// </summary>
    public static async Task ClaimAsync(BlogsDbContext db, string nickname, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (await db.FormerNicknames.SingleOrDefaultAsync(f => f.Nickname == nickname, ct) is { } former)
        {
            db.FormerNicknames.Remove(former);
        }
    }

    /// <summary>
    /// Keeps <paramref name="nickname"/> as given up by <paramref name="subsiteId"/> at <paramref name="now"/>; nothing is
    /// saved. A nickname in use has no such record: taking it removed the record (<see cref="ClaimAsync"/>).
    /// </summary>
    public static void Release(BlogsDbContext db, string nickname, Guid subsiteId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(db);
        db.FormerNicknames.Add(new FormerNickname { Nickname = nickname, SubsiteId = subsiteId, ReleasedAt = now });
    }

    /// <summary>
    /// The blog a former nickname leads to (ADR-0090), or <c>null</c> when no blog gave it up; the caller answers with the
    /// blog's own address.
    /// </summary>
    public static Task<Guid?> FormerOwnerAsync(BlogsDbContext db, string nickname, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.FormerNicknames.AsNoTracking()
            .Where(f => f.Nickname == nickname)
            .Select(f => (Guid?)f.SubsiteId)
            .SingleOrDefaultAsync(ct);
    }

    /// <summary>
    /// Whether a save failed because another request took the same address in between: the unique nickname index or the
    /// key of the former nicknames.
    /// </summary>
    public static bool IsAddressRace(DbUpdateException e) =>
        e?.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: BlogsDbContext.NicknameIndex or BlogsDbContext.FormerNicknameKey,
        };
}
