// Builds the events of a run as the engine sends them, numbering them in
// order, so that tests can play a run into the app step by step.

using System.Text.Json;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests.Fakes;

/// <summary>The events of one run, numbered from 1.</summary>
internal sealed class RunScript(string runId)
{
    private int _seq;

    public string RunId { get; } = runId;

    public static PlannedTest Planned(string testId, string file, string name, int? row = null, string? skip = null) =>
        new() { TestId = testId, File = file, Name = name, Row = row, Skip = skip };

    public RunStartedEvent Started(params PlannedTest[] tests) => new()
    {
        RunId = RunId,
        Seq = Next(),
        Env = "local",
        Browser = "chromium",
        Settings = new RunSettings { Viewport = new Viewport { Width = 1280, Height = 720 }, Locale = "en-GB", Timezone = "UTC" },
        StartedAt = "2026-10-10T09:15:00Z",
        Tests = tests,
    };

    public TestStartedEvent TestStarted(string testId) => new() { RunId = RunId, Seq = Next(), TestId = testId, StartedAt = "2026-10-10T09:15:01Z" };

    public StepStartedEvent Step(string testId, string stepId, string title, StepSection section = StepSection.Steps, string? parent = null, string file = "tests/a.test.yaml", int line = 5) => new()
    {
        RunId = RunId,
        Seq = Next(),
        TestId = testId,
        StepId = stepId,
        ParentStepId = parent,
        Section = section,
        Action = "click",
        Params = JsonSerializer.SerializeToElement(new { target = "submit" }),
        Page = "main",
        Title = title,
        Location = new Location { File = file, Line = line, Column = 5 },
    };

    public StepPassedEvent Passed(string testId, string stepId, SnapshotState snapshot = SnapshotState.Saved) => new()
    {
        RunId = RunId,
        Seq = Next(),
        TestId = testId,
        StepId = stepId,
        DurationMs = 120,
        Locators = [],
        Snapshot = new SnapshotStatus { State = snapshot },
    };

    public StepFailedEvent Failed(string testId, string stepId) => new()
    {
        RunId = RunId,
        Seq = Next(),
        TestId = testId,
        StepId = stepId,
        DurationMs = 5000,
        Error = new ErrorInfo
        {
            Code = "TextMismatch",
            Message = "The text of \"order total\" is \"£12.00\", not \"£10.00\".",
            Hint = "Check the price in the demo data.",
            Expected = JsonSerializer.SerializeToElement("£10.00"),
            Actual = JsonSerializer.SerializeToElement("£12.00"),
            Candidates =
            [
                new CandidateMatches { Candidate = new Dictionary<string, JsonElement> { ["testId"] = JsonSerializer.SerializeToElement("total") }, Matches = 0 },
                new CandidateMatches { Candidate = new Dictionary<string, JsonElement> { ["css"] = JsonSerializer.SerializeToElement(".total") }, Matches = 2 },
            ],
        },
        Locators = [],
        Snapshot = new SnapshotStatus { State = SnapshotState.Saved },
    };

    public StepSkippedEvent Skipped(string testId, string stepId, StepSkipReason reason, string message) =>
        new() { RunId = RunId, Seq = Next(), TestId = testId, StepId = stepId, Reason = reason, Message = message };

    public ScreenshotReadyEvent Screenshot(string testId, string stepId, string path) =>
        new() { RunId = RunId, Seq = Next(), TestId = testId, StepId = stepId, Page = "main", Path = path, Width = 1280, Height = 720 };

    public SnapshotReadyEvent Snapshot(string testId, string stepId) => new() { RunId = RunId, Seq = Next(), TestId = testId, StepId = stepId, Page = "main" };

    public LogEvent Warning(string testId, string stepId, string code, string message) =>
        new() { RunId = RunId, Seq = Next(), Level = LogLevel.Warn, Code = code, Message = message, TestId = testId, StepId = stepId };

    public TestSkippedEvent TestSkipped(string testId, string reason) => new() { RunId = RunId, Seq = Next(), TestId = testId, Reason = reason };

    public TestFinishedEvent TestFinished(string testId, RunOutcome status) => new() { RunId = RunId, Seq = Next(), TestId = testId, Status = status, DurationMs = 1500 };

    public RunFinishedEvent Finished(RunOutcome status, int passed = 0, int failed = 0, int cancelled = 0, int skipped = 0) => new()
    {
        RunId = RunId,
        Seq = Next(),
        Status = status,
        DurationMs = 2100,
        Totals = new RunTotals { Passed = passed, Failed = failed, Cancelled = cancelled, Skipped = skipped },
    };

    /// <summary>Skips a number, as if an event were lost.</summary>
    public void Lose() => Next();

    private int Next() => ++_seq;
}
