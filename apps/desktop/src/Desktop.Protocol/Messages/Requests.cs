// Parameters and results of every request of the protocol (docs/protocol.md,
// "Requests"). Requests without parameters or with a null result use
// EmptyParams and NullResult.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Desktop.Protocol.Messages;

/// <summary>The request method names of the protocol.</summary>
public static class Methods
{
    /// <summary>Handshake; must be the first request.</summary>
    public const string Initialize = "initialize";
    /// <summary>Cancels any run, closes browsers and exits.</summary>
    public const string Shutdown = "shutdown";
    /// <summary>Opens a project folder.</summary>
    public const string OpenProject = "openProject";
    /// <summary>Lists the project's tests.</summary>
    public const string ListTests = "listTests";
    /// <summary>Lists built-in and user actions.</summary>
    public const string ListActions = "listActions";
    /// <summary>Validates files or an unsaved buffer.</summary>
    public const string Validate = "validate";
    /// <summary>Starts a run.</summary>
    public const string StartRun = "startRun";
    /// <summary>Cancels a run.</summary>
    public const string CancelRun = "cancelRun";
    /// <summary>Shows a step's saved page state in a test browser.</summary>
    public const string OpenSnapshot = "openSnapshot";

    /// <summary>Every method, in the order of docs/protocol.md.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Initialize, Shutdown, OpenProject, ListTests, ListActions, Validate, StartRun, CancelRun, OpenSnapshot];
}

/// <summary>Parameters of a request that takes none (<c>shutdown</c>, <c>listActions</c>).</summary>
public sealed record EmptyParams
{
    /// <summary>The one instance.</summary>
    public static EmptyParams Instance { get; } = new();
}

/// <summary>Result of a request that answers <c>null</c> (<c>shutdown</c>, <c>cancelRun</c>, <c>openSnapshot</c>).</summary>
public sealed record NullResult
{
    /// <summary>The one instance.</summary>
    public static NullResult Instance { get; } = new();
}

/// <summary>A program's name and version.</summary>
public sealed record SoftwareInfo
{
    /// <summary>Name, such as an npm package name.</summary>
    public required string Name { get; init; }
    /// <summary>Version.</summary>
    public required string Version { get; init; }
}

/// <summary>Parameters of <c>initialize</c>.</summary>
public sealed record InitializeParams
{
    /// <summary>The protocol version the client speaks.</summary>
    public required string ProtocolVersion { get; init; }
    /// <summary>The client's name and version.</summary>
    public required SoftwareInfo Client { get; init; }
}

/// <summary>What the engine can do.</summary>
public sealed record EngineCapabilities
{
    /// <summary>Browsers the engine can run; empty when it cannot launch any yet.</summary>
    public required IReadOnlyList<string> Browsers { get; init; }
}

/// <summary>Result of <c>initialize</c>.</summary>
public sealed record InitializeResult
{
    /// <summary>The protocol version the engine speaks.</summary>
    public required string ProtocolVersion { get; init; }
    /// <summary>The engine's name and version.</summary>
    public required SoftwareInfo Engine { get; init; }
    /// <summary>What the engine can do.</summary>
    public required EngineCapabilities Capabilities { get; init; }
}

/// <summary>Parameters of <c>openProject</c>.</summary>
public sealed record OpenProjectParams
{
    /// <summary>Absolute path of the project folder.</summary>
    public required string Root { get; init; }
}

/// <summary>Result of <c>openProject</c>.</summary>
public sealed record OpenProjectResult
{
    /// <summary>Absolute path of the project folder.</summary>
    public required string Root { get; init; }
    /// <summary>Absolute path of the config file.</summary>
    public required string ConfigFile { get; init; }
    /// <summary>Names of the environments the config defines.</summary>
    public required IReadOnlyList<string> Environments { get; init; }
    /// <summary>The default environment; absent only when there are no environments.</summary>
    public string? DefaultEnvironment { get; init; }
    /// <summary>Names of the saved logins the config defines.</summary>
    public required IReadOnlyList<string> Logins { get; init; }
    /// <summary>Config and user-action problems.</summary>
    public required IReadOnlyList<Diagnostic> Diagnostics { get; init; }
}

/// <summary>Parameters of <c>listTests</c>.</summary>
public sealed record ListTestsParams
{
    /// <summary>Only tests with one of these tags, when given.</summary>
    public IReadOnlyList<string>? Tags { get; init; }
}

/// <summary>One test file, as <c>listTests</c> reports it.</summary>
public sealed record TestInfo
{
    /// <summary>Path relative to the project root.</summary>
    public required string File { get; init; }
    /// <summary>The test's name, possibly with <c>${row.…}</c> placeholders.</summary>
    public required string Name { get; init; }
    /// <summary>The test's tags.</summary>
    public required IReadOnlyList<string> Tags { get; init; }
    /// <summary>Number of data rows (test instances).</summary>
    public required int Rows { get; init; }
}

/// <summary>Result of <c>listTests</c>.</summary>
public sealed record ListTestsResult
{
    /// <summary>The tests.</summary>
    public required IReadOnlyList<TestInfo> Tests { get; init; }
}

/// <summary>Where an action comes from.</summary>
public sealed record ActionSource
{
    /// <summary>Built-in or a file of the project.</summary>
    public required ActionSourceKind Kind { get; init; }
    /// <summary>The file, for a user action; relative to the project root.</summary>
    public string? File { get; init; }
}

/// <summary>One action, as <c>listActions</c> reports it.</summary>
public sealed record ActionInfo
{
    /// <summary>Name used in step files, such as <c>fill</c> or <c>shop.addToCart</c>.</summary>
    public required string Name { get; init; }
    /// <summary>What the action does.</summary>
    public required string Description { get; init; }
    /// <summary>The parameter the short form fills, or null when there is no short form.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required string? Shorthand { get; init; }
    /// <summary>JSON Schema of the action's long form; <c>{}</c> when unknown.</summary>
    public required JsonElement ParamsSchema { get; init; }
    /// <summary>Where the action is defined.</summary>
    public required ActionSource Source { get; init; }
}

/// <summary>Result of <c>listActions</c>.</summary>
public sealed record ListActionsResult
{
    /// <summary>Built-in actions, then the open project's user actions.</summary>
    public required IReadOnlyList<ActionInfo> Actions { get; init; }
}

/// <summary>An unsaved editor buffer to validate.</summary>
public sealed record UnsavedFile
{
    /// <summary>The path the buffer will be saved under, relative to the project root.</summary>
    public required string File { get; init; }
    /// <summary>The buffer's text.</summary>
    public required string Text { get; init; }
}

/// <summary>Parameters of <c>validate</c>: exactly one of <see cref="Files"/> and <see cref="Content"/>.</summary>
public sealed record ValidateParams
{
    /// <summary>Files on disk, relative to the project root.</summary>
    public IReadOnlyList<string>? Files { get; init; }
    /// <summary>An unsaved buffer.</summary>
    public UnsavedFile? Content { get; init; }
}

/// <summary>Result of <c>validate</c>.</summary>
public sealed record ValidateResult
{
    /// <summary>Every problem found.</summary>
    public required IReadOnlyList<Diagnostic> Diagnostics { get; init; }
}

/// <summary>Options of a run.</summary>
public sealed record RunOptions
{
    /// <summary>Show the browser window.</summary>
    public bool? Headed { get; init; }
    /// <summary>Browser name from <c>capabilities.browsers</c>.</summary>
    public string? Browser { get; init; }
    /// <summary>Log in again instead of using saved logins.</summary>
    public bool? RefreshLogins { get; init; }
}

/// <summary>Parameters of <c>startRun</c>.</summary>
public sealed record StartRunParams
{
    /// <summary>Test files to run, relative to the project root.</summary>
    public IReadOnlyList<string>? Files { get; init; }
    /// <summary>Run the tests with any of these tags.</summary>
    public IReadOnlyList<string>? Tags { get; init; }
    /// <summary>Environment name; the config's default when absent.</summary>
    public string? Env { get; init; }
    /// <summary>Run options.</summary>
    public RunOptions? Options { get; init; }
}

/// <summary>Result of <c>startRun</c>, returned before the run starts.</summary>
public sealed record StartRunResult
{
    /// <summary>The run's identifier.</summary>
    public required string RunId { get; init; }
    /// <summary>Absolute path of the run's results folder.</summary>
    public required string ResultsDir { get; init; }
}

/// <summary>Parameters of <c>cancelRun</c>.</summary>
public sealed record CancelRunParams
{
    /// <summary>The run to cancel.</summary>
    public required string RunId { get; init; }
}

/// <summary>Parameters of <c>openSnapshot</c>.</summary>
public sealed record OpenSnapshotParams
{
    /// <summary>The run.</summary>
    public required string RunId { get; init; }
    /// <summary>The test instance.</summary>
    public required string TestId { get; init; }
    /// <summary>The step.</summary>
    public required string StepId { get; init; }
}
