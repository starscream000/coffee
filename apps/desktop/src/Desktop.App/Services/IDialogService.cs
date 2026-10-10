// Questions the app asks the user in a dialog, behind an interface so that view
// models are tested with a fake that answers for the user.

using Avalonia.Controls;
using Avalonia.Layout;

namespace Desktop.App.Services;

/// <summary>What the user chose when asked about unsaved changes.</summary>
public enum UnsavedChangesChoice
{
    /// <summary>Save the changes, then go on.</summary>
    Save,
    /// <summary>Throw the changes away, then go on.</summary>
    Discard,
    /// <summary>Do not go on; keep editing.</summary>
    Cancel,
}

/// <summary>Asks the user questions.</summary>
public interface IDialogService
{
    /// <summary>Asks what to do with unsaved changes before something closes.</summary>
    /// <param name="files">The files with unsaved changes, relative to the project root.</param>
    /// <returns>The choice; <see cref="UnsavedChangesChoice.Cancel"/> when the dialog is closed without one.</returns>
    Task<UnsavedChangesChoice> AskUnsavedChangesAsync(IReadOnlyList<string> files);

    /// <summary>
    /// Asks what to do with unsaved changes before a run, because the engine
    /// runs the files as they are on disk: save them and run, run without saving
    /// (the text stays unsaved), or not run.
    /// </summary>
    /// <param name="files">The files with unsaved changes, relative to the project root.</param>
    /// <returns><see cref="UnsavedChangesChoice.Save"/> to save and run, <see cref="UnsavedChangesChoice.Discard"/> to run what is on disk, <see cref="UnsavedChangesChoice.Cancel"/> not to run.</returns>
    Task<UnsavedChangesChoice> AskSaveBeforeRunAsync(IReadOnlyList<string> files);

    /// <summary>Asks whether to overwrite a file that changed on disk since it was opened.</summary>
    /// <param name="file">The file, relative to the project root.</param>
    /// <returns>True to overwrite.</returns>
    Task<bool> AskOverwriteAsync(string file);
}

/// <summary>Dialogs as small modal windows over the main window.</summary>
/// <param name="owner">Returns the window that owns the dialogs.</param>
public sealed class AvaloniaDialogService(Func<Window?> owner) : IDialogService
{
    /// <inheritdoc />
    public async Task<UnsavedChangesChoice> AskUnsavedChangesAsync(IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var list = string.Join(Environment.NewLine, files.Select(f => "  " + f));
        var text = files.Count == 1
            ? $"{files[0]} has unsaved changes. Save them?"
            : $"These files have unsaved changes:{Environment.NewLine}{list}{Environment.NewLine}Save them?";
        var answer = await AskAsync("Unsaved changes", text, [("Save", UnsavedChangesChoice.Save), ("Don't save", UnsavedChangesChoice.Discard), ("Cancel", UnsavedChangesChoice.Cancel)]);
        return answer ?? UnsavedChangesChoice.Cancel;
    }

    /// <inheritdoc />
    public async Task<UnsavedChangesChoice> AskSaveBeforeRunAsync(IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var text = $"The engine runs the files as they are on disk, and these have unsaved changes:{Environment.NewLine}{string.Join(Environment.NewLine, files.Select(f => "  " + f))}";
        var answer = await AskAsync("Save before running?", text, [("Save and run", UnsavedChangesChoice.Save), ("Run without saving", UnsavedChangesChoice.Discard), ("Cancel", UnsavedChangesChoice.Cancel)]);
        return answer ?? UnsavedChangesChoice.Cancel;
    }

    /// <inheritdoc />
    public async Task<bool> AskOverwriteAsync(string file)
    {
        var answer = await AskAsync(
            "File changed on disk",
            $"{file} changed on disk since it was opened. Overwrite it with your version?",
            [("Overwrite", true), ("Cancel", false)]);
        return answer ?? false;
    }

    private async Task<T?> AskAsync<T>(string title, string text, IReadOnlyList<(string Label, T Value)> buttons)
        where T : struct
    {
        if (owner() is not { } parent)
        {
            return null;
        }

        T? result = null;
        var dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            MaxWidth = 560,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var (label, value) in buttons)
        {
            var button = new Button { Content = label };
            button.Click += (_, _) =>
            {
                result = value;
                dialog.Close();
            };
            row.Children.Add(button);
        }

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 16,
            Children = { new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, row },
        };
        await dialog.ShowDialog(parent);
        return result;
    }
}
