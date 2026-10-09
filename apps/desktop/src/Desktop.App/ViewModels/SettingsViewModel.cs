// The settings panel: where Node and the engine are, with the search order of
// ADR D0004 explained.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.App.Services;
using Desktop.Engine;

namespace Desktop.App.ViewModels;

/// <summary>The settings panel.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly Func<Task> _applied;

    /// <summary>Creates the panel.</summary>
    /// <param name="store">Where settings are kept.</param>
    /// <param name="applied">Called after saving, to restart the engine with the new paths.</param>
    public SettingsViewModel(ISettingsStore store, Func<Task> applied)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _applied = applied;
        Reload();
    }

    /// <summary>Raised when the panel should close.</summary>
    public event EventHandler? Closed;

    /// <summary>Path of the engine's dist/main.js; empty to search.</summary>
    [ObservableProperty]
    private string _enginePath = string.Empty;

    /// <summary>Path of the node executable; empty to search.</summary>
    [ObservableProperty]
    private string _nodePath = string.Empty;

    /// <summary>Why saving failed, if it did.</summary>
    [ObservableProperty]
    private string? _error;

    /// <summary>How the engine is found when the path is empty.</summary>
    public static string EngineHelp =>
        $"Empty: the {EngineLocator.EngineVariable} environment variable, then an engine bundled with the app, then packages/engine/dist/main.js of the repository the app runs from.";

    /// <summary>How Node is found when the path is empty.</summary>
    public static string NodeHelp => "Empty: a Node bundled with the app, then node on the PATH.";

    /// <summary>Loads the stored values into the fields.</summary>
    public void Reload()
    {
        var settings = _store.Load();
        EnginePath = settings.EnginePath ?? string.Empty;
        NodePath = settings.NodePath ?? string.Empty;
        Error = null;
    }

    /// <summary>Saves and restarts the engine.</summary>
    /// <returns>A task that completes when the engine has restarted.</returns>
    [RelayCommand]
    private async Task SaveAsync()
    {
        var settings = _store.Load() with
        {
            EnginePath = string.IsNullOrWhiteSpace(EnginePath) ? null : EnginePath.Trim(),
            NodePath = string.IsNullOrWhiteSpace(NodePath) ? null : NodePath.Trim(),
        };
        try
        {
            _store.Save(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error = $"The settings could not be saved: {ex.Message}";
            return;
        }

        Closed?.Invoke(this, EventArgs.Empty);
        await _applied();
    }

    /// <summary>Closes without saving.</summary>
    [RelayCommand]
    private void Cancel()
    {
        Reload();
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
