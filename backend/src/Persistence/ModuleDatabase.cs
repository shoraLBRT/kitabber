namespace Platform.Persistence;

/// <summary>A module database known to the migrator: the context type and the schema it owns.</summary>
public sealed record ModuleDatabase(Type ContextType, string Schema)
{
    /// <summary>The migration history table; it lives in the module's own schema.</summary>
    public const string HistoryTable = "__ef_migrations_history";
}
