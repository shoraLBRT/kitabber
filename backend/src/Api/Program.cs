using Platform.Api;
using Platform.Configuration;
using Platform.Http;
using Platform.Http.RateLimiting;
using Platform.Messaging;
using Platform.Modules.Blogs;
using Platform.Modules.Comments;
using Platform.Modules.Editor;
using Platform.Modules.Identity.SignIn;
using Platform.Modules.Media;
using Platform.Modules.Posts;
using Platform.Modules.Preferences;
using Platform.Modules.Reactions;
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
builder.AddPostsModule();
builder.AddCommentsModule();
builder.AddReactionsModule();
builder.AddPreferencesModule();
builder.AddMediaModule();
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
