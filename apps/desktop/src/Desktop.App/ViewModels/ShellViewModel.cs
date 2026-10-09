// The main window's view model: the start page or the open project, the engine
// status, the settings panel and the notice bar.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.App.Services;
using Desktop.Engine;
using Desktop.Protocol;

namespace Desktop.App.ViewModels;

/// <summary>The main window.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IEngineService _engine;
    private readonly ISettingsStore _settings;
    private readonly IFolderPicker _folderPicker;
    private readonly IProjectFiles _files;
    private readonly IUiDispatcher _dispatcher;
    private readonly Func<string, bool> _directoryExists;

    /// <summary>Creates the main window's view model.</summary>
    /// <param name="engine">The engine.</param>
    /// <param name="settings">Settings and recent projects.</param>
    /// <param name="folderPicker">The folder dialog.</param>
    /// <param name="files">Project files.</param>
    /// <param name="dispatcher">The UI thread.</param>
    /// <param name="directoryExists">Says whether a folder exists; <see cref="Directory.Exists"/> by default.</param>
    public ShellViewModel(
        IEngineService engine,
        ISettingsStore settings,
        IFolderPicker folderPicker,
        IProjectFiles files,
        IUiDispatcher dispatcher,
        Func<string, bool>? directoryExists = null)
    {
        _engine = engine;
        _settings = settings;
        _folderPicker = folderPicker;
        _files = files;
        _dispatcher = dispatcher;
        _directoryExists = directoryExists ?? Directory.Exists;
        Engine = new EngineStatusViewModel(engine);
        Settings = new SettingsViewModel(settings, RestartEngineAsync);
        Settings.Closed += (_, _) => IsSettingsOpen = false;
        engine.StateChanged += (_, _) => OnEngineStateChanged();
        RefreshRecent();
    }

    /// <summary>The product's display name.</summary>
    public static string ProductName => Product.DisplayName;

    /// <summary>The window title: the project's name and the product's.</summary>
    public string Title => Workspace is { } w ? $"{w.Name} – {Product.DisplayName}" : Product.DisplayName;

    /// <summary>The engine's status and log.</summary>
    public EngineStatusViewModel Engine { get; }

    /// <summary>The settings panel.</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>Recent projects for the start page.</summary>
    public ObservableCollection<RecentProjectViewModel> RecentProjects { get; } = [];

    /// <summary>True when there are recent projects to show.</summary>
    public bool HasRecentProjects => RecentProjects.Count > 0;

    /// <summary>The open project, or null on the start page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProjectOpen), nameof(Title))]
    private WorkspaceViewModel? _workspace;

    /// <summary>True when a project is open.</summary>
    public bool IsProjectOpen => Workspace is not null;

    /// <summary>A message for the user in the notice bar, or null.</summary>
    [ObservableProperty]
    private string? _notice;

    /// <summary>True when <see cref="Notice"/> reports a failure.</summary>
    [ObservableProperty]
    private bool _noticeIsError;

    /// <summary>True while a project is being opened.</summary>
    [ObservableProperty]
    private bool _isOpening;

    /// <summary>True while the settings panel is shown.</summary>
    [ObservableProperty]
    private bool _isSettingsOpen;

    /// <summary>Starts the engine. Called once when the window opens.</summary>
    /// <returns>A task that completes when the engine is ready or has failed.</returns>
    public Task InitializeAsync() => _engine.StartAsync();

    /// <summary>Closes the project and stops the engine. Called when the window closes.</summary>
    /// <returns>A task that completes when the engine is gone.</returns>
    public async Task ShutdownAsync()
    {
        Workspace?.Dispose();
        await _engine.StopAsync();
    }

    /// <summary>Asks for a folder and opens it as a project.</summary>
    /// <returns>A task that completes when the project is open or opening failed.</returns>
    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        var folder = await _folderPicker.PickFolderAsync("Open a project folder");
        if (folder is not null)
        {
            await OpenProjectAsync(folder);
        }
    }

    /// <summary>Opens a recent project.</summary>
    /// <param name="project">The entry picked.</param>
    /// <returns>A task that completes when the project is open or opening failed.</returns>
    [RelayCommand]
    private Task OpenRecentAsync(RecentProjectViewModel project) => OpenProjectAsync(project.Path);

    /// <summary>Removes a project from the recent list.</summary>
    /// <param name="project">The entry.</param>
    [RelayCommand]
    private void ForgetRecent(RecentProjectViewModel project)
    {
        var settings = _settings.Load();
        TrySave(settings with { RecentProjects = [.. settings.RecentProjects.Where(p => p != project.Path)] });
        RefreshRecent();
    }

    /// <summary>Opens a folder as a project, replacing any open one.</summary>
    /// <param name="root">The folder.</param>
    /// <returns>A task that completes when the project is open or opening failed (see <see cref="Notice"/>).</returns>
    public async Task OpenProjectAsync(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!_directoryExists(root))
        {
            ShowError($"The folder {root} does not exist any more.");
            return;
        }

        IsOpening = true;
        Notice = null;
        try
        {
            if (_engine.State != EngineState.Ready)
            {
                await _engine.StartAsync();
                if (_engine.State != EngineState.Ready)
                {
                    ShowError($"The project cannot be opened because the engine is not running. {_engine.Failure?.Message}");
                    return;
                }
            }

            var project = await _engine.OpenProjectAsync(root);
            var workspace = new WorkspaceViewModel(project, _engine, _files, _dispatcher, Engine);
            workspace.ReopenRequested += (_, _) => _ = ReopenAsync();
            await workspace.LoadAsync();
            Workspace?.Dispose();
            Workspace = workspace;
            TrySave(_settings.Load().WithRecentProject(root));
            RefreshRecent();
        }
        catch (EngineRequestException ex)
        {
            ShowError(ex.Name == ErrorCodes.ProjectInvalid
                ? $"{root} is not a project: {ex.Message}"
                : $"The project could not be opened: {ex.Message}");
        }
        catch (EngineException ex)
        {
            ShowError($"The project could not be opened: {ex.Message}");
        }
        finally
        {
            IsOpening = false;
        }
    }

    /// <summary>Closes the project and shows the start page.</summary>
    [RelayCommand]
    private void CloseProject()
    {
        Workspace?.Dispose();
        Workspace = null;
        RefreshRecent();
    }

    /// <summary>Restarts the engine, then opens the open project again.</summary>
    /// <returns>A task that completes when the engine and the project are back.</returns>
    [RelayCommand]
    private async Task RestartEngineAsync()
    {
        Notice = null;
        await _engine.StartAsync();
        if (_engine.State == EngineState.Ready && Workspace is { } workspace)
        {
            await OpenProjectAsync(workspace.Root);
        }
    }

    /// <summary>Opens or closes the settings panel.</summary>
    [RelayCommand]
    private void ToggleSettings()
    {
        Settings.Reload();
        IsSettingsOpen = !IsSettingsOpen;
    }

    /// <summary>Hides the notice bar.</summary>
    [RelayCommand]
    private void DismissNotice() => Notice = null;

    private Task ReopenAsync() => Workspace is { } w ? OpenProjectAsync(w.Root) : Task.CompletedTask;

    private void OnEngineStateChanged()
    {
        if (_engine.State == EngineState.Failed && Workspace is not null)
        {
            ShowError($"The engine stopped. Restart it to go on working with the project. {_engine.Failure?.Message}");
        }
    }

    private void ShowError(string message)
    {
        Notice = message;
        NoticeIsError = true;
    }

    private void TrySave(AppSettings settings)
    {
        try
        {
            _settings.Save(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError($"The settings could not be saved: {ex.Message}");
        }
    }

    private void RefreshRecent()
    {
        RecentProjects.Clear();
        foreach (var path in _settings.Load().RecentProjects)
        {
            RecentProjects.Add(new RecentProjectViewModel(path, _directoryExists(path)));
        }

        OnPropertyChanged(nameof(HasRecentProjects));
    }
}
