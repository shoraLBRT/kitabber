using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Platform.Modules.Search;

/// <summary>Where OpenSearch is (ADR-0094).</summary>
/// <param name="Url">Its REST API, for example <c>http://localhost:9200</c>.</param>
/// <param name="IndexPrefix">Put before every index and alias name: lets tests share one cluster.</param>
public sealed record OpenSearchOptions(Uri Url, string IndexPrefix = "");

/// <summary>
/// <see cref="ISearchIndex"/> over the REST API of OpenSearch (ADR-0094), with no client library. Every index is reached
/// by its alias (<c>posts</c>, <c>blogs</c>): the first write creates the index <c>&lt;alias&gt;_initial</c> behind it,
/// a full reindex writes a new index and moves the alias in one step. Text is analysed by the built-in
/// <c>russian</c> analyzer (Snowball stemming, Russian stop words); the subfield <c>exact</c> keeps the words as written.
/// </summary>
public sealed class OpenSearchIndex : ISearchIndex, IDisposable
{
    /// <summary>The documents a bulk request carries.</summary>
    public const int BulkSize = 500;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly string _prefix;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<SearchIndexKind, bool> _ready = new();

    public OpenSearchIndex(OpenSearchOptions options, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        _http = new HttpClient { BaseAddress = options.Url, Timeout = TimeSpan.FromSeconds(30) };
        _prefix = options.IndexPrefix;
        _time = time;
    }

    public void Dispose() => _http.Dispose();

    /// <summary>The alias searches and writes go through.</summary>
    public string Alias(SearchIndexKind index) =>
        _prefix + index.ToString().ToLowerInvariant();

    public async Task PutAsync(IndexedDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        await EnsureAsync(document.Index, cancellationToken);
        using var response = await _http.PutAsync(
            new Uri($"{Alias(document.Index)}/_doc/{document.Id}", UriKind.Relative),
            Json(Serialize(document)),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task RemoveAsync(SearchIndexKind index, Guid id, CancellationToken cancellationToken)
    {
        using var response = await _http.DeleteAsync(
            new Uri($"{Alias(index)}/_doc/{id}", UriKind.Relative), cancellationToken);
        // Not found: the document, or the whole index, is not there; either way it is gone.
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccessAsync(response, cancellationToken);
        }
    }

    public async Task<int> ReplaceAllAsync(
        SearchIndexKind index, IAsyncEnumerable<IndexedDocument> documents, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var alias = Alias(index);
        var name = $"{alias}_{_time.GetUtcNow().ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture)}";
        await CreateAsync(index, name, withAlias: false, cancellationToken);
        var count = 0;
        var batch = new List<IndexedDocument>(BulkSize);
        await foreach (var document in documents.WithCancellation(cancellationToken))
        {
            batch.Add(document);
            if (batch.Count == BulkSize)
            {
                count += await BulkAsync(name, batch, cancellationToken);
                batch.Clear();
            }
        }

        count += await BulkAsync(name, batch, cancellationToken);
        using (var refreshed = await _http.PostAsync(new Uri($"{name}/_refresh", UriKind.Relative), null, cancellationToken))
        {
            await EnsureSuccessAsync(refreshed, cancellationToken);
        }

        var old = await IndexesOfAsync(alias, cancellationToken);
        var actions = new JsonArray();
        foreach (var previous in old)
        {
            actions.Add(new JsonObject { ["remove"] = new JsonObject { ["index"] = previous, ["alias"] = alias } });
        }

        actions.Add(new JsonObject { ["add"] = new JsonObject { ["index"] = name, ["alias"] = alias } });
        using (var moved = await _http.PostAsync(
            new Uri("_aliases", UriKind.Relative),
            Json(new JsonObject { ["actions"] = actions }.ToJsonString()),
            cancellationToken))
        {
            await EnsureSuccessAsync(moved, cancellationToken);
        }

        foreach (var previous in old)
        {
            using var deleted = await _http.DeleteAsync(new Uri(previous, UriKind.Relative), cancellationToken);
            await EnsureSuccessAsync(deleted, cancellationToken);
        }

        _ready[index] = true;
        return count;
    }

    public async Task<IReadOnlyList<Guid>> SearchAsync(
        SearchIndexKind index, string query, int size, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var request = new JsonObject
        {
            ["size"] = size,
            ["_source"] = false,
            ["query"] = new JsonObject
            {
                ["multi_match"] = new JsonObject
                {
                    ["query"] = query,
                    ["fields"] = new JsonArray([.. Fields(index).Select(f => (JsonNode)f)]),
                    ["operator"] = "and",
                },
            },
        };
        using var response = await _http.PostAsync(
            new Uri($"{Alias(index)}/_search", UriKind.Relative), Json(request.ToJsonString()), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        await EnsureSuccessAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        return [.. result!["hits"]!["hits"]!.AsArray().Select(h => Guid.Parse(h!["_id"]!.GetValue<string>()))];
    }

    public async Task<IReadOnlyList<IndexedDocument>> SuggestAsync(
        SearchIndexKind index, string query, int size, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // bool_prefix: the words before the last one are matched as in SearchAsync, the last one as the start of a
        // word, so «кош» already finds «кошка» while it is typed (ADR-0097).
        var request = new JsonObject
        {
            ["size"] = size,
            ["query"] = new JsonObject
            {
                ["multi_match"] = new JsonObject
                {
                    ["query"] = query,
                    ["type"] = "bool_prefix",
                    ["fields"] = new JsonArray([.. Fields(index).Select(f => (JsonNode)f)]),
                    ["operator"] = "and",
                },
            },
        };
        using var response = await _http.PostAsync(
            new Uri($"{Alias(index)}/_search", UriKind.Relative), Json(request.ToJsonString()), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        await EnsureSuccessAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        return [.. result!["hits"]!["hits"]!.AsArray().Select(h => Deserialize(
            index, Guid.Parse(h!["_id"]!.GetValue<string>()), h["_source"]!.AsObject()))];
    }

    /// <summary>The document <paramref name="id"/> of <paramref name="index"/> from the JSON <see cref="Serialize"/> wrote.</summary>
    public static IndexedDocument Deserialize(SearchIndexKind index, Guid id, JsonObject source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return index switch
        {
            SearchIndexKind.Posts => new IndexedPost(
                id,
                source["blogId"]!.GetValue<Guid>(),
                source["authorAccountId"]!.GetValue<Guid>(),
                source["title"]?.GetValue<string>(),
                source["text"]?.GetValue<string>() ?? string.Empty,
                source["publishedAt"]!.GetValue<DateTimeOffset>()),
            _ => new IndexedBlog(
                id,
                source["kind"]!.GetValue<string>(),
                source["ownerAccountId"]?.GetValue<Guid>(),
                source["name"]?.GetValue<string>(),
                source["nickname"]?.GetValue<string>(),
                source["description"]?.GetValue<string>()),
        };
    }

    // The fields a query searches, the most telling first.
    private static string[] Fields(SearchIndexKind index) => index switch
    {
        SearchIndexKind.Posts => ["title^2", "text"],
        _ => ["name^3", "nickname^2", "description"],
    };

    private async Task EnsureAsync(SearchIndexKind index, CancellationToken cancellationToken)
    {
        if (_ready.ContainsKey(index))
        {
            return;
        }

        using (var head = await _http.SendAsync(
            new HttpRequestMessage(HttpMethod.Head, new Uri(Alias(index), UriKind.Relative)), cancellationToken))
        {
            if (head.StatusCode != HttpStatusCode.NotFound)
            {
                await EnsureSuccessAsync(head, cancellationToken);
                _ready[index] = true;
                return;
            }
        }

        await CreateAsync(index, $"{Alias(index)}_initial", withAlias: true, cancellationToken);
        _ready[index] = true;
    }

    private async Task CreateAsync(SearchIndexKind index, string name, bool withAlias, CancellationToken cancellationToken)
    {
        var definition = Definition(index);
        if (withAlias)
        {
            definition["aliases"] = new JsonObject { [Alias(index)] = new JsonObject() };
        }

        using var response = await _http.PutAsync(
            new Uri(name, UriKind.Relative), Json(definition.ToJsonString()), cancellationToken);
        // Another worker created it first.
        if (response.StatusCode == HttpStatusCode.BadRequest
            && (await response.Content.ReadAsStringAsync(cancellationToken))
            .Contains("resource_already_exists_exception", StringComparison.Ordinal))
        {
            return;
        }

        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<int> BulkAsync(string name, List<IndexedDocument> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        var body = new StringBuilder();
        foreach (var document in batch)
        {
            body.Append(new JsonObject { ["index"] = new JsonObject { ["_id"] = document.Id.ToString() } }.ToJsonString())
                .Append('\n')
                .Append(Serialize(document))
                .Append('\n');
        }

        using var content = new StringContent(body.ToString(), Encoding.UTF8, "application/x-ndjson");
        using var response = await _http.PostAsync(new Uri($"{name}/_bulk", UriKind.Relative), content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        if (result!["errors"]?.GetValue<bool>() == true)
        {
            throw new InvalidOperationException($"OpenSearch refused documents of the bulk write into {name}.");
        }

        return batch.Count;
    }

    private async Task<IReadOnlyList<string>> IndexesOfAsync(string alias, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(new Uri($"_alias/{alias}", UriKind.Relative), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        await EnsureSuccessAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        return [.. result!.Select(p => p.Key)];
    }

    // The settings and the mappings of a new index: strict, so a field nobody declared is an error, not a guess.
    private static JsonObject Definition(SearchIndexKind index)
    {
        var properties = index switch
        {
            SearchIndexKind.Posts => new JsonObject
            {
                ["title"] = Text(),
                ["text"] = Text(),
                ["blogId"] = Keyword(),
                ["authorAccountId"] = Keyword(),
                ["publishedAt"] = new JsonObject { ["type"] = "date" },
            },
            _ => new JsonObject
            {
                ["kind"] = Keyword(),
                ["ownerAccountId"] = Keyword(),
                ["name"] = Text(),
                ["nickname"] = new JsonObject
                {
                    ["type"] = "text",
                    ["analyzer"] = "standard",
                    ["fields"] = new JsonObject { ["keyword"] = Keyword() },
                },
                ["description"] = Text(),
            },
        };
        return new JsonObject
        {
            ["settings"] = new JsonObject { ["number_of_shards"] = 1, ["number_of_replicas"] = 0 },
            ["mappings"] = new JsonObject { ["dynamic"] = "strict", ["properties"] = properties },
        };
    }

    private static JsonObject Text() => new()
    {
        ["type"] = "text",
        ["analyzer"] = "russian",
        ["fields"] = new JsonObject { ["exact"] = new JsonObject { ["type"] = "text", ["analyzer"] = "standard" } },
    };

    private static JsonObject Keyword() => new() { ["type"] = "keyword" };

    /// <summary>The JSON stored for <paramref name="document"/>: its fields without the id, which is the key.</summary>
    public static string Serialize(IndexedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, document.GetType(), _json);
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"OpenSearch answered {(int)response.StatusCode} to {response.RequestMessage?.Method} "
                + $"{response.RequestMessage?.RequestUri}: {body}",
                null,
                response.StatusCode);
        }
    }
}
