using Platform.Testing.Containers;

// One PostgreSQL and one OpenSearch container for the whole assembly; each test class creates its own database and
// gives its indexes a prefix of its own (ADR-0008, ADR-0094).
[assembly: AssemblyFixture(typeof(PostgresFixture))]
[assembly: AssemblyFixture(typeof(OpenSearchFixture))]
