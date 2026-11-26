namespace Platform.Modules.Reactions;

/// <summary>Why a like is refused (FR-REACT, ADR-0061).</summary>
public enum LikeRefusal
{
    /// <summary>The account is the author: no one rates their own post or comment.</summary>
    OwnContent = 1,

    /// <summary>A guest comment, carried over from outside (FR-TG-6): its author has no account to be rated.</summary>
    GuestComment = 2,
}

/// <summary>The rules of a like, apart from the database: the endpoints apply them before they write.</summary>
public static class LikeRules
{
    /// <summary>
    /// Why <paramref name="account"/> may not like content by <paramref name="authorAccountId"/>, or <c>null</c> when it
    /// may; a <c>null</c> author is a guest comment's.
    /// </summary>
    public static LikeRefusal? Refusal(Guid account, Guid? authorAccountId) =>
        authorAccountId is not { } author ? LikeRefusal.GuestComment
        : author == account ? LikeRefusal.OwnContent
        : null;
}
