// The client side of the JSON-RPC session with an engine: sends requests with
// increasing ids, matches responses to them, passes events on, and fails every
// waiting request when the engine's output ends.

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Desktop.Protocol.Framing;
using Desktop.Protocol.Json;
using Desktop.Protocol.Messages;

namespace Desktop.Engine;

/// <summary>
/// A JSON-RPC connection over an engine's stdin and stdout. Events and problems
/// are raised on a background thread, in the order the engine sent them.
/// </summary>
/// <example>
/// <code>
/// await using var connection = new JsonRpcConnection(transport);
/// connection.EventReceived += (_, e) => Console.WriteLine(e);
/// connection.Start();
/// var result = await connection.SendAsync&lt;OpenProjectParams, OpenProjectResult&gt;(Methods.OpenProject, new() { Root = root }, ct);
/// </code>
/// </example>
public sealed class JsonRpcConnection : IAsyncDisposable
{
    private const int ExcerptLength = 200;
    private readonly IEngineTransport _transport;
    private readonly MessageLineReader _reader;
    private readonly MessageLineWriter _writer;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<IncomingMessage>> _pending = new();
    private readonly CancellationTokenSource _stop = new();
    private Task? _readLoop;
    private int _nextId;
    private volatile EngineException? _closedBecause;

    /// <summary>Creates a connection; call <see cref="Start"/> to begin reading.</summary>
    /// <param name="transport">The engine's streams.</param>
    public JsonRpcConnection(IEngineTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _transport = transport;
        _reader = new MessageLineReader(transport.Input);
        _writer = new MessageLineWriter(transport.Output);
    }

    /// <summary>An event (a notification) arrived.</summary>
    public event EventHandler<EngineEvent>? EventReceived;

    /// <summary>The engine sent something the connection could not use; the session goes on.</summary>
    public event EventHandler<ProtocolProblem>? ProblemReported;

    /// <summary>
    /// Completes when the connection stopped reading, with the reason: an
    /// <see cref="EngineExitedException"/> when the engine's output ended, an
    /// <see cref="EngineConnectionFailedException"/> when reading failed for any
    /// other reason. Every request still waiting has then failed with it.
    /// </summary>
    public Task<EngineException> Closed => _closed.Task;

    private readonly TaskCompletionSource<EngineException> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Starts reading the engine's output.</summary>
    /// <exception cref="InvalidOperationException">Already started.</exception>
    public void Start()
    {
        if (_readLoop is not null)
        {
            throw new InvalidOperationException("The connection is already started.");
        }

        _readLoop = Task.Run(ReadLoopAsync);
    }

    /// <summary>Sends a request and waits for its response.</summary>
    /// <typeparam name="TParams">The parameters' type.</typeparam>
    /// <typeparam name="TResult">The result's type; <see cref="NullResult"/> for methods that answer null.</typeparam>
    /// <param name="method">The method.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="cancellationToken">Stops waiting; the engine still handles the request, and its answer is dropped.</param>
    /// <returns>The result.</returns>
    /// <exception cref="EngineRequestException">The engine answered with an error.</exception>
    /// <exception cref="EngineExitedException">The engine's output ended before the answer.</exception>
    /// <exception cref="MessageTooLargeException">The request is over the protocol's size limit.</exception>
    /// <exception cref="ProtocolViolationException">The result does not fit <typeparamref name="TResult"/>.</exception>
    /// <exception cref="EngineConnectionFailedException">The connection stopped reading for a reason other than the engine exiting.</exception>
    public async Task<TResult> SendAsync<TParams, TResult>(string method, TParams parameters, CancellationToken cancellationToken = default)
    {
        var id = Interlocked.Increment(ref _nextId);
        var waiter = new TaskCompletionSource<IncomingMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = waiter;
        try
        {
            // Checked after registering, so a close that happens in between still fails this request.
            if (_closedBecause is { } closed)
            {
                throw Copy(closed);
            }

            var request = new JsonRpcRequest
            {
                Id = ProtocolJson.ToElement(id),
                Method = method,
                Params = ProtocolJson.ToElement(parameters),
            };
            try
            {
                await _writer.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException) when (_closedBecause is not null || _transport.Exited.IsCompleted)
            {
                // The pipe broke because the engine exited; report that instead.
                throw Copy(await Closed.ConfigureAwait(false));
            }

            var answer = await waiter.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return answer switch
            {
                IncomingErrorResponse error => throw new EngineRequestException(method, error.Response.Error),
                IncomingSuccessResponse success when typeof(TResult) == typeof(NullResult) => (TResult)(object)NullResult.Instance,
                IncomingSuccessResponse success => ReadResult<TResult>(method, success.Response.Result),
                _ => throw new InvalidOperationException($"Unexpected answer {answer.GetType().Name}."),
            };
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        if (_readLoop is not null)
        {
            try
            {
                await _readLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping.
            }
        }

        _reader.Dispose();
        _writer.Dispose();
        _stop.Dispose();
    }

    private async Task ReadLoopAsync()
    {
        Exception? failure = null;
        try
        {
            while (await _reader.ReadAsync(_stop.Token).ConfigureAwait(false) is { } line)
            {
                Handle(line);
            }
        }
        catch (IOException)
        {
            // The pipe broke: the engine is gone. Handled below like the end of output.
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // Disposed.
        }
        catch (Exception ex)
        {
            // Anything else would leave requests waiting for ever; fail them instead.
            failure = ex;
        }

        EngineException reason;
        if (failure is not null)
        {
            reason = new EngineConnectionFailedException(failure);
        }
        else
        {
            // Output ends just before the process exits; wait briefly for the exit code, unless disposing.
            int? exitCode = null;
            var exitWait = _stop.IsCancellationRequested ? TimeSpan.Zero : TimeSpan.FromSeconds(5);
            if (await Task.WhenAny(_transport.Exited, Task.Delay(exitWait)).ConfigureAwait(false) == _transport.Exited)
            {
                exitCode = await _transport.Exited.ConfigureAwait(false);
            }

            reason = new EngineExitedException(exitCode, _transport.StderrTail);
        }

        _closedBecause = reason;
        foreach (var waiter in _pending.Values)
        {
            waiter.TrySetException(Copy(reason));
        }

        _closed.TrySetResult(reason);
    }

    private static EngineException Copy(EngineException reason) => reason switch
    {
        EngineExitedException exited => new EngineExitedException(exited.ExitCode, exited.StderrTail),
        EngineConnectionFailedException failed when failed.InnerException is { } inner => new EngineConnectionFailedException(inner),
        _ => reason,
    };

    private static TResult ReadResult<TResult>(string method, JsonElement result)
    {
        try
        {
            return ProtocolJson.Read<TResult>(result);
        }
        catch (JsonException ex)
        {
            throw new ProtocolViolationException(
                $"The engine's answer to \"{method}\" does not fit this app's protocol ({ex.Message}). The engine and the app may be of different versions; the engine log has the details.",
                ex);
        }
    }

    private void Handle(FramedLine line)
    {
        if (line.IsOversize)
        {
            Report($"The engine sent a line of {line.DiscardedBytes:N0} bytes, over the protocol's limit; it was discarded.");
            return;
        }

        IncomingMessage message;
        try
        {
            message = IncomingMessage.Parse(line.Bytes);
        }
        catch (JsonException ex)
        {
            Report($"The engine sent a line that is not a protocol message: {ex.Message}", line.Bytes);
            return;
        }

        switch (message)
        {
            case IncomingNotification notification:
                HandleNotification(notification.Notification, line.Bytes!);
                break;
            case IncomingSuccessResponse success:
                Complete(success.Response.Id, message, line.Bytes!);
                break;
            case IncomingErrorResponse error when error.Response.Id.ValueKind == JsonValueKind.Null:
                Report($"The engine reported an error for no particular request: {error.Response.Error.Message}");
                break;
            case IncomingErrorResponse error:
                Complete(error.Response.Id, message, line.Bytes!);
                break;
        }
    }

    private void HandleNotification(JsonRpcNotification notification, byte[] bytes)
    {
        EngineEvent engineEvent;
        try
        {
            var parameters = notification.Params ?? ProtocolJson.ToElement(EmptyParams.Instance);
            engineEvent = EngineEvents.Read(notification.Method, parameters);
        }
        catch (JsonException ex)
        {
            Report($"The engine sent a \"{notification.Method}\" event that does not fit the protocol: {ex.Message}", bytes);
            return;
        }

        try
        {
            EventReceived?.Invoke(this, engineEvent);
        }
        catch (Exception ex)
        {
            // A subscriber's bug must not stop the connection from reading.
            Report($"A handler of the \"{notification.Method}\" event failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Complete(JsonElement id, IncomingMessage message, byte[] bytes)
    {
        if (id.ValueKind == JsonValueKind.Number && id.TryGetInt32(out var number) && _pending.TryGetValue(number, out var waiter))
        {
            waiter.TrySetResult(message);
            return;
        }

        Report("The engine answered a request that is not waiting (it may have been cancelled).", bytes);
    }

    private void Report(string message, byte[]? bytes = null)
    {
        string? excerpt = null;
        if (bytes is not null)
        {
            var text = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, ExcerptLength * 4));
            excerpt = text.Length > ExcerptLength ? text[..ExcerptLength] + "…" : text;
        }

        try
        {
            ProblemReported?.Invoke(this, new ProtocolProblem(message, excerpt));
        }
        catch (Exception ex)
        {
            // The problem cannot be reported anywhere else; keep reading.
            System.Diagnostics.Trace.TraceError($"A protocol problem handler failed: {ex}");
        }
    }
}
