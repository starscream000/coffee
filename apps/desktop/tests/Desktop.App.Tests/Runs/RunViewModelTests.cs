// The run as the app shows it, built only from the engine's events
// (instruction D0003, task 9): a passing run, a failure with every detail,
// skipped steps and tests, and a gap in the events.

using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels.Runs;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests.Runs;

public sealed class RunViewModelTests
{
    private const string FileA = "tests/checkout/a.test.yaml";
    private const string FileB = "tests/checkout/b.test.yaml";

    private static void Play(RunViewModel run, params EngineEvent[] events)
    {
        foreach (var e in events)
        {
            run.Apply(e);
        }
    }

    [Fact]
    public void Events_of_other_runs_are_ignored()
    {
        var run = new RunViewModel("run-1");

        run.Apply(new RunScript("run-2").Started(RunScript.Planned("t1", FileA, "A")));

        Assert.Empty(run.Tests);
        Assert.Equal(RunState.Starting, run.State);
    }

    [Fact]
    public void Ending_unfinished_interrupts_what_was_running()
    {
        var run = new RunViewModel("run-1");
        var s = new RunScript("run-1");
        Play(run, s.Started(RunScript.Planned("t1", FileA, "A"), RunScript.Planned("t2", FileB, "B")), s.TestStarted("t1"), s.Step("t1", "s1", "Open"));

        run.EndUnfinished("The record ends here.");

        Assert.Equal(RunState.Unfinished, run.State);
        Assert.Equal("Did not finish", run.StatusText);
        Assert.Equal(StepRunState.Interrupted, run.Test("t1")!.Step("s1")!.State);
        Assert.Equal(TestRunState.Interrupted, run.Test("t2")!.State);
        Assert.Equal("The record ends here.", Assert.Single(run.Messages));
    }

    [Fact]
    public void A_passing_run_is_built_from_its_events()
    {
        var run = new RunViewModel("run-1");
        var s = new RunScript("run-1");

        Play(run,
            s.Started(RunScript.Planned("t1", FileA, "A")),
            s.TestStarted("t1"),
            s.Step("t1", "s1", "Open the shop", StepSection.Before),
            s.Passed("t1", "s1"),
            s.Screenshot("t1", "s1", "/shots/s1.png"),
            s.Snapshot("t1", "s1"),
            s.Step("t1", "s2", "Click submit"),
            s.Step("t1", "s2.1", "Inside the flow", parent: "s2"),
            s.Passed("t1", "s2.1"),
            s.Passed("t1", "s2"),
            s.TestFinished("t1", RunOutcome.Passed),
            s.Finished(RunOutcome.Passed, passed: 1));

        Assert.Equal(RunState.Passed, run.State);
        Assert.Equal("1 passed, 0 failed, 0 cancelled, 0 skipped", run.TotalsText);
        Assert.Equal("local", run.Environment);
        var test = Assert.Single(run.Tests);
        Assert.Same(test, run.SelectedTest);
        Assert.Equal(TestRunState.Passed, test.State);
        Assert.Equal(["before", "steps"], test.Sections.Select(x => x.Name));
        var first = test.Step("s1")!;
        Assert.Equal("/shots/s1.png", first.ScreenshotPath);
        Assert.True(first.HasPageState);
        Assert.Equal(1, test.Step("s2.1")!.Depth);
        Assert.All(test.AllSteps, step => Assert.Equal(StepRunState.Passed, step.State));
        Assert.False(run.IsActive);
    }

    [Fact]
    public void A_failure_shows_its_code_message_hint_expected_actual_and_candidates()
    {
        var run = new RunViewModel("run-1");
        var s = new RunScript("run-1");

        Play(run,
            s.Started(RunScript.Planned("t1", FileA, "A"), RunScript.Planned("t2", FileB, "B", skip: "Waiting for the new checkout")),
            s.TestStarted("t1"),
            s.Step("t1", "s1", "Check the total"),
            s.Warning("t1", "s1", "LocatorFallback", "\"order total\" was found by its second candidate."),
            s.Failed("t1", "s1"),
            s.Skipped("t1", "s2", StepSkipReason.PreviousFailure, "skipped: an earlier step failed"),
            s.Step("t1", "s3", "Log out", StepSection.After),
            s.Skipped("t1", "s3", StepSkipReason.VariableNotSet, "skipped: orderNumber was never set"),
            s.TestFinished("t1", RunOutcome.Failed),
            s.TestSkipped("t2", "Waiting for the new checkout"),
            s.Finished(RunOutcome.Failed, failed: 1, skipped: 1));

        Assert.Equal(RunState.Failed, run.State);
        var test = run.Test("t1")!;
        Assert.Equal(TestRunState.Failed, test.State);
        var failed = test.SelectedStep!;
        Assert.Equal("s1", failed.StepId);
        Assert.True(failed.IsSelected);
        Assert.Equal("TextMismatch", failed.Error!.Code);
        Assert.Equal("Check the price in the demo data.", failed.Error.Hint);
        Assert.Equal("\"£10.00\"", failed.ExpectedText);
        Assert.Equal("\"£12.00\"", failed.ActualText);
        Assert.Equal([0, 2], failed.Candidates.Select(c => c.Matches));
        Assert.Contains("LocatorFallback", Assert.Single(failed.Warnings), StringComparison.Ordinal);
        Assert.Contains(test.Messages, m => m.Contains("an earlier step failed", StringComparison.Ordinal));
        Assert.Equal(StepRunState.Skipped, test.Step("s3")!.State);
        Assert.Equal("skipped: orderNumber was never set", test.Step("s3")!.SkipMessage);
        Assert.Equal(["steps", "after"], test.Sections.Select(x => x.Name));
        var skipped = run.Test("t2")!;
        Assert.Equal(TestRunState.Skipped, skipped.State);
        Assert.Equal("Waiting for the new checkout", skipped.SkipReason);
        Assert.Equal("–", skipped.Glyph);
    }

    [Fact]
    public void A_gap_in_the_events_is_noted()
    {
        var run = new RunViewModel("run-1");
        var s = new RunScript("run-1");
        Play(run, s.Started(RunScript.Planned("t1", FileA, "A")));
        s.Lose();
        s.Lose();
        Play(run, s.TestStarted("t1"));

        Assert.Equal("Events 2 to 3 of this run are missing; what is shown may be incomplete.", Assert.Single(run.Messages));
    }
}
