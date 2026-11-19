var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Placeholder endpoint for the empty skeleton; the API framework arrives with E01-07.
app.MapGet("/health", () => Results.Ok());

app.Run();
