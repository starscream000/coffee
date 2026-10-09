// The app's engine: finds it with the current settings, starts it as a child
// process, and forwards its state, log and events to the UI thread.

using System.Reflection;
using Desktop.Engine;
using Desktop.Protocol;
using Desktop.Protocol.Messages;

namespace Desktop.App.Services;

/// <summary>An <see cref="IEngineService"/> backed by a real engine process.</summary>
public sealed class EngineService : IEngineService, IAsyncDisposable
{
    private readonly EngineSession _session;
    private readonly ISettingsStore _settings;
    private readonly IUiDispatcher _dispatcher;
    private readonly Func<string?, string?, EngineSearchInput> _searchInput;
    private Exception? _failure;
    private volatile bool _shuttingDown;

    /// <summary>Creates the service.</summary>
    /// <param name="settings">Where the Node and engine paths are stored.</param>
    /// <param name="dispatcher">The UI thread.</param>
    /// <param name="startTransport">Starts an engine; <see cref="EngineProcess.Start"/> in the app.</param>
    /// <param name="searchInput">Builds the locator's input from the configured engine and Node paths.</param>
    public EngineService(
        ISettingsStore settings,
        IUiDispatcher dispatcher,
        Func<EngineLaunch, Action<string>, IEngineTransport>? startTransport = null,
        Func<string?, string?, EngineSearchInput>? searchInput = null)
    {
        _settings = settings;
        _dispatcher = dispatcher;
        _searchInput = searchInput ?? EngineSearchInput.ForThisMachine;
        _session = new EngineSession(startTransport ?? EngineProcess.Start);
        _session.StateChanged += (_, _) => _dispatcher.Post(() => StateChanged?.Invoke(this, EventArgs.Empty));
        _session.StderrLine += (_, line) => Log(EngineLogSource.Stderr, line);
        _session.ProblemReported += (_, problem) =>
            Log(EngineLogSource.Protocol, problem.Excerpt is null ? problem.Message : $"{problem.Message} [{problem.Excerpt}]");
        _session.EventReceived += (_, e) => _dispatcher.Post(() => EventReceived?.Invoke(this, e));
    }

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public event EventHandler<EngineLogLine>? LogLine;

    /// <inheritdoc />
    public event EventHandler<EngineEvent>? EventReceived;

    /// <summary>This app's version, sent to the engine in <c>initialize</c>.</summary>
    public static string AppVersion =>
        typeof(EngineService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    /// <inheritdoc />
    public EngineState State => _failure is not null && _session.State != EngineState.Ready ? EngineState.Failed : _session.State;

    /// <inheritdoc />
    public InitializeResult? Info => _session.Engine;

    /// <inheritdoc />
    public Exception? Failure => _failure ?? _session.Failure;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_shuttingDown)
        {
            return;
        }

        _failure = null;
        var settings = _settings.Load();
        EngineLocation location;
        try
        {
            location = EngineLocator.Locate(_searchInput(settings.EnginePath, settings.NodePath));
        }
        catch (EngineNotFoundException ex)
        {
            foreach (var line in ex.Searched)
            {
                Log(EngineLogSource.App, line);
            }

            await _session.StopAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            _failure = ex;
            _dispatcher.Post(() => StateChanged?.Invoke(this, EventArgs.Empty));
            return;
        }

        foreach (var line in location.Log)
        {
            Log(EngineLogSource.App, line);
        }

        Log(EngineLogSource.App, $"Starting {location.Launch.NodePath} {string.Join(' ', location.Launch.Arguments)}");
        try
        {
            var info = await _session.StartAsync(location.Launch, AppVersion, cancellationToken).ConfigureAwait(false);
            Log(EngineLogSource.App, $"Engine {info.Engine.Name} {info.Engine.Version} ready, protocol {info.ProtocolVersion} (this app speaks {ProtocolVersion.Current}).");
        }
        catch (EngineClosedException)
        {
            // The app is shutting down: starting nothing is the point.
        }
        catch (EngineException ex)
        {
            Log(EngineLogSource.App, ex.Message);
        }
        catch (Exception ex)
        {
            // Not an engine error, so a bug or an operating-system failure; keep the detail.
            _failure = ex;
            Log(EngineLogSource.App, $"Starting the engine failed unexpectedly: {ex}");
            _dispatcher.Post(() => StateChanged?.Invoke(this, EventArgs.Empty));
        }
    }

    /// <inheritdoc />
    public async Task ShutdownAsync()
    {
        _shuttingDown = true;
        _failure = null;
        await _session.CloseAsync().ConfigureAwait(false);
        Log(EngineLogSource.App, "Engine stopped.");
    }

    /// <inheritdoc />
    public Task<OpenProjectResult> OpenProjectAsync(string root, CancellationToken cancellationToken = default) =>
        _session.Client.OpenProjectAsync(root, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TestInfo>?> ListTestsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return (await _session.Client.ListTestsAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Tests;
        }
        catch (EngineRequestException ex) when (ex.Name == ErrorCodes.MethodNotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActionInfo>> ListActionsAsync(CancellationToken cancellationToken = default) =>
        (await _session.Client.ListActionsAsync(cancellationToken).ConfigureAwait(false)).Actions;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Diagnostic>> ValidateAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default) =>
        files.Count == 0 ? [] : (await _session.Client.ValidateFilesAsync(files, cancellationToken).ConfigureAwait(false)).Diagnostics;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Diagnostic>> ValidateContentAsync(string file, string text, CancellationToken cancellationToken = default) =>
        (await _session.Client.ValidateContentAsync(file, text, cancellationToken).ConfigureAwait(false)).Diagnostics;

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await ShutdownAsync().ConfigureAwait(false);

    private void Log(EngineLogSource source, string text)
    {
        var line = new EngineLogLine(DateTimeOffset.Now, source, text);
        _dispatcher.Post(() => LogLine?.Invoke(this, line));
    }
}
