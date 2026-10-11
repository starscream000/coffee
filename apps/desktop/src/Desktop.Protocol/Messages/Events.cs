// The events the engine sends (docs/protocol.md, "Events"). A run's events
// carry runId and seq (seq grows by one per event within a run); a
// recording's events carry recordingId instead.

using System.Text.Json;

namespace Desktop.Protocol.Messages;

/// <summary>Base of every event the engine sends: a run's, or a recording's.</summary>
public abstract record ProtocolEvent;

/// <summary>Base of every event of a run.</summary>
public abstract record EngineEvent : ProtocolEvent
{
    /// <summary>The run the event belongs to.</summary>
    public required string RunId { get; init; }
    /// <summary>Sequence number within the run, increasing by one per event, so gaps can be detected.</summary>
    public required int Seq { get; init; }
}

/// <summary>Browser settings of a run.</summary>
public sealed record RunSettings
{
    /// <summary>Viewport size.</summary>
    public required Viewport Viewport { get; init; }
    /// <summary>Browser locale, such as <c>en-GB</c>.</summary>
    public required string Locale { get; init; }
    /// <summary>Time zone, such as <c>Europe/London</c>.</summary>
    public required string Timezone { get; init; }
}

/// <summary>A viewport size in CSS pixels.</summary>
public sealed record Viewport
{
    /// <summary>Width.</summary>
    public required int Width { get; init; }
    /// <summary>Height.</summary>
    public required int Height { get; init; }
}

/// <summary>A test instance announced at the start of a run.</summary>
public sealed record PlannedTest
{
    /// <summary>Test instance id: file plus <c>#</c> and the row index. Treat as opaque.</summary>
    public required string TestId { get; init; }
    /// <summary>The test file, relative to the project root.</summary>
    public required string File { get; init; }
    /// <summary>The test's name, with row values filled in.</summary>
    public required string Name { get; init; }
    /// <summary>Data-row index, for tests with data.</summary>
    public int? Row { get; init; }
    /// <summary>The test's <c>skip</c> text, when it will be skipped.</summary>
    public string? Skip { get; init; }
}

/// <summary><c>runStarted</c>: the run began.</summary>
public sealed record RunStartedEvent : EngineEvent
{
    /// <summary>Environment name.</summary>
    public required string Env { get; init; }
    /// <summary>Browser name.</summary>
    public required string Browser { get; init; }
    /// <summary>Browser settings.</summary>
    public required RunSettings Settings { get; init; }
    /// <summary>Start time, ISO 8601 UTC.</summary>
    public required string StartedAt { get; init; }
    /// <summary>Every test instance of the run, in order.</summary>
    public required IReadOnlyList<PlannedTest> Tests { get; init; }
}

/// <summary><c>testStarted</c>: a test instance began.</summary>
public sealed record TestStartedEvent : EngineEvent
{
    /// <summary>The test instance.</summary>
    public required string TestId { get; init; }
    /// <summary>Start time, ISO 8601 UTC.</summary>
    public required string StartedAt { get; init; }
}

/// <summary><c>stepStarted</c>: a step began.</summary>
public sealed record StepStartedEvent : EngineEvent
{
    /// <summary>The test instance.</summary>
    public required string TestId { get; init; }
    /// <summary>The step, such as <c>steps.3</c> or <c>steps.4/steps.1</c>.</summary>
    public required string StepId { get; init; }
    /// <summary>The calling step, for a step inside a flow.</summary>
    public string? ParentStepId { get; init; }
    /// <summary>The step's section.</summary>
    public required StepSection Section { get; init; }
    /// <summary>The action's name.</summary>
    public required string Action { get; init; }
    /// <summary>The step's long form, variables not interpolated.</summary>
    public required JsonElement Params { get; init; }
    /// <summary>The page the step acts on.</summary>
    public required string Page { get; init; }
    /// <summary>A short description of the step, for people.</summary>
    public required string Title { get; init; }
    /// <summary>The step's place in its file.</summary>
    public required Location Location { get; init; }
}

/// <summary><c>stepPassed</c>: a step passed.</summary>
public sealed record StepPassedEvent : EngineEvent
{
    /// <summary>The test instance.</summary>
    public required string TestId { get; init; }
    /// <summary>The step.</summary>
    public required string StepId { get; init; }
    /// <summary>How long the step took, in milliseconds.</summary>
    public required double DurationMs { get; init; }
    /// <summary>Every target the step resolved, in order.</summary>
    public required IReadOnlyList<LocatorUse> Locators { get; init; }
    /// <summary>What happened to the page snapshot.</summary>
    public required SnapshotStatus Snapshot { get; init; }
}

/// <summary><c>stepFailed</c>: a step failed.</summary>
public sealed record StepFailedEvent : EngineEvent
{
    /// <summary>The test instance.</summary>
    public required string TestId { get; init; }
    /// <summary>The step.</summary>
    public required string StepId { get; init; }
    /// <summary>How long the step took, in milliseconds.</summary>
    public required double DurationMs { get; init; }
    /// <summary>Why it failed.</summary>
    public required ErrorInfo Error { get; init; }
    /// <summary>Every target the step resolved, in order.</summary>
    public required IReadOnlyList<LocatorUse> Locators { get; init; }
    /// <summary>What happened to the page snapshot.</summary>
    public required SnapshotStatus Snapshot { get; init; }
}

/// <summary><c>stepSkipped</c>: a step did not run.</summary>
public sealed record StepSkippedEvent : EngineEvent
{
    /// <summary>The test instance.</summary>
    public required string TestId { get; init; }
    /// <summary>The step.</summary>
    public required string StepId { get; init; }
    /// <summary>Why.</summary>
    public required StepSkipReason Reason { get; init; }
    /// <summary>Why, for people.</summary>
    public required string Message { get; init; }
    /// <summary>The variable that was never set, for <see cref="StepSkipReason.VariableNotSet"/>.</summary>
    public string? Variable { get; init; }
    /// <summary>The step's section, when no <c>stepStarted</c> was sent for it.</summary>
    public StepSection? Section { get; init; }
    /// <summary>The action, when no <c>stepStarted</c> was sent for the step.</summary>
    public string? Action { get; init; }
    /// <summary>The step's title, when no <c>stepStarted</c> was sent for it.</summary>
    public string? Title { get; init; }
    /// <summary>The step's place in its file, when no <c>stepStarted</c> was sent for it.</summary>
    public Location? Location { get; init; }
}

/// <summary><c>screenshotReady</c>: a step's screenshot was saved.</summary>
public sealed record ScreenshotReadyEvent : EngineEvent
{
    /// <summary>The test instance.</summary>
    public required string TestId { get; init; }
    /// <summary>The step.</summary>
    public required string StepId { get; init; }
    /// <summary>The page shown.</summary>
    public required string Page { get; init; }
    /// <summary>Absolute path of the PNG file.</summary>
    public required string Path { get; init; }
    /// <summary>Width in pixels.</summary>
    public required int Width { get; init; }
    /// <summary>Height in pixels.</summary>
    public required int Height { get; init; }
}

/// <summary><c>snapshotReady</c>: a step's page snapshot was saved.</summary>
public sealed record SnapshotReadyEvent : EngineEvent
{
    /// <summary>The test instance.</summary>
    public required string TestId { get; init; }
    /// <summary>The step.</summary>
    public required string StepId { get; init; }
    /// <summary>The page recorded.</summary>
    public required string Page { get; init; }
}

/// <summary><c>pageOpened</c>: a new page (tab) opened.</summary>
public sealed record PageOpenedEvent : EngineEvent
{
    /// <summary>The test instance.</summary>
    public required string TestId { get; init; }
    /// <summary>The step that opened it.</summary>
    public required string StepId { get; init; }
    /// <summary>The page's name.</summary>
    public required string Page { get; init; }
    /// <summary>True when no step named the page and the engine chose a name.</summary>
    public required bool Automatic { get; init; }
}

/// <summary><c>log</c>: a message from the engine during a run.</summary>
public sealed record LogEvent : EngineEvent
{
    /// <summary>Level.</summary>
    public required LogLevel Level { get; init; }
    /// <summary>The message, secrets masked.</summary>
    public required string Message { get; init; }
    /// <summary>Stable code, such as <c>LocatorFallback</c>.</summary>
    public string? Code { get; init; }
    /// <summary>The test instance, when the message concerns one.</summary>
    public string? TestId { get; init; }
    /// <summary>The step, when the message concerns one.</summary>
    public string? StepId { get; init; }
    /// <summary>The place in a step file, when known.</summary>
    public Location? Location { get; init; }
    /// <summary>Facts that belong to <see cref="Code"/>, documented per code.</summary>
    public JsonElement? Data { get; init; }
}

/// <summary><c>testSkipped</c>: a test instance was skipped (sent instead of testStarted … testFinished).</summary>
public sealed record TestSkippedEvent : EngineEvent
{
    /// <summary>The test instance.</summary>
    public required string TestId { get; init; }
    /// <summary>The test's <c>skip</c> text.</summary>
    public required string Reason { get; init; }
}

/// <summary><c>testFinished</c>: a test instance ended.</summary>
public sealed record TestFinishedEvent : EngineEvent
{
    /// <summary>The test instance.</summary>
    public required string TestId { get; init; }
    /// <summary>Outcome.</summary>
    public required RunOutcome Status { get; init; }
    /// <summary>How long it took, in milliseconds.</summary>
    public required double DurationMs { get; init; }
}

/// <summary>Counts of test instances by outcome.</summary>
public sealed record RunTotals
{
    /// <summary>Passed.</summary>
    public required int Passed { get; init; }
    /// <summary>Failed.</summary>
    public required int Failed { get; init; }
    /// <summary>Cancelled.</summary>
    public required int Cancelled { get; init; }
    /// <summary>Skipped.</summary>
    public required int Skipped { get; init; }
}

/// <summary><c>runFinished</c>: the run ended.</summary>
public sealed record RunFinishedEvent : EngineEvent
{
    /// <summary>Outcome of the whole run.</summary>
    public required RunOutcome Status { get; init; }
    /// <summary>How long it took, in milliseconds.</summary>
    public required double DurationMs { get; init; }
    /// <summary>Counts by outcome.</summary>
    public required RunTotals Totals { get; init; }
}

/// <summary>Base of every event of a recording.</summary>
public abstract record RecordingEvent : ProtocolEvent
{
    /// <summary>The recording the event belongs to.</summary>
    public required string RecordingId { get; init; }
}

/// <summary>A recorded step in the canonical long form, as the file holds it.</summary>
public sealed record RecordedStep
{
    /// <summary>The action, such as <c>click</c>.</summary>
    public required string Action { get; init; }
    /// <summary>The step's parameters in the long form.</summary>
    public required JsonElement Params { get; init; }
    /// <summary>The page it acts on, when not <c>main</c>.</summary>
    public string? Page { get; init; }
    /// <summary>The name of the tab it opens.</summary>
    public string? Opens { get; init; }
}

/// <summary><c>recordingStarted</c>: the recording browser is open at the start address.</summary>
public sealed record RecordingStartedEvent : RecordingEvent
{
    /// <summary>The test file, relative to the project root.</summary>
    public required string File { get; init; }
    /// <summary>The start address as given.</summary>
    public required string StartUrl { get; init; }
}

/// <summary><c>stepRecorded</c>: a step was added.</summary>
public sealed record StepRecordedEvent : RecordingEvent
{
    /// <summary>The step's place in the file's <c>steps</c> (0 is the first <c>goto</c>).</summary>
    public required int Index { get; init; }
    /// <summary>The step.</summary>
    public required RecordedStep Step { get; init; }
    /// <summary>The targets the step made, by name, as the file holds them.</summary>
    public required IReadOnlyDictionary<string, JsonElement> Targets { get; init; }
    /// <summary>Why the step is marked for review, when it is.</summary>
    public string? Review { get; init; }
}

/// <summary><c>stepChanged</c>: a step changed (a fill's text, or <c>opens</c> added).</summary>
public sealed record StepChangedEvent : RecordingEvent
{
    /// <summary>The step's place in the file's <c>steps</c>.</summary>
    public required int Index { get; init; }
    /// <summary>The step as it is now.</summary>
    public required RecordedStep Step { get; init; }
    /// <summary>Why the step is marked for review, when it is.</summary>
    public string? Review { get; init; }
}

/// <summary><c>recordingNotice</c>: something the recorder saw and did not record.</summary>
public sealed record RecordingNoticeEvent : RecordingEvent
{
    /// <summary>What kind of thing it was, such as <c>drag</c>; an open set.</summary>
    public required string Kind { get; init; }
    /// <summary>What it saw, for people.</summary>
    public required string Message { get; init; }
    /// <summary>The page it happened on.</summary>
    public required string Page { get; init; }
    /// <summary>The page's address.</summary>
    public required string Url { get; init; }
}

/// <summary><c>recordingStopped</c>: the recording ended; the file holds what was recorded.</summary>
public sealed record RecordingStoppedEvent : RecordingEvent
{
    /// <summary>The test file, relative to the project root.</summary>
    public required string File { get; init; }
    /// <summary>How it ended.</summary>
    public required RecordingStopReason Reason { get; init; }
    /// <summary>How many steps the file holds.</summary>
    public required int Steps { get; init; }
}

/// <summary><c>recordingVerified</c>: the verify run of a recording ended.</summary>
public sealed record RecordingVerifiedEvent : RecordingEvent
{
    /// <summary>The verify run.</summary>
    public required string RunId { get; init; }
    /// <summary>Whether every step passed.</summary>
    public required VerifyOutcome Status { get; init; }
}

/// <summary>
/// An event this client does not know, from a newer engine. Clients must
/// ignore unknown events; it is passed on so that it can be logged.
/// </summary>
public sealed record UnknownEngineEvent : EngineEvent
{
    /// <summary>The event's method name.</summary>
    public required string Method { get; init; }
    /// <summary>The event's parameters as received.</summary>
    public required JsonElement Params { get; init; }
}

/// <summary>Maps event method names to their C# types and reads events.</summary>
public static class EngineEvents
{
    /// <summary>Every event the protocol defines, from method name to type, in the order of docs/protocol.md.</summary>
    public static IReadOnlyDictionary<string, Type> ByMethod { get; } = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["runStarted"] = typeof(RunStartedEvent),
        ["testStarted"] = typeof(TestStartedEvent),
        ["stepStarted"] = typeof(StepStartedEvent),
        ["stepPassed"] = typeof(StepPassedEvent),
        ["stepFailed"] = typeof(StepFailedEvent),
        ["stepSkipped"] = typeof(StepSkippedEvent),
        ["screenshotReady"] = typeof(ScreenshotReadyEvent),
        ["snapshotReady"] = typeof(SnapshotReadyEvent),
        ["pageOpened"] = typeof(PageOpenedEvent),
        ["log"] = typeof(LogEvent),
        ["testSkipped"] = typeof(TestSkippedEvent),
        ["testFinished"] = typeof(TestFinishedEvent),
        ["runFinished"] = typeof(RunFinishedEvent),
        ["recordingStarted"] = typeof(RecordingStartedEvent),
        ["stepRecorded"] = typeof(StepRecordedEvent),
        ["stepChanged"] = typeof(StepChangedEvent),
        ["recordingNotice"] = typeof(RecordingNoticeEvent),
        ["recordingStopped"] = typeof(RecordingStoppedEvent),
        ["recordingVerified"] = typeof(RecordingVerifiedEvent),
    };

    /// <summary>
    /// Reads a notification's parameters as the event its method names, or as
    /// an <see cref="UnknownEngineEvent"/> when the method is not known.
    /// </summary>
    /// <param name="method">The notification's method.</param>
    /// <param name="parameters">The notification's params.</param>
    /// <returns>The event.</returns>
    /// <exception cref="JsonException">A known event whose fields do not fit its type.</exception>
    public static ProtocolEvent Read(string method, JsonElement parameters)
    {
        if (ByMethod.TryGetValue(method, out var type))
        {
            return (ProtocolEvent)(parameters.Deserialize(type, Json.ProtocolJson.Options)
                ?? throw new JsonException($"Event {method} has null params."));
        }

        var runId = parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("runId", out var r) && r.ValueKind == JsonValueKind.String
            ? r.GetString()!
            : string.Empty;
        var seq = parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("seq", out var s) && s.TryGetInt32(out var n)
            ? n
            : -1;
        return new UnknownEngineEvent { Method = method, Params = parameters.Clone(), RunId = runId, Seq = seq };
    }
}
