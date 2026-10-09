// The data of error responses (docs/protocol.md, "Error codes"): every error's
// data carries its name; some carry more.

namespace Desktop.Protocol.Messages;

/// <summary>The <c>error.data</c> every error response carries.</summary>
public record ErrorData
{
    /// <summary>The error's name, such as <c>ProjectNotOpen</c>.</summary>
    public required string Name { get; init; }
}

/// <summary>Data of <c>IncompatibleProtocol</c>.</summary>
public sealed record IncompatibleProtocolData : ErrorData
{
    /// <summary>The version the client sent.</summary>
    public required string ClientProtocolVersion { get; init; }
    /// <summary>The version the engine speaks.</summary>
    public required string EngineProtocolVersion { get; init; }
    /// <summary>The engine's own version.</summary>
    public required string EngineVersion { get; init; }
}

/// <summary>Data of <c>StepFilesInvalid</c>.</summary>
public sealed record StepFilesInvalidData : ErrorData
{
    /// <summary>The problems that stopped the run.</summary>
    public required IReadOnlyList<Diagnostic> Diagnostics { get; init; }
}

/// <summary>Data of <c>SnapshotUnavailable</c>.</summary>
public sealed record SnapshotUnavailableData : ErrorData
{
    /// <summary>Absolute path of the step's screenshot, to show instead.</summary>
    public required string Screenshot { get; init; }
}
