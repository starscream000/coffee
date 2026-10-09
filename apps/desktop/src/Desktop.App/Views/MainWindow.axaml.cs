// Code-behind of the main window: starts the engine when the window opens and
// stops it before the window closes.

using Avalonia.Controls;
using Desktop.App.ViewModels;

namespace Desktop.App.Views;

/// <summary>The main window of the app.</summary>
public partial class MainWindow : Window
{
    private bool _shutDown;

    /// <summary>Creates the window and its controls.</summary>
    public MainWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is ShellViewModel shell)
            {
                await shell.InitializeAsync();
            }
        };
    }

    /// <inheritdoc />
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnClosing(e);
        if (_shutDown || DataContext is not ShellViewModel shell)
        {
            return;
        }

        // Ask about unsaved changes, stop the engine (it closes its browsers), then close for real.
        e.Cancel = true;
        if (!await shell.ConfirmCloseWindowAsync())
        {
            return;
        }

        _shutDown = true;
        await shell.ShutdownAsync();
        Close();
    }
}
