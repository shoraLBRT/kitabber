using Platform.Testing.Containers;

// One PostgreSQL and one Redis container for the whole assembly; each test class creates its own database (ADR-0008).
[assembly: AssemblyFixture(typeof(PostgresFixture))]
[assembly: AssemblyFixture(typeof(RedisFixture))]
