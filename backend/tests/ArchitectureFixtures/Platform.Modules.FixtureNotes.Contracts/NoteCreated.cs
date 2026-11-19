namespace Platform.Modules.FixtureNotes.Contracts;

/// <summary>An event other modules may depend on: the allowed way to talk to the module.</summary>
public sealed record NoteCreated(Guid NoteId);
