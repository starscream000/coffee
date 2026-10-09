// Finding 3 of review D0001: starts and stops of the engine run one at a time,
// in the order asked, and nothing starts after the session is closed. Each
// test counts the engines started and those still running.

using Desktop.Protocol.Messages;

namespace Desktop.Engine.Tests;

public sealed class EngineLifecycleTests
{
    private static readonly EngineLaunch Launch = new("node", "/engine/dist/main.js");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A session whose engines are counted, with an initialize answer that waits for <see cref="Release"/>.</summary>
    private sealed class Counted
    {
        private readonly List<FakeEngine> _engines = [];
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Counted(bool holdInitialize)
        {
            if (!holdInitialize)
            {
                _gate.SetResult();
            }

            Session = new EngineSession((_, _) =>
            {
                var engine = new FakeEngine(async (request, e) =>
                {
                    if (request.Method == Methods.Initialize)
                    {
                        await _gate.Task;
                    }

                    return FakeEngine.Default(request, e);
                });
                lock (_engines)
                {
                    _engines.Add(engine);
                }

                return engine;
            });
        }

        public EngineSession Session { get; }

        public int Started
        {
            get
            {
                lock (_engines)
                {
                    return _engines.Count;
                }
            }
        }

        public int Running
        {
            get
            {
                lock (_engines)
                {
                    return _engines.Count(e => !e.Exited.IsCompleted);
                }
            }
        }

        public void Release() => _gate.TrySetResult();

        public async Task WaitForStartedAsync(int count)
        {
            for (var i = 0; i < 200 && Started < count; i++)
            {
                await Task.Delay(10, Ct);
            }

            Assert.Equal(count, Started);
        }
    }

    [Fact]
    public async Task Two_starts_at_once_leave_exactly_one_engine()
    {
        var counted = new Counted(holdInitialize: true);
        await using var _ = counted.Session;

        // The first start waits in its handshake while the second is asked for.
        var first = counted.Session.StartAsync(Launch, "1", Ct);
        await counted.WaitForStartedAsync(1);
        var second = counted.Session.StartAsync(Launch, "1", Ct);
        await Task.Delay(100, Ct);
        Assert.Equal(1, counted.Started);
        counted.Release();
        await first;
        await second;

        Assert.Equal(EngineState.Ready, counted.Session.State);
        Assert.Equal((2, 1), (counted.Started, counted.Running));
        Assert.NotNull(counted.Session.Client);
    }

    [Fact]
    public async Task A_stop_during_a_start_leaves_no_engine_and_the_state_Stopped()
    {
        var counted = new Counted(holdInitialize: true);
        await using var _ = counted.Session;

        var start = counted.Session.StartAsync(Launch, "1", Ct);
        await counted.WaitForStartedAsync(1);
        var stop = counted.Session.StopAsync(cancellationToken: Ct);
        await Task.Delay(100, Ct);
        Assert.False(stop.IsCompleted);
        Assert.Equal(1, counted.Running);
        counted.Release();
        await start;
        await stop;

        Assert.Equal(EngineState.Stopped, counted.Session.State);
        Assert.Equal(0, counted.Running);
    }

    [Fact]
    public async Task Start_stop_start_in_one_go_ends_with_one_ready_engine()
    {
        var counted = new Counted(holdInitialize: false);
        await using var _ = counted.Session;

        await Task.WhenAll(
            counted.Session.StartAsync(Launch, "1", Ct),
            counted.Session.StopAsync(cancellationToken: Ct),
            counted.Session.StartAsync(Launch, "1", Ct));

        Assert.Equal(EngineState.Ready, counted.Session.State);
        Assert.Equal((2, 1), (counted.Started, counted.Running));
    }

    [Fact]
    public async Task After_close_a_start_does_nothing_even_one_queued_before_the_close()
    {
        var counted = new Counted(holdInitialize: true);
        var first = counted.Session.StartAsync(Launch, "1", Ct);
        await counted.WaitForStartedAsync(1);
        var queuedBeforeClose = counted.Session.StartAsync(Launch, "1", Ct);

        var close = counted.Session.CloseAsync();
        var afterClose = counted.Session.StartAsync(Launch, "1", Ct);
        counted.Release();

        await first;
        await Assert.ThrowsAsync<EngineClosedException>(() => queuedBeforeClose);
        await Assert.ThrowsAsync<EngineClosedException>(() => afterClose);
        await close;
        Assert.True(counted.Session.IsClosed);
        Assert.Equal(EngineState.Stopped, counted.Session.State);
        Assert.Equal((1, 0), (counted.Started, counted.Running));
    }

    [Fact]
    public async Task The_client_of_a_session_that_is_not_ready_is_a_typed_engine_error()
    {
        var counted = new Counted(holdInitialize: false);
        await using var _ = counted.Session;
        var ex = Assert.Throws<EngineNotReadyException>(() => counted.Session.Client);
        Assert.Equal(EngineState.Stopped, ex.State);
        Assert.IsAssignableFrom<EngineException>(ex);
    }
}
