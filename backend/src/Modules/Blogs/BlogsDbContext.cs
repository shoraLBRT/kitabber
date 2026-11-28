using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Platform.Messaging;
using Platform.Modules.Blogs.Subscriptions;
using Platform.Modules.Blogs.Topics;
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

    /// <summary>The primary key of the nicknames blogs gave up: one record per nickname (ADR-0090).</summary>
    public const string FormerNicknameKey = "pk_former_nicknames";

    public DbSet<Subsite> Subsites => Set<Subsite>();

    /// <summary>The nicknames blogs gave up and whose addresses lead to the blogs' new ones (E04-05, ADR-0090).</summary>
    public DbSet<FormerNickname> FormerNicknames => Set<FormerNickname>();

    /// <summary>Who follows which blog or topic (E11-03, ADR-0085).</summary>
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    /// <summary>The moderators the owners of topics appointed (E11-12, ADR-0121).</summary>
    public DbSet<TopicModerator> TopicModerators => Set<TopicModerator>();

    /// <summary>The offers to hand a topic to another account, waiting for its consent (E11-12, ADR-0121).</summary>
    public DbSet<TopicTransferOffer> TopicTransferOffers => Set<TopicTransferOffer>();

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
            subsite.Property(s => s.Rules).HasMaxLength(SubsiteRules.RulesMaxLength * 8);
            subsite.Property(s => s.Nickname).HasMaxLength(SubsiteRules.NicknameMaxLength);
            subsite.HasIndex(s => s.Nickname).IsUnique().HasDatabaseName(NicknameIndex);
            subsite.HasIndex(s => s.OwnerAccountId).IsUnique().HasDatabaseName(PersonalBlogIndex)
                .HasFilter("type = 'Personal'");
        });
        modelBuilder.Entity<FormerNickname>(former =>
        {
            former.HasKey(f => f.Nickname).HasName(FormerNicknameKey);
            former.Property(f => f.Nickname).HasMaxLength(SubsiteRules.NicknameMaxLength);
            former.HasIndex(f => f.SubsiteId);
            former.HasOne<Subsite>().WithMany().HasForeignKey(f => f.SubsiteId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Subscription>(subscription =>
        {
            // One subscription per account and blog: the key makes subscribing again a no-op (ADR-0085). Its first
            // column serves the account's own list («Моя лента», E08-04).
            subscription.HasKey(s => new { s.AccountId, s.SubsiteId });
            subscription.HasIndex(s => s.SubsiteId).HasDatabaseName(SubscriptionBlogIndex);
            subscription.HasOne<Subsite>().WithMany().HasForeignKey(s => s.SubsiteId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<TopicModerator>(moderator =>
        {
            // One appointment per topic and account; the second index serves «where is this account a moderator» (E15).
            moderator.HasKey(m => new { m.SubsiteId, m.AccountId });
            moderator.HasIndex(m => m.AccountId);
            moderator.HasOne<Subsite>().WithMany().HasForeignKey(m => m.SubsiteId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<TopicTransferOffer>(offer =>
        {
            // At most one offer per topic (ADR-0121): a new offer replaces the old one.
            offer.HasKey(o => o.SubsiteId);
            offer.HasIndex(o => o.ToAccountId);
            offer.HasOne<Subsite>().WithMany().HasForeignKey(o => o.SubsiteId).OnDelete(DeleteBehavior.Cascade);
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
