using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Configuration;
using Platform.Messaging;
using Platform.Persistence;

namespace Platform.Modules.Search;

/// <summary>The search engine (ADR-0094).</summary>
public enum SearchProvider
{
    OpenSearch,
    InMemory,
}

/// <summary>
/// The Search module in the workers host (ADR-0094): its consumers keep the indexes of posts, blogs and topics in line
/// with the events of Posts and Blogs, and <see cref="SearchReindexer"/> rebuilds them. It reads the posts and the blogs
/// through <c>IPostSearchSource</c> and <c>IBlogSearchSource</c>, which their modules register in the same host.
/// </summary>
public static class SearchModule
{
    /// <summary>The platform database, as in the migrator (ADR-0004).</summary>
    public const string ConnectionStringName = "Platform";

    public const string ProviderKey = "Search:Provider";
    public const string OpenSearchUrlKey = "Search:OpenSearch:Url";

    /// <summary>The address of OpenSearch in compose.yaml.</summary>
    public const string DefaultOpenSearchUrl = "http://localhost:9200";

    /// <summary>The settings the workers check at start for this module; each one is described in docs/CONFIG.md.</summary>
    public static IReadOnlyList<Setting> Settings { get; } =
    [
        new(ProviderKey, "search engine: OpenSearch, or InMemory for tests")
        {
            Default = nameof(SearchProvider.OpenSearch),
            AllowedValues = Enum.GetNames<SearchProvider>(),
        },
        new(OpenSearchUrlKey, "REST API of OpenSearch, for example http://opensearch:9200")
        {
            Default = DefaultOpenSearchUrl,
        },
    ];

    /// <summary>The module's database, the search engine of <c>Search:Provider</c> and the reindex, in the workers host.</summary>
    public static IServiceCollection AddSearchWorkers(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddModuleDbContext<SearchDbContext>(configuration.GetConnectionString(ConnectionStringName)!);
        if (ReadProvider(configuration) == SearchProvider.InMemory)
        {
            services.TryAddSingleton<InMemorySearchIndex>();
            services.TryAddSingleton<ISearchIndex>(sp => sp.GetRequiredService<InMemorySearchIndex>());
        }
        else
        {
            var url = new Uri(configuration[OpenSearchUrlKey] ?? DefaultOpenSearchUrl, UriKind.Absolute);
            services.TryAddSingleton(new OpenSearchOptions(url));
            services.TryAddSingleton<ISearchIndex, OpenSearchIndex>();
        }

        services.AddScoped<SearchReindexer>();
        return services;
    }

    /// <summary>The module's consumers with the inbox in its schema (ADR-0005); the module publishes nothing.</summary>
    public static IBusRegistrationConfigurator AddSearchConsumers(this IBusRegistrationConfigurator bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        bus.AddConsumer<PostChangedSearchConsumer, PostChangedSearchConsumerDefinition>();
        bus.AddConsumer<BlogChangedSearchConsumer, BlogChangedSearchConsumerDefinition>();
        bus.AddEntityFrameworkOutbox<SearchDbContext>(outbox =>
        {
            outbox.UseModulePostgres();
            outbox.DuplicateDetectionWindow = TimeSpan.FromDays(1);
        });
        return bus;
    }

    private static SearchProvider ReadProvider(IConfiguration configuration) =>
        Enum.TryParse<SearchProvider>(configuration[ProviderKey], ignoreCase: true, out var provider)
            ? provider
            : SearchProvider.OpenSearch;
}
