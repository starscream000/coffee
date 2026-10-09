// Tests of the search order of ADR D0004. The inputs are made-up paths with no
// drive letter ("/repo/…"), so both the Windows rules (node.exe, ";" in PATH)
// and the others (node, ":") run on every system: Path.GetFullPath turns
// "/repo" into "C:\repo" on Windows and leaves it alone elsewhere, and the
// PATH value itself never contains a drive letter's colon.

using Desktop.Protocol;

namespace Desktop.Engine.Tests;

public sealed class EngineLocatorTests
{
    private static readonly string AppDir = Full("/repo/apps/desktop/src/Desktop.App/bin");
    private static readonly string Workspace = Full("/repo/pnpm-workspace.yaml");
    private static readonly string CheckoutEngine = Full("/repo/packages/engine/dist/main.js");

    public static TheoryData<bool> BothSystems => [true, false];

    private static string Full(string path) => Path.GetFullPath(path);

    /// <summary>The Node executable's name under the rules being tested.</summary>
    private static string NodeIn(string folder, bool windows) => Full($"{folder}/{(windows ? "node.exe" : "node")}");

    /// <summary>A PATH value under the rules being tested, from folders without drive letters.</summary>
    private static string PathVariable(bool windows, params string[] folders) => string.Join(windows ? ';' : ':', folders);

    private static EngineSearchInput Input(
        bool windows,
        IEnumerable<string> files,
        Dictionary<string, string>? variables = null,
        string? engine = null,
        string? node = null)
    {
        var existing = files.Select(Full).ToHashSet();
        var env = variables ?? new Dictionary<string, string> { ["PATH"] = PathVariable(windows, "/elsewhere", "/repo/tools") };
        return new EngineSearchInput
        {
            ConfiguredEngine = engine,
            ConfiguredNode = node,
            AppBaseDirectory = AppDir,
            GetEnvironmentVariable = name => env.GetValueOrDefault(name),
            FileExists = existing.Contains,
            IsWindows = windows,
        };
    }

    [Fact]
    public void The_variable_name_uses_the_products_prefix()
    {
        Assert.Equal(Product.EnvPrefix + "ENGINE", EngineLocator.EngineVariable);
    }

    [Theory]
    [MemberData(nameof(BothSystems))]
    public void Finds_the_engine_of_the_checkout_and_node_on_the_PATH(bool windows)
    {
        var node = NodeIn("/repo/tools", windows);
        var location = EngineLocator.Locate(Input(windows, [Workspace, CheckoutEngine, node]));
        Assert.Equal(new EngineLaunch(node, CheckoutEngine), location.Launch);
        Assert.Contains(location.Log, l => l.Contains("development checkout: using", StringComparison.Ordinal));
        Assert.Contains(location.Log, l => l.Contains(NodeIn("/elsewhere", windows) + " does not exist", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(BothSystems))]
    public void Uses_only_the_executable_name_of_the_system(bool windows)
    {
        // Only the other system's executable exists: it must not be taken.
        var ex = Assert.Throws<EngineNotFoundException>(() =>
            EngineLocator.Locate(Input(windows, [Workspace, CheckoutEngine, NodeIn("/repo/tools", !windows)])));
        Assert.Contains("Install Node", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(BothSystems))]
    public void Finds_a_bundled_node_in_the_systems_layout(bool windows)
    {
        var bundled = windows ? Full($"{AppDir}/node/node.exe") : Full($"{AppDir}/node/bin/node");
        var location = EngineLocator.Locate(Input(windows, [Workspace, CheckoutEngine, bundled]));
        Assert.Equal(bundled, location.Launch.NodePath);
    }

    [Theory]
    [MemberData(nameof(BothSystems))]
    public void Settings_come_before_the_environment_variable_which_comes_before_the_checkout(bool windows)
    {
        var configured = Full("/repo/configured/main.js");
        var fromVariable = Full("/repo/variable/main.js");
        var node = NodeIn("/repo/tools", windows);
        var files = new[] { Workspace, CheckoutEngine, node, configured, fromVariable };
        var variables = new Dictionary<string, string>
        {
            ["PATH"] = PathVariable(windows, "/repo/tools"),
            [EngineLocator.EngineVariable] = fromVariable,
        };

        Assert.Equal(configured, EngineLocator.Locate(Input(windows, files, variables, engine: configured)).Launch.EngineMainPath);
        Assert.Equal(fromVariable, EngineLocator.Locate(Input(windows, files, variables)).Launch.EngineMainPath);
    }

    [Theory]
    [MemberData(nameof(BothSystems))]
    public void A_configured_engine_that_does_not_exist_is_skipped_and_logged(bool windows)
    {
        var missing = Full("/repo/nowhere/main.js");
        var location = EngineLocator.Locate(Input(windows, [Workspace, CheckoutEngine, NodeIn("/repo/tools", windows)], engine: missing));
        Assert.Equal(CheckoutEngine, location.Launch.EngineMainPath);
        Assert.Contains(location.Log, l => l.Contains(missing + " does not exist", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(BothSystems))]
    public void Configured_node_wins_over_the_PATH(bool windows)
    {
        var node = Full("/repo/my-node");
        var location = EngineLocator.Locate(Input(windows, [Workspace, CheckoutEngine, NodeIn("/repo/tools", windows), node], node: node));
        Assert.Equal(node, location.Launch.NodePath);
    }

    [Theory]
    [MemberData(nameof(BothSystems))]
    public void Says_where_it_looked_when_there_is_no_engine(bool windows)
    {
        var ex = Assert.Throws<EngineNotFoundException>(() => EngineLocator.Locate(Input(windows, [NodeIn("/repo/tools", windows)])));
        Assert.Contains("pnpm build", ex.Message, StringComparison.Ordinal);
        Assert.Equal(4, ex.Searched.Count);
        Assert.Contains(ex.Searched, l => l.Contains("development checkout: not set", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(BothSystems))]
    public void Says_how_to_fix_a_missing_node(bool windows)
    {
        var ex = Assert.Throws<EngineNotFoundException>(() =>
            EngineLocator.Locate(Input(windows, [Workspace, CheckoutEngine], new Dictionary<string, string>())));
        Assert.Contains("Install Node", ex.Message, StringComparison.Ordinal);
    }
}
