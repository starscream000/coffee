// An open project: its test explorer, problems, tabs (step files and the action
// catalogue), and the reaction to files changing on disk.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.App.Services;
using Desktop.Engine;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels;

/// <summary>An open project.</summary>
public sealed partial class WorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly IEngineService _engine;
    private readonly IProjectFiles _files;
    private readonly IUiDispatcher _dispatcher;
    private readonly IDialogService _dialogs;
    private readonly IDelay _delay;
    private readonly HashSet<string> _pendingChanges = new(StringComparer.Ordinal);
    private readonly Dictionary<StepFileViewModel, Action> _detach = [];
    private IDisposable? _watch;
    private Task _refresh = Task.CompletedTask;
    private bool _refreshing;
    private int _validateGeneration;
    private int _listGeneration;

    /// <summary>Creates the workspace for a project the engine has opened. Call <see cref="LoadAsync"/> next.</summary>
    /// <param name="project">The engine's answer to <c>openProject</c>.</param>
    /// <param name="engine">The engine.</param>
    /// <param name="files">The project's files.</param>
    /// <param name="dispatcher">The UI thread, for file-change notices.</param>
    /// <param name="engineStatus">The engine's status and log, shown in the bottom panel.</param>
    /// <param name="dialogs">Asks about unsaved changes and overwriting.</param>
    /// <param name="delay">Waits before validating text while typing.</param>
    public WorkspaceViewModel(
        OpenProjectResult project,
        IEngineService engine,
        IProjectFiles files,
        IUiDispatcher dispatcher,
        EngineStatusViewModel engineStatus,
        IDialogService dialogs,
        IDelay delay)
    {
        _dialogs = dialogs;
        _delay = delay;
        ArgumentNullException.ThrowIfNull(project);
        EngineStatus = engineStatus;
        _engine = engine;
        _files = files;
        _dispatcher = dispatcher;
        Project = project;
        Environments = [.. project.Environments];
        _selectedEnvironment = project.DefaultEnvironment;
        Tabs.Add(Actions);
        SelectedTab = Actions;
        Explorer.FileActivated += (_, file) => OpenFile(file);
        Problems.ProblemActivated += (_, d) => OpenFile(d.File, d.Line);
        Problems.Changed += (_, _) => ProblemsChanged();
        Problems.SetProjectDiagnostics(project.Diagnostics);
    }

    /// <summary>Raised when the config file changed and the project must be opened again.</summary>
    public event EventHandler? ReopenRequested;

    /// <summary>The engine's answer to <c>openProject</c>.</summary>
    public OpenProjectResult Project { get; }

    /// <summary>The engine's status and log.</summary>
    public EngineStatusViewModel EngineStatus { get; }

    /// <summary>The project folder's name.</summary>
    public string Name => Path.GetFileName(Project.Root.TrimEnd('/', '\\'));

    /// <summary>The project folder.</summary>
    public string Root => Project.Root;

    /// <summary>The environments of the config.</summary>
    public IReadOnlyList<string> Environments { get; }

    /// <summary>True when the config defines environments.</summary>
    public bool HasEnvironments => Environments.Count > 0;

    /// <summary>The saved logins of the config, comma-separated, or "none".</summary>
    public string LoginsText => Project.Logins.Count == 0 ? "none" : string.Join(", ", Project.Logins);

    /// <summary>The test explorer.</summary>
    public TestExplorerViewModel Explorer { get; } = new();

    /// <summary>The problems panel.</summary>
    public ProblemsViewModel Problems { get; } = new();

    /// <summary>The action catalogue tab.</summary>
    public ActionCatalogViewModel Actions { get; } = new();

    /// <summary>The tabs in the centre.</summary>
    public ObservableCollection<WorkspaceTabViewModel> Tabs { get; } = [];

    /// <summary>The selected tab.</summary>
    [ObservableProperty]
    private WorkspaceTabViewModel? _selectedTab;

    /// <summary>The environment the next run will use.</summary>
    [ObservableProperty]
    private string? _selectedEnvironment;

    /// <summary>True while the engine is asked something.</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>What happened last, for the status bar.</summary>
    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>Lists tests and actions, validates every test file, and starts watching the folder.</summary>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>A task that completes when everything is loaded.</returns>
    /// <exception cref="EngineException">The engine failed or sent something unusable; the caller reports it.</exception>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await LoadTestsAsync(cancellationToken);
        Actions.Load(await _engine.ListActionsAsync(cancellationToken));
        await ValidateCoreAsync(cancellationToken);
        _watch ??= _files.Watch(Root, batch => _dispatcher.Post(() => _ = OnFilesChangedAsync(batch)));
    }

    /// <summary>Validates every test file again. Never throws: a failure is shown in <see cref="Status"/> with the detail in the engine log.</summary>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>A task that completes when the problems are updated.</returns>
    [RelayCommand]
    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await ValidateCoreAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Fail("Validation failed", ex);
        }
    }

    /// <summary>Opens a file in a tab (or selects its tab) and shows a line.</summary>
    /// <param name="file">The file, relative to the project root.</param>
    /// <param name="line">The line to show, if any.</param>
    public void OpenFile(string file, int? line = null)
    {
        var tab = Tabs.OfType<StepFileViewModel>().FirstOrDefault(t => t.File == file);
        if (tab is null)
        {
            tab = new StepFileViewModel(file, new StepFileServices(Root, _files, _engine, _dialogs, _delay, EngineStatus.Report));
            tab.LoadFromDisk();
            Attach(tab);
        }

        SelectedTab = tab;
        if (line is { } n)
        {
            tab.Reveal(n);
        }
    }

    /// <summary>
    /// Handles a batch of changed files. A change to the config or to a user
    /// action's source asks for the project to be opened again; otherwise open
    /// tabs are re-read, tests re-listed when test files came or went, and all
    /// test files validated again. One refresh runs at a time: batches that
    /// arrive meanwhile are joined into one more refresh. Never throws.
    /// </summary>
    /// <param name="changed">Relative paths.</param>
    /// <returns>A task that completes when every refresh asked for so far has run.</returns>
    public Task OnFilesChangedAsync(IReadOnlyCollection<string> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        _pendingChanges.UnionWith(changed);
        if (!_refreshing)
        {
            _refreshing = true;
            _refresh = RefreshWhilePendingAsync();
        }

        return _refresh;
    }

    /// <summary>True when at least one tab has unsaved changes.</summary>
    public bool HasUnsavedChanges => Tabs.OfType<StepFileViewModel>().Any(t => t.IsDirty);

    /// <summary>Saves every tab with unsaved changes.</summary>
    /// <returns>True when every one was saved.</returns>
    public async Task<bool> SaveAllAsync()
    {
        var saved = true;
        foreach (var tab in Tabs.OfType<StepFileViewModel>().Where(t => t.IsDirty).ToList())
        {
            saved &= await tab.SaveAsync();
        }

        return saved;
    }

    [RelayCommand(CanExecute = nameof(HasUnsavedChanges))]
    private async Task SaveAll() => await SaveAllAsync();

    /// <summary>
    /// Says whether the project may close: when tabs have unsaved changes, asks
    /// once whether to save them all, discard them, or not close.
    /// </summary>
    /// <returns>True when the project may close.</returns>
    public async Task<bool> ConfirmCloseAsync()
    {
        var unsaved = Tabs.OfType<StepFileViewModel>().Where(t => t.IsDirty).Select(t => t.File).ToList();
        if (unsaved.Count == 0)
        {
            return true;
        }

        return await _dialogs.AskUnsavedChangesAsync(unsaved) switch
        {
            UnsavedChangesChoice.Save => await SaveAllAsync(),
            UnsavedChangesChoice.Discard => true,
            _ => false,
        };
    }

    /// <summary>
    /// Hands this workspace's step file tabs over to the workspace that replaces
    /// it for the same project: the tab objects themselves move, with their
    /// text, undo history, caret, scroll position and "changed on disk" state,
    /// and stop reporting to this workspace.
    /// </summary>
    /// <returns>The tabs in order, and the selected one, if a step file tab was selected.</returns>
    public (IReadOnlyList<StepFileViewModel> Tabs, StepFileViewModel? Selected) ReleaseTabs()
    {
        var tabs = Tabs.OfType<StepFileViewModel>().ToList();
        var selected = SelectedTab as StepFileViewModel;
        foreach (var tab in tabs)
        {
            if (_detach.Remove(tab, out var detach))
            {
                detach();
            }

            Tabs.Remove(tab);
        }

        SelectedTab = Actions;
        return (tabs, selected);
    }

    /// <summary>Takes over the tabs another workspace of the same project released.</summary>
    /// <param name="released">What <see cref="ReleaseTabs"/> returned.</param>
    public void AdoptTabs((IReadOnlyList<StepFileViewModel> Tabs, StepFileViewModel? Selected) released)
    {
        foreach (var tab in released.Tabs)
        {
            Attach(tab);
        }

        if (released.Selected is { } selected)
        {
            SelectedTab = selected;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var tab in Tabs.OfType<StepFileViewModel>())
        {
            tab.Dispose();
        }

        _watch?.Dispose();
        _watch = null;
    }

    private async Task RefreshWhilePendingAsync()
    {
        try
        {
            while (_pendingChanges.Count > 0)
            {
                var batch = _pendingChanges.ToList();
                _pendingChanges.Clear();
                await RefreshAsync(batch);
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async Task RefreshAsync(IReadOnlyCollection<string> changed)
    {
        try
        {
            if (changed.Any(ProjectFileKinds.NeedsReopen))
            {
                _pendingChanges.Clear();
                ReopenRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            foreach (var tab in Tabs.OfType<StepFileViewModel>().Where(t => changed.Contains(t.File)))
            {
                tab.OnDiskChanged();
            }

            if (changed.Any(f => f.EndsWith(".test.yaml", StringComparison.OrdinalIgnoreCase)))
            {
                await LoadTestsAsync();
            }

            await ValidateCoreAsync();
        }
        catch (Exception ex)
        {
            Fail("Could not refresh after a file change", ex);
        }
    }

    private async Task ValidateCoreAsync(CancellationToken cancellationToken = default)
    {
        var generation = ++_validateGeneration;
        IsBusy = true;
        try
        {
            var files = Explorer.Files;
            var diagnostics = await _engine.ValidateAsync(files, cancellationToken);
            if (generation != _validateGeneration)
            {
                return;
            }

            Problems.SetValidationDiagnostics(diagnostics);
            Status = $"Validated {Plural(files.Count, "test file")} at {DateTime.Now:HH:mm:ss}: {Problems.Summary.ToLowerInvariant()}.";
        }
        finally
        {
            if (generation == _validateGeneration)
            {
                IsBusy = false;
            }
        }
    }

    private void Fail(string what, Exception ex)
    {
        Status = $"{what}: {ex.Message}";
        EngineStatus.Report($"{what}: {ex}");
    }

    private async Task LoadTestsAsync(CancellationToken cancellationToken = default)
    {
        var generation = ++_listGeneration;
        var tests = await _engine.ListTestsAsync(cancellationToken);
        if (generation != _listGeneration)
        {
            return;
        }

        Explorer.Load(tests);
    }

    private void Attach(StepFileViewModel tab)
    {
        void OnClose(object? sender, EventArgs e) => _ = CloseTabAsync(tab);
        void OnContent(object? sender, IReadOnlyList<Diagnostic>? diagnostics) => Problems.SetFileOverride(tab.File, diagnostics);
        void OnSaved(object? sender, EventArgs e) => _ = ValidateAsync();
        void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StepFileViewModel.IsDirty))
            {
                OnPropertyChanged(nameof(HasUnsavedChanges));
                SaveAllCommand.NotifyCanExecuteChanged();
            }
        }

        tab.CloseRequested += OnClose;
        tab.ContentDiagnosticsChanged += OnContent;
        tab.Saved += OnSaved;
        tab.PropertyChanged += OnChanged;
        _detach[tab] = () =>
        {
            tab.CloseRequested -= OnClose;
            tab.ContentDiagnosticsChanged -= OnContent;
            tab.Saved -= OnSaved;
            tab.PropertyChanged -= OnChanged;
        };
        if (tab.IsDirty && tab.ContentDiagnostics is { } content)
        {
            Problems.SetFileOverride(tab.File, content);
        }

        tab.ApplyDiagnostics(Problems.All);
        Tabs.Add(tab);
        OnPropertyChanged(nameof(HasUnsavedChanges));
        SaveAllCommand.NotifyCanExecuteChanged();
    }

    private async Task CloseTabAsync(StepFileViewModel tab)
    {
        try
        {
            if (!await tab.ConfirmCloseAsync())
            {
                return;
            }
        }
        catch (Exception ex)
        {
            Fail($"Could not close {tab.File}", ex);
            return;
        }

        var index = Tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        Tabs.Remove(tab);
        if (_detach.Remove(tab, out var detach))
        {
            detach();
        }

        Problems.SetFileOverride(tab.File, null);
        tab.Dispose();
        if (SelectedTab is null || SelectedTab == tab)
        {
            SelectedTab = Tabs[Math.Clamp(index - 1, 0, Tabs.Count - 1)];
        }
    }

    private void ProblemsChanged()
    {
        Explorer.ApplyProblemCounts(Problems.CountsByFile());
        foreach (var tab in Tabs.OfType<StepFileViewModel>())
        {
            tab.ApplyDiagnostics(Problems.All);
        }
    }

    private static string Plural(int n, string what) => n == 1 ? $"1 {what}" : $"{n} {what}s";
}
