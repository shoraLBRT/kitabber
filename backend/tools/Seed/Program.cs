// Fills the platform database with demo data: `dotnet run --project backend/tools/Seed [-- --Seed:RandomSeed=<int>]`.
return await Platform.Seeding.SeedApp.RunAsync(args, Console.Out, Console.Error, Platform.Seed.SeedCatalog.AddModules);
