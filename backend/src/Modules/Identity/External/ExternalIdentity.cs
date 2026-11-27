using System.Text.RegularExpressions;

namespace Platform.Modules.Identity.External;

/// <summary>
/// One external identity linked to an account (FR-ID-4, ADR-0098): the pair «provider, subject» — who the person is at
/// Yandex, VK, Google, Telegram or, later, at another project of the owner. A pair belongs to at most one account, and
/// an account has at most one identity of each provider. The subject is the provider's own stable identifier of the
/// person (<c>sub</c> of OpenID Connect, the user id of Telegram) and never leaves the Identity module (FR-ID-1).
/// </summary>
public sealed class ExternalIdentity
{
    public const int SubjectMaxLength = 255;

    private ExternalIdentity()
    {
    }

    public Guid Id { get; private set; }

    public Guid AccountId { get; private set; }

    /// <summary>The provider's key, <see cref="ExternalProviders"/>: «yandex», «vk», «google», «telegram».</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>The person's identifier at the provider, exactly as the provider gives it (case matters).</summary>
    public string Subject { get; private set; } = string.Empty;

    public DateTimeOffset LinkedAt { get; private set; }

    internal static ExternalIdentity Link(Guid accountId, string provider, string subject, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        AccountId = accountId,
        Provider = provider,
        Subject = subject,
        LinkedAt = now,
    };
}

/// <summary>
/// The keys of external providers (ADR-0098). A key is lowercase Latin letters, digits and hyphens, 2–32 characters,
/// starting with a letter; the four sign-in providers of FR-AUTH are named here, and another project of the owner gets a
/// key of its own when the accounts are merged (E18-06).
/// </summary>
public static partial class ExternalProviders
{
    public const int MaxLength = 32;

    public const string Yandex = "yandex";
    public const string Vk = "vk";
    public const string Google = "google";
    public const string Telegram = "telegram";

    /// <summary>The provider's name as people know it, for letters; the key itself for a provider not named here.</summary>
    public static string DisplayName(string provider) => provider switch
    {
        Yandex => "Яндекс",
        Vk => "VK",
        Google => "Google",
        Telegram => "Telegram",
        _ => provider,
    };

    /// <summary>Whether <paramref name="provider"/> is a well-formed provider key.</summary>
    public static bool IsValid(string? provider) => provider is not null && KeyPattern().IsMatch(provider);

    /// <summary>Whether <paramref name="subject"/> is a usable subject: not blank, no surrounding spaces, not too long.</summary>
    public static bool IsValidSubject(string? subject) =>
        !string.IsNullOrWhiteSpace(subject)
        && subject.Length <= ExternalIdentity.SubjectMaxLength
        && subject.Trim().Length == subject.Length;

    [GeneratedRegex("^[a-z][a-z0-9-]{1,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}

/// <summary>What happened to a link in the journal (ADR-0098).</summary>
public enum ExternalIdentityAction
{
    Linked = 1,
    Unlinked = 2,
}

/// <summary>
/// One line of the journal of links (ADR-0098): which pair was linked to or unlinked from the account, and when. Rows
/// are only added; they go with the account when it is deleted, because the subject identifies the person.
/// </summary>
public sealed class ExternalIdentityLogEntry
{
    private ExternalIdentityLogEntry()
    {
    }

    public Guid Id { get; private set; }

    public Guid AccountId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public ExternalIdentityAction Action { get; private set; }

    public DateTimeOffset At { get; private set; }

    internal static ExternalIdentityLogEntry Of(ExternalIdentity identity, ExternalIdentityAction action, DateTimeOffset at) =>
        new()
        {
            Id = Guid.CreateVersion7(at),
            AccountId = identity.AccountId,
            Provider = identity.Provider,
            Subject = identity.Subject,
            Action = action,
            At = at,
        };
}
