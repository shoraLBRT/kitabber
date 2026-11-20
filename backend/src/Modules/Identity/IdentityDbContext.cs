using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenIddict.EntityFrameworkCore.Models;
using Platform.Persistence;

namespace Platform.Modules.Identity;

/// <summary>
/// The database of the Identity module: schema <c>identity</c>. Mail, passwords and external identifiers are stored
/// here and nowhere else, and so are the OpenID Connect clients, authorizations and tokens; no other schema has a
/// foreign key to these tables (FR-ID-1).
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : ModuleDbContext<IdentityDbContext>(options), IModuleDbContext
{
    public static string Schema => "identity";

    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // The provider's clients, authorizations and tokens (ADR-0018): OpenIddict's tables, in this schema too.
        // Their table names follow the snake_case of every other table.
        modelBuilder.UseOpenIddict();
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreApplication>().ToTable("oidc_applications");
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreAuthorization>().ToTable("oidc_authorizations");
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreScope>().ToTable("oidc_scopes");
        modelBuilder.Entity<OpenIddictEntityFrameworkCoreToken>().ToTable("oidc_tokens");
        modelBuilder.Entity<Account>(account =>
        {
            account.HasKey(a => a.Id);
            // The application always sets the identifier (UUID v7); the database never generates one.
            account.Property(a => a.Id).ValueGeneratedNever();
        });
    }
}

/// <summary>For <c>dotnet ef migrations add</c>; no database is contacted.</summary>
public sealed class IdentityDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        ModuleDatabaseServiceCollectionExtensions.UseModuleDatabase<IdentityDbContext>(
            options, "Host=localhost;Database=design_time");
        return new IdentityDbContext(options.Options);
    }
}
