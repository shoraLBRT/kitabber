using System.Globalization;
using System.Text.RegularExpressions;

namespace Platform.Modules.Blogs;

/// <summary>What a <see cref="Subsite"/> is (FR-PROFILE, as on DTF: one entity with a type).</summary>
public enum SubsiteType
{
    /// <summary>The personal blog of an account: every account has exactly one.</summary>
    Personal = 1,

    /// <summary>A topic (FR-SUB): many authors post into it; created by the editors.</summary>
    Topic = 2,
}

/// <summary>
/// A blog (ADR-0023): the personal blog of an account or a topic. The owner is known only by the account_id
/// (FR-ID-1); the name, the description and the nickname are the profile people see.
/// </summary>
public sealed class Subsite
{
    public Guid Id { get; init; }

    public SubsiteType Type { get; init; }

    /// <summary>The account whose personal blog this is; <c>null</c> for a topic.</summary>
    public Guid? OwnerAccountId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>The name, at most <see cref="SubsiteRules.NameMaxLength"/> characters; <c>null</c> until it is set.</summary>
    public string? Name { get; private set; }

    /// <summary>The description, at most <see cref="SubsiteRules.DescriptionMaxLength"/> characters.</summary>
    public string? Description { get; private set; }

    /// <summary>The short address in lower case (<see cref="SubsiteRules.NormalizeNickname"/>); <c>null</c> until it is set.</summary>
    public string? Nickname { get; private set; }

    /// <summary>The personal blog of <paramref name="ownerAccountId"/>, created at <paramref name="createdAt"/>, with an empty profile.</summary>
    public static Subsite CreatePersonal(Guid ownerAccountId, DateTimeOffset createdAt)
    {
        if (ownerAccountId == Guid.Empty)
        {
            throw new ArgumentException("A personal blog needs its owner's account_id.", nameof(ownerAccountId));
        }

        return new Subsite
        {
            Id = Guid.CreateVersion7(createdAt),
            Type = SubsiteType.Personal,
            OwnerAccountId = ownerAccountId,
            CreatedAt = createdAt,
        };
    }

    /// <summary>Sets the name (trimmed; empty clears it). Throws when it is longer than the rule allows.</summary>
    public void Rename(string? name)
    {
        var value = SubsiteRules.Clean(name);
        if (!SubsiteRules.IsValidName(value))
        {
            throw new ArgumentException($"The name is longer than {SubsiteRules.NameMaxLength} characters.", nameof(name));
        }

        Name = value;
    }

    /// <summary>Sets the description (trimmed; empty clears it). Throws when it is longer than the rule allows.</summary>
    public void Describe(string? description)
    {
        var value = SubsiteRules.Clean(description);
        if (!SubsiteRules.IsValidDescription(value))
        {
            throw new ArgumentException(
                $"The description is longer than {SubsiteRules.DescriptionMaxLength} characters.", nameof(description));
        }

        Description = value;
    }

    /// <summary>
    /// Sets the nickname. Only its form is checked here; whether it is taken or reserved, and how often it may change,
    /// is E04-05.
    /// </summary>
    public void SetNickname(string? nickname)
    {
        var value = SubsiteRules.NormalizeNickname(nickname)
            ?? throw new ArgumentException("Not a nickname the platform accepts.", nameof(nickname));
        Nickname = value;
    }
}

/// <summary>The rules of a blog's profile (FR-PROFILE).</summary>
public static partial class SubsiteRules
{
    /// <summary>The name: at most 25 characters (FR-PROFILE).</summary>
    public const int NameMaxLength = 25;

    /// <summary>The description: at most 160 characters (FR-PROFILE).</summary>
    public const int DescriptionMaxLength = 160;

    public const int NicknameMinLength = 3;
    public const int NicknameMaxLength = 32;

    /// <summary>
    /// The length a person sees: text elements (grapheme clusters), so an emoji or a letter with a combining mark is
    /// one character, the way the client counts it.
    /// </summary>
    public static int LengthOf(string? text) => string.IsNullOrEmpty(text) ? 0 : new StringInfo(text).LengthInTextElements;

    public static bool IsValidName(string? name) => LengthOf(Clean(name)) <= NameMaxLength;

    public static bool IsValidDescription(string? description) => LengthOf(Clean(description)) <= DescriptionMaxLength;

    /// <summary>
    /// The nickname in its stored form: trimmed, without a leading <c>@</c>, in lower case; Latin letters, digits and
    /// <c>_</c>, from <see cref="NicknameMinLength"/> to <see cref="NicknameMaxLength"/> characters, starting with a
    /// letter. <c>id</c> followed by digits only is the address of a blog without a nickname (<c>id123</c>), so it is
    /// never a nickname. <c>null</c> when <paramref name="nickname"/> is not one.
    /// </summary>
    public static string? NormalizeNickname(string? nickname)
    {
        var value = nickname?.Trim().TrimStart('@').ToLowerInvariant();
        return value is not null && NicknameForm().IsMatch(value) && !IdForm().IsMatch(value) ? value : null;
    }

    /// <summary>Trimmed text, or <c>null</c> when nothing is left.</summary>
    internal static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    [GeneratedRegex("^[a-z][a-z0-9_]{2,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex NicknameForm();

    [GeneratedRegex("^id[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex IdForm();
}
