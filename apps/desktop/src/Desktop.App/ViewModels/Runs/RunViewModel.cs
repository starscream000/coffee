// A run as its events describe it. Built only from protocol events, so a live
// run and a finished run read back from its events.ndjson (ADR 0015 of the
// repository) use the same code and look the same.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels.Runs;

/// <summary>Where a run stands.</summary>
public enum RunState
{
    /// <summary>Asked for; no event yet.</summary>
    Starting,
    /// <summary>Running.</summary>
    Running,
    /// <summary>Finished; every test passed or was skipped.</summary>
    Passed,
    /// <summary>Finished; at least one test failed.</summary>
    Failed,
    /// <summary>Finished after a cancel.</summary>
    Cancelled,
    /// <summary>Ended without <c>runFinished</c>: the engine stopped, or the record ends early.</summary>
    Unfinished,
}

/// <summary>A run, live or read back from its folder.</summary>
public sealed partial class RunViewModel : ObservableObject
{
    private readonly Dictionary<string, TestRunViewModel> _tests = new(StringComparer.Ordinal);
    private int _lastSeq;

    /// <summary>Creates an empty run.</summary>
    /// <param name="runId">The run's id.</param>
    /// <param name="resultsDir">Its folder, when known.</param>
    public RunViewModel(string runId, string? resultsDir = null)
    {
        RunId = runId;
        ResultsDir = resultsDir;
    }

    /// <summary>The run's id.</summary>
    public string RunId { get; }

    /// <summary>Absolute path of the run's folder, when known.</summary>
    public string? ResultsDir { get; }

    /// <summary>The test instances, in the order <c>runStarted</c> announced them.</summary>
    public ObservableCollection<TestRunViewModel> Tests { get; } = [];

    /// <summary>Messages about the run as a whole: warnings without a test, gaps, the engine stopping.</summary>
    public ObservableCollection<string> Messages { get; } = [];

    /// <summary>Where it stands.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive), nameof(StatusText), nameof(IsFinished))]
    private RunState _state = RunState.Starting;

    /// <summary>The environment, from <c>runStarted</c>.</summary>
    [ObservableProperty]
    private string _environment = string.Empty;

    /// <summary>The browser, from <c>runStarted</c>.</summary>
    [ObservableProperty]
    private string _browser = string.Empty;

    /// <summary>When it started, local time, from <c>runStarted</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartedText))]
    private DateTimeOffset? _startedAt;

    /// <summary>"3 passed, 1 failed, 0 cancelled, 1 skipped", once finished.</summary>
    [ObservableProperty]
    private string _totalsText = string.Empty;

    /// <summary>The run's duration, once finished.</summary>
    [ObservableProperty]
    private double? _durationMs;

    /// <summary>True while a cancel was asked for and the run has not ended yet.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isCancelling;

    /// <summary>The selected test instance, whose steps are shown.</summary>
    [ObservableProperty]
    private TestRunViewModel? _selectedTest;

    /// <summary>True while the run is starting or running.</summary>
    public bool IsActive => State is RunState.Starting or RunState.Running;

    /// <summary>True once the run ended, finished or not.</summary>
    public bool IsFinished => !IsActive;

    /// <summary>The start time as text, such as "2026-10-10 14:03:12".</summary>
    public string StartedText => StartedAt is { } t ? t.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) : RunId;

    /// <summary>The state for people.</summary>
    public string StatusText => State switch
    {
        RunState.Starting => "Starting…",
        RunState.Running when IsCancelling => "Cancelling…",
        RunState.Running => "Running…",
        RunState.Passed => "Passed",
        RunState.Failed => "Failed",
        RunState.Cancelled => "Cancelled",
        _ => "Did not finish",
    };

    /// <summary>Finds a test instance by id.</summary>
    /// <param name="testId">The instance's id.</param>
    /// <returns>The instance, or null.</returns>
    public TestRunViewModel? Test(string testId) => _tests.GetValueOrDefault(testId);

    /// <summary>Applies one event of this run. Events of other runs are ignored; unknown events are noted.</summary>
    /// <param name="engineEvent">The event.</param>
    public void Apply(EngineEvent engineEvent)
    {
        ArgumentNullException.ThrowIfNull(engineEvent);
        if (engineEvent.RunId != RunId)
        {
            return;
        }

        if (engineEvent.Seq > 0)
        {
            if (_lastSeq > 0 && engineEvent.Seq != _lastSeq + 1)
            {
                Messages.Add($"Events {_lastSeq + 1} to {engineEvent.Seq - 1} of this run are missing; what is shown may be incomplete.");
            }

            _lastSeq = Math.Max(_lastSeq, engineEvent.Seq);
        }

        switch (engineEvent)
        {
            case RunStartedEvent e:
                State = RunState.Running;
                Environment = e.Env;
                Browser = e.Browser;
                StartedAt = DateTimeOffset.TryParse(e.StartedAt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var t) ? t : null;
                foreach (var planned in e.Tests)
                {
                    var test = new TestRunViewModel(planned);
                    _tests[planned.TestId] = test;
                    Tests.Add(test);
                }

                SelectedTest ??= Tests.FirstOrDefault();
                break;
            case TestStartedEvent e when Test(e.TestId) is { } test:
                test.State = TestRunState.Running;
                if (SelectedTest is null || SelectedTest.State is TestRunState.Pending or TestRunState.Skipped or TestRunState.Passed)
                {
                    SelectedTest = test;
                }

                break;
            case StepStartedEvent e when Test(e.TestId) is { } test:
                test.Add(e);
                break;
            case StepPassedEvent e when Test(e.TestId)?.Step(e.StepId) is { } step:
                step.Apply(e);
                break;
            case StepFailedEvent e when Test(e.TestId) is { } test && test.Step(e.StepId) is { } step:
                step.Apply(e);
                test.SelectedStep ??= step;
                break;
            case StepSkippedEvent e when Test(e.TestId)?.Step(e.StepId) is { } step:
                step.Apply(e);
                break;
            case StepSkippedEvent e when Test(e.TestId) is { } test:
                // A step that never started (skipped after a failure) is still listed.
                test.Messages.Add($"{e.StepId} skipped: {e.Message}");
                break;
            case ScreenshotReadyEvent e when Test(e.TestId)?.Step(e.StepId) is { } step:
                step.ScreenshotPath = e.Path;
                break;
            case SnapshotReadyEvent e when Test(e.TestId)?.Step(e.StepId) is { } step:
                step.HasPageState = true;
                break;
            case PageOpenedEvent e when Test(e.TestId) is { } test:
                test.Messages.Add(e.Automatic ? $"A new page opened without a name; it is called \"{e.Page}\"." : $"Page \"{e.Page}\" opened.");
                break;
            case LogEvent e:
                AddLog(e);
                break;
            case TestSkippedEvent e when Test(e.TestId) is { } test:
                test.State = TestRunState.Skipped;
                test.SkipReason = e.Reason;
                break;
            case TestFinishedEvent e when Test(e.TestId) is { } test:
                test.DurationMs = e.DurationMs;
                test.State = e.Status switch
                {
                    RunOutcome.Passed => TestRunState.Passed,
                    RunOutcome.Failed => TestRunState.Failed,
                    RunOutcome.Cancelled => TestRunState.Cancelled,
                    _ => TestRunState.Interrupted,
                };
                foreach (var step in test.AllSteps)
                {
                    step.Interrupt();
                }

                break;
            case RunFinishedEvent e:
                DurationMs = e.DurationMs;
                TotalsText = $"{e.Totals.Passed} passed, {e.Totals.Failed} failed, {e.Totals.Cancelled} cancelled, {e.Totals.Skipped} skipped";
                IsCancelling = false;
                State = e.Status switch
                {
                    RunOutcome.Passed => RunState.Passed,
                    RunOutcome.Failed => RunState.Failed,
                    RunOutcome.Cancelled => RunState.Cancelled,
                    _ => RunState.Unfinished,
                };
                break;
            case UnknownEngineEvent e:
                Messages.Add($"An event this app does not know arrived (\"{e.Method}\"); it is ignored.");
                break;
        }
    }

    /// <summary>Ends a run that will get no more events: the engine stopped, or its record ends without <c>runFinished</c>.</summary>
    /// <param name="why">Why, for people.</param>
    public void EndUnfinished(string why)
    {
        if (!IsActive)
        {
            return;
        }

        foreach (var test in Tests)
        {
            test.Interrupt();
        }

        Messages.Add(why);
        IsCancelling = false;
        State = RunState.Unfinished;
    }

    private void AddLog(LogEvent e)
    {
        var text = e.Code is { } code ? $"{e.Level.ToString().ToLowerInvariant()} {code}: {e.Message}" : $"{e.Level.ToString().ToLowerInvariant()}: {e.Message}";
        if (e.TestId is { } testId && Test(testId) is { } test)
        {
            if (e.StepId is { } stepId && test.Step(stepId) is { } step)
            {
                step.Warnings.Add(text);
            }
            else
            {
                test.Messages.Add(text);
            }
        }
        else if (e.Level is LogLevel.Warn or LogLevel.Error)
        {
            Messages.Add(text);
        }
    }
}
