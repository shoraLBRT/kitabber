using System.Reflection;
using MassTransit;
using Platform.Mail;

namespace Platform.Architecture.Tests;

/// <summary>
/// A module publishes, sends and mails only through its own outbox (ADR-0005, item 7; #393): the scope's plain
/// <see cref="IPublishEndpoint"/>, <see cref="ISendEndpointProvider"/> and <see cref="IMailer"/> are the outbox of the
/// host's first module, so a module that takes them depends on the order the host registers its modules in.
/// </summary>
public sealed class ModuleOutboxTests
{
    private static readonly Type[] _hostWide = [typeof(IPublishEndpoint), typeof(ISendEndpointProvider), typeof(IMailer)];

    [Fact]
    public void ModulesTakeNoHostWidePublisherOrMailer()
    {
        var platform = ModuleBoundaries.ReadClosure(AppContext.BaseDirectory, ModuleBoundaries.Hosts);
        var modules = platform.Select(a => a.Name).Where(ModuleBoundaries.IsModuleImplementation)
            .Select(name => Assembly.Load(new AssemblyName(name)))
            .ToList();

        // Not vacuous: Identity, which sends mail and publishes account events, is scanned.
        Assert.Contains(modules, a => a.GetName().Name == "Platform.Modules.Identity");
        Assert.Empty(modules.SelectMany(HostWideDependencies));
    }

    [Fact]
    public void ModuleTakingTheHostWidePublisherIsReported()
    {
        var violations = HostWideDependencies(typeof(ModuleOutboxTests).Assembly).ToList();

        Assert.Contains(violations, v => v.Contains(nameof(LeakyPublisher), StringComparison.Ordinal)
            && v.Contains(nameof(IPublishEndpoint), StringComparison.Ordinal));
        Assert.Contains(violations, v => v.Contains(nameof(LeakyPublisher.Mail), StringComparison.Ordinal)
            && v.Contains(nameof(IMailer), StringComparison.Ordinal));
    }

    // Every constructor and method parameter, endpoint lambdas included (they compile into methods of hidden types).
    private static IEnumerable<string> HostWideDependencies(Assembly assembly)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var type in assembly.GetTypes())
        {
            var members = type.GetConstructors(all).Cast<MethodBase>().Concat(type.GetMethods(all));
            foreach (var member in members)
            {
                foreach (var parameter in member.GetParameters().Where(p => _hostWide.Contains(p.ParameterType)))
                {
                    yield return $"{type.FullName}.{member.Name}({parameter.ParameterType.Name} {parameter.Name})";
                }
            }
        }
    }
}

/// <summary>Takes the host-wide publisher and mailer: what <see cref="ModuleOutboxTests"/> reports.</summary>
public sealed class LeakyPublisher(IPublishEndpoint publish)
{
    public Task Publish(object message) => publish.Publish(message);

    public static Task Mail(IMailer mailer) => mailer.SendAsync(
        "reader@example.org", "login-code", System.Globalization.CultureInfo.InvariantCulture, new Dictionary<string, string>());
}
