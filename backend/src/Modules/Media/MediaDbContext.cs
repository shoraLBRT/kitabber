using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Platform.Persistence;

namespace Platform.Modules.Media;

/// <summary>
/// The database of the Media module: schema <c>media</c>. A file refers to its owner only by the account_id, with no
/// foreign key to the schema <c>identity</c> (FR-ID-1).
/// </summary>
public sealed class MediaDbContext(DbContextOptions<MediaDbContext> options)
    : ModuleDbContext<MediaDbContext>(options), IModuleDbContext
{
    /// <summary>No two files share a storage key.</summary>
    public const string StorageKeyIndex = "ux_media_files_storage_key";

    public static string Schema => "media";

    public DbSet<MediaFile> Files => Set<MediaFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<MediaFile>(file =>
        {
            file.ToTable("media_files");
            file.HasKey(f => f.Id);
            file.Property(f => f.Id).ValueGeneratedNever();
            file.Property(f => f.Kind).HasConversion<string>().HasMaxLength(16);
            file.Property(f => f.ContentType).HasMaxLength(127);
            file.Property(f => f.StorageKey).HasMaxLength(255);
            file.Property(f => f.FileName).HasMaxLength(MediaFile.FileNameMaxLength);
            file.HasIndex(f => f.StorageKey).IsUnique().HasDatabaseName(StorageKeyIndex);
            file.HasIndex(f => f.OwnerAccountId);
        });
    }
}

/// <summary>For <c>dotnet ef migrations add</c>; no database is contacted.</summary>
public sealed class MediaDesignTimeFactory : IDesignTimeDbContextFactory<MediaDbContext>
{
    public MediaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MediaDbContext>();
        ModuleDatabaseServiceCollectionExtensions.UseModuleDatabase<MediaDbContext>(
            options, "Host=localhost;Database=design_time");
        return new MediaDbContext(options.Options);
    }
}
