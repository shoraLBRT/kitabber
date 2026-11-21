using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Http;
using Platform.Modules.Media.Contracts;
using Platform.Modules.Media.Images;
using Platform.Modules.Media.Storage;
using Platform.Modules.Media.Uploads;
using Platform.Persistence;

namespace Platform.Modules.Media;

/// <summary>The Media module in the API host (ADR-0030): the upload API, its database and the object storage.</summary>
public static class MediaModule
{
    /// <summary>The upload API (<see cref="UploadEndpoints"/>), its database and the storage of <c>Media:Storage:Provider</c>.</summary>
    public static WebApplicationBuilder AddMediaModule(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var configuration = builder.Configuration;
        var services = builder.Services;
        services.AddModuleDbContext<MediaDbContext>(configuration.GetConnectionString(MediaSettings.ConnectionStringName)!);
        services.TryAddSingleton(MediaSettings.ReadLimits(configuration));
        if (MediaSettings.ReadProvider(configuration) == StorageProvider.InMemory)
        {
            services.TryAddSingleton<InMemoryObjectStorage>();
            services.TryAddSingleton<IObjectStorage>(sp => sp.GetRequiredService<InMemoryObjectStorage>());
        }
        else
        {
            // Read when first needed: the host has already checked the settings at start (ADR-0007).
            services.TryAddSingleton(_ => MediaSettings.ReadS3(configuration));
            services.TryAddSingleton<IObjectStorage, S3ObjectStorage>();
        }

        // Read when first needed, like the storage: the host has already checked the settings at start.
        services.TryAddSingleton(_ => new ImgproxyUrls(MediaSettings.ReadImages(configuration)));
        services.AddScoped<MediaUploads>();
        services.AddScoped<IMediaLibrary, MediaLibrary>();
        services.AddApiEndpoints<UploadEndpoints>();
        return builder;
    }
}
