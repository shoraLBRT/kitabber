namespace Platform.Modules.Identity.Contracts;

/// <summary>
/// An account is here to stay (ADR-0023): it has just got its first confirmed way to sign in, the email confirmed by a
/// code. A registration that is still waiting for its code is not one yet: it may be replaced and deleted (ADR-0021).
/// Published once per account through the Identity outbox; other modules create what every account has, such as its
/// personal blog, and must handle a repeat without harm.
/// </summary>
/// <param name="AccountId">The account_id (UUID v7).</param>
/// <param name="ActivatedAt">When the account was activated.</param>
public sealed record AccountActivated(Guid AccountId, DateTimeOffset ActivatedAt);

/// <summary>What other modules' demo data may rely on (ADR-0009): the demo accounts the Identity seeder writes.</summary>
public static class IdentityDemo
{
    /// <summary>The number of demo accounts; the <c>n</c>-th has the identifier <c>SeedData.Id("account", n)</c>, n from 0.</summary>
    public const int AccountCount = 20;

    /// <summary>The kind of the demo accounts' identifiers in <c>SeedData.Id</c>.</summary>
    public const string AccountKind = "account";
}
