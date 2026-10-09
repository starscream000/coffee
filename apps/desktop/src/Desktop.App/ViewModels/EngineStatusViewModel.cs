// The engine's status for the top bar and the engine log panel.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Desktop.App.Services;
using Desktop.Engine;

namespace Desktop.App.ViewModels;

/// <summary>Shows the engine's state and keeps its log.</summary>
public sealed partial class EngineStatusViewModel : ObservableObject
{
    /// <summary>Lines kept in the log; older lines are dropped.</summary>
    public const int MaxLogLines = 2000;

    private readonly IEngineService _engine;

    /// <summary>Creates the view model and follows the engine's state.</summary>
    /// <param name="engine">The engine.</param>
    public EngineStatusViewModel(IEngineService engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
        engine.StateChanged += (_, _) => Refresh();
        engine.LogLine += (_, line) =>
        {
            Log.Add(line);
            while (Log.Count > MaxLogLines)
            {
                Log.RemoveAt(0);
            }
        };
        Refresh();
    }

    /// <summary>The engine log: what the app did, the engine's stderr, protocol problems.</summary>
    public ObservableCollection<EngineLogLine> Log { get; } = [];

    /// <summary>The state in one or two words.</summary>
    [ObservableProperty]
    private string _stateText = string.Empty;

    /// <summary>More about the state: versions when ready, the reason when failed.</summary>
    [ObservableProperty]
    private string _detail = string.Empty;

    /// <summary>True when requests can be sent.</summary>
    [ObservableProperty]
    private bool _isReady;

    /// <summary>True while starting or stopping.</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>True when the engine failed or could not be found.</summary>
    [ObservableProperty]
    private bool _isFailed;

    /// <summary>True when the engine reports at least one browser it can run.</summary>
    [ObservableProperty]
    private bool _canRunTests;

    private void Refresh()
    {
        var state = _engine.State;
        IsReady = state == EngineState.Ready;
        IsBusy = state is EngineState.Starting or EngineState.Stopping;
        IsFailed = state == EngineState.Failed;
        CanRunTests = IsReady && _engine.Info?.Capabilities.Browsers.Count > 0;
        StateText = state switch
        {
            EngineState.Ready => "Engine ready",
            EngineState.Starting => "Starting engine…",
            EngineState.Stopping => "Stopping engine…",
            EngineState.Failed => "Engine not running",
            _ => "Engine stopped",
        };
        Detail = state switch
        {
            EngineState.Ready when _engine.Info is { } info =>
                $"{info.Engine.Name} {info.Engine.Version} · protocol {info.ProtocolVersion} · "
                + (info.Capabilities.Browsers.Count > 0
                    ? $"browsers: {string.Join(", ", info.Capabilities.Browsers)}"
                    : "cannot run tests yet (no browsers)"),
            EngineState.Failed => _engine.Failure?.Message ?? "The engine stopped.",
            _ => string.Empty,
        };
    }
}
