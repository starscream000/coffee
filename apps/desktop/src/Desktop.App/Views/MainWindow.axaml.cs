// Code-behind of the main window: starts the engine when the window opens,
// stops it before the window closes, and closes the top bar's menus once one
// of their buttons is used.

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
        foreach (var (button, menu) in new (Button, Control)[] { (RunMenuButton, RunMenu), (ProjectMenuButton, ProjectMenu), (EngineButton, EngineMenu) })
        {
            // A check box is a button too, but ticking it is not a choice that ends the menu.
            menu.AddHandler(Button.ClickEvent, (_, e) =>
            {
                if (e.Source is not CheckBox)
                {
                    button.Flyout?.Hide();
                }
            });
        }

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
