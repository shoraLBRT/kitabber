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
using Platform.Modules.Notifications;
using Platform.Modules.Posts;
using Platform.Modules.Preferences;
using Platform.Modules.Reactions;
using Platform.Modules.Search;
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
builder.AddNotificationsModule();
builder.AddMediaModule();
builder.AddSearchModule();
builder.Services.AddPlatformMessaging(
    builder.Configuration,
    bus => bus.AddIdentityOutbox().AddBlogsOutbox().AddCommentsOutbox().AddReactionsOutbox().AddPostsOutbox());
builder.Services.AddPlatformRealtime(builder.Configuration);

var app = builder.Build();
app.UsePlatformApi();
app.MapIdentityEndpoints();
app.MapRealtime();
app.MapApiObservability();

app.Run();

/// <summary>The entry point, visible to the in-process API tests (WebApplicationFactory).</summary>
public partial class Program;
