using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Platform.Messaging;
using Platform.Persistence;

namespace Platform.Modules.Search;

/// <summary>
/// The database of the Search module: schema <c>search</c> (ADR-0094). The indexes live in OpenSearch; the schema holds
/// only the inbox of the module's consumers, so a redelivered event is handled once.
/// </summary>
public sealed class SearchDbContext(DbContextOptions<SearchDbContext> options)
    : ModuleDbContext<SearchDbContext>(options), IModuleDbContext
{
    public static string Schema => "search";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddOutboxTables();
    }
}

/// <summary>For <c>dotnet ef migrations add</c>; no database is contacted.</summary>
public sealed class SearchDesignTimeFactory : IDesignTimeDbContextFactory<SearchDbContext>
{
    public SearchDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SearchDbContext>();
        ModuleDatabaseServiceCollectionExtensions.UseModuleDatabase<SearchDbContext>(
            options, "Host=localhost;Database=design_time");
        return new SearchDbContext(options.Options);
    }
}
