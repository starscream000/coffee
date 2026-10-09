// Owns one engine process for the app: starts it, runs the handshake, exposes
// the typed client, and notices when the engine stops on its own (a crash).

using Desktop.Protocol.Messages;

namespace Desktop.Engine;

/// <summary>The life of an engine session.</summary>
public enum EngineState
{
    /// <summary>No engine is running.</summary>
    Stopped,
    /// <summary>The engine is starting or in the handshake.</summary>
    Starting,
    /// <summary>The handshake succeeded; requests may be sent.</summary>
    Ready,
    /// <summary>A shutdown was requested.</summary>
    Stopping,
    /// <summary>The engine stopped without being asked, or the handshake failed.</summary>
    Failed,
}

/// <summary>
/// One engine for the app. Events are raised on background threads; the app
/// moves them onto its UI thread.
/// </summary>
/// <example>
/// <code>
/// await using var session = new EngineSession(EngineProcess.Start);
/// session.StateChanged += (_, state) => …;
/// await session.StartAsync(location.Launch, appVersion, ct);
/// var project = await session.Client.OpenProjectAsync(root, ct);
/// </code>
/// </example>
public sealed class EngineSession : IAsyncDisposable
{
    private readonly Func<EngineLaunch, Action<string>, IEngineTransport> _startTransport;
    private readonly Lock _gate = new();
    private IEngineTransport? _transport;
    private JsonRpcConnection? _connection;
    private EngineClient? _client;

    /// <summary>Creates a session that starts engines with <paramref name="startTransport"/>.</summary>
    /// <param name="startTransport">Starts an engine and calls its argument for each stderr line; <see cref="EngineProcess.Start"/> in the app.</param>
    public EngineSession(Func<EngineLaunch, Action<string>, IEngineTransport> startTransport)
    {
        ArgumentNullException.ThrowIfNull(startTransport);
        _startTransport = startTransport;
    }

    /// <summary>The state changed.</summary>
    public event EventHandler<EngineState>? StateChanged;

    /// <summary>The engine wrote a line to stderr.</summary>
    public event EventHandler<string>? StderrLine;

    /// <summary>An engine event arrived.</summary>
    public event EventHandler<EngineEvent>? EventReceived;

    /// <summary>The engine sent something unusable; the session goes on.</summary>
    public event EventHandler<ProtocolProblem>? ProblemReported;

    /// <summary>The current state.</summary>
    public EngineState State { get; private set; } = EngineState.Stopped;

    /// <summary>The engine's answer to <c>initialize</c>, while <see cref="State"/> is <see cref="EngineState.Ready"/>.</summary>
    public InitializeResult? Engine { get; private set; }

    /// <summary>Why the session failed, when <see cref="State"/> is <see cref="EngineState.Failed"/>.</summary>
    public Exception? Failure { get; private set; }

    /// <summary>The typed client.</summary>
    /// <exception cref="InvalidOperationException">The session is not ready.</exception>
    public EngineClient Client => State == EngineState.Ready && _client is not null
        ? _client
        : throw new InvalidOperationException($"The engine is not ready (it is {State}). Start it first.");

    /// <summary>Starts an engine and runs the handshake. Stops any engine this session already runs.</summary>
    /// <param name="launch">What to start.</param>
    /// <param name="clientVersion">This app's version, sent in <c>initialize</c>.</param>
    /// <param name="cancellationToken">Stops waiting for the handshake; the engine is then stopped.</param>
    /// <returns>The engine's versions and capabilities.</returns>
    /// <exception cref="EngineStartException">Node could not be started.</exception>
    /// <exception cref="IncompatibleEngineException">The engine refused this client's protocol version.</exception>
    /// <exception cref="EngineExitedException">The engine stopped during the handshake.</exception>
    public async Task<InitializeResult> StartAsync(EngineLaunch launch, string clientVersion, CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        SetState(EngineState.Starting);
        try
        {
            var transport = _startTransport(launch, line => StderrLine?.Invoke(this, line));
            var connection = new JsonRpcConnection(transport);
            connection.EventReceived += (_, e) => EventReceived?.Invoke(this, e);
            connection.ProblemReported += (_, p) => ProblemReported?.Invoke(this, p);
            lock (_gate)
            {
                _transport = transport;
                _connection = connection;
                _client = new EngineClient(connection);
            }

            connection.Start();
            _ = WatchForExitAsync(connection);
            var result = await _client.InitializeAsync(clientVersion, cancellationToken).ConfigureAwait(false);
            Engine = result;
            Failure = null;
            SetState(EngineState.Ready);
            return result;
        }
        catch (Exception ex)
        {
            Failure = ex;
            await ReleaseAsync().ConfigureAwait(false);
            SetState(EngineState.Failed);
            throw;
        }
    }

    /// <summary>
    /// Sends <c>shutdown</c> and waits for the engine to exit; kills it if it
    /// does not answer within <paramref name="grace"/>. Does nothing when no
    /// engine runs.
    /// </summary>
    /// <param name="grace">How long to wait for a clean exit; 10 seconds when null.</param>
    /// <param name="cancellationToken">Stops waiting; the engine is then killed.</param>
    /// <returns>A task that completes when the engine is gone.</returns>
    public async Task StopAsync(TimeSpan? grace = null, CancellationToken cancellationToken = default)
    {
        EngineClient? client;
        IEngineTransport? transport;
        lock (_gate)
        {
            client = _client;
            transport = _transport;
        }

        if (transport is null)
        {
            return;
        }

        SetState(EngineState.Stopping);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(grace ?? TimeSpan.FromSeconds(10));
        try
        {
            if (client is not null && !transport.Exited.IsCompleted)
            {
                await client.ShutdownAsync(timeout.Token).ConfigureAwait(false);
            }

            await transport.Exited.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or EngineException or IOException)
        {
            transport.Kill();
        }

        await ReleaseAsync().ConfigureAwait(false);
        Engine = null;
        SetState(EngineState.Stopped);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private async Task WatchForExitAsync(JsonRpcConnection connection)
    {
        var reason = await connection.Closed.ConfigureAwait(false);
        lock (_gate)
        {
            if (!ReferenceEquals(_connection, connection) || State is EngineState.Stopping or EngineState.Stopped)
            {
                return;
            }
        }

        Failure = reason;
        Engine = null;
        SetState(EngineState.Failed);
    }

    private async Task ReleaseAsync()
    {
        JsonRpcConnection? connection;
        IEngineTransport? transport;
        lock (_gate)
        {
            connection = _connection;
            transport = _transport;
            _connection = null;
            _transport = null;
            _client = null;
        }

        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        if (transport is not null)
        {
            await transport.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void SetState(EngineState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(this, state);
    }
}
