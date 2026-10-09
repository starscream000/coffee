// The engine as the view models see it: its state, its log, and the requests
// the app sends. EngineService implements it with a real engine process; tests
// use a fake.

using Desktop.Engine;
using Desktop.Protocol.Messages;

namespace Desktop.App.Services;

/// <summary>The app's engine: lifecycle, log and requests. Events are raised on the UI thread.</summary>
public interface IEngineService
{
    /// <summary>The state changed (see <see cref="State"/>, <see cref="Info"/>, <see cref="Failure"/>).</summary>
    event EventHandler? StateChanged;

    /// <summary>A line for the engine log: stderr, the search for the engine, protocol problems.</summary>
    event EventHandler<EngineLogLine>? LogLine;

    /// <summary>The engine sent a run event.</summary>
    event EventHandler<EngineEvent>? EventReceived;

    /// <summary>The engine's state.</summary>
    EngineState State { get; }

    /// <summary>The engine's answer to <c>initialize</c>, while ready.</summary>
    InitializeResult? Info { get; }

    /// <summary>Why the engine is not running, when it failed.</summary>
    Exception? Failure { get; }

    /// <summary>
    /// Finds and starts the engine (stopping any running one) with the current
    /// settings. Does nothing once <see cref="ShutdownAsync"/> was called.
    /// </summary>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>A task that completes when the engine is ready or has failed; it never throws (see <see cref="Failure"/>).</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the engine for good, because the app is closing: later starts do nothing.</summary>
    /// <returns>A task that completes when the engine is gone.</returns>
    Task ShutdownAsync();

    /// <summary>Sends <c>openProject</c>.</summary>
    /// <param name="root">The project folder.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The project.</returns>
    Task<OpenProjectResult> OpenProjectAsync(string root, CancellationToken cancellationToken = default);

    /// <summary>Sends <c>listTests</c>.</summary>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The tests.</returns>
    Task<IReadOnlyList<TestInfo>> ListTestsAsync(CancellationToken cancellationToken = default);

    /// <summary>Sends <c>listActions</c>.</summary>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The actions.</returns>
    Task<IReadOnlyList<ActionInfo>> ListActionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Sends <c>validate</c> for files on disk.</summary>
    /// <param name="files">Relative paths.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The problems found.</returns>
    Task<IReadOnlyList<Diagnostic>> ValidateAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default);

    /// <summary>Sends <c>validate</c> for an unsaved buffer.</summary>
    /// <param name="file">The file the text belongs to, relative to the project root.</param>
    /// <param name="text">The text being edited.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The problems found.</returns>
    Task<IReadOnlyList<Diagnostic>> ValidateContentAsync(string file, string text, CancellationToken cancellationToken = default);
}

/// <summary>Where a line of the engine log came from.</summary>
public enum EngineLogSource
{
    /// <summary>The app itself (starting, stopping, searching).</summary>
    App,
    /// <summary>The engine's stderr.</summary>
    Stderr,
    /// <summary>A problem in the engine's protocol output.</summary>
    Protocol,
}

/// <summary>One line of the engine log.</summary>
/// <param name="Time">When it was received, local time.</param>
/// <param name="Source">Where it came from.</param>
/// <param name="Text">The line.</param>
public sealed record EngineLogLine(DateTimeOffset Time, EngineLogSource Source, string Text);
