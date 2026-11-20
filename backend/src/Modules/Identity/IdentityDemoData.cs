using Platform.Seeding;

namespace Platform.Modules.Identity;

/// <summary>
/// The factory of accounts (ADR-0009). Its <c>number</c>-th account has the identifier <c>Id("account", number)</c>,
/// which other modules' factories use for authors without reading this module's tables. Demo identifiers are derived
/// from the seed (UUID v8), not from the clock, so they are the same on every run; accounts the platform creates get a
/// UUID v7 from <see cref="Account.Create"/>.
/// </summary>
public sealed class AccountFactory(SeedData data) : ObjectFactory<Account>(data)
{
    public override string Kind => "account";

    protected override Account Create(int number) => new()
    {
        Id = IdOf(number),
        CreatedAt = Data.Now - TimeSpan.FromDays(number + 1) - TimeSpan.FromSeconds(Random.Next(3600)),
    };
}

/// <summary>The demo accounts: <see cref="Count"/> of them, added only where they are missing.</summary>
public sealed class IdentityDemoSeeder(IdentityDbContext db) : IDemoSeeder
{
    public const int Count = 20;

    public string Name => IdentityDbContext.Schema;

    public Task<int> SeedAsync(SeedData data, CancellationToken cancellationToken) =>
        db.AddMissingAsync(new AccountFactory(data).BuildMany(Count).ToList(), a => a.Id, cancellationToken);
}
