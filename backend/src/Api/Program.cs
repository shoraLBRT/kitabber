using Platform.Api;
using Platform.Configuration;
using Platform.Http;
using Platform.Http.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.UseInMemoryStoresForOpenApiBuild();
builder.ConfigurePlatform(ApiSettings.All);
builder.AddApiObservability();
builder.Services.AddPlatformApi();
builder.Services.AddPlatformRateLimiting(builder.Configuration);

var app = builder.Build();
app.UsePlatformApi();
app.MapApiObservability();

app.Run();

/// <summary>The entry point, visible to the in-process API tests (WebApplicationFactory).</summary>
public partial class Program;
