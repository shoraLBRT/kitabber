using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Platform.Messaging;
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

    public DbSet<Subsite> Subsites => Set<Subsite>();

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
