using Platform.Testing.Containers;

// One PostgreSQL container for the whole assembly; each test creates its own database in it (ADR-0008).
[assembly: AssemblyFixture(typeof(PostgresFixture))]
