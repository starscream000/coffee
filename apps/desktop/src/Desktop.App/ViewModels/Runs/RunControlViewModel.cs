// Starting, following and cancelling runs for an open project: what to run,
// the environment and whether the browser is shown, the question about unsaved
// files, the engine's refusals, and events routed to the run they belong to.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.App.Services;
using Desktop.Engine;
using Desktop.Protocol;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels.Runs;

/// <summary>What a run control needs from its workspace.</summary>
/// <param name="Environment">The environment chosen for runs.</param>
/// <param name="UnsavedFiles">The files with unsaved changes.</param>
/// <param name="SaveAll">Saves every changed file; true when all were saved.</param>
/// <param name="ShowRun">Opens a run's tab.</param>
/// <param name="ShowProblems">Shows the problems that stopped a run from starting.</param>
/// <param name="Report">Writes a line into the engine log.</param>
public sealed record RunControlHost(
    Func<string?> Environment,
    Func<IReadOnlyList<string>> UnsavedFiles,
    Func<Task<bool>> SaveAll,
    Action<RunViewModel> ShowRun,
    Action<IReadOnlyList<Diagnostic>> ShowProblems,
    Action<string> Report);

/// <summary>Starts, follows and cancels runs.</summary>
public sealed partial class RunControlViewModel : ObservableObject, IDisposable
{
    private readonly IEngineService _engine;
    private readonly IDialogService _dialogs;
    private readonly RunControlHost _host;
    private readonly List<EngineEvent> _early = [];
    private bool _starting;

    /// <summary>Creates the control and follows the engine's events and state.</summary>
    /// <param name="engine">The engine.</param>
    /// <param name="dialogs">Asks about unsaved files.</param>
    /// <param name="host">What it needs from the workspace.</param>
    public RunControlViewModel(IEngineService engine, IDialogService dialogs, RunControlHost host)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
        _dialogs = dialogs;
        _host = host;
        _engine.EventReceived += OnEvent;
        _engine.StateChanged += OnEngineStateChanged;
        Refresh();
    }

    /// <summary>The command, built from the product's npm scope, that installs the browser for the engine of a checkout.</summary>
    public static string InstallCommand => $"pnpm --filter {Product.NpmScope}/engine exec playwright install chromium";

    /// <summary>The run going on, or the last one started here.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private RunViewModel? _current;

    /// <summary>Show the browser window while running (<c>options.headed</c>).</summary>
    [ObservableProperty]
    private bool _headed;

    /// <summary>True when the engine can start a browser and no run is going.</summary>
    [ObservableProperty]
    private bool _canRun;

    /// <summary>Why running is off, when it is; null otherwise.</summary>
    [ObservableProperty]
    private string? _unavailableReason;

    /// <summary>What happened to the last attempt to run, when it did not start; null otherwise.</summary>
    [ObservableProperty]
    private string? _notice;

    /// <summary>True while a run is starting or running.</summary>
    public bool IsRunning => _starting || Current?.IsActive == true;

    /// <summary>Runs every test of the project.</summary>
    /// <returns>A task that completes when the run has started or was refused.</returns>
    [RelayCommand]
    public Task RunAllAsync() => StartAsync(new StartRunParams(), "all tests");

    /// <summary>Runs the given test files.</summary>
    /// <param name="files">Relative paths of test files.</param>
    /// <returns>A task that completes when the run has started or was refused.</returns>
    public Task RunFilesAsync(IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        return files.Count == 0
            ? Task.CompletedTask
            : StartAsync(new StartRunParams { Files = files }, files.Count == 1 ? files[0] : $"{files.Count} tests");
    }

    /// <summary>Runs the tests with a tag.</summary>
    /// <param name="tag">The tag.</param>
    /// <returns>A task that completes when the run has started or was refused.</returns>
    public Task RunTagAsync(string tag) => StartAsync(new StartRunParams { Tags = [tag] }, $"tag {tag}");

    /// <summary>Asks the engine to cancel the run going on.</summary>
    /// <returns>A task that completes when cancelling has started.</returns>
    [RelayCommand(CanExecute = nameof(IsRunning))]
    private async Task CancelAsync()
    {
        if (Current is not { IsActive: true } run)
        {
            return;
        }

        run.IsCancelling = true;
        try
        {
            await _engine.CancelRunAsync(run.RunId);
        }
        catch (Exception ex)
        {
            run.IsCancelling = false;
            Notice = $"The run could not be cancelled: {ex.Message}";
            _host.Report($"Cancelling run {run.RunId} failed: {ex}");
        }
    }

    /// <summary>Hides the notice.</summary>
    [RelayCommand]
    private void DismissNotice() => Notice = null;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Current is { IsActive: true } run && _engine.State == EngineState.Ready)
        {
            // The project is closing: do not leave the engine running its tests.
            _ = _engine.CancelRunAsync(run.RunId).ContinueWith(t => _ = t.Exception, TaskScheduler.Default);
        }

        _engine.EventReceived -= OnEvent;
        _engine.StateChanged -= OnEngineStateChanged;
    }

    private async Task StartAsync(StartRunParams parameters, string what)
    {
        if (IsRunning)
        {
            Notice = "A run is already going. Wait for it to end, or cancel it.";
            return;
        }

        if (!CanRun)
        {
            Notice = UnavailableReason;
            return;
        }

        Notice = null;
        _starting = true;
        _early.Clear();
        Refresh();
        try
        {
            if (await SaveFirstAsync())
            {
                await StartCoreAsync(parameters, what);
            }
        }
        catch (Exception ex)
        {
            Notice = $"The run of {what} did not start: {ex.Message}";
            _host.Report($"Starting a run of {what} failed: {ex}");
        }
        finally
        {
            _starting = false;
            _early.Clear();
            Refresh();
        }
    }

    /// <summary>Asks about unsaved files, because the engine runs what is on disk.</summary>
    /// <returns>False when the run should not start.</returns>
    private async Task<bool> SaveFirstAsync()
    {
        var unsaved = _host.UnsavedFiles();
        if (unsaved.Count == 0)
        {
            return true;
        }

        switch (await _dialogs.AskSaveBeforeRunAsync(unsaved))
        {
            case UnsavedChangesChoice.Save when !await _host.SaveAll():
                Notice = "The run did not start because not every file could be saved.";
                return false;
            case UnsavedChangesChoice.Save or UnsavedChangesChoice.Discard:
                return true;
            default:
                return false;
        }
    }

    private async Task StartCoreAsync(StartRunParams parameters, string what)
    {
        try
        {
            var result = await _engine.StartRunAsync(parameters with
            {
                Env = _host.Environment(),
                Options = new RunOptions { Headed = Headed },
            });
            var run = new RunViewModel(result.RunId, result.ResultsDir);
            Current = run;
            _host.ShowRun(run);
            foreach (var early in _early)
            {
                run.Apply(early);
            }
        }
        catch (EngineRequestException ex) when (ex.Name == ErrorCodes.StepFilesInvalid)
        {
            var problems = ex.DataAs<StepFilesInvalidData>()?.Diagnostics ?? [];
            _host.ShowProblems(problems);
            Notice = $"The run of {what} did not start: the files have {Plural(problems.Count, "problem")} to fix first. They are in the problems panel.";
        }
        catch (EngineRequestException ex) when (ex.Name == ErrorCodes.RunInProgress)
        {
            Notice = "The engine is already running tests. Wait for that run to end, or cancel it.";
        }
    }

    private void OnEvent(object? sender, EngineEvent engineEvent)
    {
        if (Current is { } run && run.RunId == engineEvent.RunId)
        {
            run.Apply(engineEvent);
            if (!run.IsActive)
            {
                Refresh();
            }
        }
        else if (_starting)
        {
            // Events can overtake the answer to startRun on their way to the UI thread.
            _early.Add(engineEvent);
        }
    }

    private void OnEngineStateChanged(object? sender, EventArgs e)
    {
        if (_engine.State != EngineState.Ready && Current is { IsActive: true } run)
        {
            run.EndUnfinished($"The engine stopped during the run. What it reported before stopping is kept. {_engine.Failure?.Message}".TrimEnd());
        }

        Refresh();
    }

    private void Refresh()
    {
        var browsers = _engine.State == EngineState.Ready ? _engine.Info?.Capabilities.Browsers ?? [] : [];
        UnavailableReason = _engine.State != EngineState.Ready
            ? "Running is off because the engine is not running."
            : browsers.Count == 0
                ? $"Running is off because the engine cannot start a browser. Install Chromium for the engine, from the repository: {InstallCommand}"
                : null;
        CanRun = UnavailableReason is null && !IsRunning;
        OnPropertyChanged(nameof(IsRunning));
        CancelCommand.NotifyCanExecuteChanged();
    }

    private static string Plural(int n, string what) => n == 1 ? $"1 {what}" : $"{n} {what}s";
}
