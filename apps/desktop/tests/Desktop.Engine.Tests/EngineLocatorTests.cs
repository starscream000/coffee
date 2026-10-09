// Tests of the search order of ADR D0004.

using Desktop.Protocol;

namespace Desktop.Engine.Tests;

public sealed class EngineLocatorTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repo"));
    private static readonly string AppDir = Path.Combine(Root, "apps", "desktop", "src", "Desktop.App", "bin");
    private static readonly string CheckoutEngine = Path.Combine(Root, "packages", "engine", "dist", "main.js");
    private static readonly string PathNode = Path.Combine(Root, "tools", "node");

    private static EngineSearchInput Input(IEnumerable<string> files, Dictionary<string, string>? variables = null, string? engine = null, string? node = null)
    {
        var existing = files.Select(Path.GetFullPath).ToHashSet();
        var env = variables ?? new Dictionary<string, string> { ["PATH"] = Path.GetDirectoryName(PathNode)! };
        return new EngineSearchInput
        {
            ConfiguredEngine = engine,
            ConfiguredNode = node,
            AppBaseDirectory = AppDir,
            GetEnvironmentVariable = name => env.GetValueOrDefault(name),
            FileExists = existing.Contains,
            IsWindows = false,
        };
    }

    [Fact]
    public void The_variable_name_uses_the_products_prefix()
    {
        Assert.Equal(Product.EnvPrefix + "ENGINE", EngineLocator.EngineVariable);
    }

    [Fact]
    public void Finds_the_engine_of_the_checkout_and_node_on_the_PATH()
    {
        var location = EngineLocator.Locate(Input([Path.Combine(Root, "pnpm-workspace.yaml"), CheckoutEngine, PathNode]));
        Assert.Equal(new EngineLaunch(PathNode, CheckoutEngine), location.Launch);
        Assert.Contains(location.Log, l => l.Contains("development checkout: using", StringComparison.Ordinal));
    }

    [Fact]
    public void Settings_come_before_the_environment_variable_which_comes_before_the_checkout()
    {
        var configured = Path.Combine(Root, "configured", "main.js");
        var fromVariable = Path.Combine(Root, "variable", "main.js");
        var files = new[] { Path.Combine(Root, "pnpm-workspace.yaml"), CheckoutEngine, PathNode, configured, fromVariable };
        var variables = new Dictionary<string, string> { ["PATH"] = Path.GetDirectoryName(PathNode)!, [EngineLocator.EngineVariable] = fromVariable };

        Assert.Equal(configured, EngineLocator.Locate(Input(files, variables, engine: configured)).Launch.EngineMainPath);
        Assert.Equal(fromVariable, EngineLocator.Locate(Input(files, variables)).Launch.EngineMainPath);
    }

    [Fact]
    public void A_configured_engine_that_does_not_exist_is_skipped_and_logged()
    {
        var missing = Path.Combine(Root, "nowhere", "main.js");
        var location = EngineLocator.Locate(Input([Path.Combine(Root, "pnpm-workspace.yaml"), CheckoutEngine, PathNode], engine: missing));
        Assert.Equal(CheckoutEngine, location.Launch.EngineMainPath);
        Assert.Contains(location.Log, l => l.Contains(missing + " does not exist", StringComparison.Ordinal));
    }

    [Fact]
    public void Configured_node_wins_over_the_PATH()
    {
        var node = Path.Combine(Root, "my-node");
        var location = EngineLocator.Locate(Input([Path.Combine(Root, "pnpm-workspace.yaml"), CheckoutEngine, PathNode, node], node: node));
        Assert.Equal(node, location.Launch.NodePath);
    }

    [Fact]
    public void Says_where_it_looked_when_there_is_no_engine()
    {
        var ex = Assert.Throws<EngineNotFoundException>(() => EngineLocator.Locate(Input([PathNode])));
        Assert.Contains("pnpm build", ex.Message, StringComparison.Ordinal);
        Assert.Equal(4, ex.Searched.Count);
    }

    [Fact]
    public void Says_how_to_fix_a_missing_node()
    {
        var ex = Assert.Throws<EngineNotFoundException>(() =>
            EngineLocator.Locate(Input([Path.Combine(Root, "pnpm-workspace.yaml"), CheckoutEngine], new Dictionary<string, string>())));
        Assert.Contains("Install Node", ex.Message, StringComparison.Ordinal);
    }
}
