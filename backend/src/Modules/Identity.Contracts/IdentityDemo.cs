namespace Platform.Modules.Identity.Contracts;

/// <summary>What other modules' demo data may rely on (ADR-0009): the demo accounts the Identity seeder writes.</summary>
public static class IdentityDemo
{
    /// <summary>The number of demo accounts; the <c>n</c>-th has the identifier <c>SeedData.Id("account", n)</c>, n from 0.</summary>
    public const int AccountCount = 20;

    /// <summary>The kind of the demo accounts' identifiers in <c>SeedData.Id</c>.</summary>
    public const string AccountKind = "account";
}
