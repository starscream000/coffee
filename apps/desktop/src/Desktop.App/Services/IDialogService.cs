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

    /// <summary>Asks for the folder and name of a new file.</summary>
    /// <param name="what">What is created, for people, such as "test".</param>
    /// <param name="folder">The folder offered, relative to the project root.</param>
    /// <param name="ending">The ending the name gets, such as <c>.test.yaml</c>, shown as a hint.</param>
    /// <returns>The folder and name typed; null when cancelled.</returns>
    Task<(string Folder, string Name)?> AskNewFileAsync(string what, string folder, string ending);

    /// <summary>Asks for a file's new name.</summary>
    /// <param name="file">The file, relative to the project root.</param>
    /// <returns>The name typed; null when cancelled.</returns>
    Task<string?> AskRenameAsync(string file);

    /// <summary>Asks whether to delete a file.</summary>
    /// <param name="file">The file, relative to the project root.</param>
    /// <returns>True to delete it.</returns>
    Task<bool> AskDeleteAsync(string file);

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

    /// <inheritdoc />
    public async Task<(string Folder, string Name)?> AskNewFileAsync(string what, string folder, string ending)
    {
        var values = await AskTextsAsync($"New {what}", $"The file is created in the project with \"{ending}\" at the end of its name.", [("Folder", folder), ("Name", string.Empty)], "Create");
        return values is [var f, var n] ? (f, n) : null;
    }

    /// <inheritdoc />
    public async Task<string?> AskRenameAsync(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var values = await AskTextsAsync($"Rename {file}", "The file stays in its folder.", [("New name", file[(file.LastIndexOf('/') + 1)..])], "Rename");
        return values is [var name] ? name : null;
    }

    /// <inheritdoc />
    public async Task<bool> AskDeleteAsync(string file)
    {
        var answer = await AskAsync("Delete file", $"Delete {file}? This cannot be undone in the app.", [("Delete", true), ("Cancel", false)]);
        return answer ?? false;
    }

    private async Task<IReadOnlyList<string>?> AskTextsAsync(string title, string text, IReadOnlyList<(string Label, string Value)> fields, string confirm)
    {
        if (owner() is not { } parent)
        {
            return null;
        }

        IReadOnlyList<string>? result = null;
        var dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            MinWidth = 420,
            MaxWidth = 560,
        };
        var boxes = fields.Select(f => new TextBox { Text = f.Value }).ToList();
        var ok = new Button { Content = confirm, IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        ok.Click += (_, _) =>
        {
            result = [.. boxes.Select(b => b.Text ?? string.Empty)];
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();
        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        for (var i = 0; i < fields.Count; i++)
        {
            panel.Children.Add(new TextBlock { Text = fields[i].Label, FontWeight = Avalonia.Media.FontWeight.SemiBold });
            panel.Children.Add(boxes[i]);
        }

        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Avalonia.Thickness(0, 8, 0, 0), Children = { ok, cancel } });
        dialog.Content = panel;
        dialog.Opened += (_, _) => boxes[^1].Focus();
        await dialog.ShowDialog(parent);
        return result;
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
