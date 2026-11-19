using Platform.Testing.Containers;

// One container of each kind for the whole assembly, shared by every test class (ADR-0008).
[assembly: AssemblyFixture(typeof(RabbitMqFixture))]
[assembly: AssemblyFixture(typeof(RedisFixture))]
[assembly: AssemblyFixture(typeof(PostgresFixture))]
