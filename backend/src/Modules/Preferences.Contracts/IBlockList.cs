namespace Platform.Modules.Preferences.Contracts;

/// <summary>
/// The black lists of the platform (E11-06, FR-SUB, ADR-0091) as other modules see them: whom a person blocked. A
/// blocked account's content is hidden from the person who blocked it, and it may not write to them — reply to their
/// comments, comment under their posts or, later, send them a message. The Preferences module implements it over its
/// own tables.
/// </summary>
public interface IBlockList
{
    /// <summary>The accounts <paramref name="accountId"/> blocked; empty when none.</summary>
    Task<IReadOnlySet<Guid>> BlockedByAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Whether <paramref name="ownerAccountId"/> blocked <paramref name="accountId"/>.</summary>
    Task<bool> IsBlockedAsync(Guid ownerAccountId, Guid accountId, CancellationToken cancellationToken);
}
