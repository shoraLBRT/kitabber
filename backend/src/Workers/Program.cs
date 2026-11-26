// Background workers host: consumes the bus (RabbitMQ) and delivers module outboxes (ADR-0005).
// `search-reindex` rebuilds the search indexes instead and exits (ADR-0094).
if (args is [Platform.Modules.Search.SearchReindexCommand.Name, .. var options])
{
    using var command = Platform.Workers.WorkerHost.CreateBuilder(options).Build();
    return await Platform.Modules.Search.SearchReindexCommand.RunAsync(command.Services, Console.Out, Console.Error);
}

using var host = Platform.Workers.WorkerHost.CreateBuilder(args).Build();
await host.RunAsync();
return 0;
