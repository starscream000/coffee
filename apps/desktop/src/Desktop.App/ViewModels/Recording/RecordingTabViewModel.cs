// The Record tab of one recording (instruction D0004, tasks 15 to 17): the
// steps as they arrive, as cards, with a step that changes (typing) updated in
// place; the notices of what was not recorded; Stop. Once the recording ends,
// the tab checks the test by itself (verifyRecording) and says whether it
// runs: passed, "This test runs"; failed, which step and why, with the test
// opened at that step. A password that matched no secret is explained in plain
// words. If the engine stops, the tab says that what was recorded is kept.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.App.ViewModels.Runs;
using Desktop.Engine;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels.Recording;

/// <summary>Where a recording is.</summary>
public enum RecordingPhase
{
    /// <summary>The browser is open and steps arrive.</summary>
    Recording,
    /// <summary>Stop was asked for.</summary>
    Stopping,
    /// <summary>The recording ended; the test is being run once to check it.</summary>
    Checking,
    /// <summary>The check passed: the test runs.</summary>
    Passed,
    /// <summary>The check failed.</summary>
    Failed,
    /// <summary>The check could not run.</summary>
    NotChecked,
    /// <summary>The engine stopped during the recording; what was recorded is kept.</summary>
    EngineStopped,
}

/// <summary>What a Record tab needs from the workspace.</summary>
/// <param name="Stop">Stops the recording (<c>stopRecording</c>).</param>
/// <param name="Verify">Starts the check of the recording (<c>verifyRecording</c>): the run, or null and why not.</param>
/// <param name="ShowRun">Opens a run in its tab.</param>
/// <param name="OpenTest">Opens the test, at the step on a line when one is given.</param>
/// <param name="Report">Writes a line into the engine log.</param>
public sealed record RecordingHost(
    Func<string, Task> Stop,
    Func<string, Task<(RunViewModel? Run, string? Problem)>> Verify,
    Action<RunViewModel> ShowRun,
    Action<string, int?> OpenTest,
    Action<string> Report);

/// <summary>The Record tab of one recording.</summary>
public sealed partial class RecordingTabViewModel : WorkspaceTabViewModel
{
    private readonly RecordingHost _host;

    /// <summary>Creates the tab for a recording the engine started.</summary>
    /// <param name="recordingId">The recording.</param>
    /// <param name="file">The test file, relative to the project root.</param>
    /// <param name="host">What the tab needs from the workspace.</param>
    public RecordingTabViewModel(string recordingId, string file, RecordingHost host)
    {
        RecordingId = recordingId;
        File = file;
        _host = host;
    }

    /// <summary>The recording's identifier.</summary>
    public string RecordingId { get; }

    /// <summary>The test file, relative to the project root.</summary>
    public string File { get; }

    /// <inheritdoc />
    public override string Title => IsRecording ? "● Recording" : $"Recorded: {File[(File.LastIndexOf('/') + 1)..]}";

    /// <inheritdoc />
    public override string ToolTip => File;

    /// <inheritdoc />
    public override bool CanClose => !IsRecording;

    /// <summary>The recorded steps, in the file's order.</summary>
    public ObservableCollection<RecordedStepViewModel> Steps { get; } = [];

    /// <summary>What was seen but not recorded.</summary>
    public ObservableCollection<RecordingNoticeViewModel> Notices { get; } = [];

    /// <summary>Where the recording is.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecording), nameof(IsChecking), nameof(IsPassed), nameof(IsFailed), nameof(IsDone), nameof(Title), nameof(CanClose), nameof(StatusText))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private RecordingPhase _phase;

    /// <summary>Why the check did not run, or what the engine's stop means; null otherwise.</summary>
    [ObservableProperty]
    private string? _problem;

    /// <summary>The check's run, once it started.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenRunCommand))]
    private RunViewModel? _run;

    /// <summary>The failed step's title, after a failed check.</summary>
    [ObservableProperty]
    private string? _failedStep;

    /// <summary>Why it failed, in the engine's words.</summary>
    [ObservableProperty]
    private string? _failedMessage;

    /// <summary>The failed step's line in the file.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenFailedStepCommand))]
    private int? _failedLine;

    /// <summary>A typed password that matched no secret, explained; null when there is none.</summary>
    [ObservableProperty]
    private string? _passwordHelp;

    /// <summary>True while the browser records (or stops).</summary>
    public bool IsRecording => Phase is RecordingPhase.Recording or RecordingPhase.Stopping;

    /// <summary>True while the test is checked.</summary>
    public bool IsChecking => Phase == RecordingPhase.Checking;

    /// <summary>True when the check passed.</summary>
    public bool IsPassed => Phase == RecordingPhase.Passed;

    /// <summary>True when the check failed.</summary>
    public bool IsFailed => Phase == RecordingPhase.Failed;

    /// <summary>True once the recording has ended, whatever the check said.</summary>
    public bool IsDone => !IsRecording;

    /// <summary>True when something was seen but not recorded.</summary>
    public bool HasNotices => Notices.Count > 0;

    /// <summary>What is happening, in a sentence.</summary>
    public string StatusText => Phase switch
    {
        RecordingPhase.Recording => "Recording in the browser the engine opened. Use the application as a person would: each step appears here. Press Stop, or close the browser, when you are done.",
        RecordingPhase.Stopping => "Stopping the recording…",
        RecordingPhase.Checking => $"The recording ended. Checking that {File} runs…",
        RecordingPhase.Passed => "This test runs: it passed when it was checked.",
        RecordingPhase.Failed => "The test did not pass when it was checked.",
        RecordingPhase.NotChecked => $"The recording ended and {File} holds what was recorded, but it could not be checked.",
        _ => $"The engine stopped during the recording. What was recorded so far is kept in {File}.",
    };

    /// <summary>Applies an event of this recording; events of other recordings are ignored.</summary>
    /// <param name="recordingEvent">The event.</param>
    public void Apply(RecordingEvent recordingEvent)
    {
        ArgumentNullException.ThrowIfNull(recordingEvent);
        if (recordingEvent.RecordingId != RecordingId)
        {
            return;
        }

        switch (recordingEvent)
        {
            case StepRecordedEvent recorded:
                Put(recorded.Index, recorded.Step, recorded.Review);
                break;
            case StepChangedEvent changed:
                Put(changed.Index, changed.Step, changed.Review);
                break;
            case RecordingNoticeEvent notice:
                Notices.Add(new RecordingNoticeViewModel(notice.Kind, notice.Message, $"{notice.Page} · {notice.Url}"));
                OnPropertyChanged(nameof(HasNotices));
                break;
            case RecordingStoppedEvent when IsRecording:
                Phase = RecordingPhase.Checking;
                _ = CheckAsync();
                break;
            case RecordingVerifiedEvent verified when Run is null || Run.RunId == verified.RunId:
                Phase = verified.Status == VerifyOutcome.Passed ? RecordingPhase.Passed : RecordingPhase.Failed;
                if (IsFailed)
                {
                    DescribeFailure();
                }

                break;
        }
    }

    /// <summary>Says that the engine stopped: while recording, what was recorded is kept; while checking, the check is lost.</summary>
    /// <param name="why">The engine's failure, if any.</param>
    public void EngineStopped(string? why)
    {
        if (IsRecording)
        {
            Phase = RecordingPhase.EngineStopped;
            Problem = $"{Steps.Count} recorded step{(Steps.Count == 1 ? string.Empty : "s")} are in the file. Restart the engine to go on: open the test to edit or run it. {why}".TrimEnd();
        }
        else if (IsChecking)
        {
            Phase = RecordingPhase.NotChecked;
            Problem = $"The engine stopped before the check ended. Run the test once the engine is back. {why}".TrimEnd();
        }
    }

    /// <summary>Stops the recording; the engine writes the last step and closes its browser.</summary>
    /// <returns>A task that completes when the engine has answered.</returns>
    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        Phase = RecordingPhase.Stopping;
        try
        {
            await _host.Stop(RecordingId);
        }
        catch (EngineException ex)
        {
            _host.Report($"Stopping the recording {RecordingId} failed: {ex}");
            if (Phase == RecordingPhase.Stopping)
            {
                Phase = RecordingPhase.Recording;
                Problem = $"The recording did not stop: {ex.Message} Close the recording browser to end it.";
            }
        }
    }

    /// <summary>Opens the recorded test.</summary>
    [RelayCommand]
    private void OpenTest() => _host.OpenTest(File, null);

    /// <summary>Opens the recorded test at the step that failed its check.</summary>
    [RelayCommand(CanExecute = nameof(HasFailedLine))]
    private void OpenFailedStep() => _host.OpenTest(File, FailedLine);

    /// <summary>Opens the check's run in its tab.</summary>
    [RelayCommand(CanExecute = nameof(HasRun))]
    private void OpenRun()
    {
        if (Run is { } run)
        {
            _host.ShowRun(run);
        }
    }

    private bool CanStop() => Phase == RecordingPhase.Recording;

    private bool HasFailedLine() => FailedLine is not null;

    private bool HasRun() => Run is not null;

    private void Put(int index, RecordedStep step, string? review)
    {
        if (Steps.FirstOrDefault(s => s.Index == index) is { } known)
        {
            known.Update(step, review);
        }
        else
        {
            var at = 0;
            while (at < Steps.Count && Steps[at].Index < index)
            {
                at++;
            }

            Steps.Insert(at, new RecordedStepViewModel(index, step, review));
        }

        PasswordHelp = Steps.FirstOrDefault(s => s.PasswordVariable is not null) is { } password
            ? $"You typed a password into {password.Target ?? "a password field"}. It matched none of the project's secrets, so it was not written into the test: "
              + $"the step uses an empty variable, {password.PasswordVariable}, in its place, and the test fails until it has a value. "
              + "Declare the password as a secret in the project's config file, give it its value in the .env file, "
              + "then change the step's value to ${secrets.NAME}, with the secret's name."
            : null;
    }

    private async Task CheckAsync()
    {
        var (run, problem) = await _host.Verify(RecordingId);
        if (run is null)
        {
            Phase = RecordingPhase.NotChecked;
            Problem = problem;
            return;
        }

        Run = run;
    }

    private void DescribeFailure()
    {
        var failed = Run?.Tests.SelectMany(t => t.AllSteps).FirstOrDefault(s => s.IsFailed);
        FailedStep = failed?.Title;
        FailedMessage = failed?.Error?.Message ?? Run?.Tests.SelectMany(t => t.Messages).Concat(Run.Messages).FirstOrDefault();
        FailedLine = failed is not null && failed.Location.File == File ? failed.Location.Line : null;
    }
}
