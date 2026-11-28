using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Configuration;
using Platform.Http;
using Platform.Http.RateLimiting;
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
/// The Search module. In the workers host (ADR-0094) its consumers keep the indexes of posts, blogs and topics in line
/// with the events of Posts and Blogs, and <see cref="SearchReindexer"/> rebuilds them; it reads the posts and the blogs
/// through <c>IPostSearchSource</c> and <c>IBlogSearchSource</c>, which their modules register in the same host. In the
/// API host (ADR-0097) it only reads the indexes, with no database of its own: <see cref="SearchEndpoints"/>.
/// </summary>
public static class SearchModule
{
    /// <summary>The platform database, as in the migrator (ADR-0004).</summary>
    public const string ConnectionStringName = "Platform";

    public const string ProviderKey = "Search:Provider";
    public const string OpenSearchUrlKey = "Search:OpenSearch:Url";

    /// <summary>The address of OpenSearch in compose.yaml.</summary>
    public const string DefaultOpenSearchUrl = "http://localhost:9200";

    /// <summary>
    /// The rate limit of the suggestions (E14-02, ADR-0097): the pauses in one person's typing, counted by account or,
    /// for a guest, by address. Overridden under <c>RateLimiting:Policies:search.suggestions</c>.
    /// </summary>
    public static RateLimitPolicy SuggestionsPolicy { get; } = new(120, TimeSpan.FromMinutes(1));

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
        AddSearchIndex(services, configuration);
        services.AddScoped<SearchReindexer>();
        return services;
    }

    /// <summary>The settings the API checks at start for this module: those of the engine and of the rate limit.</summary>
    public static IReadOnlyList<Setting> ApiSettings { get; } =
    [
        .. Settings,
        .. RateLimitingSettings.Policy(
            SearchEndpoints.SuggestionsPolicy, SuggestionsPolicy, "search suggestions while a query is typed"),
    ];

    /// <summary>The search engine of <c>Search:Provider</c> and the suggestions of the header's search, in the API host.</summary>
    public static WebApplicationBuilder AddSearchModule(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        AddSearchIndex(builder.Services, builder.Configuration);
        builder.Services.AddRateLimitPolicy(SearchEndpoints.SuggestionsPolicy, SuggestionsPolicy);
        builder.Services.AddApiEndpoints<SearchEndpoints>();
        // The results page (E14-03, ADR-0124) counts into the same limit.
        builder.Services.AddApiEndpoints<SearchResultEndpoints>();
        return builder;
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

    private static void AddSearchIndex(IServiceCollection services, IConfiguration configuration)
    {
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
    }

    private static SearchProvider ReadProvider(IConfiguration configuration) =>
        Enum.TryParse<SearchProvider>(configuration[ProviderKey], ignoreCase: true, out var provider)
            ? provider
            : SearchProvider.OpenSearch;
}
