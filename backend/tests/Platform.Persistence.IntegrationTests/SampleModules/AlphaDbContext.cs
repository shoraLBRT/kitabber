using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Platform.Persistence.IntegrationTests.SampleModules;

/// <summary>
/// A sample module with two migrations: the second one adds a table with reference data, so rolling it back
/// is observable. Real modules follow the same shape.
/// </summary>
public sealed class AlphaDbContext(DbContextOptions<AlphaDbContext> options)
    : ModuleDbContext<AlphaDbContext>(options), IModuleDbContext
{
    public static string Schema => "alpha";

    public DbSet<Note> Notes => Set<Note>();

    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Note>(note => note.Property(n => n.Title).HasMaxLength(200));
        modelBuilder.Entity<Category>(category =>
        {
            category.Property(c => c.Name).HasMaxLength(100);
            // Reference data ships with the migration that creates the table (ADR-0004).
            category.HasData(new Category { Id = 1, Name = "news" }, new Category { Id = 2, Name = "games" });
        });
    }
}

public sealed class Note
{
    public Guid Id { get; set; }

    public required string Title { get; set; }
}

public sealed class Category
{
    public int Id { get; set; }

    public required string Name { get; set; }
}

/// <summary>For <c>dotnet ef migrations add</c>; no database is contacted.</summary>
public sealed class AlphaDesignTimeFactory : IDesignTimeDbContextFactory<AlphaDbContext>
{
    public AlphaDbContext CreateDbContext(string[] args) =>
        new(SampleModuleOptions.Create<AlphaDbContext>());
}
