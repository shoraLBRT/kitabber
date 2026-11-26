using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Platform.Persistence;

namespace Platform.Modules.Posts;

/// <summary>
/// The database of the Posts module: schema <c>posts</c> (ADR-0029). A post refers to its author and its blog only by
/// their identifiers, with no foreign key to the schemas <c>identity</c> and <c>blogs</c> (FR-ID-1, ADR-0025). Deleted
/// posts stay in the table and are left out of every query.
/// </summary>
public sealed class PostsDbContext(DbContextOptions<PostsDbContext> options)
    : ModuleDbContext<PostsDbContext>(options), IModuleDbContext
{
    /// <summary>One post per message of an external channel: it makes the import idempotent (ADR-0003).</summary>
    public const string SourceIndex = "ux_posts_source";

    /// <summary>The fresh feed: published posts by publication time and id, newest first (ADR-0043).</summary>
    public const string FreshFeedIndex = "ix_posts_fresh_feed";

    /// <summary>The longest channel and message identifiers of an external source.</summary>
    public const int SourceKeyMaxLength = 128;

    public const int SourceUrlMaxLength = 2048;

    public static string Schema => "posts";

    public DbSet<Post> Posts => Set<Post>();

    /// <summary>Who opened a post or saw its card in a period (ADR-0071): one row per post, kind, viewer and day.</summary>
    public DbSet<PostViewMark> ViewMarks => Set<PostViewMark>();

    /// <summary>The secret of each day that turns a guest's address into a pseudonym (ADR-0071).</summary>
    public DbSet<ViewSalt> ViewSalts => Set<ViewSalt>();

    /// <summary>The hotness of published posts, the order of the popular feed (ADR-0086).</summary>
    public DbSet<PostHotness> Hotness => Set<PostHotness>();

    /// <summary>The coefficients every stored score was computed with: one row (ADR-0086).</summary>
    public DbSet<HotnessFormulaState> HotnessFormula => Set<HotnessFormulaState>();

    /// <summary>The popular feed: scores, highest first, the id breaking ties (ADR-0086).</summary>
    public const string PopularFeedIndex = "ix_post_hotness_popular_feed";

    public const int HotnessFormulaMaxLength = 128;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Post>(post =>
        {
            post.HasKey(p => p.Id);
            post.Property(p => p.Id).ValueGeneratedNever();
            post.Property(p => p.Body).HasColumnType("jsonb");
            post.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            post.Property(p => p.Origin).HasConversion<string>().HasMaxLength(16);
            post.Property(p => p.SourceChannel).HasMaxLength(SourceKeyMaxLength);
            post.Property(p => p.SourceMessage).HasMaxLength(SourceKeyMaxLength);
            post.Property(p => p.SourceUrl).HasMaxLength(SourceUrlMaxLength);
            post.Ignore(p => p.IsDeleted);
            post.Ignore(p => p.Source);
            post.HasQueryFilter(p => p.DeletedAt == null);
            post.HasIndex(p => new { p.Origin, p.SourceChannel, p.SourceMessage }).IsUnique()
                .HasDatabaseName(SourceIndex)
                .HasFilter("source_channel IS NOT NULL");
            post.HasIndex(p => new { p.BlogId, p.PublishedAt });
            post.HasIndex(p => new { p.AuthorAccountId, p.CreatedAt });
            post.HasIndex(p => new { p.PublishedAt, p.Id }).IsDescending()
                .HasDatabaseName(FreshFeedIndex)
                .HasFilter("status = 'Published' AND deleted_at IS NULL");
        });
        modelBuilder.Entity<PostViewMark>(mark =>
        {
            mark.ToTable("post_views");
            mark.HasKey(m => new { m.PostId, m.Kind, m.Viewer, m.PeriodStart });
            mark.Property(m => m.Kind).HasConversion<string>().HasMaxLength(16);
            mark.Property(m => m.Viewer).HasMaxLength(ViewRules.ViewerMaxLength);
            mark.HasIndex(m => m.PeriodStart);
        });
        modelBuilder.Entity<ViewSalt>(salt =>
        {
            salt.ToTable("view_salts");
            salt.HasKey(s => s.PeriodStart);
            salt.Property(s => s.PeriodStart).ValueGeneratedNever();
        });
        modelBuilder.Entity<PostHotness>(hotness =>
        {
            hotness.ToTable("post_hotness");
            hotness.HasKey(h => h.PostId);
            hotness.Property(h => h.PostId).ValueGeneratedNever();
            hotness.Property(h => h.Formula).HasMaxLength(HotnessFormulaMaxLength);
            hotness.Ignore(h => h.Inputs);
            hotness.HasIndex(h => new { h.Score, h.PostId }).IsDescending().HasDatabaseName(PopularFeedIndex);
        });
        modelBuilder.Entity<HotnessFormulaState>(state =>
        {
            state.ToTable("hotness_formula");
            state.HasKey(s => s.Id);
            state.Property(s => s.Id).ValueGeneratedNever();
            state.Property(s => s.Formula).HasMaxLength(HotnessFormulaMaxLength);
        });
    }
}

/// <summary>For <c>dotnet ef migrations add</c>; no database is contacted.</summary>
public sealed class PostsDesignTimeFactory : IDesignTimeDbContextFactory<PostsDbContext>
{
    public PostsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PostsDbContext>();
        ModuleDatabaseServiceCollectionExtensions.UseModuleDatabase<PostsDbContext>(
            options, "Host=localhost;Database=design_time");
        return new PostsDbContext(options.Options);
    }
}
