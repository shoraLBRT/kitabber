using System.Globalization;
using System.Text.RegularExpressions;

namespace Platform.Modules.Blogs;

/// <summary>What a <see cref="Subsite"/> is (FR-PROFILE, as on DTF: one entity with a type).</summary>
public enum SubsiteType
{
    /// <summary>The personal blog of an account: every account has exactly one.</summary>
    Personal = 1,

    /// <summary>A topic (FR-TOPIC, ADR-0082): many authors post into it; it has an owner who runs it.</summary>
    Topic = 2,
}

/// <summary>
/// A blog (ADR-0025): the personal blog of an account or a topic. The owner is known only by the account_id
/// (FR-ID-1); the name, the description and the nickname are the profile people see.
/// </summary>
public sealed class Subsite
{
    public Guid Id { get; init; }

    public SubsiteType Type { get; init; }

    /// <summary>
    /// The account whose personal blog this is, or the owner of a topic (E11-01, ADR-0082). Only a topic made before the
    /// owners came may have none.
    /// </summary>
    public Guid? OwnerAccountId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>The name, at most <see cref="SubsiteRules.NameMaxLength"/> characters; <c>null</c> until it is set.</summary>
    public string? Name { get; private set; }

    /// <summary>The description, at most <see cref="SubsiteRules.DescriptionMaxLength"/> characters.</summary>
    public string? Description { get; private set; }

    /// <summary>The short address in lower case (<see cref="SubsiteRules.NormalizeNickname"/>); <c>null</c> until it is set.</summary>
    public string? Nickname { get; private set; }

    /// <summary>
    /// The avatar: an image uploaded through the Media module (ADR-0034), already cropped by the client; <c>null</c>
    /// until it is set. Only the identifier is kept here, the file stays in the schema <c>media</c>.
    /// </summary>
    public Guid? AvatarMediaId { get; private set; }

    /// <summary>
    /// The cover (E04-04, ADR-0079): an image uploaded through the Media module, kept whole; <c>null</c> until it is set.
    /// </summary>
    public Guid? CoverMediaId { get; private set; }

    /// <summary>
    /// Which part of the cover the header shows (ADR-0079): the vertical offset in percent, from 0 (the top edge of the
    /// picture) to 100 (the bottom edge), as CSS <c>object-position</c> reads it; <see cref="SubsiteRules.CoverPositionDefault"/>
    /// without a cover.
    /// </summary>
    public int CoverPosition { get; private set; } = SubsiteRules.CoverPositionDefault;

    /// <summary>
    /// A topic named <paramref name="name"/> at the address <paramref name="nickname"/>, owned by
    /// <paramref name="ownerAccountId"/> (E11-01, ADR-0082). Topics are made by the administration (FR-TOPIC) and, later,
    /// by people (E11-11, #77); until those tools come, only the demo data and the tests make them.
    /// </summary>
    public static Subsite CreateTopic(string name, string nickname, Guid ownerAccountId, DateTimeOffset createdAt)
    {
        if (ownerAccountId == Guid.Empty)
        {
            throw new ArgumentException("A topic needs its owner's account_id.", nameof(ownerAccountId));
        }

        var topic = new Subsite
        {
            Id = Guid.CreateVersion7(createdAt),
            Type = SubsiteType.Topic,
            OwnerAccountId = ownerAccountId,
            CreatedAt = createdAt,
        };
        topic.Rename(name);
        topic.SetNickname(nickname);
        return topic;
    }

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
    /// Sets the avatar to the uploaded image <paramref name="mediaId"/>, or removes it with <c>null</c>. Whether the file
    /// is an image of the owner is checked by the caller through <c>IMediaLibrary</c> (ADR-0034).
    /// </summary>
    public void SetAvatar(Guid? mediaId)
    {
        if (mediaId == Guid.Empty)
        {
            throw new ArgumentException("Not a media file identifier.", nameof(mediaId));
        }

        AvatarMediaId = mediaId;
    }

    /// <summary>
    /// Sets the cover to the uploaded image <paramref name="mediaId"/> shown at <paramref name="position"/> (ADR-0079).
    /// Whether the file is an image of the owner is checked by the caller through <c>IMediaLibrary</c>.
    /// </summary>
    public void SetCover(Guid mediaId, int position)
    {
        if (mediaId == Guid.Empty)
        {
            throw new ArgumentException("Not a media file identifier.", nameof(mediaId));
        }

        if (!SubsiteRules.IsValidCoverPosition(position))
        {
            throw new ArgumentOutOfRangeException(
                nameof(position), position, $"The cover position is from 0 to {SubsiteRules.CoverPositionMax}.");
        }

        CoverMediaId = mediaId;
        CoverPosition = position;
    }

    /// <summary>Removes the cover; the header shows the plain background again.</summary>
    public void RemoveCover()
    {
        CoverMediaId = null;
        CoverPosition = SubsiteRules.CoverPositionDefault;
    }

    /// <summary>
    /// Sets the nickname without counting it as a change: for topics, the demo data and the tests. Only its form is
    /// checked here; whether it is taken or reserved is checked by the caller (<see cref="BlogAddresses"/>).
    /// </summary>
    public void SetNickname(string? nickname)
    {
        var value = SubsiteRules.NormalizeNickname(nickname)
            ?? throw new ArgumentException("Not a nickname the platform accepts.", nameof(nickname));
        Nickname = value;
    }

    /// <summary>
    /// When the owner last changed the nickname (E04-05, ADR-0090); <c>null</c> until the first change. Setting the first
    /// nickname of a blog that had none is not a change.
    /// </summary>
    public DateTimeOffset? NicknameChangedAt { get; private set; }

    /// <summary>
    /// From when the owner may change the nickname again: <see cref="SubsiteRules.NicknameChangeInterval"/> after the last
    /// change; <c>null</c> when it was never changed.
    /// </summary>
    public DateTimeOffset? NicknameChangeableFrom => NicknameChangedAt + SubsiteRules.NicknameChangeInterval;

    /// <summary>Whether the owner may change the nickname at <paramref name="now"/> (ADR-0090: once in 30 days).</summary>
    public bool CanChangeNicknameAt(DateTimeOffset now) => NicknameChangeableFrom is not { } from || now >= from;

    /// <summary>
    /// The owner's change of the nickname (E04-05, ADR-0090) at <paramref name="now"/>. The first nickname of a blog that
    /// had none is free; any later change starts <see cref="SubsiteRules.NicknameChangeInterval"/> again and throws
    /// <see cref="InvalidOperationException"/> before it has passed. Whether the nickname is taken or reserved is checked
    /// by the caller. Returns the nickname the blog had before, or <c>null</c> when it had none or nothing changed.
    /// </summary>
    public string? ChangeNickname(string nickname, DateTimeOffset now)
    {
        var value = SubsiteRules.NormalizeNickname(nickname)
            ?? throw new ArgumentException("Not a nickname the platform accepts.", nameof(nickname));
        if (value == Nickname)
        {
            return null;
        }

        var former = Nickname;
        if (former is not null)
        {
            if (!CanChangeNicknameAt(now))
            {
                throw new InvalidOperationException($"The nickname may change again from {NicknameChangeableFrom:O}.");
            }

            NicknameChangedAt = now;
        }

        Nickname = value;
        return former;
    }

    /// <summary>
    /// When the owner of a personal blog finished or skipped the onboarding (E03-10, ADR-0102); <c>null</c> while it is
    /// still ahead. The blogs that existed before the onboarding have the time of their creation, so only new accounts see it.
    /// </summary>
    public DateTimeOffset? OnboardedAt { get; private set; }

    /// <summary>The owner of this personal blog has not been through the onboarding yet (ADR-0102).</summary>
    public bool OnboardingPending => Type == SubsiteType.Personal && OnboardedAt is null;

    /// <summary>
    /// Marks the onboarding as done at <paramref name="now"/>. Returns <c>false</c> and changes nothing when it was done
    /// already or the blog is a topic: the onboarding happens once (ADR-0102).
    /// </summary>
    public bool FinishOnboarding(DateTimeOffset now)
    {
        if (!OnboardingPending)
        {
            return false;
        }

        OnboardedAt = now;
        return true;
    }
}

/// <summary>
/// A nickname a blog gave up (E04-05, ADR-0090). Its address leads to the blog's new one until another blog or topic takes
/// it; for <see cref="SubsiteRules.FormerNicknameHold"/> after <see cref="ReleasedAt"/> nobody else may.
/// </summary>
public sealed class FormerNickname
{
    /// <summary>The nickname in its stored form; one record per nickname.</summary>
    public required string Nickname { get; init; }

    /// <summary>The blog that had it.</summary>
    public Guid SubsiteId { get; init; }

    /// <summary>When the blog gave it up.</summary>
    public DateTimeOffset ReleasedAt { get; init; }

    /// <summary>Whether another blog may take it at <paramref name="now"/>.</summary>
    public bool IsFreeAt(DateTimeOffset now) => now >= ReleasedAt + SubsiteRules.FormerNicknameHold;
}

/// <summary>The rules of a blog's profile (FR-PROFILE).</summary>
public static partial class SubsiteRules
{
    /// <summary>The name: at most 25 characters (FR-PROFILE).</summary>
    public const int NameMaxLength = 25;

    /// <summary>The description: at most 160 characters (FR-PROFILE).</summary>
    public const int DescriptionMaxLength = 160;

    /// <summary>The cover position (ADR-0079): percent from the top edge of the picture, 0 to 100.</summary>
    public const int CoverPositionMax = 100;

    /// <summary>The middle of the picture: where a new cover starts.</summary>
    public const int CoverPositionDefault = 50;

    /// <summary>How often the owner may change the nickname (FR-PROFILE: not more than once in 30 days).</summary>
    public static readonly TimeSpan NicknameChangeInterval = TimeSpan.FromDays(30);

    /// <summary>
    /// How long a nickname a blog gave up stays its own (ADR-0090, a proposal awaiting the owner's confirmation): no
    /// other blog or topic may take it in that time.
    /// </summary>
    public static readonly TimeSpan FormerNicknameHold = TimeSpan.FromDays(90);

    public const int NicknameMinLength = 3;
    public const int NicknameMaxLength = 32;

    /// <summary>
    /// The length a person sees: text elements (grapheme clusters), so an emoji or a letter with a combining mark is
    /// one character, the way the client counts it.
    /// </summary>
    public static int LengthOf(string? text) => string.IsNullOrEmpty(text) ? 0 : new StringInfo(text).LengthInTextElements;

    public static bool IsValidName(string? name) => LengthOf(Clean(name)) <= NameMaxLength;

    /// <summary>
    /// The shortest name the onboarding takes (E03-10, FR-AUTH: «имя (2–30 символов)», ADR-0102). The longest is
    /// <see cref="NameMaxLength"/>: the onboarding sets the same name the blog settings edit.
    /// </summary>
    public const int OnboardingNameMinLength = 2;

    /// <summary>
    /// The name of the onboarding's first step (ADR-0102): from <see cref="OnboardingNameMinLength"/> to
    /// <see cref="NameMaxLength"/> characters, spaces at the ends not counted. An empty name is a skipped step, not this.
    /// </summary>
    public static bool IsValidOnboardingName(string? name) =>
        LengthOf(Clean(name)) is var length && length >= OnboardingNameMinLength && length <= NameMaxLength;

    public static bool IsValidDescription(string? description) => LengthOf(Clean(description)) <= DescriptionMaxLength;

    public static bool IsValidCoverPosition(int position) => position is >= 0 and <= CoverPositionMax;

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
