// Waiting for a while, behind an interface so that tests control time instead
// of really waiting (validation while typing waits 300 ms after the last key).

namespace Desktop.App.Services;

/// <summary>Waits for a span of time.</summary>
public interface IDelay
{
    /// <summary>Completes after <paramref name="span"/>, or is cancelled.</summary>
    /// <param name="span">How long to wait.</param>
    /// <param name="cancellationToken">Ends the wait early with <see cref="OperationCanceledException"/>.</param>
    /// <returns>The wait.</returns>
    Task WaitAsync(TimeSpan span, CancellationToken cancellationToken);
}

/// <summary>Real time: <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</summary>
public sealed class RealDelay : IDelay
{
    /// <inheritdoc />
    public Task WaitAsync(TimeSpan span, CancellationToken cancellationToken) => Task.Delay(span, cancellationToken);
}
