using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenIddict.EntityFrameworkCore.Models;
using Platform.Messaging;
using Platform.Modules.Identity.Email;
using Platform.Persistence;

namespace Platform.Modules.Identity;

/// <summary>
/// The database of the Identity module: schema <c>identity</c>. Mail, passwords and external identifiers are stored
/// here and nowhere else, and so are the OpenID Connect clients, authorizations and tokens; no other schema has a
/// foreign key to these tables (FR-ID-1). The module's outbox (ADR-0005) carries its letters to the workers.
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : ModuleDbContext<IdentityDbContext>(options), IModuleDbContext
{
    /// <summary>The unique index on <see cref="Account.NormalizedEmail"/> (ADR-0020).</summary>
    public const string NormalizedEmailIndex = "ux_accounts_normalized_email";

    public static string Schema => "identity";

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<EmailConfirmation> EmailConfirmations => Set<EmailConfirmation>();

    public DbSet<SignIn.SignInCode> SignInCodes => Set<SignIn.SignInCode>();

    public DbSet<Passwords.PasswordReset> PasswordResets => Set<Passwords.PasswordReset>();

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
        modelBuilder.AddOutboxTables();
        modelBuilder.Entity<Account>(account =>
        {
            account.HasKey(a => a.Id);
            // The application always sets the identifier (UUID v7); the database never generates one.
            account.Property(a => a.Id).ValueGeneratedNever();
            account.Property(a => a.Email).HasMaxLength(EmailAddresses.MaxLength);
            account.Property(a => a.NormalizedEmail).HasMaxLength(EmailAddresses.MaxLength);
            // Unique among all accounts, confirmed or not (FR-ID); accounts without an email (NULL) do not collide.
            account.HasIndex(a => a.NormalizedEmail).IsUnique().HasDatabaseName(NormalizedEmailIndex);
            account.Ignore(a => a.HasConfirmedEmail);
            account.Ignore(a => a.IsPendingRegistration);
            account.Property(a => a.PasswordHash).HasMaxLength(256);
            account.Property(a => a.SessionStamp).HasMaxLength(64);
        });
        modelBuilder.Entity<EmailConfirmation>(confirmation =>
        {
            confirmation.HasKey(c => c.AccountId);
            confirmation.Property(c => c.AccountId).ValueGeneratedNever();
            confirmation.Property(c => c.Email).HasMaxLength(EmailAddresses.MaxLength);
            confirmation.Property(c => c.NormalizedEmail).HasMaxLength(EmailAddresses.MaxLength);
            confirmation.HasOne<Account>().WithOne().HasForeignKey<EmailConfirmation>(c => c.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        // One-time sign-in codes (ADR-0022): one per address, registered or not, so no account is referenced.
        modelBuilder.Entity<SignIn.SignInCode>(code =>
        {
            code.HasKey(c => c.NormalizedEmail);
            code.Property(c => c.NormalizedEmail).HasMaxLength(EmailAddresses.MaxLength);
            code.Property(c => c.Email).HasMaxLength(EmailAddresses.MaxLength);
            code.HasIndex(c => c.Id).IsUnique();
        });
        // Password reset codes (ADR-0036): one per address, registered or not, like the sign-in codes.
        modelBuilder.Entity<Passwords.PasswordReset>(reset =>
        {
            reset.HasKey(r => r.NormalizedEmail);
            reset.Property(r => r.NormalizedEmail).HasMaxLength(EmailAddresses.MaxLength);
            reset.HasIndex(r => r.Id).IsUnique();
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
