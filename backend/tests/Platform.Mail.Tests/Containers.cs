using Platform.Testing.Containers;

// One Mailpit for the whole assembly (ADR-0008): every test sends to an address of its own.
[assembly: AssemblyFixture(typeof(MailpitFixture))]
