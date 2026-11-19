using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Platform.Persistence.IntegrationTests.SampleModules;

/// <summary>A second sample module with a table of the same name as Alpha's, to show the schemas are separate.</summary>
public sealed class BetaDbContext(DbContextOptions<BetaDbContext> options)
    : ModuleDbContext<BetaDbContext>(options), IModuleDbContext
{
    public static string Schema => "beta";

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Note>(note => note.Property(n => n.Title).HasMaxLength(200));
    }
}

/// <summary>For <c>dotnet ef migrations add</c>; no database is contacted.</summary>
public sealed class BetaDesignTimeFactory : IDesignTimeDbContextFactory<BetaDbContext>
{
    public BetaDbContext CreateDbContext(string[] args) =>
        new(SampleModuleOptions.Create<BetaDbContext>());
}

/// <summary>A context whose schema breaks the naming rule; registering it must fail.</summary>
public sealed class BadSchemaDbContext(DbContextOptions<BadSchemaDbContext> options)
    : ModuleDbContext<BadSchemaDbContext>(options), IModuleDbContext
{
    public static string Schema => "Bad-Schema";
}

internal static class SampleModuleOptions
{
    public const string OfflineConnectionString = "Host=localhost;Database=design_time";

    public static DbContextOptions<TContext> Create<TContext>(string connectionString = OfflineConnectionString)
        where TContext : ModuleDbContext<TContext>, IModuleDbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        ModuleDatabaseServiceCollectionExtensions.UseModuleDatabase<TContext>(builder, connectionString);
        return builder.Options;
    }
}
