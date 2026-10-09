// An engine in memory for the connection and session tests: answers requests
// through a handler, sends events and raw lines on demand, and exits on demand.

using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Desktop.Protocol.Framing;
using Desktop.Protocol.Json;
using Desktop.Protocol.Messages;

namespace Desktop.Engine.Tests;

/// <summary>A request the fake engine received.</summary>
internal sealed record FakeRequest(int Id, string Method, JsonElement Params);

/// <summary>What the fake engine answers: a result, an error, or nothing.</summary>
internal sealed record FakeReply(object? Result, JsonRpcError? Error)
{
    public static FakeReply Ok(object? result) => new(result, null);

    public static FakeReply Fail(int code, string name, string message, object? moreData = null)
    {
        var data = moreData is null
            ? ProtocolJson.ToElement(new { name })
            : ProtocolJson.ToElement(moreData);
        return new(null, new JsonRpcError { Code = code, Message = message, Data = data });
    }
}

/// <summary>An engine in memory, as an <see cref="IEngineTransport"/>.</summary>
internal sealed class FakeEngine : IEngineTransport
{
    private readonly Pipe _toClient = new();
    private readonly Pipe _toEngine = new();
    private readonly TaskCompletionSource<int> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly Func<FakeRequest, FakeEngine, Task<FakeReply?>> _handler;
    private readonly Task _loop;
    private string _stderr = string.Empty;

    public FakeEngine(Func<FakeRequest, FakeEngine, Task<FakeReply?>>? handler = null)
    {
        _handler = handler ?? ((request, engine) => Task.FromResult(Default(request, engine)));
        Input = _toClient.Reader.AsStream();
        Output = _toEngine.Writer.AsStream();
        _loop = Task.Run(LoopAsync);
    }

    public Stream Input { get; }

    public Stream Output { get; }

    public Task<int> Exited => _exit.Task;

    public string StderrTail => _stderr;

    public ConcurrentQueue<FakeRequest> Requests { get; } = new();

    public bool Killed { get; private set; }

    /// <summary>The standard answer to initialize and shutdown; null (no answer) for anything else.</summary>
    public static FakeReply? Default(FakeRequest request, FakeEngine engine)
    {
        switch (request.Method)
        {
            case Methods.Initialize:
                return FakeReply.Ok(new InitializeResult
                {
                    ProtocolVersion = Protocol.ProtocolVersion.Current,
                    Engine = new SoftwareInfo { Name = "fake-engine", Version = "0.0.1" },
                    Capabilities = new EngineCapabilities { Browsers = [] },
                });
            case Methods.Shutdown:
                engine.ExitSoon(0);
                return FakeReply.Ok(null);
            default:
                return null;
        }
    }

    public async Task SendLineAsync(string line)
    {
        await _writeGate.WaitAsync();
        try
        {
            await _toClient.Writer.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"));
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public Task SendEventAsync<T>(string method, T parameters) =>
        SendLineAsync(JsonSerializer.Serialize(new JsonRpcNotification { Method = method, Params = ProtocolJson.ToElement(parameters) }, ProtocolJson.Options));

    /// <summary>Ends the engine: its stdout closes, then the process "exits".</summary>
    public async Task ExitAsync(int code, string stderr = "")
    {
        _stderr = stderr;
        await _toClient.Writer.CompleteAsync();
        _exit.TrySetResult(code);
    }

    /// <summary>Exits shortly after the current answer is sent, like a real engine after shutdown.</summary>
    public void ExitSoon(int code) => _ = Task.Run(async () =>
    {
        await Task.Delay(30);
        await ExitAsync(code);
    });

    public void Kill()
    {
        Killed = true;
        _ = ExitAsync(137);
    }

    public async ValueTask DisposeAsync()
    {
        await _toEngine.Writer.CompleteAsync();
        await _loop;
        if (!_exit.Task.IsCompleted)
        {
            await ExitAsync(0);
        }

        _writeGate.Dispose();
    }

    private async Task LoopAsync()
    {
        using var reader = new MessageLineReader(_toEngine.Reader.AsStream());
        while (await reader.ReadAsync() is { } line)
        {
            using var document = JsonDocument.Parse(line.Bytes!);
            var root = document.RootElement;
            var request = new FakeRequest(
                root.GetProperty("id").GetInt32(),
                root.GetProperty("method").GetString()!,
                root.TryGetProperty("params", out var p) ? p.Clone() : default);
            Requests.Enqueue(request);
            _ = Task.Run(async () =>
            {
                var reply = await _handler(request, this);
                if (reply is null || _exit.Task.IsCompleted)
                {
                    return;
                }

                object response = reply.Error is null
                    ? new JsonRpcSuccessResponse { Id = ProtocolJson.ToElement(request.Id), Result = ProtocolJson.ToElement(reply.Result) }
                    : new JsonRpcErrorResponse { Id = ProtocolJson.ToElement(request.Id), Error = reply.Error };
                await SendLineAsync(JsonSerializer.Serialize(response, response.GetType(), ProtocolJson.Options));
            });
        }
    }
}
