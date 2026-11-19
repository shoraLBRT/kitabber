using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Platform.Testing.Api;

namespace Platform.Api.Tests;

/// <summary>Every error of the API is RFC 9457 problem details with a machine-readable code (ADR-0006).</summary>
public sealed class ProblemDetailsTests : IClassFixture<ProblemDetailsTests.Host>
{
    private readonly HttpClient _client;

    public ProblemDetailsTests(Host host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _client = host.CreateClient();
    }

    [Fact]
    public async Task UnknownRouteIsNotFound()
    {
        var problem = await SendAsync(HttpMethod.Get, "/api/v1/no-such-thing", HttpStatusCode.NotFound);
        Assert.Equal("not_found", Code(problem));
    }

    [Fact]
    public async Task WrongMethodIsMethodNotAllowed()
    {
        var problem = await SendAsync(HttpMethod.Delete, "/api/v1/test/items", HttpStatusCode.MethodNotAllowed);
        Assert.Equal("method_not_allowed", Code(problem));
    }

    [Fact]
    public async Task InvalidBodyIsValidationFailedWithFieldErrors()
    {
        var problem = await SendAsync(HttpMethod.Post, "/api/v1/test/items", HttpStatusCode.BadRequest, """{"name":""}""");

        Assert.Equal("validation_failed", Code(problem));
        Assert.True(problem.GetProperty("errors").TryGetProperty("name", out var messages), problem.ToString());
        Assert.NotEqual(0, messages.GetArrayLength());
    }

    [Fact]
    public async Task ValidBodySucceeds()
    {
        using var response = await _client.PostAsJsonAsync("/api/v1/test/items", new { name = "ok" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task UnreadableBodyIsBadRequest()
    {
        var problem = await SendAsync(HttpMethod.Post, "/api/v1/test/items", HttpStatusCode.BadRequest, "{not json");
        Assert.Equal("bad_request", Code(problem));
    }

    [Fact]
    public async Task ApiProblemExceptionKeepsItsStatusAndCode()
    {
        var problem = await SendAsync(HttpMethod.Get, "/api/v1/test/conflict", HttpStatusCode.Conflict);

        Assert.Equal("test.already_exists", Code(problem));
        Assert.Equal("Already exists", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task UnhandledExceptionIsInternalErrorWithoutDetails()
    {
        var problem = await SendAsync(HttpMethod.Get, "/api/v1/test/crash", HttpStatusCode.InternalServerError);

        Assert.Equal("internal_error", Code(problem));
        Assert.DoesNotContain("secret internals", problem.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnonymousRequestToProtectedEndpointIsUnauthorized()
    {
        var problem = await SendAsync(HttpMethod.Get, "/api/v1/test/admin", HttpStatusCode.Unauthorized);
        Assert.Equal("unauthorized", Code(problem));
    }

    [Fact]
    public async Task RequestWithoutTheRightRoleIsForbidden()
    {
        var problem = await SendAsync(HttpMethod.Get, "/api/v1/test/admin", HttpStatusCode.Forbidden, role: "reader");
        Assert.Equal("forbidden", Code(problem));
    }

    [Fact]
    public async Task RequestWithTheRightRoleSucceeds()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test/admin");
        request.As("tester", "admin");
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string? Code(JsonElement problem) => problem.GetProperty("code").GetString();

    private async Task<JsonElement> SendAsync(
        HttpMethod method, string path, HttpStatusCode expected, string? json = null, string? role = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        if (role is not null)
        {
            request.As("tester", role);
        }

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(expected == response.StatusCode, $"expected {expected}, got {response.StatusCode}: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = JsonDocument.Parse(body).RootElement.Clone();
        Assert.Equal((int)expected, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("title").GetString()), body);
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()), body);
        return problem;
    }

    public sealed class Host : IDisposable
    {
        private readonly PlatformApiFactory _factory = new PlatformApiFactory().WithModule(TestModule.Register);

        public HttpClient CreateClient() => _factory.CreateClient();

        public void Dispose() => _factory.Dispose();
    }
}
