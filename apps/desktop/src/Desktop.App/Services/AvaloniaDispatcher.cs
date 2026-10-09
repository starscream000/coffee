// The app's dispatcher: Avalonia's UI thread.

using Avalonia.Threading;

namespace Desktop.App.Services;

/// <summary>Posts work to Avalonia's UI thread.</summary>
public sealed class AvaloniaDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
