// Enforces the dependency rule for Desktop.Engine: it depends on Desktop.Protocol
// and the .NET base library only (no user interface, no engine code).

namespace Desktop.Engine.Tests;

public sealed class DependencyRuleTests
{
    [Fact]
    public void Engine_references_only_the_protocol_and_the_base_library()
    {
        var references = EngineAssembly.Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(n => !n.StartsWith("System", StringComparison.Ordinal) && n != "netstandard")
            .ToList();
        Assert.All(references, n => Assert.Equal("Desktop.Protocol", n));
    }
}
