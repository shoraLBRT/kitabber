using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Platform.Messaging;
using Platform.Persistence;

namespace Platform.Modules.Notifications;

/// <summary>
/// The database of the Notifications module: schema <c>notifications</c> (ADR-0092). A notification refers to people,
/// posts, comments and blogs only by their identifiers, with no foreign key to another schema (FR-ID-1).
/// </summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
    : ModuleDbContext<NotificationsDbContext>(options), IModuleDbContext
{
    /// <summary>One notification per recipient and action: a repeated event adds nothing (ADR-0092).</summary>
    public const string SourceIndex = "ux_notifications_source";

    /// <summary>The recipient's notifications, newest first: the feed of E12-02 walks it.</summary>
    public const string FeedIndex = "ix_notifications_feed";

    /// <summary>The recipient's notifications by their row: the grouped feed and the counter of E12-03 walk it.</summary>
    public const string GroupIndex = "ix_notifications_group";

    public const int SourceKeyMaxLength = 128;

    public const int GroupKeyMaxLength = 96;

    /// <summary>
    /// The row of a notification (E12-03, ADR-0122), a stored generated column: one row for the reactions to a post or
    /// to a comment, for the reposts of a post, for the comments under a post and for the new subscribers of a blog;
    /// every other notification is a row of its own, by its id. A new grouped type is a new branch here and a
    /// migration.
    /// </summary>
    public const string GroupKeySql =
        "CASE type"
        + " WHEN 'Reaction' THEN 'reaction:' || coalesce(post_id::text, '') || ':' || coalesce(comment_id::text, '')"
        + " WHEN 'Repost' THEN 'repost:' || coalesce(post_id::text, '')"
        + " WHEN 'PostComment' THEN 'post_comment:' || coalesce(post_id::text, '')"
        + " WHEN 'NewSubscriber' THEN 'new_subscriber:' || coalesce(blog_id::text, '')"
        + " ELSE id::text END";

    public const int ReactionKindMaxLength = 32;

    public static string Schema => "notifications";

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // The inbox of the consumers (ADR-0005): a repeated event is not handled twice.
        modelBuilder.AddOutboxTables();
        modelBuilder.Entity<Notification>(notification =>
        {
            notification.HasKey(n => n.Id);
            notification.Property(n => n.Id).ValueGeneratedNever();
            notification.Property(n => n.Type).HasConversion<string>().HasMaxLength(32);
            notification.Property(n => n.SourceKey).HasMaxLength(SourceKeyMaxLength);
            notification.Property(n => n.ReactionKind).HasMaxLength(ReactionKindMaxLength);
            notification.Property(n => n.GroupKey).HasMaxLength(GroupKeyMaxLength)
                .HasComputedColumnSql(GroupKeySql, stored: true);
            notification.HasIndex(n => new { n.RecipientAccountId, n.SourceKey }).IsUnique()
                .HasDatabaseName(SourceIndex);
            notification.HasIndex(n => new { n.RecipientAccountId, n.CreatedAt, n.Id })
                .IsDescending(false, true, true)
                .HasDatabaseName(FeedIndex);
            notification.HasIndex(n => new { n.RecipientAccountId, n.GroupKey, n.CreatedAt })
                .HasDatabaseName(GroupIndex);
            notification.HasIndex(n => n.ActorAccountId);
        });
    }
}

/// <summary>For <c>dotnet ef migrations add</c>; no database is contacted.</summary>
public sealed class NotificationsDesignTimeFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>();
        ModuleDatabaseServiceCollectionExtensions.UseModuleDatabase<NotificationsDbContext>(
            options, "Host=localhost;Database=design_time");
        return new NotificationsDbContext(options.Options);
    }
}
