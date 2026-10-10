// An open project: its test explorer, problems, tabs (step files and the action
// catalogue), and the reaction to files changing on disk.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.App.Services;
using Desktop.App.StepFiles;
using Desktop.App.ViewModels.Runs;
using Desktop.App.ViewModels.Steps;
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
    private string? _historyRunId;

    /// <summary>Creates the workspace for a project the engine has opened. Call <see cref="LoadAsync"/> next.</summary>
    /// <param name="project">The engine's answer to <c>openProject</c>.</param>
    /// <param name="engine">The engine.</param>
    /// <param name="files">The project's files.</param>
    /// <param name="dispatcher">The UI thread, for file-change notices.</param>
    /// <param name="engineStatus">The engine's status and log, shown in the bottom panel.</param>
    /// <param name="dialogs">Asks about unsaved changes and overwriting.</param>
    /// <param name="delay">Waits before validating text while typing.</param>
    /// <param name="runRecords">Reads the records of earlier runs; the project's run folders on disk by default.</param>
    public WorkspaceViewModel(
        OpenProjectResult project,
        IEngineService engine,
        IProjectFiles files,
        IUiDispatcher dispatcher,
        EngineStatusViewModel engineStatus,
        IDialogService dialogs,
        IDelay delay,
        IRunRecords? runRecords = null)
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
        Runs = new RunControlViewModel(engine, dialogs, new RunControlHost(
            () => SelectedEnvironment,
            () => [.. Tabs.OfType<StepFileViewModel>().Where(t => t.IsDirty).Select(t => t.File)],
            SaveAllAsync,
            ShowRun,
            Problems.SetValidationDiagnostics,
            EngineStatus.Report));
        History = new RunHistoryViewModel(runRecords ?? new DiskRunRecords(), new RunHistoryHost(
            project.Root,
            () => Runs.IsRunning ? Runs.Current?.RunId : null,
            TrySelectRun,
            run => ShowRun(run, live: false),
            EngineStatus.Report));
        Runs.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RunControlViewModel.IsRunning) && Runs.Current is { } current && _historyRunId != (current.IsActive ? current.RunId : null))
            {
                // A run started or ended: its folder came, or its record is complete.
                _historyRunId = current.IsActive ? current.RunId : null;
                _ = History.RefreshAsync();
            }
        };
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

    /// <summary>Starts, follows and cancels runs.</summary>
    public RunControlViewModel Runs { get; }

    /// <summary>The project's earlier runs.</summary>
    public RunHistoryViewModel History { get; }

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
        // The list of earlier runs fills in when read; loading does not wait for it.
        _ = History.RefreshAsync();
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
            tab = new StepFileViewModel(file, new StepFileServices(Root, _files, _engine, _dialogs, _delay, EngineStatus.Report, RunFileAsync, ActionChoices, SharedTargets, RenameFileAsync, DeleteFileAsync));
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

    /// <summary>What happened to the last attempt to create, rename or delete a file, when it did not work; null otherwise.</summary>
    [ObservableProperty]
    private string? _fileNotice;

    /// <summary>Runs every test of the project.</summary>
    /// <returns>A task that completes when the run has started or was refused.</returns>
    [RelayCommand]
    private Task RunAllAsync() => Runs.RunAllAsync();

    /// <summary>Creates a test in the folder selected in the explorer (else <c>tests</c>) and opens it.</summary>
    /// <returns>A task that completes when the file is open or was not created.</returns>
    [RelayCommand]
    private Task NewTestAsync() => NewFileAsync(NewFileKind.Test);

    /// <summary>Creates a flow (in <c>flows</c>) and opens it.</summary>
    /// <returns>A task that completes when the file is open or was not created.</returns>
    [RelayCommand]
    private Task NewFlowAsync() => NewFileAsync(NewFileKind.Flow);

    /// <summary>Creates a shared targets file (in <c>targets</c>) and opens it.</summary>
    /// <returns>A task that completes when the file is open or was not created.</returns>
    [RelayCommand]
    private Task NewTargetsAsync() => NewFileAsync(NewFileKind.Targets);

    /// <summary>Hides the file notice.</summary>
    [RelayCommand]
    private void DismissFileNotice() => FileNotice = null;

    /// <summary>
    /// Asks for a folder and name, writes a minimal file of the kind that the
    /// engine reads without problems, and opens it. An existing file is never
    /// overwritten.
    /// </summary>
    /// <param name="kind">What to create.</param>
    /// <returns>A task that completes when the file is open or was not created.</returns>
    public async Task NewFileAsync(NewFileKind kind)
    {
        FileNotice = null;
        var what = NewFiles.Words(kind);
        var folder = kind == NewFileKind.Test && Explorer.SelectedNode is { } node
            ? node.IsFolder ? node.Path : node.Path[..Math.Max(node.Path.LastIndexOf('/'), 0)]
            : NewFiles.DefaultFolder(kind);
        try
        {
            if (await _dialogs.AskNewFileAsync(what, folder, NewFiles.Ending(kind)) is not { } answer)
            {
                return;
            }

            var path = NewFiles.PathFor(answer.Folder, answer.Name, kind);
            if (_files.Exists(Root, path))
            {
                FileNotice = $"{path} already exists. Choose another name.";
                return;
            }

            _files.WriteText(Root, path, NewFiles.Template(kind, path));
            OpenFile(path);
            await OnFilesChangedAsync([path]);
        }
        catch (ArgumentException ex)
        {
            FileNotice = $"The {what} was not created: {ex.Message}";
        }
        catch (Exception ex)
        {
            FileNotice = $"The {what} was not created: {ex.Message}";
            EngineStatus.Report($"Creating a {what} failed: {ex}");
        }
    }

    /// <summary>Renames a tab's file in its folder and opens it under the new name. A tab with unsaved changes is saved or reverted first.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns>A task that completes when the file is renamed or was not.</returns>
    public async Task RenameFileAsync(StepFileViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        FileNotice = null;
        if (tab.IsDirty)
        {
            FileNotice = $"Save or revert {tab.File} before renaming it.";
            return;
        }

        if (NewFiles.KindOf(tab.File) is not { } kind)
        {
            FileNotice = "Only test, flow and targets files can be renamed here.";
            return;
        }

        try
        {
            if (await _dialogs.AskRenameAsync(tab.File) is not { } name)
            {
                return;
            }

            // The name keeps the file's kind: "checkout" becomes "checkout.test.yaml" for a test.
            var folder = tab.File[..Math.Max(tab.File.LastIndexOf('/'), 0)];
            var path = NewFiles.PathFor(folder, name, kind);
            if (path == tab.File)
            {
                return;
            }

            if (_files.Exists(Root, path))
            {
                FileNotice = $"{path} already exists. Choose another name.";
                return;
            }

            _files.Move(Root, tab.File, path);
            var old = tab.File;
            RemoveTab(tab);
            OpenFile(path);
            await OnFilesChangedAsync([old, path]);
        }
        catch (ArgumentException ex)
        {
            FileNotice = $"{tab.File} was not renamed: {ex.Message}";
        }
        catch (Exception ex)
        {
            FileNotice = $"{tab.File} was not renamed: {ex.Message}";
            EngineStatus.Report($"Renaming {tab.File} failed: {ex}");
        }
    }

    /// <summary>Deletes a tab's file after asking, and closes the tab.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns>A task that completes when the file is deleted or was not.</returns>
    public async Task DeleteFileAsync(StepFileViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        FileNotice = null;
        try
        {
            if (!await _dialogs.AskDeleteAsync(tab.File))
            {
                return;
            }

            _files.Delete(Root, tab.File);
            RemoveTab(tab);
            await OnFilesChangedAsync([tab.File]);
        }
        catch (Exception ex)
        {
            FileNotice = $"{tab.File} was not deleted: {ex.Message}";
            EngineStatus.Report($"Deleting {tab.File} failed: {ex}");
        }
    }

    /// <summary>
    /// Runs what is selected in the explorer: a test file, or every test in a
    /// folder (as filtered by the search and the tag); every test when nothing is selected.
    /// </summary>
    /// <returns>A task that completes when the run has started or was refused.</returns>
    [RelayCommand]
    private Task RunSelectedAsync()
    {
        if (Explorer.SelectedNode is not { } node)
        {
            return Runs.RunAllAsync();
        }

        return Runs.RunFilesAsync(node.IsTest ? [node.Path] : [.. TestsBelow(node)]);
    }

    /// <summary>Runs the tests with the tag chosen in the explorer; every test when none is chosen.</summary>
    /// <returns>A task that completes when the run has started or was refused.</returns>
    [RelayCommand]
    private Task RunTagAsync() =>
        Explorer.SelectedTag == TestExplorerViewModel.AllTags ? Runs.RunAllAsync() : Runs.RunTagAsync(Explorer.SelectedTag);

    /// <summary>Runs one test file; says so when the file is not a test (a flow or the targets file).</summary>
    /// <param name="file">The file, relative to the project root.</param>
    /// <returns>A task that completes when the run has started or was refused.</returns>
    public Task RunFileAsync(string file)
    {
        if (!Explorer.Files.Contains(file, StringComparer.Ordinal))
        {
            Runs.Notice = $"{file} is not a test, so it cannot be run on its own. Run a test that uses it.";
            return Task.CompletedTask;
        }

        return Runs.RunFilesAsync([file]);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Runs.Dispose();
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

        RemoveTab(tab);
    }

    /// <summary>Closes a tab without asking.</summary>
    private void RemoveTab(StepFileViewModel tab)
    {
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

    private IReadOnlyList<StepActionChoice> ActionChoices() =>
        [.. Actions.All.Select(a => new StepActionChoice(a.Name, a.Description, a.Action.Shorthand, [.. a.Parameters.Where(p => p.IsRequired).Select(p => p.Name)], a.Action.ParamsSchema))];

    /// <summary>The names of the targets in the project's shared targets files, as the files are now (open tabs included).</summary>
    private IReadOnlyList<string> SharedTargets() =>
        [.. _files.FindFiles(Root, TargetNames.SharedFileEnding)
            .SelectMany(f => TargetNames.Of(Tabs.OfType<StepFileViewModel>().FirstOrDefault(t => t.File == f)?.Document.Text ?? _files.TryReadText(Root, f) ?? string.Empty))];

    private static IEnumerable<string> TestsBelow(ExplorerNodeViewModel node) =>
        node.IsTest ? [node.Path] : node.Children.SelectMany(TestsBelow);

    private bool TrySelectRun(string runId)
    {
        var tab = Tabs.OfType<RunTabViewModel>().FirstOrDefault(t => t.Run.RunId == runId);
        if (tab is not null)
        {
            SelectedTab = tab;
        }

        return tab is not null;
    }

    private void ShowRun(RunViewModel run) => ShowRun(run, live: true);

    private void ShowRun(RunViewModel run, bool live)
    {
        var tab = new RunTabViewModel(run, _engine, (file, line) => OpenFile(file, line), live ? Runs.CancelCommand : null);
        tab.CloseRequested += (_, _) =>
        {
            var index = Tabs.IndexOf(tab);
            if (index < 0)
            {
                return;
            }

            if (run.IsActive)
            {
                Runs.Notice = "A run's tab stays open while the run goes on. Cancel the run first.";
                return;
            }

            Tabs.RemoveAt(index);
            if (SelectedTab is null || SelectedTab == tab)
            {
                SelectedTab = Tabs[Math.Clamp(index - 1, 0, Tabs.Count - 1)];
            }
        };
        Tabs.Add(tab);
        SelectedTab = tab;
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
