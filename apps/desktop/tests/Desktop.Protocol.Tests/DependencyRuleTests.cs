// Enforces the dependency rule for Desktop.Protocol: it depends on nothing but
// the .NET base library.

namespace Desktop.Protocol.Tests;

public sealed class DependencyRuleTests
{
    [Fact]
    public void Protocol_references_only_the_base_library()
    {
        // The base library is every assembly of the shared framework, the folder that holds System.Object's assembly.
        var framework = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var others = typeof(Product).Assembly.GetReferencedAssemblies()
            .Where(a => !File.Exists(Path.Combine(framework, a.Name + ".dll")))
            .Select(a => a.Name)
            .ToList();
        Assert.Equal([], others);
    }
}
