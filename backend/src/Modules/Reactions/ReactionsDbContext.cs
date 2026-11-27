using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Platform.Messaging;
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

    /// <summary>The rated comments of a post, read at once for its tree and for the feed (ADR-0081).</summary>
    public const string RatingPostIndex = "ix_ratings_post";

    public DbSet<Reaction> Reactions => Set<Reaction>();

    /// <summary>The kept ratings of posts and comments (ADR-0081).</summary>
    public DbSet<Rating> Ratings => Set<Rating>();

    /// <summary>The table of <see cref="ReactionSet"/>: reference data the migration seeds.</summary>
    public const string ReactionSetTable = "reaction_set";

    /// <summary>The directory of reactions the platform offers (ADR-0095).</summary>
    public DbSet<ReactionSetEntry> ReactionSet => Set<ReactionSetEntry>();

    /// <summary>The bookmarks under a post: its page reads them with one query (ADR-0113).</summary>
    public const string BookmarkPostIndex = "ix_bookmarks_post";

    /// <summary>The bookmarks of posts and comments (E10-07, ADR-0113).</summary>
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // The outbox of the events the module publishes (ADR-0005, ADR-0092).
        modelBuilder.AddOutboxTables();
        modelBuilder.Entity<Reaction>(reaction =>
        {
            // One reaction per person and target: the key makes a repeated like a no-op (ADR-0061).
            reaction.HasKey(r => new { r.TargetKind, r.TargetId, r.AccountId });
            reaction.Property(r => r.TargetKind).HasConversion<string>().HasMaxLength(16);
            reaction.Property(r => r.Kind).HasConversion<string>().HasMaxLength(16);
            reaction.HasIndex(r => r.PostId).HasDatabaseName(PostIndex);
            reaction.HasIndex(r => new { r.AccountId, r.CreatedAt });
        });
        modelBuilder.Entity<Rating>(rating =>
        {
            rating.HasKey(r => new { r.TargetKind, r.TargetId });
            rating.Property(r => r.TargetKind).HasConversion<string>().HasMaxLength(16);
            rating.HasIndex(r => r.PostId).HasDatabaseName(RatingPostIndex);
        });
        modelBuilder.Entity<Bookmark>(bookmark =>
        {
            // One bookmark per person and target: a repeated put is a no-op (ADR-0113).
            bookmark.HasKey(b => new { b.TargetKind, b.TargetId, b.AccountId });
            bookmark.Property(b => b.TargetKind).HasConversion<string>().HasMaxLength(16);
            bookmark.HasIndex(b => b.PostId).HasDatabaseName(BookmarkPostIndex);
            // The person's bookmarks, newest first, for the section «Закладки» (E04-12).
            bookmark.HasIndex(b => new { b.AccountId, b.CreatedAt });
        });
        modelBuilder.Entity<ReactionSetEntry>(entry =>
        {
            entry.ToTable(ReactionSetTable);
            entry.HasKey(e => e.Code);
            entry.Property(e => e.Code).HasMaxLength(ReactionSetEntry.CodeMaxLength);
            entry.Property(e => e.Title).HasMaxLength(ReactionSetEntry.TitleMaxLength);
            // The default set is reference data: every environment has it from the migration (ADR-0095).
            entry.HasData(ReactionSetEntry.Defaults);
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
