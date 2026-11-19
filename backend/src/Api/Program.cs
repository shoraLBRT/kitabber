using Platform.Api;
using Platform.Configuration;
using Platform.Http;

var builder = WebApplication.CreateBuilder(args);
builder.ConfigurePlatform(ApiSettings.All);
builder.Services.AddPlatformApi();

var app = builder.Build();
app.UsePlatformApi();

// Liveness probe for compose and orchestration; outside the versioned API.
app.MapGet("/health", () => Results.Ok()).ExcludeFromApi();

app.Run();

/// <summary>The entry point, visible to the in-process API tests (WebApplicationFactory).</summary>
public partial class Program;
