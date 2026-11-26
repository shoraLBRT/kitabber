using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Platform.Messaging;
using Platform.Modules.Preferences.Blocks;
using Platform.Persistence;

namespace Platform.Modules.Preferences;

/// <summary>
/// The database of the Preferences module: schema <c>preferences</c> (ADR-0070). A row refers to the account only by
/// its identifier, with no foreign key to the schema <c>identity</c> (FR-ID-1).
/// </summary>
public sealed class PreferencesDbContext(DbContextOptions<PreferencesDbContext> options)
    : ModuleDbContext<PreferencesDbContext>(options), IModuleDbContext
{
    public static string Schema => "preferences";

    public DbSet<AccountPreferences> AccountPreferences => Set<AccountPreferences>();

    public DbSet<BlockedAccount> BlockedAccounts => Set<BlockedAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // The inbox of the consumers (ADR-0005): a repeated event is not handled twice.
        modelBuilder.AddOutboxTables();
        modelBuilder.Entity<AccountPreferences>(preferences =>
        {
            // One row per account; an account without a row has the defaults (ADR-0070).
            preferences.HasKey(p => p.AccountId);
            preferences.Property(p => p.Theme).HasConversion<string>().HasMaxLength(16);
        });
        modelBuilder.Entity<BlockedAccount>(blocked =>
        {
            // One entry per pair (ADR-0091): the key serves the reads of one person's list; the index of the blocked
            // account serves the erasure of a deleted account from every list.
            blocked.HasKey(b => new { b.OwnerAccountId, b.AccountId });
            blocked.HasIndex(b => b.AccountId);
        });
    }
}

/// <summary>For <c>dotnet ef migrations add</c>; no database is contacted.</summary>
public sealed class PreferencesDesignTimeFactory : IDesignTimeDbContextFactory<PreferencesDbContext>
{
    public PreferencesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PreferencesDbContext>();
        ModuleDatabaseServiceCollectionExtensions.UseModuleDatabase<PreferencesDbContext>(
            options, "Host=localhost;Database=design_time");
        return new PreferencesDbContext(options.Options);
    }
}
