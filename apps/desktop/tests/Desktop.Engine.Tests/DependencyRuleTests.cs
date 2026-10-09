// Enforces the dependency rule for Desktop.Engine: it depends on Desktop.Protocol
// and the .NET base library only (no user interface, no engine code).

namespace Desktop.Engine.Tests;

public sealed class DependencyRuleTests
{
    [Fact]
    public void Engine_references_only_the_protocol_and_the_base_library()
    {
        // The base library is every assembly of the shared framework, the folder that holds System.Object's assembly.
        var framework = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var others = typeof(EngineSession).Assembly.GetReferencedAssemblies()
            .Where(a => !File.Exists(Path.Combine(framework, a.Name + ".dll")))
            .Select(a => a.Name)
            .ToList();
        Assert.Equal(["Desktop.Protocol"], others);
    }
}
