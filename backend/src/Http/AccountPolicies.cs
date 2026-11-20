namespace Platform.Http;

/// <summary>
/// Authorization policies any module's endpoint may require by name (<c>RequireAuthorization(...)</c>); the Identity
/// module registers them, so other modules depend on the name only, not on Identity.
/// </summary>
public static class AccountPolicies
{
    /// <summary>
    /// A signed-in account with a confirmed email (FR-ID, ADR-0020): required to publish and to comment. An account
    /// without one gets 403 with the code <see cref="EmailNotConfirmedCode"/>.
    /// </summary>
    public const string ConfirmedEmail = "confirmed-email";

    /// <summary>The problem code of a request refused by <see cref="ConfirmedEmail"/>.</summary>
    public const string EmailNotConfirmedCode = "email_not_confirmed";
}
