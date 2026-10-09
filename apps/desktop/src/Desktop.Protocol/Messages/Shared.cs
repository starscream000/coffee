// Types shared by several protocol messages (docs/protocol.md, "Shared types").

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Desktop.Protocol.Messages;

/// <summary>A place in a step file. Lines and columns are 1-based.</summary>
public record Location
{
    /// <summary>Path of the file, relative to the project root, with forward slashes.</summary>
    public required string File { get; init; }
    /// <summary>Line, from 1.</summary>
    public required int Line { get; init; }
    /// <summary>Column, from 1.</summary>
    public required int Column { get; init; }
}

/// <summary>A problem found in a step file, the config or a user action.</summary>
public sealed record Diagnostic : Location
{
    /// <summary>Last line of the problem's range, when known.</summary>
    public int? EndLine { get; init; }
    /// <summary>Last column of the problem's range, when known.</summary>
    public int? EndColumn { get; init; }
    /// <summary>How serious it is.</summary>
    public required DiagnosticSeverity Severity { get; init; }
    /// <summary>Stable code such as <c>UnknownAction</c>.</summary>
    public required string Code { get; init; }
    /// <summary>What is wrong, for people.</summary>
    public required string Message { get; init; }
    /// <summary>What to do about it, when the engine knows.</summary>
    public string? Hint { get; init; }
}

/// <summary>How one target of a step was found (docs/protocol.md, <c>LocatorUse</c>).</summary>
public sealed record LocatorUse
{
    /// <summary>Parameter name, such as <c>target</c>; <c>frame</c> or <c>within</c> when nested.</summary>
    public required string Param { get; init; }
    /// <summary>Target name; absent for inline targets.</summary>
    public string? Target { get; init; }
    /// <summary>Index of the candidate that matched; null when none matched. Above 0 means a fallback.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required int? CandidateIndex { get; init; }
    /// <summary>The candidate used, after interpolation, secrets masked; null when none matched.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required IReadOnlyDictionary<string, JsonElement>? Candidate { get; init; }
    /// <summary>How the target's frame was found.</summary>
    public LocatorUse? Frame { get; init; }
    /// <summary>How the target's containing element was found.</summary>
    public LocatorUse? Within { get; init; }
}

/// <summary>What happened to a step's page snapshot.</summary>
public sealed record SnapshotStatus
{
    /// <summary>The outcome.</summary>
    public required SnapshotState State { get; init; }
    /// <summary>Why recording failed; only with <see cref="SnapshotState.Failed"/>.</summary>
    public string? Reason { get; init; }
}

/// <summary>Why a step failed (docs/protocol.md, <c>ErrorInfo</c>).</summary>
public sealed record ErrorInfo
{
    /// <summary>Stable code such as <c>TargetNotFound</c> or <c>AssertionFailed</c>.</summary>
    public required string Code { get; init; }
    /// <summary>What went wrong, secrets already masked.</summary>
    public required string Message { get; init; }
    /// <summary>What to do about it, when the engine knows.</summary>
    public string? Hint { get; init; }
    /// <summary>The step's place in its file.</summary>
    public Location? Location { get; init; }
    /// <summary>The value an assertion expected, of any JSON type.</summary>
    public JsonElement? Expected { get; init; }
    /// <summary>The value an assertion found, of any JSON type.</summary>
    public JsonElement? Actual { get; init; }
    /// <summary>For a target not found: every candidate tried and how many elements it matched.</summary>
    public IReadOnlyList<CandidateMatches>? Candidates { get; init; }
}

/// <summary>One locator candidate and how many elements it last matched.</summary>
public sealed record CandidateMatches
{
    /// <summary>The candidate.</summary>
    public required IReadOnlyDictionary<string, JsonElement> Candidate { get; init; }
    /// <summary>Number of elements it matched.</summary>
    public required int Matches { get; init; }
}
