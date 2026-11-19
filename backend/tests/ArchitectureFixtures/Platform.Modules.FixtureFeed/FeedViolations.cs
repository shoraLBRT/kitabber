using Microsoft.EntityFrameworkCore;
using Platform.Modules.FixtureNotes;
using Platform.Modules.FixtureNotes.Contracts;
using Platform.Persistence;

namespace Platform.Modules.FixtureFeed;

/// <summary>Uses an event of another module (allowed) and its internal rule (the deliberate violation).</summary>
public static class FeedBuilder
{
    public static Type SubscribesTo => typeof(NoteCreated);

    public static int PreviewLength => NoteRules.MaxLength;
}

/// <summary>Takes the schema of another module: the deliberate schema violation.</summary>
public sealed class FeedDbContext(DbContextOptions<FeedDbContext> options)
    : ModuleDbContext<FeedDbContext>(options), IModuleDbContext
{
    public static string Schema => "fixture_notes";
}
