// Tests of the JSON-RPC connection against an engine in memory.

using Desktop.Protocol;
using Desktop.Protocol.Messages;

namespace Desktop.Engine.Tests;

public sealed class JsonRpcConnectionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(FakeEngine Engine, JsonRpcConnection Connection)> Connect(Func<FakeRequest, FakeEngine, Task<FakeReply?>>? handler = null)
    {
        var engine = new FakeEngine(handler);
        var connection = new JsonRpcConnection(engine);
        connection.Start();
        await Task.Yield();
        return (engine, connection);
    }

    [Fact]
    public async Task Matches_responses_to_requests_whatever_their_order()
    {
        var (engine, connection) = await Connect(async (request, _) =>
        {
            var root = request.Params.GetProperty("root").GetString()!;
            await Task.Delay(root == "slow" ? 150 : 0);
            return FakeReply.Ok(new OpenProjectResult { Root = root, ConfigFile = root + "/c", Environments = [], Logins = [], Diagnostics = [] });
        });
        await using var _ = engine;
        await using var __ = connection;

        var slow = connection.SendAsync<OpenProjectParams, OpenProjectResult>(Methods.OpenProject, new() { Root = "slow" }, Ct);
        var fast = connection.SendAsync<OpenProjectParams, OpenProjectResult>(Methods.OpenProject, new() { Root = "fast" }, Ct);

        Assert.Equal("fast", (await fast).Root);
        Assert.False(slow.IsCompleted);
        Assert.Equal("slow", (await slow).Root);
        Assert.Equal([1, 2], engine.Requests.Select(r => r.Id).Order());
    }

    [Fact]
    public async Task An_error_response_becomes_an_EngineRequestException_with_name_and_data()
    {
        var diagnostics = new[] { new Diagnostic { File = "a.test.yaml", Line = 2, Column = 3, Severity = DiagnosticSeverity.Error, Code = "UnknownAction", Message = "m" } };
        var (engine, connection) = await Connect((_, _) =>
            Task.FromResult<FakeReply?>(FakeReply.Fail(-32005, ErrorCodes.StepFilesInvalid, "invalid", new { name = ErrorCodes.StepFilesInvalid, diagnostics })));
        await using var _ = engine;
        await using var __ = connection;

        var ex = await Assert.ThrowsAsync<EngineRequestException>(() =>
            connection.SendAsync<StartRunParams, StartRunResult>(Methods.StartRun, new(), Ct));
        Assert.Equal((-32005, ErrorCodes.StepFilesInvalid, "invalid"), (ex.Code, ex.Name, ex.Message));
        Assert.Equal("UnknownAction", Assert.Single(ex.DataAs<StepFilesInvalidData>()!.Diagnostics).Code);
    }

    [Fact]
    public async Task A_null_result_reads_as_NullResult()
    {
        var (engine, connection) = await Connect((_, _) => Task.FromResult<FakeReply?>(FakeReply.Ok(null)));
        await using var _ = engine;
        await using var __ = connection;
        Assert.Same(NullResult.Instance, await connection.SendAsync<CancelRunParams, NullResult>(Methods.CancelRun, new() { RunId = "r" }, Ct));
    }

    [Fact]
    public async Task Passes_events_on_in_order_and_survives_bad_lines()
    {
        var (engine, connection) = await Connect();
        await using var _ = engine;
        await using var __ = connection;
        var events = new List<EngineEvent>();
        var problems = new List<ProtocolProblem>();
        var done = new TaskCompletionSource();
        connection.EventReceived += (_, e) =>
        {
            events.Add(e);
            if (e is RunFinishedEvent)
            {
                done.TrySetResult();
            }
        };
        connection.ProblemReported += (_, p) => problems.Add(p);

        await engine.SendEventAsync("testStarted", new { runId = "r", seq = 1, testId = "a#0", startedAt = "t" });
        await engine.SendLineAsync("this is not json");
        await engine.SendLineAsync("""{"jsonrpc":"2.0","method":"testStarted","params":{"runId":"r","seq":2}}""");
        await engine.SendLineAsync("""{"jsonrpc":"2.0","id":99,"result":null}""");
        await engine.SendEventAsync("somethingNew", new { runId = "r", seq = 3 });
        await engine.SendEventAsync("runFinished", new { runId = "r", seq = 4, status = "passed", durationMs = 5, totals = new { passed = 1, failed = 0, cancelled = 0, skipped = 0 } });
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        Assert.Collection(
            events,
            e => Assert.IsType<TestStartedEvent>(e),
            e => Assert.Equal("somethingNew", Assert.IsType<UnknownEngineEvent>(e).Method),
            e => Assert.Equal(RunOutcome.Passed, Assert.IsType<RunFinishedEvent>(e).Status));
        Assert.Equal(3, problems.Count);
        Assert.Contains("not a protocol message", problems[0].Message, StringComparison.Ordinal);
        Assert.Equal("this is not json", problems[0].Excerpt);
        Assert.Contains("\"testStarted\" event that does not fit", problems[1].Message, StringComparison.Ordinal);
        Assert.Contains("not waiting", problems[2].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fails_waiting_requests_with_exit_code_and_stderr_when_the_engine_stops()
    {
        var (engine, connection) = await Connect((_, _) => Task.FromResult<FakeReply?>(null));
        await using var _ = engine;
        await using var __ = connection;

        var waiting = connection.SendAsync<EmptyParams, ListActionsResult>(Methods.ListActions, EmptyParams.Instance, Ct);
        await Task.Delay(50, Ct);
        await engine.ExitAsync(1, "TypeError: boom\n    at main.js:1");

        var ex = await Assert.ThrowsAsync<EngineExitedException>(() => waiting);
        Assert.Equal(1, ex.ExitCode);
        Assert.Contains("TypeError: boom", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, Assert.IsType<EngineExitedException>(await connection.Closed).ExitCode);

        var later = await Assert.ThrowsAsync<EngineExitedException>(() =>
            connection.SendAsync<EmptyParams, ListActionsResult>(Methods.ListActions, EmptyParams.Instance, Ct));
        Assert.Equal(1, later.ExitCode);
    }

    [Fact]
    public async Task A_cancelled_request_stops_waiting_and_its_late_answer_is_reported()
    {
        var release = new TaskCompletionSource();
        var (engine, connection) = await Connect(async (_, _) =>
        {
            await release.Task;
            return FakeReply.Ok(new ListActionsResult { Actions = [] });
        });
        await using var _ = engine;
        await using var __ = connection;
        var problem = new TaskCompletionSource<ProtocolProblem>();
        connection.ProblemReported += (_, p) => problem.TrySetResult(p);

        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            connection.SendAsync<EmptyParams, ListActionsResult>(Methods.ListActions, EmptyParams.Instance, cancel.Token));
        release.SetResult();
        Assert.Contains("not waiting", (await problem.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_throwing_event_handler_is_reported_and_reading_goes_on()
    {
        var (engine, connection) = await Connect((_, _) => Task.FromResult<FakeReply?>(FakeReply.Ok(null)));
        await using var _ = engine;
        await using var __ = connection;
        var seen = new List<int>();
        var problems = new List<ProtocolProblem>();
        connection.EventReceived += (_, e) =>
        {
            seen.Add(e.Seq);
            if (e.Seq == 1)
            {
                throw new InvalidOperationException("subscriber bug");
            }
        };
        connection.ProblemReported += (_, p) =>
        {
            problems.Add(p);
            throw new InvalidOperationException("problem handler bug too");
        };

        await engine.SendEventAsync("testStarted", new { runId = "r", seq = 1, testId = "a#0", startedAt = "t" });
        await engine.SendEventAsync("testStarted", new { runId = "r", seq = 2, testId = "a#0", startedAt = "t" });
        var answer = await connection.SendAsync<CancelRunParams, NullResult>(Methods.CancelRun, new() { RunId = "r" }, Ct);

        Assert.Same(NullResult.Instance, answer);
        Assert.Equal([1, 2], seen);
        Assert.Contains("subscriber bug", Assert.Single(problems).Message, StringComparison.Ordinal);
        Assert.False(connection.Closed.IsCompleted);
    }

    [Fact]
    public async Task A_read_failure_fails_waiting_requests_and_completes_Closed()
    {
        var transport = new FailingTransport();
        await using var connection = new JsonRpcConnection(transport);
        connection.Start();

        var waiting = connection.SendAsync<EmptyParams, ListActionsResult>(Methods.ListActions, EmptyParams.Instance, Ct);
        transport.Fail(new InvalidOperationException("reader broke"));

        var ex = await Assert.ThrowsAsync<EngineConnectionFailedException>(() => waiting);
        Assert.Contains("reader broke", ex.Message, StringComparison.Ordinal);
        Assert.IsType<EngineConnectionFailedException>(await connection.Closed.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        await Assert.ThrowsAsync<EngineConnectionFailedException>(() =>
            connection.SendAsync<EmptyParams, ListActionsResult>(Methods.ListActions, EmptyParams.Instance, Ct));
    }

    [Fact]
    public async Task A_result_that_does_not_fit_is_a_protocol_violation()
    {
        var (engine, connection) = await Connect((_, _) => Task.FromResult<FakeReply?>(FakeReply.Ok(new { actions = "not a list" })));
        await using var _ = engine;
        await using var __ = connection;

        var ex = await Assert.ThrowsAsync<ProtocolViolationException>(() =>
            connection.SendAsync<EmptyParams, ListActionsResult>(Methods.ListActions, EmptyParams.Instance, Ct));
        Assert.Contains("\"listActions\"", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>A transport whose output stream fails on demand with any exception.</summary>
    private sealed class FailingTransport : IEngineTransport
    {
        private readonly FailingStream _input = new();

        public Stream Input => _input;

        public Stream Output { get; } = new MemoryStream();

        public Task<int> Exited { get; } = new TaskCompletionSource<int>().Task;

        public string StderrTail => string.Empty;

        public void Fail(Exception ex) => _input.Failure.TrySetException(ex);

        public void Kill()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class FailingStream : Stream
        {
            public TaskCompletionSource<int> Failure { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
                new(Failure.Task.WaitAsync(cancellationToken));

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
