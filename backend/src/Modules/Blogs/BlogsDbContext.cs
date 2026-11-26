using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Platform.Messaging;
using Platform.Modules.Blogs.Subscriptions;
using Platform.Persistence;

namespace Platform.Modules.Blogs;

/// <summary>
/// The database of the Blogs module: schema <c>blogs</c>. A blog refers to its owner only by the account_id, with no
/// foreign key to the schema <c>identity</c> (FR-ID-1). The inbox and outbox tables (ADR-0005) let the workers handle
/// each Identity event once.
/// </summary>
public sealed class BlogsDbContext(DbContextOptions<BlogsDbContext> options)
    : ModuleDbContext<BlogsDbContext>(options), IModuleDbContext
{
    /// <summary>One personal blog per account.</summary>
    public const string PersonalBlogIndex = "ux_subsites_personal_owner";

    /// <summary>No two blogs share a nickname.</summary>
    public const string NicknameIndex = "ux_subsites_nickname";

    public static string Schema => "blogs";

    /// <summary>The subscribers of a blog, counted for its profile (ADR-0085).</summary>
    public const string SubscriptionBlogIndex = "ix_subscriptions_subsite";

    public DbSet<Subsite> Subsites => Set<Subsite>();

    /// <summary>Who follows which blog or topic (E11-03, ADR-0085).</summary>
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddOutboxTables();
        modelBuilder.Entity<Subsite>(subsite =>
        {
            subsite.HasKey(s => s.Id);
            subsite.Property(s => s.Id).ValueGeneratedNever();
            subsite.Property(s => s.Type).HasConversion<string>().HasMaxLength(16);
            // The rules count characters as people see them (SubsiteRules.LengthOf); the columns leave room for the
            // UTF-16 units of emoji and combining marks, the rules keep the real limit.
            subsite.Property(s => s.Name).HasMaxLength(SubsiteRules.NameMaxLength * 8);
            subsite.Property(s => s.Description).HasMaxLength(SubsiteRules.DescriptionMaxLength * 8);
            subsite.Property(s => s.Nickname).HasMaxLength(SubsiteRules.NicknameMaxLength);
            subsite.HasIndex(s => s.Nickname).IsUnique().HasDatabaseName(NicknameIndex);
            subsite.HasIndex(s => s.OwnerAccountId).IsUnique().HasDatabaseName(PersonalBlogIndex)
                .HasFilter("type = 'Personal'");
        });
        modelBuilder.Entity<Subscription>(subscription =>
        {
            // One subscription per account and blog: the key makes subscribing again a no-op (ADR-0085). Its first
            // column serves the account's own list («Моя лента», E08-04).
            subscription.HasKey(s => new { s.AccountId, s.SubsiteId });
            subscription.HasIndex(s => s.SubsiteId).HasDatabaseName(SubscriptionBlogIndex);
            subscription.HasOne<Subsite>().WithMany().HasForeignKey(s => s.SubsiteId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

/// <summary>For <c>dotnet ef migrations add</c>; no database is contacted.</summary>
public sealed class BlogsDesignTimeFactory : IDesignTimeDbContextFactory<BlogsDbContext>
{
    public BlogsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BlogsDbContext>();
        ModuleDatabaseServiceCollectionExtensions.UseModuleDatabase<BlogsDbContext>(
            options, "Host=localhost;Database=design_time");
        return new BlogsDbContext(options.Options);
    }
}
