namespace Platform.Modules.Identity;

/// <summary>
/// An account (FR-ID-1): the global identity of a person, the part that a future shared account service takes over.
/// The profile (name, nickname, avatar) is not here: it belongs to the Blogs module and refers to the account only by
/// <see cref="Id"/>. Mail, external sign-ins, sessions and consents join the account with their own tasks (E18).
/// </summary>
public sealed class Account
{
    /// <summary>The account_id: a UUID v7 for every account the platform creates (ADR-0002).</summary>
    public Guid Id { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>A new account with a UUID v7 identifier taken at the current time of <paramref name="time"/>.</summary>
    public static Account Create(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        var now = time.GetUtcNow();
        return new Account { Id = AccountIds.New(now), CreatedAt = now };
    }
}

/// <summary>The rules of account_id (FR-ID-1): a UUID v7, ordered by the time the account was created.</summary>
public static class AccountIds
{
    /// <summary>A UUID v7 whose timestamp is <paramref name="createdAt"/>.</summary>
    public static Guid New(DateTimeOffset createdAt) => Guid.CreateVersion7(createdAt);

    /// <summary>Whether <paramref name="id"/> is a UUID v7 of the RFC 9562 variant.</summary>
    public static bool IsVersion7(Guid id) => id.Version == 7 && id.Variant is >= 0x8 and <= 0xB;
}
