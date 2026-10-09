// Asks the user for a folder. Wraps Avalonia's storage provider so view models
// can be tested without a window.

using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Desktop.App.Services;

/// <summary>Asks the user to choose a folder.</summary>
public interface IFolderPicker
{
    /// <summary>Shows a folder dialog.</summary>
    /// <param name="title">The dialog's title.</param>
    /// <returns>The chosen folder's absolute path, or null when the user cancelled.</returns>
    Task<string?> PickFolderAsync(string title);
}

/// <summary>The folder dialog of the main window.</summary>
/// <param name="owner">Returns the window that owns the dialog.</param>
public sealed class AvaloniaFolderPicker(Func<TopLevel?> owner) : IFolderPicker
{
    /// <inheritdoc />
    public async Task<string?> PickFolderAsync(string title)
    {
        if (owner() is not { } topLevel)
        {
            return null;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }
}
