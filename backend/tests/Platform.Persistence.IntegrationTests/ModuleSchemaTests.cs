using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Platform.Persistence.IntegrationTests.SampleModules;

namespace Platform.Persistence.IntegrationTests;

/// <summary>Every module has its own DbContext and its own schema. These checks need no database.</summary>
public sealed class ModuleSchemaTests
{
    [Fact]
    public void EveryTableAndTheHistoryLiveInTheModuleSchema()
    {
        AssertOwnSchema(new AlphaDbContext(SampleModuleOptions.Create<AlphaDbContext>()), "alpha");
        AssertOwnSchema(new BetaDbContext(SampleModuleOptions.Create<BetaDbContext>()), "beta");
    }

    [Fact]
    public void NamesAreSnakeCase()
    {
        using var db = new AlphaDbContext(SampleModuleOptions.Create<AlphaDbContext>());
        var note = db.Model.FindEntityType(typeof(Note))!;

        Assert.Equal("notes", note.GetTableName());
        Assert.Equal("title", note.FindProperty(nameof(Note.Title))!.GetColumnName());
    }

    [Fact]
    public void TwoModulesCannotShareASchema()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        ModuleDatabase[] modules = [new(typeof(AlphaDbContext), "alpha"), new(typeof(BetaDbContext), "alpha")];

        var e = Assert.Throws<InvalidOperationException>(() => new ModuleMigrator(services, modules));
        Assert.Contains("Schema 'alpha' is claimed by AlphaDbContext, BetaDbContext", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SchemaMustBeSnakeCase()
    {
        var e = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddModuleDbContext<BadSchemaDbContext>("Host=localhost"));
        Assert.Contains("'Bad-Schema'", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigratorWithoutConnectionStringIsAUsageError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await Migrator.MigratorApp.RunAsync(["migrate", "--ConnectionStrings:Platform="], output, error);

        Assert.Equal(2, exit);
        Assert.Contains("connection string 'Platform' is not set", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Every DbContext in the platform is a module context. The migrator references every module, so their
    /// assemblies are next to this test.
    /// </summary>
    [Fact]
    public void EveryPlatformDbContextIsAModuleDbContext()
    {
        var assemblies = Directory.GetFiles(AppContext.BaseDirectory, "Platform.*.dll")
            .Where(path => !Path.GetFileName(path).Contains("Tests", StringComparison.Ordinal))
            .Select(path => Assembly.LoadFrom(path))
            .ToList();
        Assert.Contains(assemblies, a => a.GetName().Name == "Platform.Migrator");

        var offenders = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(DbContext).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => !typeof(IModuleDbContext).IsAssignableFrom(t) || !IsModuleDbContext(t))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(offenders);
    }

    private static bool IsModuleDbContext(Type type)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ModuleDbContext<>))
            {
                return true;
            }
        }

        return false;
    }

    private static void AssertOwnSchema(DbContext db, string schema)
    {
        using (db)
        {
            Assert.Equal(schema, db.Model.GetDefaultSchema());
            Assert.All(db.Model.GetEntityTypes(), entity => Assert.Equal(schema, entity.GetSchema()));
            var relational = RelationalOptionsExtension.Extract(db.GetService<IDbContextOptions>());
            Assert.Equal(schema, relational.MigrationsHistoryTableSchema);
            Assert.Equal(ModuleDatabase.HistoryTable, relational.MigrationsHistoryTableName);
        }
    }
}
