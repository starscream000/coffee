// The byte streams to and from an engine, and its lifetime: a real process
// (EngineProcess) or, in tests, an engine in memory.

namespace Desktop.Engine;

/// <summary>A running engine as seen by the connection: its streams and its exit.</summary>
public interface IEngineTransport : IAsyncDisposable
{
    /// <summary>The engine's stdout: protocol messages, one per line.</summary>
    Stream Input { get; }

    /// <summary>The engine's stdin.</summary>
    Stream Output { get; }

    /// <summary>Completes with the exit code when the engine has exited.</summary>
    Task<int> Exited { get; }

    /// <summary>The last lines the engine wrote to stderr, joined by newlines.</summary>
    string StderrTail { get; }

    /// <summary>Ends the engine at once, without a shutdown request.</summary>
    void Kill();
}
