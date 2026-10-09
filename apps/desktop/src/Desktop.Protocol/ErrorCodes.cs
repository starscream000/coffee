// The protocol's error codes and names, as one table (docs/protocol.md,
// "Error codes"). Error responses carry the name in error.data.name.

namespace Desktop.Protocol;

/// <summary>
/// Every error code the protocol defines, with its name. The JSON-RPC standard
/// codes come first, then the protocol's own. A test checks this table against
/// <c>packages/protocol/src/errors.ts</c>.
/// </summary>
/// <example>
/// <code>
/// if (error.Name == ErrorCodes.ProjectNotOpen) { … }
/// </code>
/// </example>
public static class ErrorCodes
{
    /// <summary>The engine could not parse a line as JSON.</summary>
    public const string ParseError = nameof(ParseError);
    /// <summary>The message is not a valid JSON-RPC request.</summary>
    public const string InvalidRequest = nameof(InvalidRequest);
    /// <summary>The engine does not implement the method.</summary>
    public const string MethodNotFound = nameof(MethodNotFound);
    /// <summary>The request's parameters are invalid.</summary>
    public const string InvalidParams = nameof(InvalidParams);
    /// <summary>An unexpected error inside the engine.</summary>
    public const string InternalError = nameof(InternalError);
    /// <summary>A request before a successful <c>initialize</c>.</summary>
    public const string NotInitialized = nameof(NotInitialized);
    /// <summary><c>initialize</c> with an incompatible protocol version; the engine then exits.</summary>
    public const string IncompatibleProtocol = nameof(IncompatibleProtocol);
    /// <summary>A project request before <c>openProject</c>.</summary>
    public const string ProjectNotOpen = nameof(ProjectNotOpen);
    /// <summary>No or unreadable config file.</summary>
    public const string ProjectInvalid = nameof(ProjectInvalid);
    /// <summary><c>startRun</c> with validation errors (<c>data.diagnostics</c>).</summary>
    public const string StepFilesInvalid = nameof(StepFilesInvalid);
    /// <summary><c>startRun</c> while a run is active.</summary>
    public const string RunInProgress = nameof(RunInProgress);
    /// <summary><c>cancelRun</c> or <c>openSnapshot</c> with an unknown run.</summary>
    public const string RunNotFound = nameof(RunNotFound);
    /// <summary><c>openSnapshot</c> for a step without a snapshot.</summary>
    public const string SnapshotNotFound = nameof(SnapshotNotFound);
    /// <summary>A message over the size limit.</summary>
    public const string MessageTooLarge = nameof(MessageTooLarge);
    /// <summary>A snapshot exists but cannot be shown; <c>data.screenshot</c> has the screenshot.</summary>
    public const string SnapshotUnavailable = nameof(SnapshotUnavailable);

    /// <summary>Every error, from name to numeric code, in the order of the protocol's table.</summary>
    public static IReadOnlyList<KeyValuePair<string, int>> All { get; } =
    [
        new(ParseError, -32700),
        new(InvalidRequest, -32600),
        new(MethodNotFound, -32601),
        new(InvalidParams, -32602),
        new(InternalError, -32603),
        new(NotInitialized, -32001),
        new(IncompatibleProtocol, -32002),
        new(ProjectNotOpen, -32003),
        new(ProjectInvalid, -32004),
        new(StepFilesInvalid, -32005),
        new(RunInProgress, -32006),
        new(RunNotFound, -32007),
        new(SnapshotNotFound, -32008),
        new(MessageTooLarge, -32009),
        new(SnapshotUnavailable, -32010),
    ];

    /// <summary>Finds the name of a numeric error code.</summary>
    /// <param name="code">A code received in an error response.</param>
    /// <returns>The name, or <see langword="null"/> for a code this client does not know (newer engines may send new codes).</returns>
    public static string? NameOf(int code)
    {
        foreach (var (name, value) in All)
        {
            if (value == code)
            {
                return name;
            }
        }

        return null;
    }
}
