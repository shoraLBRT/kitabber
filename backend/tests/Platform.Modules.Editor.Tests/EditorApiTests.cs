using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Platform.Testing.Api;

namespace Platform.Modules.Editor.Tests;

/// <summary>The Api host with the test sign-in; the Editor module keeps no data, so no containers are needed.</summary>
public sealed class EditorApi : PlatformApiFactory
{
    public EditorApi() => WithModule(services => services.AddTestAuthentication());
}

/// <summary><c>POST /api/v1/editor/documents/validation</c>: the happy path, access rights and a validation error.</summary>
public sealed class EditorApiTests(EditorApi api) : IClassFixture<EditorApi>
{
    private const string _path = "/api/v1/editor/documents/validation";
    private const string _account = "0193a6c4-0000-7000-8000-000000000001";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReturnsTheCleanedDocument()
    {
        using var client = api.CreateClient();
        using var response = await client.SendAsync(Post("""
            {"version": 1, "blocks": [
              {"id": "p1", "type": "paragraph", "data": {"text": "<a href=\"javascript:alert(1)\" onclick=\"x()\">hi</a><img src=x onerror=alert(1)>"}}
            ]}
            """).As(_account), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(1, body.GetProperty("version").GetInt32());
        Assert.Equal("<a rel=\"nofollow ugc noopener noreferrer\">hi</a>",
            body.GetProperty("blocks")[0].GetProperty("data").GetProperty("text").GetString());
    }

    [Fact]
    public async Task RequiresASignedInAccount()
    {
        using var client = api.CreateClient();
        using var response = await client.SendAsync(Post("""{"version": 1, "blocks": []}"""), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RejectsAnUnknownBlockWithItsPlace()
    {
        using var client = api.CreateClient();
        using var response = await client.SendAsync(Post("""
            {"version": 1, "blocks": [
              {"id": "a", "type": "delimiter", "data": {}},
              {"id": "b", "type": "button", "data": {"url": "https://example.test"}},
              {"id": "c", "type": "paragraph", "data": {"text": "", "align": "left"}}
            ]}
            """).As(_account), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        var errors = problem.GetProperty("errors");
        Assert.Equal(["blocks[1].type", "blocks[2].data.align"], errors.EnumerateObject().Select(e => e.Name).Order());
    }

    private static HttpRequestMessage Post(string json) =>
        new(HttpMethod.Post, _path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
