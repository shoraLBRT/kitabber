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

    public const int SourceKeyMaxLength = 128;

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
            notification.HasIndex(n => new { n.RecipientAccountId, n.SourceKey }).IsUnique()
                .HasDatabaseName(SourceIndex);
            notification.HasIndex(n => new { n.RecipientAccountId, n.CreatedAt, n.Id })
                .IsDescending(false, true, true)
                .HasDatabaseName(FeedIndex);
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
