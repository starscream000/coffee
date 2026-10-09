// Tests of the engine session's life: handshake, refusal, crash, shutdown.

using Desktop.Protocol;
using Desktop.Protocol.Messages;

namespace Desktop.Engine.Tests;

public sealed class EngineSessionTests
{
    private static readonly EngineLaunch Launch = new("node", "/engine/dist/main.js");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (EngineSession Session, List<EngineState> States, Func<FakeEngine> Engine) Create(Func<FakeRequest, FakeEngine, Task<FakeReply?>>? handler = null)
    {
        FakeEngine? engine = null;
        var session = new EngineSession((_, _) => engine = new FakeEngine(handler));
        var states = new List<EngineState>();
        session.StateChanged += (_, s) =>
        {
            lock (states)
            {
                states.Add(s);
            }
        };
        return (session, states, () => engine!);
    }

    [Fact]
    public async Task Start_runs_the_handshake_and_becomes_ready()
    {
        var (session, states, engine) = Create();
        await using var _ = session;

        var result = await session.StartAsync(Launch, "1.2.3", Ct);

        Assert.Equal(EngineState.Ready, session.State);
        Assert.Equal("fake-engine", result.Engine.Name);
        Assert.Equal([EngineState.Starting, EngineState.Ready], states);
        var initialize = Assert.Single(engine().Requests);
        Assert.Equal(Methods.Initialize, initialize.Method);
        Assert.Equal(ProtocolVersion.Current, initialize.Params.GetProperty("protocolVersion").GetString());
        Assert.Equal(Product.ClientName, initialize.Params.GetProperty("client").GetProperty("name").GetString());
        Assert.Equal("1.2.3", initialize.Params.GetProperty("client").GetProperty("version").GetString());
    }

    [Fact]
    public async Task An_incompatible_engine_fails_the_start_with_a_clear_exception()
    {
        var (session, _, _) = Create((request, engine) =>
        {
            engine.ExitSoon(3);
            return Task.FromResult<FakeReply?>(FakeReply.Fail(-32002, ErrorCodes.IncompatibleProtocol, "Update the engine.", new
            {
                name = ErrorCodes.IncompatibleProtocol,
                clientProtocolVersion = ProtocolVersion.Current,
                engineProtocolVersion = "0.0.9",
                engineVersion = "0.0.9",
            }));
        });
        await using var _ = session;

        var ex = await Assert.ThrowsAsync<IncompatibleEngineException>(() => session.StartAsync(Launch, "1.0.0", Ct));
        Assert.Equal("0.0.9", ex.EngineProtocolVersion);
        Assert.Equal("Update the engine.", ex.Message);
        Assert.Equal(EngineState.Failed, session.State);
        Assert.Same(ex, session.Failure);
        Assert.Throws<InvalidOperationException>(() => session.Client);
    }

    [Fact]
    public async Task An_engine_that_stops_on_its_own_puts_the_session_in_Failed()
    {
        var (session, _, engine) = Create();
        await using var _ = session;
        var failed = new TaskCompletionSource();
        session.StateChanged += (_, s) =>
        {
            if (s == EngineState.Failed)
            {
                failed.TrySetResult();
            }
        };
        await session.StartAsync(Launch, "1.0.0", Ct);

        await engine().ExitAsync(1, "out of memory");
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        var reason = Assert.IsType<EngineExitedException>(session.Failure);
        Assert.Equal(1, reason.ExitCode);
        Assert.Contains("out of memory", reason.Message, StringComparison.Ordinal);
        Assert.Null(session.Engine);
    }

    [Fact]
    public async Task Stop_sends_shutdown_and_waits_for_the_exit()
    {
        var (session, states, engine) = Create();
        await using var _ = session;
        await session.StartAsync(Launch, "1.0.0", Ct);

        await session.StopAsync(cancellationToken: Ct);

        Assert.Equal(EngineState.Stopped, session.State);
        Assert.Equal([Methods.Initialize, Methods.Shutdown], engine().Requests.Select(r => r.Method));
        Assert.False(engine().Killed);
        Assert.Equal([EngineState.Starting, EngineState.Ready, EngineState.Stopping, EngineState.Stopped], states);
    }

    [Fact]
    public async Task Stop_kills_an_engine_that_does_not_answer_shutdown()
    {
        var (session, _, engine) = Create((request, e) =>
            Task.FromResult(request.Method == Methods.Shutdown ? null : FakeEngine.Default(request, e)));
        await using var _ = session;
        await session.StartAsync(Launch, "1.0.0", Ct);

        await session.StopAsync(TimeSpan.FromMilliseconds(200), Ct);

        Assert.True(engine().Killed);
        Assert.Equal(EngineState.Stopped, session.State);
    }

    [Fact]
    public async Task Passes_stderr_and_events_on()
    {
        var (session, _, engine) = Create();
        await using var _ = session;
        var received = new TaskCompletionSource<EngineEvent>();
        session.EventReceived += (_, e) => received.TrySetResult(e);
        await session.StartAsync(Launch, "1.0.0", Ct);

        await engine().SendEventAsync("testSkipped", new { runId = "r", seq = 1, testId = "a#0", reason = "flaky" });

        Assert.Equal("flaky", Assert.IsType<TestSkippedEvent>(await received.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct)).Reason);
    }
}
