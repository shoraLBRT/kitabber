using Microsoft.EntityFrameworkCore;
using Platform.Persistence;

namespace Platform.Modules.FixtureNotes;

/// <summary>An internal rule of the module that no other module may use.</summary>
public static class NoteRules
{
    public static int MaxLength => 100;
}

/// <summary>The module's database: schema <c>fixture_notes</c>, the module name in snake_case.</summary>
public sealed class NotesDbContext(DbContextOptions<NotesDbContext> options)
    : ModuleDbContext<NotesDbContext>(options), IModuleDbContext
{
    public static string Schema => "fixture_notes";
}
