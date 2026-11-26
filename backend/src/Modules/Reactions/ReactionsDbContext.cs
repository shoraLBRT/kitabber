using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Platform.Persistence;

namespace Platform.Modules.Reactions;

/// <summary>
/// The database of the Reactions module: schema <c>reactions</c> (ADR-0061). A reaction refers to its target, its post
/// and the account only by their identifiers, with no foreign key to the schemas <c>posts</c>, <c>comments</c> and
/// <c>identity</c> (FR-ID-1).
/// </summary>
public sealed class ReactionsDbContext(DbContextOptions<ReactionsDbContext> options)
    : ModuleDbContext<ReactionsDbContext>(options), IModuleDbContext
{
    /// <summary>Every reaction under a post: the post page reads them with one query.</summary>
    public const string PostIndex = "ix_reactions_post";

    public static string Schema => "reactions";

    public DbSet<Reaction> Reactions => Set<Reaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Reaction>(reaction =>
        {
            // One reaction per person and target: the key makes a repeated like a no-op (ADR-0061).
            reaction.HasKey(r => new { r.TargetKind, r.TargetId, r.AccountId });
            reaction.Property(r => r.TargetKind).HasConversion<string>().HasMaxLength(16);
            reaction.Property(r => r.Kind).HasConversion<string>().HasMaxLength(16);
            reaction.HasIndex(r => r.PostId).HasDatabaseName(PostIndex);
            reaction.HasIndex(r => new { r.AccountId, r.CreatedAt });
        });
    }
}

/// <summary>For <c>dotnet ef migrations add</c>; no database is contacted.</summary>
public sealed class ReactionsDesignTimeFactory : IDesignTimeDbContextFactory<ReactionsDbContext>
{
    public ReactionsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ReactionsDbContext>();
        ModuleDatabaseServiceCollectionExtensions.UseModuleDatabase<ReactionsDbContext>(
            options, "Host=localhost;Database=design_time");
        return new ReactionsDbContext(options.Options);
    }
}
