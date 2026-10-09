// Moves work onto the UI thread. View models receive engine events on
// background threads and use this so that they are only touched from one thread.

namespace Desktop.App.Services;

/// <summary>Runs actions on the UI thread.</summary>
public interface IUiDispatcher
{
    /// <summary>Queues <paramref name="action"/> to run on the UI thread.</summary>
    /// <param name="action">The work.</param>
    void Post(Action action);
}

/// <summary>A dispatcher that runs actions at once, on the calling thread. For tests.</summary>
public sealed class ImmediateDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }
}
