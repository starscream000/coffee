// Enforces the dependency rule for Desktop.Protocol: it depends on nothing but
// the .NET base library.

namespace Desktop.Protocol.Tests;

public sealed class DependencyRuleTests
{
    [Fact]
    public void Protocol_references_only_the_base_library()
    {
        var references = typeof(Product).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(n => !n.StartsWith("System", StringComparison.Ordinal) && n != "netstandard")
            .ToList();
        Assert.Empty(references);
    }
}
