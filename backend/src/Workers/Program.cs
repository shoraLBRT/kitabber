// Background workers host: consumes the bus (RabbitMQ) and delivers module outboxes (ADR-0005).
var host = Platform.Workers.WorkerHost.CreateBuilder(args).Build();
host.Run();
