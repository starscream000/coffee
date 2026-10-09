// Finds Node and the engine's entry script in the order of ADR D0004: settings,
// the product's ENGINE environment variable, an engine bundled with the app,
// then the engine of a development checkout above the app.

using Desktop.Protocol;

namespace Desktop.Engine;

/// <summary>How to start an engine: the Node executable and the engine's entry script.</summary>
/// <param name="NodePath">Absolute path of the Node executable, or a bare <c>node</c> resolved on the PATH.</param>
/// <param name="EngineMainPath">Absolute path of the engine's <c>dist/main.js</c>.</param>
public sealed record EngineLaunch(string NodePath, string EngineMainPath)
{
    /// <summary>The arguments after Node: the entry script and <c>--stdio</c>.</summary>
    public IReadOnlyList<string> Arguments => [EngineMainPath, "--stdio"];
}

/// <summary>Where the locator may look; production code fills it from the machine, tests from fakes.</summary>
public sealed record EngineSearchInput
{
    /// <summary>The engine path from the app's settings, if set.</summary>
    public string? ConfiguredEngine { get; init; }

    /// <summary>The Node path from the app's settings, if set.</summary>
    public string? ConfiguredNode { get; init; }

    /// <summary>The folder of the app's executable.</summary>
    public required string AppBaseDirectory { get; init; }

    /// <summary>Reads an environment variable; null when unset.</summary>
    public required Func<string, string?> GetEnvironmentVariable { get; init; }

    /// <summary>Says whether a file exists.</summary>
    public required Func<string, bool> FileExists { get; init; }

    /// <summary>True on Windows, where executables end in <c>.exe</c> and PATH entries are split by <c>;</c>.</summary>
    public bool IsWindows { get; init; } = OperatingSystem.IsWindows();

    /// <summary>The search input for this machine.</summary>
    /// <param name="configuredEngine">The engine path from the settings.</param>
    /// <param name="configuredNode">The Node path from the settings.</param>
    /// <returns>The input.</returns>
    public static EngineSearchInput ForThisMachine(string? configuredEngine, string? configuredNode) => new()
    {
        ConfiguredEngine = configuredEngine,
        ConfiguredNode = configuredNode,
        AppBaseDirectory = AppContext.BaseDirectory,
        GetEnvironmentVariable = Environment.GetEnvironmentVariable,
        FileExists = File.Exists,
    };
}

/// <summary>The result of a search: what to start, and where the locator looked.</summary>
/// <param name="Launch">What to start.</param>
/// <param name="Log">One line per place looked at, for the engine log.</param>
public sealed record EngineLocation(EngineLaunch Launch, IReadOnlyList<string> Log);

/// <summary>Finds Node and the engine.</summary>
/// <example>
/// <code>
/// var location = EngineLocator.Locate(EngineSearchInput.ForThisMachine(settings.EnginePath, settings.NodePath));
/// </code>
/// </example>
public static class EngineLocator
{
    /// <summary>Name of the environment variable that overrides the engine path, such as <c>CFE_ENGINE</c>.</summary>
    public static string EngineVariable => Product.EnvPrefix + "ENGINE";

    /// <summary>Finds the engine's entry script and the Node executable.</summary>
    /// <param name="input">Where to look.</param>
    /// <returns>What to start and the search log.</returns>
    /// <exception cref="EngineNotFoundException">No engine or no Node was found; the message lists where the locator looked.</exception>
    public static EngineLocation Locate(EngineSearchInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var log = new List<string>();
        var engine = FindEngine(input, log)
            ?? throw new EngineNotFoundException(
                "The engine was not found. Build it with \"pnpm build\" in the repository, or set its path (packages/engine/dist/main.js) in Settings."
                + Environment.NewLine + string.Join(Environment.NewLine, log),
                log);
        var node = FindNode(input, log)
            ?? throw new EngineNotFoundException(
                "Node was not found. Install Node 24, or set the path of the node executable in Settings."
                + Environment.NewLine + string.Join(Environment.NewLine, log),
                log);
        return new EngineLocation(new EngineLaunch(node, engine), log);
    }

    private static string? FindEngine(EngineSearchInput input, List<string> log)
    {
        var candidates = new List<(string Source, string? Path)>
        {
            ("settings", input.ConfiguredEngine),
            ($"environment variable {EngineVariable}", input.GetEnvironmentVariable(EngineVariable)),
            ("bundled with the app", Path.Combine(input.AppBaseDirectory, "engine", "dist", "main.js")),
            ("development checkout", FindCheckoutEngine(input)),
        };
        return FirstExisting(candidates, input, log, "engine");
    }

    private static string? FindCheckoutEngine(EngineSearchInput input)
    {
        for (var dir = new DirectoryInfo(input.AppBaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (input.FileExists(Path.Combine(dir.FullName, "pnpm-workspace.yaml")))
            {
                return Path.Combine(dir.FullName, "packages", "engine", "dist", "main.js");
            }
        }

        return null;
    }

    private static string? FindNode(EngineSearchInput input, List<string> log)
    {
        var executable = input.IsWindows ? "node.exe" : "node";
        var bundled = input.IsWindows
            ? Path.Combine(input.AppBaseDirectory, "node", executable)
            : Path.Combine(input.AppBaseDirectory, "node", "bin", executable);
        var candidates = new List<(string Source, string? Path)>
        {
            ("settings", input.ConfiguredNode),
            ("bundled with the app", bundled),
        };
        var pathVariable = input.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathVariable.Split(input.IsWindows ? ';' : ':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            candidates.Add(("PATH", Path.Combine(dir, executable)));
        }

        return FirstExisting(candidates, input, log, "node");
    }

    private static string? FirstExisting(List<(string Source, string? Path)> candidates, EngineSearchInput input, List<string> log, string what)
    {
        foreach (var (source, path) in candidates)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                log.Add($"{what}: {source}: not set");
                continue;
            }

            var full = Path.GetFullPath(path);
            if (input.FileExists(full))
            {
                log.Add($"{what}: {source}: using {full}");
                return full;
            }

            log.Add($"{what}: {source}: {full} does not exist");
        }

        return null;
    }
}
