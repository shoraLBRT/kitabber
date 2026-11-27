using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Platform.Messaging;
using Platform.Persistence;

namespace Platform.Modules.Comments;

/// <summary>
/// The database of the Comments module: schema <c>comments</c> (ADR-0048). A comment refers to its post and its author
/// only by their identifiers, with no foreign key to the schemas <c>posts</c> and <c>identity</c> (FR-ID-1).
/// </summary>
public sealed class CommentsDbContext(DbContextOptions<CommentsDbContext> options)
    : ModuleDbContext<CommentsDbContext>(options), IModuleDbContext
{
    /// <summary>The tree of a post in the order of its paths: every tree and branch read walks this index.</summary>
    public const string TreeIndex = "ix_comments_tree";

    /// <summary>One guest comment per outside message: it makes the import idempotent (ADR-0003).</summary>
    public const string SourceIndex = "ux_comments_source";

    /// <summary>The shown native comments by the time they were written: the widget of the day reads it (ADR-0105).</summary>
    public const string DayIndex = "ix_comments_day";

    /// <summary>A native comment has an author account; a guest one has none, but has a name and a source.</summary>
    public const string KindCheck = "ck_comments_kind";

    /// <summary>The longest channel and message identifiers of an outside source, as for posts.</summary>
    public const int SourceKeyMaxLength = 128;

    public const int GuestUrlMaxLength = 2048;

    public static string Schema => "comments";

    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // The outbox of the events the module publishes (ADR-0005, ADR-0092).
        modelBuilder.AddOutboxTables();
        modelBuilder.Entity<Comment>(comment =>
        {
            comment.ToTable(t => t.HasCheckConstraint(
                KindCheck,
                "(kind = 'Native' AND author_account_id IS NOT NULL AND guest_name IS NULL) OR "
                + "(kind = 'Guest' AND author_account_id IS NULL AND guest_name IS NOT NULL AND source_channel IS NOT NULL)"));
            comment.HasKey(c => c.Id);
            comment.Property(c => c.Id).ValueGeneratedNever();
            // The byte collation: the order of the paths is the order of the tree, whatever the database's locale.
            comment.Property(c => c.Path).UseCollation("C");
            comment.Property(c => c.Kind).HasConversion<string>().HasMaxLength(16);
            comment.Property(c => c.Text).HasMaxLength(CommentRules.MaxTextLength);
            comment.Property(c => c.GuestName).HasMaxLength(CommentRules.MaxGuestNameLength);
            comment.Property(c => c.GuestUrl).HasMaxLength(GuestUrlMaxLength);
            comment.Property(c => c.SourceChannel).HasMaxLength(SourceKeyMaxLength);
            comment.Property(c => c.SourceMessage).HasMaxLength(SourceKeyMaxLength);
            comment.Ignore(c => c.Guest);
            comment.HasIndex(c => new { c.PostId, c.Path }).IsUnique().HasDatabaseName(TreeIndex);
            comment.HasIndex(c => new { c.SourceChannel, c.SourceMessage }).IsUnique()
                .HasDatabaseName(SourceIndex)
                .HasFilter("source_channel IS NOT NULL");
            comment.HasIndex(c => new { c.AuthorAccountId, c.CreatedAt });
            comment.HasIndex(c => c.CreatedAt).HasDatabaseName(DayIndex)
                .HasFilter("kind = 'Native' AND deleted_at IS NULL");
        });
    }
}

/// <summary>For <c>dotnet ef migrations add</c>; no database is contacted.</summary>
public sealed class CommentsDesignTimeFactory : IDesignTimeDbContextFactory<CommentsDbContext>
{
    public CommentsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CommentsDbContext>();
        ModuleDatabaseServiceCollectionExtensions.UseModuleDatabase<CommentsDbContext>(
            options, "Host=localhost;Database=design_time");
        return new CommentsDbContext(options.Options);
    }
}
