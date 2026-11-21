using Microsoft.Extensions.DependencyInjection;
using Platform.Http;
using Platform.Modules.Editor.Blocks;
using Platform.Modules.Editor.Contracts;

namespace Platform.Modules.Editor;

/// <summary>Registers the Editor module: the block schemas, their validator and the editor endpoints (ADR-0023).</summary>
public static class EditorModule
{
    public static IServiceCollection AddEditorModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<InlineHtml>();
        services.AddSingleton<IBlockDocumentValidator>(sp =>
            new BlockDocumentValidator(BlockSchemas.All(sp.GetRequiredService<InlineHtml>())));
        services.AddApiEndpoints<EditorEndpoints>();
        return services;
    }
}
