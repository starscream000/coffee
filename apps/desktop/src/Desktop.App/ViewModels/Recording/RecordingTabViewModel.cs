// The Record tab of one recording (instruction D0004, tasks 15 and 17): the
// steps as they arrive, as cards, with a step that changes (typing) updated in
// place; the notices of what was not recorded; Stop. Closing the recording
// browser ends the recording too. If the engine stops, the tab says that what
// was recorded is kept.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    /// <summary>The recording ended; the file holds what was recorded.</summary>
    Stopped,
    /// <summary>The engine stopped during the recording; what was recorded is kept.</summary>
    EngineStopped,
}

/// <summary>What a Record tab needs from the workspace.</summary>
/// <param name="Stop">Stops the recording (<c>stopRecording</c>).</param>
/// <param name="OpenTest">Opens the test, at the step on a line when one is given.</param>
/// <param name="Report">Writes a line into the engine log.</param>
public sealed record RecordingHost(
    Func<string, Task> Stop,
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
    [NotifyPropertyChangedFor(nameof(IsRecording), nameof(IsDone), nameof(Title), nameof(CanClose), nameof(StatusText))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private RecordingPhase _phase;

    /// <summary>What went wrong, such as a stop that failed or the engine stopping; null otherwise.</summary>
    [ObservableProperty]
    private string? _problem;

    /// <summary>True while the browser records (or stops).</summary>
    public bool IsRecording => Phase is RecordingPhase.Recording or RecordingPhase.Stopping;

    /// <summary>True once the recording has ended.</summary>
    public bool IsDone => !IsRecording;

    /// <summary>True when something was seen but not recorded.</summary>
    public bool HasNotices => Notices.Count > 0;

    /// <summary>What is happening, in a sentence.</summary>
    public string StatusText => Phase switch
    {
        RecordingPhase.Recording => "Recording in the browser the engine opened. Use the application as a person would: each step appears here. Press Stop, or close the browser, when you are done.",
        RecordingPhase.Stopping => "Stopping the recording…",
        RecordingPhase.Stopped => $"The recording ended. {File} holds what was recorded.",
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
                Phase = RecordingPhase.Stopped;
                break;
        }
    }

    /// <summary>Says that the engine stopped: while recording, what was recorded is kept.</summary>
    /// <param name="why">The engine's failure, if any.</param>
    public void EngineStopped(string? why)
    {
        if (IsRecording)
        {
            Phase = RecordingPhase.EngineStopped;
            Problem = $"{Steps.Count} recorded step{(Steps.Count == 1 ? string.Empty : "s")} are in the file. Restart the engine to go on: open the test to edit or run it. {why}".TrimEnd();
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

    private bool CanStop() => Phase == RecordingPhase.Recording;

    private void Put(int index, RecordedStep step, string? review)
    {
        if (Steps.FirstOrDefault(s => s.Index == index) is { } known)
        {
            known.Update(step, review);
            return;
        }

        var at = 0;
        while (at < Steps.Count && Steps[at].Index < index)
        {
            at++;
        }

        Steps.Insert(at, new RecordedStepViewModel(index, step, review));
    }
}
