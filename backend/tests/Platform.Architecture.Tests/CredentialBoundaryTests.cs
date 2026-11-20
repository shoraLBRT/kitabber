using Microsoft.EntityFrameworkCore;
using Platform.Persistence;

namespace Platform.Architecture.Tests;

/// <summary>
/// FR-ID-1: no table outside the schema <c>identity</c> holds mail, a password or an external identifier, and no
/// foreign key crosses schemas. The rule reads the EF Core model of each module; <see cref="LeakyPostsDbContext"/>
/// breaks it on purpose, so the test proves the rule catches each kind of violation.
/// </summary>
public sealed class CredentialBoundaryTests
{
    [Fact]
    public void ModuleStoringCredentialsOrReferencingAnotherSchemaIsReported()
    {
        var tables = ModuleBoundaries.ReadTables("Platform.Modules.Posts", typeof(LeakyPostsDbContext));

        var violations = ModuleBoundaries.CheckTables(tables);

        Assert.Equal(4, violations.Count);
        Assert.Contains(violations, v => v.StartsWith("posts.posts (", StringComparison.Ordinal)
            && v.Contains(".author_email: mail, passwords", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.Contains(".password_hash: mail, passwords", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.Contains("-> identity.accounts: no foreign keys between schemas", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.StartsWith("identity.accounts (", StringComparison.Ordinal)
            && v.Contains("lives only in schema 'posts'", StringComparison.Ordinal));
    }

    [Fact]
    public void IdentityModuleMayStoreCredentials()
    {
        TableNode logins = new(
            "Platform.Modules.Identity", "IdentityDbContext", "identity", "external_logins",
            ["id", "account_id", "login_provider", "provider_key", "email", "password_hash"], ["identity.accounts"]);

        Assert.Empty(ModuleBoundaries.CheckTables([logins]));
    }

    [Theory]
    [InlineData("email", true)]
    [InlineData("normalized_email", true)]
    [InlineData("author_e_mail", true)]
    [InlineData("password_hash", true)]
    [InlineData("passwd", true)]
    [InlineData("external_id", true)]
    [InlineData("yandex_external_id", true)]
    [InlineData("login_provider", true)]
    [InlineData("provider_key", true)]
    [InlineData("account_id", false)]
    [InlineData("author_id", false)]
    [InlineData("title", false)]
    [InlineData("created_at", false)]
    public void CredentialColumnsAreRecognized(string column, bool credential) =>
        Assert.Equal(credential, ModuleBoundaries.IsCredentialColumn(column));
}

/// <summary>
/// A module context that breaks FR-ID-1 in every way the rule checks: it stores the author's mail and a password hash,
/// maps a table of the schema <c>identity</c> and has a foreign key to it.
/// </summary>
public sealed class LeakyPostsDbContext(DbContextOptions<LeakyPostsDbContext> options)
    : ModuleDbContext<LeakyPostsDbContext>(options), IModuleDbContext
{
    public static string Schema => "posts";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<LeakyAccount>().ToTable("accounts", "identity");
        modelBuilder.Entity<LeakyPost>(post => post.ToTable("posts").HasOne<LeakyAccount>().WithMany().HasForeignKey(p => p.AuthorId));
    }
}

public sealed class LeakyAccount
{
    public Guid Id { get; set; }
}

public sealed class LeakyPost
{
    public Guid Id { get; set; }

    public Guid AuthorId { get; set; }

    public string? AuthorEmail { get; set; }

    public string? PasswordHash { get; set; }
}
