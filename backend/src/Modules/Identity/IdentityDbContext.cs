using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenIddict.EntityFrameworkCore.Models;
using Platform.Messaging;
using Platform.Modules.Identity.Email;
using Platform.Modules.Identity.External;
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

    /// <summary>The unique index on the pair «provider, subject» of an external identity (ADR-0098).</summary>
    public const string ExternalSubjectIndex = "ux_external_identities_provider_subject";

    /// <summary>The unique index on «account, provider»: one identity of each provider per account (ADR-0098).</summary>
    public const string ExternalProviderIndex = "ux_external_identities_account_provider";

    public static string Schema => "identity";

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<EmailConfirmation> EmailConfirmations => Set<EmailConfirmation>();

    public DbSet<SignIn.SignInCode> SignInCodes => Set<SignIn.SignInCode>();

    public DbSet<Passwords.PasswordReset> PasswordResets => Set<Passwords.PasswordReset>();

    public DbSet<SignIn.AccountSession> AccountSessions => Set<SignIn.AccountSession>();

    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();

    public DbSet<ExternalIdentityLogEntry> ExternalIdentityLog => Set<ExternalIdentityLogEntry>();

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
        // The devices signed in (ADR-0078): one row per session at the provider, gone when it is signed out.
        modelBuilder.Entity<SignIn.AccountSession>(session =>
        {
            session.HasKey(s => s.Id);
            session.Property(s => s.Id).ValueGeneratedNever();
            session.Property(s => s.Device).HasMaxLength(SignIn.AccountSession.DeviceMaxLength);
            session.HasIndex(s => new { s.AccountId, s.LastActiveAt });
            session.HasOne<Account>().WithMany().HasForeignKey(s => s.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        // External identities (FR-ID-4, ADR-0098): a pair «provider, subject» belongs to one account, an account has
        // one identity of each provider, and both go with the account.
        modelBuilder.Entity<ExternalIdentity>(identity =>
        {
            identity.ToTable("external_identities");
            identity.HasKey(i => i.Id);
            identity.Property(i => i.Id).ValueGeneratedNever();
            identity.Property(i => i.Provider).HasMaxLength(ExternalProviders.MaxLength);
            identity.Property(i => i.Subject).HasMaxLength(ExternalIdentity.SubjectMaxLength);
            identity.HasIndex(i => new { i.Provider, i.Subject }).IsUnique().HasDatabaseName(ExternalSubjectIndex);
            identity.HasIndex(i => new { i.AccountId, i.Provider }).IsUnique().HasDatabaseName(ExternalProviderIndex);
            identity.HasOne<Account>().WithMany().HasForeignKey(i => i.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        // The journal of links (ADR-0098): rows are only added, and erased with the account (the subject names a person).
        modelBuilder.Entity<ExternalIdentityLogEntry>(entry =>
        {
            entry.ToTable("external_identity_log");
            entry.HasKey(e => e.Id);
            entry.Property(e => e.Id).ValueGeneratedNever();
            entry.Property(e => e.Provider).HasMaxLength(ExternalProviders.MaxLength);
            entry.Property(e => e.Subject).HasMaxLength(ExternalIdentity.SubjectMaxLength);
            entry.Property(e => e.Action).HasConversion<string>().HasMaxLength(16);
            entry.HasIndex(e => new { e.AccountId, e.At });
            entry.HasOne<Account>().WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Cascade);
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
