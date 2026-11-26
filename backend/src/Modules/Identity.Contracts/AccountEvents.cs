using MassTransit;

namespace Platform.Modules.Identity.Contracts;

/// <summary>
/// The account was created (FR-ID-5, ADR-0087): it has just got its first confirmed way to sign in, the moment it is
/// here to stay (ADR-0025). A registration still waiting for its code is not an account and is never announced.
/// </summary>
/// <param name="AccountId">The account_id (UUID v7), the <c>sub</c> of its tokens.</param>
/// <param name="OccurredAt">When the account was created (activated).</param>
[MessageUrn(AccountEventNames.Created)]
[EntityName(AccountEventNames.Created)]
public sealed record AccountCreated(Guid AccountId, DateTimeOffset OccurredAt);

/// <summary>
/// An email of the account was confirmed by a code (FR-ID-3, ADR-0020): the first one, with <see cref="AccountCreated"/>,
/// or a new one that replaced the confirmed email. The address itself is not in the event (FR-ID-1): a project that
/// needs it reads the <c>email</c> claim of the account's tokens.
/// </summary>
/// <param name="AccountId">The account_id (UUID v7).</param>
/// <param name="OccurredAt">When the email was confirmed.</param>
[MessageUrn(AccountEventNames.EmailVerified)]
[EntityName(AccountEventNames.EmailVerified)]
public sealed record AccountEmailVerified(Guid AccountId, DateTimeOffset OccurredAt);

/// <summary>
/// The account's own data changed after it was created (FR-ID-5): <see cref="Changes"/> names what, from
/// <see cref="AccountChanges"/>. The new values are not in the event; a project that keeps a copy reads them again.
/// </summary>
/// <param name="AccountId">The account_id (UUID v7).</param>
/// <param name="OccurredAt">When the change was made.</param>
/// <param name="Changes">What changed, at least one name from <see cref="AccountChanges"/>.</param>
[MessageUrn(AccountEventNames.Updated)]
[EntityName(AccountEventNames.Updated)]
public sealed record AccountUpdated(Guid AccountId, DateTimeOffset OccurredAt, IReadOnlyList<string> Changes);

/// <summary>
/// The account was deleted (FR-ID-5): every module and project that keeps data by this account_id erases it, and must
/// handle a repeat without harm. The account_id is never given to another account.
/// </summary>
/// <param name="AccountId">The account_id (UUID v7).</param>
/// <param name="OccurredAt">When the account was deleted.</param>
[MessageUrn(AccountEventNames.Deleted)]
[EntityName(AccountEventNames.Deleted)]
public sealed record AccountDeleted(Guid AccountId, DateTimeOffset OccurredAt);

/// <summary>
/// The names of the account events (ADR-0087): <c>account.&lt;event&gt;.v&lt;version&gt;</c>. A name is both the
/// message type (<c>urn:message:&lt;name&gt;</c>) and the exchange the event is published to. A version never changes
/// incompatibly: such a change is a new version, published next to the old one until no one reads the old one.
/// </summary>
public static class AccountEventNames
{
    public const string Created = "account.created.v1";
    public const string EmailVerified = "account.email_verified.v1";
    public const string Updated = "account.updated.v1";
    public const string Deleted = "account.deleted.v1";

    /// <summary>Every account event with its type; docs/contracts has a schema for each.</summary>
    public static IReadOnlyDictionary<string, Type> All { get; } = new Dictionary<string, Type>
    {
        [Created] = typeof(AccountCreated),
        [EmailVerified] = typeof(AccountEmailVerified),
        [Updated] = typeof(AccountUpdated),
        [Deleted] = typeof(AccountDeleted),
    };
}

/// <summary>What <see cref="AccountUpdated.Changes"/> may name; a new name is a compatible change of the event.</summary>
public static class AccountChanges
{
    /// <summary>The confirmed email was replaced by another confirmed one.</summary>
    public const string Email = "email";
}
