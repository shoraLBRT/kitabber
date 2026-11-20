namespace Platform.Architecture.Tests;

/// <summary>
/// Module boundaries (docs/ARCHITECTURE.md, «Границы модулей»): the real platform obeys them, and the rules really
/// catch a module that reaches into another one (the fixture modules in tests/ArchitectureFixtures).
/// </summary>
public sealed class ModuleBoundaryTests
{
    private static readonly string _bin = AppContext.BaseDirectory;

    [Fact]
    public void PlatformAssembliesKeepModuleBoundaries()
    {
        var platform = ModuleBoundaries.ReadClosure(_bin, ModuleBoundaries.Hosts);

        // The scan is not vacuous: every host and the shared assemblies were found and read.
        Assert.All(ModuleBoundaries.Hosts, host => Assert.Contains(platform, a => a.Name == host));
        Assert.Contains(platform, a => a.Name == "Platform.Persistence");
        Assert.DoesNotContain(
            platform, a => a.Name.StartsWith(ModuleBoundaries.ModulePrefix + "Fixture", StringComparison.Ordinal));

        Assert.Empty(ModuleBoundaries.CheckReferences(platform));
        var modules = platform.Select(a => a.Name).Where(ModuleBoundaries.IsModuleImplementation).ToList();
        Assert.Contains("Platform.Modules.Identity", modules);
        Assert.Empty(ModuleBoundaries.CheckSchemas(ModuleBoundaries.ReadDbContexts(modules)));

        // FR-ID-1: credentials stay in Identity, no foreign keys between schemas. Not vacuous: the accounts are read.
        var tables = ModuleBoundaries.ReadTables(modules);
        Assert.Contains(tables, t => t.Schema == "identity" && t.Table == "accounts" && t.Columns.Contains("id"));
        Assert.Empty(ModuleBoundaries.CheckTables(tables));
    }

    [Fact]
    public void ModuleReferencingAnotherModulesInternalsIsReported()
    {
        var fixtures = ModuleBoundaries.ReadClosure(_bin, ["Platform.Modules.FixtureFeed"]);

        Assert.Contains(fixtures, a => a.Name == "Platform.Modules.FixtureNotes.Contracts");
        var violation = Assert.Single(ModuleBoundaries.CheckReferences(fixtures));
        Assert.StartsWith(
            "Platform.Modules.FixtureFeed -> Platform.Modules.FixtureNotes:", violation, StringComparison.Ordinal);
        Assert.Contains("Platform.Modules.FixtureNotes.Contracts", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleTakingAnotherModulesSchemaIsReported()
    {
        var contexts = ModuleBoundaries.ReadDbContexts(
            ["Platform.Modules.FixtureNotes", "Platform.Modules.FixtureFeed"]);

        Assert.Equal(2, contexts.Count);
        var violation = Assert.Single(ModuleBoundaries.CheckSchemas(contexts));
        Assert.Contains("FeedDbContext", violation, StringComparison.Ordinal);
        Assert.Contains("'fixture_feed'", violation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Platform.Modules.Feed", "Platform.Modules.Identity.Contracts")]
    [InlineData("Platform.Modules.Feed.Contracts", "Platform.Modules.Identity.Contracts")]
    [InlineData("Platform.Modules.Feed", "Platform.Modules.Feed.Contracts")]
    [InlineData("Platform.Modules.Feed.Infrastructure", "Platform.Modules.Feed")]
    [InlineData("Platform.Api", "Platform.Modules.Feed")]
    [InlineData("Platform.Workers", "Platform.Modules.Identity")]
    [InlineData("Platform.Modules.Feed", "Platform.Persistence")]
    [InlineData("Platform.Messaging", "Platform.Modules.Feed.Contracts")]
    [InlineData("Platform.Telegram.Gateway", "Telegram.Bot")]
    [InlineData("Platform.Telegram.Gateway.Mtproto", "WTelegramClient")]
    [InlineData("Platform.Modules.Identity", "Microsoft.AspNetCore.Identity.EntityFrameworkCore")]
    [InlineData("Platform.Modules.Identity", "OpenIddict.Server")]
    [InlineData("Platform.Api", "OpenIddict.Validation.AspNetCore")]
    public void AllowedReferencePasses(string from, string to) =>
        Assert.Empty(ModuleBoundaries.CheckReferences([new AssemblyNode(from, [to])]));

    [Theory]
    [InlineData("Platform.Modules.Feed", "Platform.Modules.Identity", "internals of module Identity")]
    [InlineData("Platform.Modules.Feed.Infrastructure", "Platform.Modules.Posts", "internals of module Posts")]
    [InlineData("Platform.Modules.Feed.Contracts", "Platform.Modules.Feed", "contracts must not depend")]
    [InlineData("Platform.Modules.Feed.Contracts", "Platform.Modules.Posts", "contracts must not depend")]
    [InlineData("Platform.Messaging", "Platform.Modules.Feed", "shared platform assembly")]
    [InlineData("Platform.Modules.Posts", "Telegram.Bot", "only Platform.Telegram.Gateway")]
    [InlineData("Platform.Api", "WTelegramClient", "only Platform.Telegram.Gateway")]
    [InlineData("Platform.Modules.Posts", "Microsoft.AspNetCore.Identity", "credentials live only")]
    [InlineData("Platform.Modules.Identity.Contracts", "Microsoft.Extensions.Identity.Core", "credentials live only")]
    [InlineData("Platform.Api", "BCrypt.Net-Next", "credentials live only")]
    public void ForbiddenReferenceIsReported(string from, string to, string reason)
    {
        var violation = Assert.Single(ModuleBoundaries.CheckReferences([new AssemblyNode(from, [to])]));
        Assert.Contains(reason, violation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Platform.Modules.Identity", "identity", true)]
    [InlineData("Platform.Modules.TelegramBridge", "telegram_bridge", true)]
    [InlineData("Platform.Modules.Posts", "comments", false)]
    [InlineData("Platform.Modules.Posts.Contracts", "posts", false)]
    [InlineData("Platform.Persistence", "posts", false)]
    public void DbContextMustUseItsOwnModuleSchema(string assembly, string schema, bool allowed)
    {
        var violations = ModuleBoundaries.CheckSchemas([new DbContextNode(assembly, "SomeDbContext", schema)]);

        Assert.Equal(allowed, violations.Count == 0);
    }
}
