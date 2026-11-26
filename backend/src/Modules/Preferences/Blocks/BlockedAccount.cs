namespace Platform.Modules.Preferences.Blocks;

/// <summary>
/// One entry of a person's black list (E11-06, FR-SUB, ADR-0091): <see cref="OwnerAccountId"/> blocked
/// <see cref="AccountId"/>. Both are only account identifiers, with no foreign key to the schema <c>identity</c>
/// (FR-ID-1).
/// </summary>
public sealed class BlockedAccount
{
    public Guid OwnerAccountId { get; init; }

    public Guid AccountId { get; init; }

    public DateTimeOffset BlockedAt { get; init; }
}

/// <summary>The rules of the black list, without a database (ADR-0091).</summary>
public static class BlockRules
{
    /// <summary>
    /// The most accounts one person may block (предложение, требует подтверждения): enough for a person, and it keeps
    /// the list a reader's feeds filter by small.
    /// </summary>
    public const int MaxBlocked = 1000;

    /// <summary>Why <paramref name="owner"/> may not block <paramref name="account"/>, or <c>null</c> when they may.</summary>
    public static string? BlockProblem(Guid owner, Guid account, int alreadyBlocked) =>
        account == Guid.Empty ? "Не указан аккаунт."
        : account == owner ? "Себя заблокировать нельзя."
        : alreadyBlocked >= MaxBlocked ? $"В чёрном списке не больше {MaxBlocked} аккаунтов."
        : null;
}
