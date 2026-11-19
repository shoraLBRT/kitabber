// Applies database migrations of every module: `dotnet run --project backend/src/Migrator [migrate|status|rollback <schema>]`.
return await Platform.Migrator.MigratorApp.RunAsync(args, Console.Out, Console.Error);
