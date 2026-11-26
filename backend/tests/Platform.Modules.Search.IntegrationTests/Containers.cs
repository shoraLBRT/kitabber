using Platform.Testing.Containers;

// One PostgreSQL, one Redis and one OpenSearch container for the whole assembly; each test class creates its own
// database and gives its indexes a prefix of its own (ADR-0008, ADR-0094). Redis is for the Api host (ADR-0097).
[assembly: AssemblyFixture(typeof(PostgresFixture))]
[assembly: AssemblyFixture(typeof(RedisFixture))]
[assembly: AssemblyFixture(typeof(OpenSearchFixture))]
