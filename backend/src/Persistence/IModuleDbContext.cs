namespace Platform.Persistence;

/// <summary>
/// A module's database context. Every module owns exactly one schema; its tables and its migration history
/// live there and nowhere else (ADR-0001, ADR-0004).
/// </summary>
public interface IModuleDbContext
{
    /// <summary>The module's schema: the module name in snake_case, for example <c>identity</c>.</summary>
    static abstract string Schema { get; }
}
