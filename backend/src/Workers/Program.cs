// Background workers host. Hosted services are registered by the tasks that add them (E01-06).
var builder = Host.CreateApplicationBuilder(args);

var host = builder.Build();
host.Run();
