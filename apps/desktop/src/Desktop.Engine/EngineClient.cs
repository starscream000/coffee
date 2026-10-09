// Typed requests to an engine (docs/protocol.md, "Requests"), on top of a
// JsonRpcConnection.

using Desktop.Protocol;
using Desktop.Protocol.Messages;

namespace Desktop.Engine;

/// <summary>Sends the protocol's requests with their C# types.</summary>
/// <example>
/// <code>
/// var client = new EngineClient(connection);
/// await client.InitializeAsync("0.1.0", ct);
/// var project = await client.OpenProjectAsync(@"C:\work\shop-tests", ct);
/// </code>
/// </example>
public sealed class EngineClient
{
    private readonly JsonRpcConnection _connection;

    /// <summary>Creates a client over a started connection.</summary>
    /// <param name="connection">The connection.</param>
    public EngineClient(JsonRpcConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>
    /// Sends <c>initialize</c> with this client's protocol version. Must be the
    /// first request of a session.
    /// </summary>
    /// <param name="clientVersion">This app's version, sent as the client's version.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The engine's versions and capabilities.</returns>
    /// <exception cref="IncompatibleEngineException">The engine speaks a protocol version this client cannot talk to.</exception>
    /// <exception cref="EngineRequestException">Any other refusal.</exception>
    /// <exception cref="EngineExitedException">The engine stopped.</exception>
    public async Task<InitializeResult> InitializeAsync(string clientVersion, CancellationToken cancellationToken = default)
    {
        var parameters = new InitializeParams
        {
            ProtocolVersion = ProtocolVersion.Current,
            Client = new SoftwareInfo { Name = Product.ClientName, Version = clientVersion },
        };
        InitializeResult result;
        try
        {
            result = await Send<InitializeParams, InitializeResult>(Methods.Initialize, parameters, cancellationToken).ConfigureAwait(false);
        }
        catch (EngineRequestException ex) when (ex.Name == ErrorCodes.IncompatibleProtocol)
        {
            var data = ex.DataAs<IncompatibleProtocolData>();
            throw new IncompatibleEngineException(ex.Message, ProtocolVersion.Current, data?.EngineProtocolVersion ?? "unknown");
        }

        if (!ProtocolVersion.IsCompatible(ProtocolVersion.Current, result.ProtocolVersion))
        {
            throw new IncompatibleEngineException(
                $"This app speaks protocol {ProtocolVersion.Current} but the engine speaks {result.ProtocolVersion}, and the engine accepted it anyway. Use an engine and an app of matching versions.",
                ProtocolVersion.Current,
                result.ProtocolVersion);
        }

        return result;
    }

    /// <summary>Sends <c>shutdown</c>: the engine cancels any run, closes browsers, answers and exits.</summary>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>A task that completes when the engine has answered.</returns>
    public Task ShutdownAsync(CancellationToken cancellationToken = default) =>
        Send<EmptyParams, NullResult>(Methods.Shutdown, EmptyParams.Instance, cancellationToken);

    /// <summary>Sends <c>openProject</c>, replacing any open project.</summary>
    /// <param name="root">Absolute path of the project folder.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The project's environments, logins and config problems.</returns>
    /// <exception cref="EngineRequestException"><c>ProjectInvalid</c> when the folder has no readable config file.</exception>
    public Task<OpenProjectResult> OpenProjectAsync(string root, CancellationToken cancellationToken = default) =>
        Send<OpenProjectParams, OpenProjectResult>(Methods.OpenProject, new OpenProjectParams { Root = root }, cancellationToken);

    /// <summary>Sends <c>listTests</c>.</summary>
    /// <param name="tags">Only tests with one of these tags; all tests when null.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The tests.</returns>
    /// <exception cref="EngineRequestException"><c>MethodNotFound</c> from engines before plan branch 9.</exception>
    public Task<ListTestsResult> ListTestsAsync(IReadOnlyList<string>? tags = null, CancellationToken cancellationToken = default) =>
        Send<ListTestsParams, ListTestsResult>(Methods.ListTests, new ListTestsParams { Tags = tags }, cancellationToken);

    /// <summary>Sends <c>listActions</c>.</summary>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>Built-in actions and the open project's user actions.</returns>
    public Task<ListActionsResult> ListActionsAsync(CancellationToken cancellationToken = default) =>
        Send<EmptyParams, ListActionsResult>(Methods.ListActions, EmptyParams.Instance, cancellationToken);

    /// <summary>Sends <c>validate</c> for files on disk.</summary>
    /// <param name="files">Paths relative to the project root.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>Every problem found.</returns>
    public Task<ValidateResult> ValidateFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default) =>
        Send<ValidateParams, ValidateResult>(Methods.Validate, new ValidateParams { Files = files }, cancellationToken);

    /// <summary>Sends <c>validate</c> for an unsaved buffer.</summary>
    /// <param name="file">The path the buffer will be saved under, relative to the project root.</param>
    /// <param name="text">The buffer's text.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>Every problem found.</returns>
    public Task<ValidateResult> ValidateContentAsync(string file, string text, CancellationToken cancellationToken = default) =>
        Send<ValidateParams, ValidateResult>(Methods.Validate, new ValidateParams { Content = new UnsavedFile { File = file, Text = text } }, cancellationToken);

    /// <summary>Sends <c>startRun</c>. Progress arrives as events.</summary>
    /// <param name="parameters">What to run and how.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The run's id and results folder.</returns>
    /// <exception cref="EngineRequestException"><c>StepFilesInvalid</c> (see <see cref="StepFilesInvalidData"/>) or <c>RunInProgress</c>.</exception>
    public Task<StartRunResult> StartRunAsync(StartRunParams parameters, CancellationToken cancellationToken = default) =>
        Send<StartRunParams, StartRunResult>(Methods.StartRun, parameters, cancellationToken);

    /// <summary>Sends <c>cancelRun</c>; returns as soon as cancellation has started.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>A task that completes when the engine has answered.</returns>
    public Task CancelRunAsync(string runId, CancellationToken cancellationToken = default) =>
        Send<CancelRunParams, NullResult>(Methods.CancelRun, new CancelRunParams { RunId = runId }, cancellationToken);

    /// <summary>Sends <c>openSnapshot</c>: the engine shows the step's saved page state in a test browser.</summary>
    /// <param name="parameters">The run, test and step.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>A task that completes when the engine has answered.</returns>
    /// <exception cref="EngineRequestException"><c>SnapshotNotFound</c>, or <c>SnapshotUnavailable</c> with <see cref="SnapshotUnavailableData"/>.</exception>
    public Task OpenSnapshotAsync(OpenSnapshotParams parameters, CancellationToken cancellationToken = default) =>
        Send<OpenSnapshotParams, NullResult>(Methods.OpenSnapshot, parameters, cancellationToken);

    private Task<TResult> Send<TParams, TResult>(string method, TParams parameters, CancellationToken cancellationToken) =>
        _connection.SendAsync<TParams, TResult>(method, parameters, cancellationToken);
}
