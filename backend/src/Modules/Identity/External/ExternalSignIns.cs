using Platform.Modules.Identity.Email;

namespace Platform.Modules.Identity.External;

/// <summary>How <see cref="ExternalSignIns.ResolveAsync"/> ended.</summary>
public enum ExternalSignInOutcome
{
    /// <summary>The person signs in to <see cref="ExternalSignInResult.AccountId"/>.</summary>
    SignedIn,

    /// <summary>The provider is now (or was already) linked to the signed-in account.</summary>
    Linked,

    /// <summary>No account has the pair, and the provider gave no confirmed email to find or create one by.</summary>
    EmailRequired,

    /// <summary>The pair belongs to another account (ADR-0098, п. 3).</summary>
    TakenByAnotherAccount,

    /// <summary>The account already has another identity of this provider (ADR-0098, п. 4).</summary>
    ProviderAlreadyLinked,
}

/// <param name="Outcome">What happened.</param>
/// <param name="AccountId">The account signed in or linked to.</param>
public sealed record ExternalSignInResult(ExternalSignInOutcome Outcome, Guid? AccountId = null);

/// <summary>
/// Signing in with an external provider (FR-AUTH, ADR-0115), once the provider has proved who the person is: the account
/// of the pair «provider, subject»; otherwise the account with the confirmed email the provider vouches for, now linked;
/// otherwise a new account with that email, confirmed, and the link. Linking to the signed-in account is the same proof
/// with the account given. A provider without a confirmed email (Telegram) signs in only to an account it is linked to.
/// </summary>
public sealed class ExternalSignIns(IdentityDbContext db, ExternalIdentities identities, AccountEmails emails, TimeProvider time)
{
    /// <summary>The account <paramref name="profile"/> signs in to, or links to <paramref name="linkTo"/>.</summary>
    public async Task<ExternalSignInResult> ResolveAsync(
        string provider, ExternalProfile profile, Guid? linkTo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var owner = await identities.FindAccountAsync(provider, profile.Subject, cancellationToken);
        if (linkTo is { } target)
        {
            if (owner is not null)
            {
                return owner == target
                    ? new ExternalSignInResult(ExternalSignInOutcome.Linked, target)
                    : new ExternalSignInResult(ExternalSignInOutcome.TakenByAnotherAccount);
            }

            return Linked(await identities.LinkAsync(target, provider, profile.Subject, cancellationToken), target);
        }

        if (owner is { } signedIn)
        {
            return new ExternalSignInResult(ExternalSignInOutcome.SignedIn, signedIn);
        }

        var normalized = profile.EmailVerified ? EmailAddresses.Normalize(profile.Email) : null;
        if (normalized is null)
        {
            return new ExternalSignInResult(ExternalSignInOutcome.EmailRequired);
        }

        // Twice at most: a sign-in of the same person or address at the same moment may take the address or the pair
        // between the look and the save; the second look sees what it took.
        for (var attempt = 0; ; attempt++)
        {
            if (await emails.FindByConfirmedEmailAsync(profile.Email!, cancellationToken) is { } accountId)
            {
                return await identities.LinkAsync(accountId, provider, profile.Subject, cancellationToken) switch
                {
                    LinkOutcome.Linked or LinkOutcome.AlreadyLinked => new ExternalSignInResult(ExternalSignInOutcome.SignedIn, accountId),
                    LinkOutcome.ProviderAlreadyLinked => new ExternalSignInResult(ExternalSignInOutcome.ProviderAlreadyLinked),
                    _ => await identities.FindAccountAsync(provider, profile.Subject, cancellationToken) is { } taken
                        ? new ExternalSignInResult(ExternalSignInOutcome.SignedIn, taken)
                        : new ExternalSignInResult(ExternalSignInOutcome.TakenByAnotherAccount),
                };
            }

            if (await CreateAsync(provider, profile, normalized, cancellationToken) is { } created)
            {
                return new ExternalSignInResult(ExternalSignInOutcome.SignedIn, created);
            }

            if (attempt > 0)
            {
                throw new InvalidOperationException("The confirmed email was taken twice while the account was created.");
            }
        }
    }

    // A new account whose email the provider has confirmed, saved with its first identity; null when another account
    // confirmed the address first.
    private async Task<Guid?> CreateAsync(
        string provider, ExternalProfile profile, string normalized, CancellationToken cancellationToken)
    {
        if (!await emails.ReleaseUnconfirmedAsync(normalized, null, cancellationToken))
        {
            return null;
        }

        var account = Account.Create(time);
        account.ConfirmEmail(profile.Email!.Trim(), normalized, time.GetUtcNow());
        db.Accounts.Add(account);
        identities.LinkToNew(account, provider, profile.Subject);
        if (await emails.SaveAsync(cancellationToken))
        {
            return account.Id;
        }

        db.ChangeTracker.Clear();
        return null;
    }

    private static ExternalSignInResult Linked(LinkOutcome outcome, Guid accountId) => outcome switch
    {
        LinkOutcome.Linked or LinkOutcome.AlreadyLinked => new ExternalSignInResult(ExternalSignInOutcome.Linked, accountId),
        LinkOutcome.TakenByAnotherAccount => new ExternalSignInResult(ExternalSignInOutcome.TakenByAnotherAccount),
        LinkOutcome.ProviderAlreadyLinked => new ExternalSignInResult(ExternalSignInOutcome.ProviderAlreadyLinked),
        _ => throw new InvalidOperationException("The signed-in account is gone."),
    };
}
