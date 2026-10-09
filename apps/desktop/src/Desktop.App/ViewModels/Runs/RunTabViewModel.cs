// A run in a tab of the workspace: the run itself, and what can be done from
// it: open a step in its file, open a step's page state.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.App.Services;
using Desktop.Engine;
using Desktop.Protocol;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels.Runs;

/// <summary>A run's tab.</summary>
public sealed partial class RunTabViewModel : WorkspaceTabViewModel
{
    private readonly IEngineService _engine;
    private readonly Action<string, int> _openFile;

    /// <summary>Creates the tab.</summary>
    /// <param name="run">The run.</param>
    /// <param name="engine">The engine, for page states.</param>
    /// <param name="openFile">Opens a file at a line.</param>
    /// <param name="cancel">The command that cancels the run going on, when this is a live run.</param>
    public RunTabViewModel(RunViewModel run, IEngineService engine, Action<string, int> openFile, IAsyncRelayCommand? cancel)
    {
        ArgumentNullException.ThrowIfNull(run);
        Run = run;
        _engine = engine;
        _openFile = openFile;
        Cancel = cancel;
        run.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(RunViewModel.StartedAt) or nameof(RunViewModel.State))
            {
                OnPropertyChanged(nameof(Title));
            }
        };
    }

    /// <summary>The run.</summary>
    public RunViewModel Run { get; }

    /// <summary>Cancels the run going on; null for a run read back from its folder.</summary>
    public IAsyncRelayCommand? Cancel { get; }

    /// <summary>True when the tab can cancel its run.</summary>
    public bool IsLive => Cancel is not null;

    /// <inheritdoc />
    public override string Title => (Run.IsActive ? "▶ " : string.Empty) + "Run " + (Run.StartedAt is { } t ? t.ToLocalTime().ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) : Run.RunId);

    /// <inheritdoc />
    public override string ToolTip => $"Run {Run.RunId}: {Run.StatusText}";

    /// <summary>What happened when a page state was asked for, if it did not open.</summary>
    [ObservableProperty]
    private string? _pageStateMessage;

    /// <summary>Opens the file of a step at its line.</summary>
    /// <param name="step">The step.</param>
    [RelayCommand]
    private void GoToStep(StepRunViewModel? step)
    {
        if (step is not null)
        {
            _openFile(step.Location.File, step.Location.Line);
        }
    }

    /// <summary>Asks the engine to show a step's saved page state in a test browser.</summary>
    /// <param name="step">The step.</param>
    /// <returns>A task that completes when the engine has answered.</returns>
    [RelayCommand]
    private async Task OpenPageStateAsync(StepRunViewModel? step)
    {
        if (step is null || Run.SelectedTest is not { } test)
        {
            return;
        }

        PageStateMessage = null;
        try
        {
            await _engine.OpenSnapshotAsync(new OpenSnapshotParams { RunId = Run.RunId, TestId = test.TestId, StepId = step.StepId });
        }
        catch (EngineRequestException ex) when (ex.Name == ErrorCodes.SnapshotUnavailable)
        {
            if (ex.DataAs<SnapshotUnavailableData>()?.Screenshot is { } screenshot)
            {
                step.ScreenshotPath ??= screenshot;
            }

            PageStateMessage = $"The page state cannot be shown: {ex.Message} The screenshot is shown instead.";
        }
        catch (Exception ex)
        {
            PageStateMessage = $"The page state cannot be shown: {ex.Message}";
        }
    }
}
