// Base of the tabs in the centre of the workspace: step files and the action
// catalogue.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Desktop.App.ViewModels;

/// <summary>A tab in the workspace's centre.</summary>
public abstract partial class WorkspaceTabViewModel : ObservableObject
{
    /// <summary>Raised when the user closes the tab.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The tab's header.</summary>
    public abstract string Title { get; }

    /// <summary>Shown when hovering the header.</summary>
    public virtual string ToolTip => Title;

    /// <summary>True when the tab has a close button.</summary>
    public virtual bool CanClose => true;

    /// <summary>
    /// Says whether the tab may close now; a tab with unsaved changes asks the
    /// user first. The base class always agrees.
    /// </summary>
    /// <returns>True when the tab may close.</returns>
    public virtual Task<bool> ConfirmCloseAsync() => Task.FromResult(true);

    /// <summary>Asks to close the tab; the workspace calls <see cref="ConfirmCloseAsync"/> before removing it.</summary>
    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
