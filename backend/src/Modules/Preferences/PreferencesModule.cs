using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Http;
using Platform.Persistence;

namespace Platform.Modules.Preferences;

/// <summary>
/// The Preferences module in the API host (ADR-0070): the settings of a person kept on the server, so that every
/// device applies the same choice, their API and their database.
/// </summary>
public static class PreferencesModule
{
    /// <summary>The platform database, as in the migrator (ADR-0004).</summary>
    public const string ConnectionStringName = "Platform";

    public static WebApplicationBuilder AddPreferencesModule(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddModuleDbContext<PreferencesDbContext>(
            builder.Configuration.GetConnectionString(ConnectionStringName)!);
        builder.Services.AddApiEndpoints<PreferenceEndpoints>();
        return builder;
    }
}
