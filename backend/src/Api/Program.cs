using Platform.Api;
using Platform.Configuration;
using Platform.Http;
using Platform.Http.RateLimiting;
using Platform.Messaging;
using Platform.Modules.Blogs;
using Platform.Modules.Editor;
using Platform.Modules.Identity.SignIn;
using Platform.Realtime;

var builder = WebApplication.CreateBuilder(args);
builder.UseInMemoryStoresForOpenApiBuild();
builder.ConfigurePlatform(ApiSettings.All);
builder.AddApiObservability();
builder.Services.AddPlatformApi();
builder.Services.AddPlatformRateLimiting(builder.Configuration);
builder.AddIdentityModule();
builder.Services.AddEditorModule();
builder.AddBlogsModule();
builder.Services.AddPlatformMessaging(builder.Configuration, bus => bus.AddIdentityOutbox());
builder.Services.AddPlatformRealtime(builder.Configuration);

var app = builder.Build();
app.UsePlatformApi();
app.MapIdentityEndpoints();
app.MapRealtime();
app.MapApiObservability();

app.Run();

/// <summary>The entry point, visible to the in-process API tests (WebApplicationFactory).</summary>
public partial class Program;
