using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Platform.Persistence;

namespace Platform.Architecture.Tests;

/// <summary>An assembly and the names of the assemblies it references, as written in its metadata.</summary>
public sealed record AssemblyNode(string Name, IReadOnlyList<string> References);

/// <summary>A module database context found in an assembly, with the schema it declares.</summary>
public sealed record DbContextNode(string Assembly, string Context, string Schema);

/// <summary>
/// A table of a module database context: its columns and the tables its foreign keys point to, as
/// <c>schema.table</c>.
/// </summary>
public sealed record TableNode(
    string Assembly,
    string Context,
    string Schema,
    string Table,
    IReadOnlyList<string> Columns,
    IReadOnlyList<string> ForeignKeys);

/// <summary>
/// The module boundary rules (docs/ARCHITECTURE.md, «Границы модулей»). Assemblies are classified by name, so
/// the rules apply to every future <c>Platform.Modules.Name</c> and <c>Platform.Modules.Name.Contracts</c>.
/// </summary>
public static class ModuleBoundaries
{
    public const string ModulePrefix = "Platform.Modules.";
    public const string ContractsSegment = "Contracts";
    public const string IdentityModule = "Identity";
    public const string TelegramGateway = "Platform.Telegram.Gateway";

    /// <summary>The composition roots: they may reference any module to register it.</summary>
    public static readonly IReadOnlyList<string> Hosts =
        ["Platform.Api", "Platform.Workers", "Platform.Migrator", "Platform.Seed"];

    /// <summary>Telegram protocol libraries: only the gateway behind <c>ITelegramGateway</c> uses them (ADR-0003).</summary>
    public static readonly IReadOnlyList<string> TelegramLibraries = ["Telegram.Bot", "WTelegramClient", "TdLib"];

    /// <summary>Credential storage and hashing libraries: only the Identity module uses them (ADR-0002).</summary>
    public static readonly IReadOnlyList<string> CredentialLibraries =
    [
        "Microsoft.AspNetCore.Identity",
        "Microsoft.Extensions.Identity",
        "OpenIddict.Core",
        "OpenIddict.EntityFrameworkCore",
        "OpenIddict.Server",
        "BCrypt.Net",
        "Konscious.Security.Cryptography",
    ];

    /// <summary>Parts of a column name that mean mail or a password: such columns live only in Identity (FR-ID-1).</summary>
    public static readonly IReadOnlyList<string> CredentialColumnParts = ["email", "e_mail", "password", "passwd"];

    /// <summary>
    /// Column names (or their last words) that mean an external sign-in identifier, the pair «provider, subject»
    /// (FR-ID-4): such columns live only in Identity.
    /// </summary>
    public static readonly IReadOnlyList<string> CredentialColumnSuffixes =
        ["external_id", "provider_key", "provider_subject", "login_provider", "oidc_subject"];

    /// <summary>The module an assembly belongs to, or <c>null</c> when it is not a module assembly.</summary>
    public static string? ModuleOf(string assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return assembly.StartsWith(ModulePrefix, StringComparison.Ordinal)
            ? assembly[ModulePrefix.Length..].Split('.')[0]
            : null;
    }

    /// <summary>Whether the assembly is a module's public contracts: <c>Platform.Modules.Name.Contracts</c>.</summary>
    public static bool IsContracts(string assembly)
    {
        if (ModuleOf(assembly) is not { } module)
        {
            return false;
        }

        var rest = assembly[(ModulePrefix.Length + module.Length)..].Split('.', StringSplitOptions.RemoveEmptyEntries);
        return rest.Length > 0 && rest[0] == ContractsSegment;
    }

    /// <summary>A module's implementation (its internals), as opposed to its contracts.</summary>
    public static bool IsModuleImplementation(string assembly) =>
        ModuleOf(assembly) is not null && !IsContracts(assembly);

    public static bool IsHost(string assembly) => Hosts.Contains(assembly);

    /// <summary>Every broken reference rule, one line each; empty when the graph is clean.</summary>
    public static IReadOnlyList<string> CheckReferences(IEnumerable<AssemblyNode> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var violations = new List<string>();
        foreach (var node in assemblies)
        {
            foreach (var reference in node.References)
            {
                if (Violation(node.Name, reference) is { } violation)
                {
                    violations.Add(violation);
                }
            }
        }

        return violations;
    }

    /// <summary>Every module database context in the wrong place or with a schema that is not its module's.</summary>
    public static IReadOnlyList<string> CheckSchemas(IEnumerable<DbContextNode> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        var violations = new List<string>();
        foreach (var context in contexts)
        {
            if (!IsModuleImplementation(context.Assembly))
            {
                violations.Add(
                    $"{context.Context} in {context.Assembly}: a module DbContext lives only in a module implementation assembly");
                continue;
            }

            var expected = SnakeCase(ModuleOf(context.Assembly)!);
            if (context.Schema != expected)
            {
                violations.Add(
                    $"{context.Context} in {context.Assembly}: schema '{context.Schema}', the module owns only '{expected}'");
            }
        }

        return violations;
    }

    /// <summary>The module name in snake_case, as <see cref="IModuleDbContext.Schema"/> requires.</summary>
    public static string SnakeCase(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var result = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0 && !char.IsUpper(name[i - 1]))
            {
                result.Append('_');
            }

            result.Append(char.ToLowerInvariant(name[i]));
        }

        return result.ToString();
    }

    /// <summary>
    /// The platform assemblies in <paramref name="directory"/> reachable from <paramref name="roots"/>, read from
    /// metadata without loading them. Only <c>Platform.*</c> assemblies are followed; others appear as references.
    /// </summary>
    public static IReadOnlyList<AssemblyNode> ReadClosure(string directory, IEnumerable<string> roots)
    {
        var found = new Dictionary<string, AssemblyNode>(StringComparer.Ordinal);
        var pending = new Queue<string>(roots);
        while (pending.TryDequeue(out var name))
        {
            var path = Path.Combine(directory, name + ".dll");
            if (found.ContainsKey(name) || !File.Exists(path))
            {
                continue;
            }

            var node = ReadAssembly(path);
            found[name] = node;
            foreach (var reference in node.References.Where(r => r.StartsWith("Platform.", StringComparison.Ordinal)))
            {
                pending.Enqueue(reference);
            }
        }

        return [.. found.Values];
    }

    /// <summary>Reads an assembly's name and references from its metadata.</summary>
    public static AssemblyNode ReadAssembly(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var name = metadata.GetString(metadata.GetAssemblyDefinition().Name);
        var references = metadata.AssemblyReferences
            .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
            .ToList();
        return new AssemblyNode(name, references);
    }

    /// <summary>The module database contexts declared in the given assemblies (loaded from the test's output).</summary>
    public static IReadOnlyList<DbContextNode> ReadDbContexts(IEnumerable<string> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var contexts = new List<DbContextNode>();
        foreach (var name in assemblies)
        {
            foreach (var type in ModuleContextTypes(name))
            {
                var getter = type.GetInterfaceMap(typeof(IModuleDbContext)).TargetMethods.Single();
                var schema = (string)getter.Invoke(null, null)!;
                contexts.Add(new DbContextNode(name, type.FullName ?? type.Name, schema));
            }
        }

        return contexts;
    }

    /// <summary>
    /// The tables of every module database context in the given assemblies, read from the EF Core model the
    /// migrations are generated from. No database is contacted.
    /// </summary>
    public static IReadOnlyList<TableNode> ReadTables(IEnumerable<string> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        return [.. assemblies.SelectMany(name => ModuleContextTypes(name).SelectMany(type => ReadTables(name, type)))];
    }

    /// <summary>The tables of one module database context, as if it were declared in <paramref name="assembly"/>.</summary>
    public static IReadOnlyList<TableNode> ReadTables(string assembly, Type context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = (DbContextOptionsBuilder)Activator.CreateInstance(
            typeof(DbContextOptionsBuilder<>).MakeGenericType(context))!;
        typeof(ModuleDatabaseServiceCollectionExtensions)
            .GetMethod(nameof(ModuleDatabaseServiceCollectionExtensions.UseModuleDatabase))!
            .MakeGenericMethod(context)
            .Invoke(null, [options, "Host=localhost;Database=architecture"]);
        using var db = (DbContext)Activator.CreateInstance(context, options.Options)!;
        var model = db.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        return
        [
            .. model.Tables.Select(table => new TableNode(
                assembly,
                context.FullName ?? context.Name,
                table.Schema ?? "public",
                table.Name,
                [.. table.Columns.Select(c => c.Name)],
                [.. table.ForeignKeyConstraints.Select(fk => $"{fk.PrincipalTable.Schema ?? "public"}.{fk.PrincipalTable.Name}")])),
        ];
    }

    /// <summary>
    /// Every table that breaks FR-ID-1 or the schema rule: a module's table outside its own schema, a foreign key to
    /// another schema, or a column for mail, a password or an external identifier outside the Identity module.
    /// </summary>
    public static IReadOnlyList<string> CheckTables(IEnumerable<TableNode> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        var violations = new List<string>();
        foreach (var table in tables)
        {
            if (!IsModuleImplementation(table.Assembly))
            {
                continue; // CheckSchemas reports a DbContext outside a module.
            }

            var module = ModuleOf(table.Assembly)!;
            var schema = SnakeCase(module);
            var name = $"{table.Schema}.{table.Table} ({table.Context})";
            if (table.Schema != schema)
            {
                violations.Add($"{name}: a table of module {module} lives only in schema '{schema}'");
            }

            foreach (var principal in table.ForeignKeys.Where(p => !p.StartsWith(table.Schema + ".", StringComparison.Ordinal)))
            {
                violations.Add(
                    $"{name} -> {principal}: no foreign keys between schemas; refer to another module by its identifier");
            }

            if (module != IdentityModule)
            {
                foreach (var column in table.Columns.Where(IsCredentialColumn))
                {
                    violations.Add(
                        $"{name}.{column}: mail, passwords and external identifiers live only in module {IdentityModule}; "
                        + "other modules know only account_id");
                }
            }
        }

        return violations;
    }

    /// <summary>Whether a snake_case column holds mail, a password or an external sign-in identifier (FR-ID-1).</summary>
    public static bool IsCredentialColumn(string column)
    {
        ArgumentNullException.ThrowIfNull(column);
        return CredentialColumnParts.Any(part => column.Contains(part, StringComparison.Ordinal))
            || CredentialColumnSuffixes.Any(suffix => column == suffix || column.EndsWith("_" + suffix, StringComparison.Ordinal));
    }

    private static IEnumerable<Type> ModuleContextTypes(string name)
    {
        var assembly = Assembly.Load(new AssemblyName(name));
        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types;
        }

        return types.OfType<Type>().Where(t => !t.IsAbstract && typeof(IModuleDbContext).IsAssignableFrom(t));
    }

    private static string? Violation(string from, string to)
    {
        if (IsModuleImplementation(to) && !IsHost(from))
        {
            var target = ModuleOf(to);
            if (IsContracts(from))
            {
                return $"{from} -> {to}: contracts must not depend on any module's internals";
            }

            if (ModuleOf(from) is { } source && source != target)
            {
                return $"{from} -> {to}: module {source} uses the internals of module {target}; "
                    + $"depend on {ModulePrefix}{target}.{ContractsSegment} or its events";
            }

            if (ModuleOf(from) is null)
            {
                return $"{from} -> {to}: a shared platform assembly must not depend on a module";
            }
        }

        if (Matches(to, TelegramLibraries) && from != TelegramGateway
            && !from.StartsWith(TelegramGateway + ".", StringComparison.Ordinal))
        {
            return $"{from} -> {to}: only {TelegramGateway} talks to Telegram; use ITelegramGateway";
        }

        if (Matches(to, CredentialLibraries) && !(IsModuleImplementation(from) && ModuleOf(from) == IdentityModule))
        {
            return $"{from} -> {to}: credentials live only in module {IdentityModule}; other modules know only account_id";
        }

        return null;
    }

    private static bool Matches(string assembly, IReadOnlyList<string> prefixes) =>
        prefixes.Any(p => assembly.StartsWith(p, StringComparison.Ordinal));
}
